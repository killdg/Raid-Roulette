using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.HealthSystem;
using EFT.CameraControl;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed class PsychWorldScares
    {
        internal readonly PsychGlassIllusion Glass = new();
        internal Action<Transform>? SceneObserver;
        private readonly Stack<Transform> pending = new();
        private readonly List<Switch> switches = new();
        private readonly List<Trunk> trunks = new();
        private readonly Dictionary<LampController, Turnable.EState> lamps = new();
        private readonly List<Transform> fixtures = new();
        private LightningLightFlicker? localPulse;
        private readonly List<Light> nearbyLights = new();
        private readonly Dictionary<Light, float> pulsingLights = new();
        private bool lightsOff;
        private static readonly RaycastHit[] sightHits = new RaycastHit[64];
        private Trunk? trunk;
        private EDoorState trunkBefore;
        private float pulseEnd;
        private float pulseNext;
        private float coldEnd;
        private float nextShake;
        private float nextCold;
        private float nextShuffle;
        private FrostbiteEffect? frost;
        private bool frostEnabled;
        private float frostRadius;
        private float appliedRadius;
        private readonly Queue<Item> shuffleQueue = new();
        private readonly HashSet<string> shuffled = new();
        private CompoundItem? shuffleBag;
        private int shuffleTotal;
        private float shuffleStep;
        private readonly Dictionary<string, GridItemAddress> shuffleTargets = new();
        private readonly HashSet<string> shuffleMoved = new();
        private int shuffleStyle;
        private int forcedShuffleStyle = -1;
        private bool scanned;
        private bool jumping;
        private bool inventoryOpen;
        private bool busy;
        private int shuffles;
        private int generation;
        private float shuffleAt = -1;
        internal static Camera? ViewCamera => CameraManager.Instance != null ? CameraManager.Instance.Camera : Camera.main;

        internal static bool Visible(Transform target, Vector3 point, float distance = 28)
        {
            var camera = ViewCamera;
            if (camera == null || (camera.transform.position - point).sqrMagnitude > distance * distance)
            {
                return false;
            }

            var screen = camera.WorldToViewportPoint(point);
            if (screen.z <= 0 || screen.x < .05f || screen.x > .95f || screen.y < .05f || screen.y > .95f)
            {
                return false;
            }

            return ClearSight(target, point);
        }

        internal static bool ClearSight(Transform target, Vector3 point)
        {
            var camera = ViewCamera;
            if (camera == null)
            {
                return false;
            }

            var direction = point - camera.transform.position;
            float length = direction.magnitude;
            if (length < .01f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(
                camera.transform.position,
                direction.normalized,
                sightHits,
                length,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.collider == null || hit.collider.isTrigger)
                {
                    continue;
                }

                if (hit.collider.GetComponentInParent<Player>() == Plugin.Local)
                {
                    continue;
                }

                if (hit.transform == target || hit.transform.IsChildOf(target))
                {
                    continue;
                }

                var door = target.GetComponent<Door>();
                if (door != null && door.CollisionColliders.Contains(hit.collider))
                {
                    continue;
                }

                if (hit.distance < length - .15f)
                {
                    return false;
                }
            }

            return true;
        }

        internal void Tick(Player player, bool automatic)
        {
            if (!scanned)
            {
                scanned = true;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded)
                    {
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            pending.Push(root.transform);
                        }
                    }
                }
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            int count = 0;
            while (pending.Count > 0 && count++ < 64)
            {
                var t = pending.Pop();
                if (t == null || t.GetComponent<Player>() != null)
                {
                    continue;
                }

                SceneObserver?.Invoke(t);
                for (int i = 0; i < t.childCount; i++)
                {
                    pending.Push(t.GetChild(i));
                }

                if (t.GetComponent<LampController>() != null || t.GetComponent<CullingLightObject>() != null || t
                    .GetComponent<CullingAdvancedLightObject>() != null || t.GetComponent<Light>() != null || t
                    .GetComponent<BaseLight>() != null || t.GetComponent<AdvancedLight>() != null || (t
                    .GetComponent<MeshRenderer>() != null && (t.name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 || t
                    .name.IndexOf("lamp", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("neon", StringComparison
                    .OrdinalIgnoreCase) >= 0)))
                {
                    fixtures.Add(t);
                }

                var light = t.GetComponent<Light>();
                if (light != null && light.type != LightType.Directional)
                {
                    nearbyLights.Add(light);
                }

                var s = t.GetComponent<Switch>();
                if (s != null && s.Lamps?.Length > 0)
                {
                    switches.Add(s);
                }

                var window = t.GetComponent<WindowBreaker>();
                if (window != null)
                {
                    Glass.Add(window);
                }

                var car = t.GetComponent<Trunk>();
                if (car != null)
                {
                    trunks.Add(car);
                }

                if (count % 8 == 0 && timer.Elapsed.TotalMilliseconds >= 1)
                {
                    break;
                }
            }

            Glass.Tick();
            if (pulseEnd > 0)
            {
                if (Time.unscaledTime >= pulseEnd)
                {
                    RestorePulse();
                }
                else if (Time.unscaledTime >= pulseNext)
                {
                    pulseNext = Time.unscaledTime + .65f;
                    lightsOff = !lightsOff;
                    foreach (var lamp in lamps.Keys)
                    {
                        if (lamp != null && lamp.LampState != Turnable.EState.Destroyed)
                        {
                            lamp.Switch(lamp.LampState == Turnable.EState.On ? Turnable.EState.Off : Turnable.EState.On);
                        }
                    }

                    if (trunk != null && trunk.DoorState is EDoorState.Open or EDoorState.Shut)
                    {
                        if (trunk.DoorState == EDoorState.Open)
                        {
                            trunk.Close();
                        }
                        else
                        {
                            trunk.Open();
                        }
                    }
                }
            }

            bool jump = player.CurrentState?.Name == EPlayerState.Jump;
            if (automatic && jump && !jumping && Time.unscaledTime >= nextCold && Random.value < .1f)
            {
                TestCold(player);
            }

            jumping = jump;
            bool open = player.HandsController?.IsInventoryOpen() == true;
            if (open)
            {
                shuffleQueue.Clear();
                shuffleBag = null;
            }
            else if (!busy && shuffleBag != null && Time.unscaledTime >= shuffleStep)
            {
                shuffleStep = Time.unscaledTime + .08f;
                StepShuffle(player);
            }

            if (automatic && inventoryOpen && !open && shuffles < 3 && Time.unscaledTime >= nextShuffle && Random.value < .25f)
            {
                shuffleAt = Time.unscaledTime + Random.Range(8f, 18f);
            }

            inventoryOpen = open;
            if (open || !automatic)
            {
                shuffleAt = -1;
            }

            if (!open && shuffleAt > 0 && Time.unscaledTime >= shuffleAt)
            {
                shuffleAt = -1;
                nextShuffle = Time.unscaledTime + 180;
                Shuffle(player);
            }
        }

        internal void TestCold(Player player)
        {
            nextCold = Time.unscaledTime + 300;
            var camera = ViewCamera;
            var effect = camera != null ? camera.GetComponent<FrostbiteEffect>() ?? camera
                .GetComponentInChildren<FrostbiteEffect>(true) : null;
            if (effect != null && !player.ActiveHealthController.FindActiveEffects<IFrostbite>().Any())
            {
                frost = effect;
                frostEnabled = effect.enabled;
                frostRadius = effect.Radius;
                appliedRadius = frostRadius;
                coldEnd = Time.unscaledTime + 10;
                nextShake = 0;
                Plugin.Trace("[RR-PSYCH] stage=cold-scare seconds=10 healthEffects=false");
            }
            else
            {
                Plugin.Trace("[RR-PSYCH] stage=cold-scare native-effect-unavailable-or-already-active");
            }
        }

        internal void TestShuffle(Player player, int style = -1)
        {
            if (player.HandsController?.IsInventoryOpen() != true)
            {
                forcedShuffleStyle = style;
                Shuffle(player);
            }
        }

        internal void LateTick(Player? player)
        {
            localPulse?.Drive(lightsOff ? 0 : 1);
            if (frost == null)
            {
                return;
            }

            if (Time.unscaledTime >= coldEnd)
            {
                RestoreCold();
                return;
            }

            if (player?.HealthController.FindActiveEffects<IFrostbite>().Any() == true)
            {
                frost = null;
                coldEnd = 0;
                return;
            }

            frost.enabled = true;
            float strength = PsychFadePolicy.ColdStrength(coldEnd - Time.unscaledTime);
            appliedRadius = Mathf.Lerp(frostRadius, .40f, strength);
            frost.Radius = appliedRadius;
            if (player != null && Time.unscaledTime >= nextShake)
            {
                nextShake = Time.unscaledTime + .25f;
                player.ProceduralWeaponAnimation?.ForceReact?.AddForce(new Vector3(Random.Range(-.1f, .1f), Random.Range(-.1f,
                     .1f), 0) * strength, .08f, 1f, .05f);
            }
        }

        internal bool LightScare(Player player)
        {
            if (pulseEnd > 0)
            {
                return false;
            }

            localPulse?.Dispose();
            localPulse = new LightningLightFlicker();
            foreach (var lamp in fixtures.Where(t => t != null && (t.position - player.Position).sqrMagnitude < 1225)
                .Select(t => t.GetComponent<LampController>()).Where(l => l != null && l.LampState != Turnable.EState.Destroyed).Distinct())
            {
                lamps[lamp] = lamp.LampState;
                if (lamp.LampState != Turnable.EState.On)
                {
                    lamp.Switch(Turnable.EState.On);
                }
            }

            foreach (var fixture in fixtures.Where(t => t != null && (t.position - player.Position).sqrMagnitude < 1225).Take(256))
            {
                localPulse.RegisterNode(fixture);
            }

            if (localPulse.OutputCount == 0 && lamps.Count == 0)
            {
                Plugin.Trace("[RR-PSYCH] stage=light-scare no-nearby-output cachedFixtures=" + fixtures.Count);
                localPulse.Dispose();
                localPulse = null;
                return false;
            }

            pulseEnd = Time.unscaledTime + 4;
            pulseNext = 0;
            lightsOff = false;
            Plugin.Trace("[RR-PSYCH] stage=local-light-pulse outputs=" + localPulse.OutputCount + " switchWiringUntouched=true");
            return true;
        }

        internal bool CarScare(Player player)
        {
            if (pulseEnd > 0)
            {
                return false;
            }

            trunk = trunks.FirstOrDefault(t => t != null && t.Operatable && string.IsNullOrEmpty(t.KeyId) && (t
                .TriggersMap == null || t.TriggersMap.Length == 0) && t.DoorState is EDoorState.Open or EDoorState.Shut && (t
                .transform.position - player.Position).sqrMagnitude < 100 && Visible(t.transform, t.transform.position + Vector3
                    .up * .5f, 15));
            if (trunk == null)
            {
                return false;
            }

            trunkBefore = trunk.DoorState;
            pulseEnd = Time.unscaledTime + Random.Range(3f, 5f);
            pulseNext = 0;
            Plugin.Trace("[RR-PSYCH] stage=car-trunk-burst id=" + trunk.Id);
            return true;
        }

        private void Shuffle(Player player)
        {
            if (busy || shuffleQueue.Count > 0 || player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack)
                .ContainedItem is not CompoundItem bag)
            {
                return;
            }

            var items = bag.Grids.SelectMany(grid => grid.Items).Where(item => item.PinLockState == EItemPinLockState.Free
                && item.CurrentAddress is GridItemAddress).ToArray();
            if (items.Length < 2)
            {
                return;
            }

            shuffleBag = bag;
            shuffled.Clear();
            shuffleMoved.Clear();
            shuffleTargets.Clear();
            shuffleTotal = items.Length;
            shuffles++;
            shuffleStyle = forcedShuffleStyle >= 0 && forcedShuffleStyle < 5 ? forcedShuffleStyle : Random.Range(0, 5);
            forcedShuffleStyle = -1;
            if (shuffleStyle < 2)
            {
                // One outcome keeps the previous small shuffle; the other visits every item.
                var selected = items.OrderBy(_ => Random.value).ToArray();
                int count = shuffleStyle == 0 ? Mathf.Max(2, Mathf.CeilToInt(items.Length * .5f)) : items.Length;
                foreach (var item in selected.Take(count))
                {
                    shuffleQueue.Enqueue(item);
                }
            }
            else
            {
                foreach (var grid in bag.Grids)
                {
                    var slots = items.Where(item => ((GridItemAddress)item.CurrentAddress).Grid == grid).OrderBy(item =>
                         ((GridItemAddress)item.CurrentAddress).LocationInGrid.y).ThenBy(item => ((GridItemAddress)item.CurrentAddress)
                        .LocationInGrid.x).ToArray();
                    var ordered = shuffleStyle == 2 ? slots.OrderBy(item => item.GetType().Name).ThenBy(item => item.TemplateId)
                        .ToArray() : shuffleStyle == 3 ? slots.OrderByDescending(item => item.Template.Width * item.Template.Height)
                        .ThenBy(item => item.TemplateId).ToArray() : System.Linq.Enumerable.Reverse(slots).ToArray();
                    for (int index = 0; index < ordered.Length; index++)
                    {
                        var location = ((GridItemAddress)slots[index].CurrentAddress).LocationInGrid.Clone();
                        shuffleTargets[ordered[index].Id] = grid.CreateItemAddress(location);
                        shuffleQueue.Enqueue(ordered[index]);
                    }
                }
            }

            Plugin.Trace("[RR-PSYCH] stage=backpack-pattern style=" + new[] { "partial-shuffle", "full-shuffle",
                 "grouped-by-type", "largest-first", "reverse-rows" }[shuffleStyle]);
            StepShuffle(player);
        }

        private void StepShuffle(Player player)
        {
            if (shuffleBag == null)
            {
                return;
            }

            if (player.Profile.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack).ContainedItem != shuffleBag)
            {
                shuffleQueue.Clear();
                shuffleBag = null;
                return;
            }

            Item? item = null;
            while (shuffleQueue.Count > 0)
            {
                var candidate = shuffleQueue.Dequeue();
                if (!shuffled.Contains(candidate.Id) && candidate.CurrentAddress is GridItemAddress address && shuffleBag.Grids
                    .Contains(address.Grid))
                {
                    item = candidate;
                    break;
                }
            }

            if (item == null)
            {
                Plugin.Trace("[RR-PSYCH] stage=backpack-rearranged moved=" + shuffleMoved.Count + " total=" + shuffleTotal
                    + " validLayoutOnly=true");
                shuffleBag = null;
                return;
            }

            var old = (GridItemAddress)item.CurrentAddress;
            int version = generation;
            shuffleTargets.TryGetValue(item.Id, out var target);
            if (target != null && target.Grid == old.Grid && target.LocationInGrid.x == old.LocationInGrid.x && target
                .LocationInGrid.y == old.LocationInGrid.y)
            {
                shuffled.Add(item.Id);
                return;
            }

            var others = shuffleBag.Grids.SelectMany(grid => grid.Items).Where(other => other != item && !shuffled
                .Contains(other.Id) && other.PinLockState == EItemPinLockState.Free && other.CurrentAddress is GridItemAddress)
                .OrderBy(other => target == null ? Random.value : SlotDistance((GridItemAddress)other.CurrentAddress, target)).Take(32);
            foreach (var other in others)
            {
                var destination = (GridItemAddress)other.CurrentAddress;
                // Pattern modes use the captured backpack anchors, never equipment slots or
                // temporary item deletion. Incompatible packed shapes simply remain in place.
                if (target != null && (destination.Grid != target.Grid || destination.LocationInGrid.x != target.LocationInGrid.x
                    || destination.LocationInGrid.y != target.LocationInGrid.y))
                {
                    continue;
                }

                var operation = ItemManipulator.Swap(item, other.CurrentAddress, other, item.CurrentAddress, player
                    .InventoryController, true);
                if (operation.Failed || !player.InventoryController.CanExecute(operation.Value))
                {
                    continue;
                }

                busy = true;
                var moved = item;
                var partner = other;
                player.InventoryController.RunNetworkTransaction(
                    operation.Value,
                    result =>
                {
                    if (version != generation)
                    {
                        return;
                    }

                    busy = false;
                    if (result.Succeed)
                    {
                        shuffled.Add(moved.Id);
                        shuffleMoved.Add(moved.Id);
                        shuffleMoved.Add(partner.Id);
                        if (shuffleStyle < 2)
                        {
                            shuffled.Add(partner.Id);
                        }
                    }
                    else
                    {
                        Plugin.Trace("[RR-PSYCH] stage=backpack-swap-rejected " + result.Error);
                    }
                }
                );
                return;
            }

            for (int attempt = 0; attempt < 32; attempt++)
            {
                if (target != null && attempt > 0)
                {
                    break;
                }

                var location = target != null && attempt == 0 ? target.LocationInGrid.Clone() : new LocationInGrid(Random.Range(0,
                     old.Grid.GridWidth), Random.Range(0, old.Grid.GridHeight), old.LocationInGrid.r);
                if (location == old.LocationInGrid)
                {
                    continue;
                }

                var operation = ItemManipulator.Move(item, old.Grid.CreateItemAddress(location), player.InventoryController, true);
                if (operation.Failed || !player.InventoryController.CanExecute(operation.Value))
                {
                    continue;
                }

                busy = true;
                var moved = item;
                player.InventoryController.RunNetworkTransaction(
                    operation.Value,
                    result =>
                {
                    if (version != generation)
                    {
                        return;
                    }

                    busy = false;
                    if (result.Succeed)
                    {
                        shuffled.Add(moved.Id);
                        shuffleMoved.Add(moved.Id);
                    }
                    else
                    {
                        Plugin.Trace("[RR-PSYCH] stage=backpack-move-rejected " + result.Error);
                    }
                }
                );
                return;
            }

            Plugin.Trace("[RR-PSYCH] stage=backpack-item-layout-blocked item=" + item.Id);
        }

        private static float SlotDistance(GridItemAddress address, GridItemAddress target)
        {
            if (address.Grid != target.Grid)
            {
                return float.MaxValue;
            }

            return Mathf.Abs(address.LocationInGrid.x - target.LocationInGrid.x) + Mathf.Abs(address.LocationInGrid.y - target
                .LocationInGrid.y);
        }

        private void RestorePulse()
        {
            localPulse?.Dispose();
            localPulse = null;
            foreach (var pair in pulsingLights)
            {
                if (pair.Key != null)
                {
                    pair.Key.intensity = pair.Value;
                }
            }

            pulsingLights.Clear();
            foreach (var pair in lamps)
            {
                if (pair.Key != null && pair.Key.LampState != Turnable.EState.Destroyed)
                {
                    pair.Key.Switch(pair.Value);
                }
            }

            lamps.Clear();
            if (trunk != null && trunk.DoorState is EDoorState.Open or EDoorState.Shut)
            {
                if (trunkBefore == EDoorState.Open)
                {
                    trunk.Open();
                }
                else
                {
                    trunk.Close();
                }
            }

            trunk = null;
            pulseEnd = 0;
        }

        private void RestoreCold()
        {
            if (frost != null)
            {
                if (Mathf.Abs(frost.Radius - appliedRadius) < .001f)
                {
                    frost.Radius = frostRadius;
                    frost.enabled = frostEnabled;
                }
            }

            frost = null;
            coldEnd = 0;
        }

        internal void Stop()
        {
            Glass.Stop();
            generation++;
            shuffleQueue.Clear();
            shuffled.Clear();
            shuffleTargets.Clear();
            shuffleMoved.Clear();
            shuffleBag = null;
            RestorePulse();
            RestoreCold();
            pending.Clear();
            switches.Clear();
            trunks.Clear();
            nearbyLights.Clear();
            fixtures.Clear();
            scanned = false;
            jumping = inventoryOpen = busy = false;
            shuffleAt = -1;
            nextCold = nextShuffle = 0;
            shuffles = 0;
        }
    }
}
