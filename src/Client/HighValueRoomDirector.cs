using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using BepInEx.Configuration;
using Diz.Jobs;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using JsonType;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed class HighValueRoomDirector
    {
        internal static HighValueRoomDirector? Current;
        private GameWorld? world;
        private bool requested;
        private bool ready;
        private int generation;
        private JArray[] kits = Array.Empty<JArray>();
        private readonly LockedLootBackpackAccess backpackAccess = new();
        private ConfigEntry<bool>? showLootMarkers;
        private readonly List<(string Id, Vector3 Position)> spawnedLoot = new();
        internal void Bind(ConfigFile config)
        {
            showLootMarkers = AdvancedMenu.Bind(
                config,
                "9. Advanced - Navigation",
                "Highlight Locked Loot",
                false,
                "Show screen markers for this event's spawned premium items while testing. Hidden unless advanced options are enabled."
            );
        }

        internal void DrawMarkers()
        {
            if (!Active || !AdvancedMenu.Enabled || showLootMarkers?.Value != true || Plugin.Local == null)
            {
                return;
            }

            var camera = PsychWorldScares.ViewCamera;
            if (camera == null)
            {
                return;
            }

            foreach (var loot in spawnedLoot)
            {
                var screen = camera.WorldToScreenPoint(loot.Position + Vector3.up * 0.2f);
                if (screen.z > 0)
                {
                    float distance = Vector3.Distance(Plugin.Local.Position, loot.Position);
                    GUI.Label(new Rect(screen.x - 65, Screen.height - screen.y, 180, 25), $"Locked Loot: {distance:0.0} m");
                }
            }
        }

        private readonly ConcurrentQueue<Action> completed = new();
        private sealed class RoomReplacement
        {
            internal Door Door = null!;
            internal Vector3 Entrance;
            internal Vector3 Direction;
            internal float Deadline;
            internal float NextProbe;
            internal int Attempts;
            internal int Wanted;
        }

        private readonly Queue<RoomReplacement> pending = new();
        private readonly HashSet<string> rooms = new();
        private readonly HashSet<string> lootPoints = new();
        public HighValueRoomDirector()
        {
            Current = this;
        }

        internal bool OwnsSpawnedItem(string id) => Active && spawnedLoot.Any(loot => loot.Id == id);
        private bool Active => world != null && Plugin.RaidContext.EndsWith(": high_value_loot", StringComparison.Ordinal);

        public void Tick()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(current, world))
            {
                backpackAccess.Restore();
                world = current;
                generation++;
                requested = ready = false;
                kits = Array.Empty<JArray>();
                rooms.Clear();
                lootPoints.Clear();
                spawnedLoot.Clear();
                pending.Clear();
            }

            while (completed.TryDequeue(out var action))
            {
                action();
            }

            if (!Active || Plugin.Local == null)
            {
                backpackAccess.Restore();
                return;
            }

            backpackAccess.Tick(Plugin.Local);
            if (!requested)
            {
                requested = true;
                LoadKits(generation);
            }

            if (ready && pending.Count > 0)
            {
                var room = pending.Dequeue();
                if (room.Door == null || Time.unscaledTime > room.Deadline)
                {
                    Plugin.Trace("[RR-KEYED-ROOM] unlock-wait-expired; no ordinary loot removed");
                    return;
                }

                if (Time.unscaledTime < room.NextProbe)
                {
                    pending.Enqueue(room);
                    return;
                }

                room.NextProbe = Time.unscaledTime + 0.5f;
                // Native UnlockCoroutine sets DoorState directly after the key animation.
                // It does not pass through GameWorld's door-state callback.
                if (room.Door.DoorState is not (EDoorState.Shut or EDoorState.Open))
                {
                    pending.Enqueue(room);
                    return;
                }

                // Wait until the player crosses the unlocked doorway. A probe at the
                // door leaf can start inside a wall or cabinet, rejecting every item.
                Vector3 playerPosition = Plugin.Local.Position;
                if (Vector3.Dot(playerPosition - room.Entrance, room.Direction) < 0.25f || Vector3.Distance(playerPosition, room
                    .Entrance) > 12)
                {
                    pending.Enqueue(room);
                    return;
                }

                try
                {
                    int placed = Populate(room.Door, playerPosition + Vector3.up * 1.2f, room.Entrance, room.Direction, room.Wanted);
                    if (placed == 0 && ++room.Attempts < 4)
                    {
                        room.NextProbe = Time.unscaledTime + 3;
                        pending.Enqueue(room);
                    }
                }
                catch (Exception error)
                {
                    Plugin.Trace("[RR-KEYED-ROOM] placement-failed=" + error.GetBaseException().Message);
                }
            }
        }

        private async void LoadKits(int expected)
        {
            try
            {
                string response = await Backend.Post("{}", "/client/raidroulette/keyed-room-kits");
                var parsed = JObject.Parse(response);
                if (parsed["data"] is JObject body)
                {
                    parsed = body;
                }

                if (parsed["ok"]?.Value<bool>() != true)
                {
                    throw new InvalidOperationException(parsed["error"]?.ToString());
                }

                var available = parsed["kits"]!.OfType<JArray>().ToArray();
                var caseTemplates = parsed["backpackCaseTemplates"]?.Values<string>().Where(value => value != null).Select(value =>
                     value!).ToArray() ?? Array.Empty<string>();
                completed.Enqueue(() =>
                {
                    if (expected == generation)
                    {
                        backpackAccess.SetTemplates(caseTemplates);
                        Warm(available, expected);
                    }
                });
            }
            catch (Exception error)
            {
                completed.Enqueue(() => Plugin.Trace("[RR-KEYED-ROOM] setup-failed=" + error.GetBaseException().Message));
            }
        }

        private async void Warm(JArray[] available, int expected)
        {
            try
            {
                var keys = available.SelectMany(kit => kit.OfType<JObject>()).Select(entry => Singleton<ItemFactory>.Instance
                    .CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24), entry["_tpl"]!.ToString(), null)).SelectMany(item =>
                     new[] { item.Prefab, item.UsePrefab }).Where(key => key != null && !string.IsNullOrEmpty(key.path)).Distinct()
                         .ToArray();
                var factory = Singleton<ObjectsFactory>.Instance;
                await factory.LoadBundlesAndCreatePools(
                    ObjectsFactory.PoolsCategory.Raid,
                    ObjectsFactory.AssemblyType.Local,
                    keys,
                    JobYieldPriority.General,
                    null,
                    factory.PoolsCancellationToken
                );
                if (expected != generation)
                {
                    return;
                }

                kits = available;
                ready = kits.Length > 0;
                Plugin.Trace("[RR-KEYED-ROOM] kits-ready=" + kits.Length);
            }
            catch (Exception error)
            {
                Plugin.Trace("[RR-KEYED-ROOM] assets-failed=" + error.GetBaseException().Message);
            }
        }

        internal void Unlocked(Door door)
        {
            if (!Active || Plugin.Local == null || string.IsNullOrEmpty(door.KeyId) || Vector3.Distance(Plugin.Local.Position,
                 door.transform.position) > 8 || !rooms.Add(door.Id))
            {
                return;
            }

            var leaf = door.CollisionColliders.FirstOrDefault(collider => collider != null);
            var centre = leaf?.bounds.center ?? door.transform.position + Vector3.up;
            var direction = centre - Plugin.Local.Position;
            direction.y = 0;
            direction.Normalize();
            // The approach vector is independent of model-specific collider axes.
            // Capture it before the opening animation rotates the door leaf.
            pending.Enqueue(new RoomReplacement { Door = door, Entrance = centre, Direction = direction, Deadline = Time
                .unscaledTime + 180, Wanted = UnityEngine.Random.Range(1, 4) });
            Plugin.Trace($"[RR-KEYED-ROOM] unlock-observed door={door.Id} state={door.DoorState}");
        }

        private bool SameRoom(Door door, Vector3 origin, Vector3 point, Vector3 entrance, Vector3 direction)
        {
            if (Vector3.Dot(point - entrance, direction) < -0.25f || Mathf.Abs(point.y - origin.y) > 2 || Vector3
                .Distance(point, origin) > 18)
            {
                return false;
            }

            var delta = point - origin;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers,
                 QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Player>() != null || hit.collider.GetComponentInParent<LootItem>() != null
                    || door.CollisionColliders.Contains(hit.collider))
                {
                    continue;
                }

                if (hit.distance < delta.magnitude - .25f)
                {
                    return false;
                }
            }

            return true;
        }

        private Item Build(JArray source)
        {
            var copy = (JArray)source.DeepClone();
            var ids = copy.OfType<JObject>().ToDictionary(entry => entry["_id"]!.ToString(), _ => Guid.NewGuid().ToString("N")
                .Substring(0, 24));
            foreach (var entry in copy.OfType<JObject>())
            {
                entry["_id"] = ids[entry["_id"]!.ToString()];
                string? parent = entry["parentId"]?.ToString();
                if (parent != null && ids.TryGetValue(parent, out var replacement))
                {
                    entry["parentId"] = replacement;
                }

                if (entry["upd"] is not JObject update)
                {
                    entry["upd"] = update = new JObject();
                }

                update["SpawnedInSession"] = true;
            }

            var flat = copy.OfType<JObject>().Select(entry => new FlatItem { _id = new MongoID(entry["_id"]!.ToString()),
                 _tpl = new MongoID(entry["_tpl"]!.ToString()), parentId = string.IsNullOrEmpty(entry["parentId"]?.ToString())
                ? (MongoID? )null : new MongoID(entry["parentId"]!.ToString()), slotId = entry["slotId"]?.ToString()!, upd =
                 new UnparsedData { JToken = entry["upd"]! }, location = entry["location"] is JToken location && location
                .Type != JTokenType.Null ? new UnparsedData { JToken = location } : null! }).ToArray();
            var result = Singleton<ItemFactory>.Instance.FlatItemsToTree(flat);
            if (result.DeserializationErrors.Count > 0)
            {
                throw new InvalidOperationException(result.DeserializationErrors[0].ToString());
            }

            var item = result.Items[flat[0]._id];
            item.SpawnedInSession = true;
            return item;
        }

        private int Populate(Door door, Vector3 origin, Vector3 entrance, Vector3 direction, int wanted)
        {
            if (!Active || door == null)
            {
                return 0;
            }

            // Use the raid's registered loose items; never enumerate the Unity scene.
            var nearby = world!.LootList.OfType<LootItem>().Where(loot => loot != null && loot.Item != null && !loot.Item
                .QuestItem && !(loot is Corpse) && !lootPoints.Contains(loot.Item.Id) && Vector3.Distance(loot.transform.position,
                 origin) <= 18 && Mathf.Abs(loot.transform.position.y - origin.y) <= 2).OrderBy(loot => Vector3.Distance(loot
                .transform.position, origin)).Take(80).ToArray();
            var candidates = nearby.Where(loot => SameRoom(door, origin, loot.transform.position + Vector3.up * .2f, entrance,
                 direction) || SameRoom(door, origin, loot.transform.position + Vector3.up * .6f, entrance, direction))
                .OrderBy(_ => UnityEngine.Random.value).Take(wanted).ToArray();
            Plugin.Trace($"[RR-KEYED-ROOM] probe door={door.Id} origin={origin} entrance={entrance} direction=" +
                $"{direction} registered={world.LootList.Count} nearby={nearby.Length} accepted={candidates.Length}");
            if (candidates.Length == 0)
            {
                foreach (var loot in nearby.Take(6))
                {
                    Plugin.Trace($"[RR-KEYED-ROOM] rejected template={loot.Item.TemplateId} position={loot.transform.position} " +
                        $"insideDot={Vector3.Dot(loot.transform.position - entrance, direction):F2}");
                }
            }

            int placed = 0;
            for (int index = 0; index < wanted; index++)
            {
                Vector3 point;
                LootItem? replaced = index < candidates.Length ? candidates[index] : null;
                if (replaced != null)
                {
                    point = replaced.transform.position;
                }
                else
                {
                    // This mode replaces real loose loot, rather than adding arbitrary floor spawns.
                    break;
                }

                var item = Build(kits[UnityEngine.Random.Range(0, kits.Length)]);
                world.SpawnLootItem(
                    new JsonLootItem { Id = item.Id, Item = item, Position = point, Rotation = Vector3.zero, IsContainer = false,
                         useGravity = false, randomRotation = false },
                    false
                );
                var spawned = world.LootList.OfType<LootItem>().FirstOrDefault(loot => loot != null && loot.Item?.Id == item.Id);
                if (spawned == null)
                {
                    Plugin.Trace($"[RR-KEYED-ROOM] spawn-not-registered template={item.TemplateId}; original preserved");
                    continue;
                }

                Plugin.Trace($"[RR-KEYED-ROOM] spawned template={item.TemplateId} position={spawned.transform.position}");
                if (replaced != null)
                {
                    lootPoints.Add(replaced.Item.Id);
                    // DestroyLoot calls LootItem.Kill, which returns the prefab to
                    // Tarkov's asset pool. Destroying its GameObject afterwards
                    // would leave a destroyed entry in that pool for later raids.
                    world.DestroyLoot(replaced);
                }

                lootPoints.Add(item.Id);
                spawnedLoot.Add((item.Id, spawned.transform.position));
                placed++;
            }

            Plugin.Trace($"[RR-KEYED-ROOM] door={door.Id} requested={wanted} placed={placed}; native geometry checked, quest loot preserved");
            return placed;
        }
    }

    // Observe the native successful unlock entry point, including doors whose coroutine
    // changes the state field without raising GameWorld's state-change notification.
    [HarmonyPatch(typeof(WorldInteractiveObject), nameof(WorldInteractiveObject.Unlock))]
    internal static class LockedLootNativeUnlock
    {
        private static void Postfix(WorldInteractiveObject __instance)
        {
            if (__instance is Door door)
            {
                HighValueRoomDirector.Current?.Unlocked(door);
            }
        }
    }

    [HarmonyPatch(typeof(GameWorld), nameof(GameWorld.WorldInteractiveObjectOnDoorStateChanged))]
    internal static class HighValueRoomUnlock
    {
        private static void Postfix(WorldInteractiveObject door, EDoorState stateBefore, EDoorState stateAfter)
        {
            if (door is Door keyed && stateBefore == EDoorState.Locked && stateAfter is EDoorState.Shut or EDoorState.Open)
            {
                HighValueRoomDirector.Current?.Unlocked(keyed);
            }
        }
    }
}
