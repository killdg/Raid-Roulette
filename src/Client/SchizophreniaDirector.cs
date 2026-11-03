using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using EFT.UI;
using EFT.HealthSystem;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.Tripwire;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private GameWorld? world;
        private bool active;
        private bool whisperShown;
        private bool falseDeathShown;
        private bool schedulesChosen;
        private bool previousRoom;
        private Vector3 lastRoomPosition;
        internal static SchizophreniaDirector? Current;
        private readonly PsychWorldScares worldScares = new();
        private float falseDeathAt;
        private float nextDoorGhost;
        private float nextGlass;
        private float nextInjury;
        private double injuryBoost;
        private float whisperAt;
        private double timeJumpAt;
        private readonly SchizophreniaClock timeJump = new();
        private readonly SchizophreniaInterior interior = new();
        private AudioClip[] glassSounds = Array.Empty<AudioClip>();
        private AudioClip[] fireworkSounds = Array.Empty<AudioClip>();
        private AudioClip[] music = Array.Empty<AudioClip>();
        private AudioSource? musicSource;
        private GameObject? musicRoot;
        private Visual? musicVisual;
        private float musicApproached = -1;
        private float incidentAt;
        private float nextError;
        private float nextGrenade;
        private float nextDecoy;
        private float nextStillCheck;
        private readonly StillnessScare stillness = new();
        private Door[] doors = Array.Empty<Door>();
        private GrenadePrefab[] grenadePrefabs = Array.Empty<GrenadePrefab>();
        private readonly List<(GameObject Root, float Expires)> sounds = new();
        private readonly List<(float At, Action Action)> scheduled = new();
        private readonly List<Visual> visuals = new();
        private TripwireSoundController? tripwire;
        private Systems.Effects.Effects? effects;
        private int doorCount;
        private int voiceCount;
        private int shotCount;
        private int impactCount;
        private int tripCount;
        private int grenadeCount;
        private int decoyCount;
        public string Status => !active ? ""
            : $"\nSchizophrenia: doors {doorCount}, voices {voiceCount}, shots {shotCount}, impacts " +
                $"{impactCount}, wires {tripCount}, grenades {grenadeCount}, decoys {decoyCount}";

        private sealed class Part
        {
            public Renderer Source = null!;
            public GameObject Copy = null!;
            public bool HeldWeapon;
            public Mesh? OwnedMesh;
        }

        private sealed class Visual
        {
            public GameObject Root = null!;
            public Transform SourceRoot = null!;
            public Player? Actor;
            public readonly List<Part> Parts = new();
            public readonly Dictionary<Transform, Transform> Bones = new();
            public Vector3 Start;
            public Vector3 End;
            public float Born;
            public float Life;
            public float PoseAt;
            public float VisibilityAt;
            public bool Grenade;
            public bool Bounced;
            public bool Music;
            public bool Static;
            public bool Crowd;
            public bool Falling;
            public bool Beckon;
            public bool ReverseFacing;
            public bool Aircraft;
            public Vector3 AircraftRotation;
            public Vector3 AircraftSpin;
            public bool RearCrowd;
            public float FadeAt = float.PositiveInfinity;
            public float FadeDuration;
            public readonly List<Material> FadeMaterials = new();
            public readonly Dictionary<Transform, (Vector3 Position, Quaternion Rotation)> GesturePose = new();
            public Vector3 Velocity;
            public Transform? WeaponRoot;
            public Transform? WeaponPalm;
            public Vector3 WeaponPalmOffset;
            public Quaternion WeaponPalmRotation;
        }

        private static float Now => Time.unscaledTime;

        private void Log(string message)
        {
            panelMessage = message;
            Plugin.Trace("[RR-PSYCH] " + message);
        }

        public void Stop()
        {
            active = false;
            PsychBtrTracking.Stop();
            ResetFakeJam();
            Player.OnPlayerDeadStatic -= PsychKilled;
            ClosePanel();
            pauseScares = false;
            planeAsset = null;
            ResetAssetCache();
            StopAirAssets();
            if (Current == this)
            {
                Current = null;
            }

            worldScares.Stop();
            StopActionScares();
            StopGunSounds();
            rapidSound.Reset();
            phantomBanks = Array.Empty<SoundBank>();
            bankScanAt = 0;
            medical.Stop();
            timeJump.Stop();
            interior.Reset();
            StopMusic();
            scheduled.Clear();
            foreach (var v in visuals)
            {
                Destroy(v);
            }

            visuals.Clear();
            foreach (var sound in sounds)
            {
                if (sound.Root != null)
                {
                    Object.Destroy(sound.Root);
                }
            }

            sounds.Clear();
            tripwire?.Dispose();
            tripwire = null;
            doors = Array.Empty<Door>();
            grenadePrefabs = Array.Empty<GrenadePrefab>();
            effects = null;
            glassSounds = fireworkSounds = Array.Empty<AudioClip>();
            stillness.Reset();
        }

        public void Tick()
        {
            try
            {
                TickCore();
            }
            catch (Exception e)
            {
                if (Now >= nextError)
                {
                    nextError = Now + 10;
                    Log("stage=director-error " + e.GetBaseException().Message);
                }
            }
        }

        private void TickCore()
        {
            var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (current is HideoutGameWorld)
            {
                current = null;
            }

            if (!ReferenceEquals(world, current))
            {
                Stop();
                timeJump.Reset();
                interior.Reset();
                music = Array.Empty<AudioClip>();
                world = current;
                whisperShown = false;
                falseDeathShown = false;
                whisperAt = 0;
                nextDoorGhost = nextGlass = nextInjury = 0;
                injuryBoost = 0;
                schedulesChosen = previousRoom = false;
                lastRoomPosition = Vector3.positiveInfinity;
            }

            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            bool running = current != null && game?.GameTimer?.StartDateTime != null && game.Status is not (GameStatus
                .Stopped or GameStatus.Stopping or GameStatus.SoftStopping) && Plugin.Local?.HealthController?.IsAlive == true
                && Plugin.RaidContext.Contains(": schizophrenia");
            if (!running)
            {
                if (active)
                {
                    Stop();
                }

                return;
            }

            if (!active)
            {
                active = true;
                Player.OnPlayerDeadStatic += PsychKilled;
                Current = this;
                if (!schedulesChosen)
                {
                    schedulesChosen = true;
                    falseDeathAt = Random.value < .20f ? 1200 : float.PositiveInfinity;
                    float duration = (float)(game!.GameTimer.SessionTime?.TotalSeconds ?? 0);
                    whisperAt = Random.value < .33f ? (float)PsychScarePolicy.WhisperAt(duration, Random.value) : float.PositiveInfinity;
                }

                medical.Warm();
                timeJumpAt = 600;
                doorCount = voiceCount = shotCount = impactCount = tripCount = grenadeCount = decoyCount = 0;
                incidentAt = Now + Random.Range(15f, 25f);
                nextGrenade = Now + 80;
                nextDecoy = Now + 45;
                nextStillCheck = 0;
                Log("stage=started mode=render-and-audio-only; no actors, bullets or live grenades spawned");
            }

            PsychBtrTracking.Tick();
            TickFakeJam();
            if (Now >= nextInjury)
            {
                nextInjury = Now + 1;
                var health = Plugin.Local!.HealthController;
                bool light = health.FindActiveEffects<ILightBleeding>().Any();
                bool heavy = health.FindActiveEffects<IHeavyBleeding>().Any();
                bool fracture = health.FindActiveEffects<IFracture>().Any();
                bool blacked = new[]
                {
                    EBodyPart.LeftArm,
                    EBodyPart.RightArm,
                    EBodyPart.LeftLeg,
                    EBodyPart.RightLeg
                }.Any(part => health.GetBodyPartHealth(part).Current <= 0);
                double boost = PsychInjuryPolicy.Boost(light, heavy, fracture, blacked);
                if (Math.Abs(boost - injuryBoost) > .001)
                {
                    Log($"stage=injury-intensity boost={boost:P0} light={light} heavy={heavy} fracture={fracture} blacked={blacked}");
                    injuryBoost = boost;
                }
            }

            double raidElapsed = game!.GameTimer.PastTime.TotalSeconds;
            bool room = !timeJump.Consumed && interior.Tick(Plugin.Local!);
            bool entry = room && !previousRoom && (Plugin.Local!.Position - lastRoomPosition).sqrMagnitude >= 4;
            if (entry)
            {
                lastRoomPosition = Plugin.Local!.Position;
            }

            previousRoom = room && (previousRoom || entry);
            bool jump = !pauseScares && entry && raidElapsed >= 600 && Random.value < .30f;
            timeJump.Tick(raidElapsed, timeJumpAt, jump);
            PanelTick();
            TickMusic();
            TickNewEncounters();
            TickActionScares();
            medical.Tick(Plugin.Local!);
            worldScares.SceneObserver = ObserveSceneObject;
            worldScares.Tick(Plugin.Local!, !pauseScares);
            PublishSceneAssets();
            if (!pauseScares && !falseDeathShown && raidElapsed >= falseDeathAt && !HasForegroundVisuals() && sounds.Count == 0)
            {
                Attempt("false-death", FalseDeath);
            }

            if (!pauseScares && !whisperShown && game!.GameTimer.PastTime.TotalSeconds >= whisperAt)
            {
                EFT.Communications.NotificationManager.DisplayMessageNotification(
                    "Did you hear that?",
                    EFT.Communications.ENotificationDurationType.Long,
                    EFT.Communications.ENotificationIconType.Quest
                );
                whisperShown = true;
                Log("stage=whisper-notification elapsed=" + game.GameTimer.PastTime.TotalSeconds);
            }

            for (int i = sounds.Count - 1; i >= 0; i--)
            {
                if (sounds[i].Root == null || Now >= sounds[i].Expires)
                {
                    if (sounds[i].Root != null)
                    {
                        Object.Destroy(sounds[i].Root);
                    }

                    sounds.RemoveAt(i);
                }
            }

            for (int i = scheduled.Count - 1; i >= 0; i--)
            {
                if (Now >= scheduled[i].At)
                {
                    var action = scheduled[i].Action;
                    scheduled.RemoveAt(i);
                    Attempt("scheduled", action);
                }
            }

            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                var v = visuals[i];
                if (v.Root == null || Now >= v.Born + v.Life || (v.SourceRoot == null && !v.Music))
                {
                    Destroy(v);
                    visuals.RemoveAt(i);
                }
                else
                {
                    Attempt("visual-update", () => UpdateVisual(v));
                }
            }

            if (!assetCacheComplete)
            {
                Attempt("incremental-assets", CacheAssets);
            }

            if (pauseScares)
            {
                return;
            }

            if (Now >= nextStillCheck)
            {
                nextStillCheck = Now + 0.25f;
                var point = Plugin.Local!.Position;
                if (stillness.Tick(
                    Now,
                    point.x,
                    point.y,
                    point.z,
                    visuals.All(v => v.Crowd) && sounds.Count == 0 && scheduled.Count == 0 && Time.smoothDeltaTime < 0.04f,
                    Random.value
                ))
                {
                    Attempt("stationary-grenade", StationaryGrenade);
                    incidentAt = Now + (float)PsychInjuryPolicy.Interval(Random.Range(18f, 35f), injuryBoost);
                    return;
                }
            }

            if (Now >= incidentAt)
            {
                incidentAt = Now + (float)PsychInjuryPolicy.Interval(Random.Range(18f, 35f), injuryBoost);
                if (HasForegroundVisuals() || sounds.Count > 0 || scheduled.Count > 0)
                {
                    return;
                }

                if (Time.smoothDeltaTime >= 0.04f)
                {
                    incidentAt = Now + 2;
                    return;
                }

                if (Random.value < .17f)
                {
                    if (Now >= nextDecoy)
                    {
                        nextDecoy = Now + Random.Range(60f, 100f);
                        Attempt("decoy", Decoy);
                    }

                    return;
                }

                // Preserve the relative weights of all remaining effects after
                // removing the original seven-ticket moving-decoy interval.
                int choice = Random.Range(0, 93);
                if (choice >= 54)
                {
                    choice += 7;
                }

                if (choice < 40)
                {
                    Attempt("door", () => SceneEffectOrSound("door", TryDoorPulse));
                }
                else if (choice < 47)
                {
                    Attempt("voice", Voice);
                }
                else if (choice < 49)
                {
                    Attempt("shots", Gunfire);
                }
                else if (choice < 51)
                {
                    Attempt("glass-sound", GlassSound);
                }
                else if (choice < 52)
                {
                    Attempt("tripwire", Tripwire);
                }
                else if (choice < 54)
                {
                    if (Now < nextGrenade)
                    {
                        return;
                    }

                    nextGrenade = Now + Random.Range(100f, 160f);
                    Attempt("grenade", Grenade);
                }
                else if (choice < 66)
                {
                    Attempt("light-switch", () => SceneEffectOrSound("light-switch", () => worldScares.LightScare(Plugin.Local!)));
                }
                else if (choice < 78)
                {
                    Attempt("car-trunk", () => SceneEffectOrSound("car-trunk", () => worldScares.CarScare(Plugin.Local!)));
                }
                else if (choice < 87)
                {
                    Attempt(
                        "glass",
                        () => SceneEffectOrSound(
                            "glass",
                            () =>
                    {
                        if (Now < nextGlass || !worldScares.Glass.Trigger(Plugin.Local!))
                        {
                            return false;
                        }

                        nextGlass = Now + 45;
                        return true;
                    }
                        )
                    );
                }
                else
                {
                    Attempt("phantom-air", FastPlane);
                }
            }
        }

        // Rear crowd has its own population limit and cleanup. Treating it as
        // a foreground incident starved every random scare while the player was
        // stationary. Short-lived foreground visuals still serialize as before.
        private bool HasForegroundVisuals()
        {
            return visuals.Any(visual => !visual.Crowd);
        }

        private void CacheAssets()
        {
            CacheLoadedAssetsIncrementally();
        }

        private void Attempt(string stage, Action action)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (Now >= nextError)
                {
                    nextError = Now + 10;
                    Log("stage=" + stage + " unavailable=" + e.GetBaseException().Message);
                }
            }
            finally
            {
                double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                if (ms >= 8)
                {
                    Log("stage=slow-effect effect=" + stage + " ms=" + ms.ToString("0.0"));
                }
            }
        }

        private Vector3 Near(float min, float max)
        {
            var local = Plugin.Local!;
            float a = Random.Range(0f, Mathf.PI * 2);
            float r = Random.Range(min, max);
            return local.Position + new Vector3(Mathf.Cos(a) * r, 1.4f, Mathf.Sin(a) * r);
        }

        private Vector3 PhantomSoundPoint()
        {
            var range = PsychScarePolicy.SoundRange(Random.value);
            return Near(range.Min, range.Max);
        }

        private void Sound(AudioClip? clip, Vector3 point, float volume = 0.8f, float maxDistance = 65)
        {
            if (clip == null)
            {
                return;
            }

            if (sounds.Count >= 12)
            {
                return;
            }

            var root = new GameObject("RR_PhantomAudio");
            root.transform.position = point;
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2;
            source.maxDistance = maxDistance;
            source.volume = volume;
            sounds.Add((root, Now + Mathf.Min(clip.length, 15) + 0.2f));
            // Route through the native gameplay mixer while keeping ownership of the sound's lifetime.
            if (Singleton<BetterAudio>.Instantiated)
            {
                var native = Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Weaponry);
                if (native != null)
                {
                    source.outputAudioMixerGroup = native.source1.outputAudioMixerGroup;
                    native.Release();
                }
            }

            source.clip = clip;
            source.Play();
        }

        private void DoorPulse()
        {
            TryDoorPulse();
        }

        private bool TryDoorPulse()
        {
            Door? door = null;
            int found = 0;
            var here = Plugin.Local!.Position;
            foreach (var candidate in doors)
            {
                if (candidate == null || (candidate.transform.position - here).sqrMagnitude >= 1600 || candidate
                    .GetType() != typeof(Door) || !candidate.Operatable || (candidate.TriggersMap != null && candidate.TriggersMap
                    .Length > 0) || candidate.NoInteractionsAllowed || candidate.IsBroken || !string.IsNullOrEmpty(candidate.KeyId)
                    || candidate.DoorState is not (EDoorState.Shut or EDoorState.Open))
                {
                    continue;
                }

                var position = candidate.transform.position;
                var bounds = candidate.CollisionColliders.FirstOrDefault(c => c != null)?.bounds;
                var seen = bounds?.center ?? position + Vector3.up;
                if (!PsychWorldScares.Visible(candidate.transform, seen))
                {
                    continue;
                }

                if (world!.RegisteredPlayers.Any(p => p.ProfileId != Plugin.Local!.ProfileId && (p.Position - position).sqrMagnitude <= 9))
                {
                    continue;
                }

                if ((Plugin.Local!.Position - position).sqrMagnitude < .64f && candidate.DoorState == EDoorState.Open)
                {
                    continue;
                }

                if (Random.Range(0, ++found) == 0)
                {
                    door = candidate;
                }
            }

            if (door == null)
            {
                Log("stage=door no-visible-safe-target cached=" + doors.Length);
                return false;
            }

            if (door.DoorState == EDoorState.Shut)
            {
                door.Open();
            }
            else
            {
                door.Close();
            }

            doorCount++;
            Log("stage=door id=" + door.Id + " position=" + door.transform.position);
            return true;
        }

        // A failed placement uses this existing incident slot, not an extra roll
        // or another scene search. Keep low-FPS/foreground gates above intact.
        private void SceneEffectOrSound(string source, Func<bool> tryEffect)
        {
            if (tryEffect())
            {
                return;
            }

            int before = sounds.Count;
            bool played;
            if (Random.value < 0.67f)
            {
                Voice();
                played = sounds.Count > before || TryGlassSound();
            }
            else
            {
                played = TryGlassSound();
                if (!played)
                {
                    Voice();
                    played = sounds.Count > before;
                }
            }

            Log("stage=placement-audio-fallback source=" + source + " played=" + played);
        }

        private void Voice()
        {
            var donors = world!.RegisteredPlayers.OfType<Player>().Where(p => p.IsAI && p.Speaker != null).ToArray();
            if (donors.Length == 0)
            {
                return;
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                var banks = donors[Random.Range(0, donors.Length)].Speaker.PhrasesBanks.Values.ToArray();
                if (banks.Length == 0)
                {
                    continue;
                }

                var clips = banks[Random.Range(0, banks.Length)]?.Clips;
                if (clips == null)
                {
                    continue;
                }

                AudioClip? chosen = null;
                int eligible = 0;
                foreach (var clip in clips)
                {
                    if (clip?.Clip != null && clip.Clip.length > 0.4f && clip.Clip.length < 10 && Random.Range(0, ++eligible) == 0)
                    {
                        chosen = clip.Clip;
                    }
                }

                if (chosen == null)
                {
                    continue;
                }

                var point = PhantomSoundPoint();
                Sound(chosen, point, 0.9f, 110);
                voiceCount++;
                Log("stage=voice clip=" + chosen.name + " position=" + point);
                return;
            }
        }

        private void Gunfire()
        {
            var donors = world!.RegisteredPlayers.OfType<Player>().Where(p => p.HealthController?.IsAlive == true).ToArray();
            if (donors.Length == 0)
            {
                return;
            }

            AudioClip? clip = null;
            string bankName = "";
            int start = Random.Range(0, donors.Length);
            for (int attempt = 0; attempt < Math.Min(6, donors.Length); attempt++)
            {
                var weapon = donors[(start + attempt) % donors.Length].GetComponentInChildren<WeaponSoundPlayer>();
                var bank = weapon != null ? weapon.Body : null;
                if (bank == null)
                {
                    continue;
                }

                try
                {
                    clip = bank.PickSingleClip(0);
                    bankName = bank.name;
                }
                catch
                {
                    continue;
                }

                if (clip != null)
                {
                    break;
                }
            }

            if (clip == null)
            {
                return;
            }

            var point = PhantomSoundPoint();
            int count = Random.Range(1, 4);
            var loaded = clip;
            for (int i = 0; i < count; i++)
            {
                float at = Now + i * Random.Range(0.13f, 0.22f);
                scheduled.Add((at, () =>
                {
                    Sound(loaded, point, 0.85f, 100);
                    shotCount++;
                }));
            }

            scheduled.Add((Now + 0.4f, Impact));
            Log("stage=gunfire bank=" + bankName + " count=" + count + " position=" + point);
        }

        private void Impact()
        {
            if (effects == null)
            {
                return;
            }

            var local = Plugin.Local!;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector3 origin = local.Position + Vector3.up * 1.5f;
                Vector3 direction = (Near(2, 7) - origin + Vector3.down * Random.Range(0.4f, 2f)).normalized;
                var hits = Physics.RaycastAll(origin, direction, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    .OrderBy(h => h.distance);
                foreach (var hit in hits)
                {
                    if (hit.collider.GetComponentInParent<Player>() != null)
                    {
                        break;
                    }

                    if (hit.distance < 1.5f)
                    {
                        continue;
                    }

                    var ballistic = hit.collider.GetComponent<BallisticCollider>() ?? hit.collider
                        .GetComponentInParent<BallisticCollider>();
                    if (ballistic == null)
                    {
                        break;
                    }

                    effects.Emit(ballistic.TypeOfMaterial, ballistic, hit.point, hit.normal, 0.75f);
                    impactCount++;
                    Log("stage=impact position=" + hit.point + " material=" + ballistic.TypeOfMaterial);
                    return;
                }
            }
        }

        private TripwireSoundController Wire() => tripwire ??= new TripwireSoundController(Plugin.Local!.ProfileId)
        {
            _isFirstPerson = false
        };
        private void Tripwire()
        {
            var point = PhantomSoundPoint();
            var storage = Wire()._soundStorage;
            Sound(storage.GetGrenadePinSound().GetClip(), point, 1, 110);
            scheduled.Add((Now + 0.2f, () => Sound(storage.GetFuzeSound().GetClip(), point, 0.85f, 110)));
            tripCount++;
            Log("stage=tripwire-pin position=" + point);
        }

        private void StationaryGrenade()
        {
            var point = Near(2, 4);
            var storage = Wire()._soundStorage;
            Sound(storage.GetGrenadePinSound().GetClip(), point, 0.9f);
            scheduled.Add((Now + 0.3f, () => Sound(storage.GetDefuseCollisionSound().GetClip(), point, 0.9f)));
            scheduled.Add((Now + 0.6f, () => Sound(storage.GetFuzeSound().GetClip(), point, 0.85f)));
            tripCount++;
            Log("stage=stationary-grenade stationarySeconds=20 chance=0.15 cooldownSeconds=240 audio-only");
        }

        private static GameObject? GrenadeRenderRoot(GrenadePrefab prefab)
        {
            // GrenadePrefab is the item wrapper. Its throwable and held visuals can
            // live in referenced prefab objects, outside the wrapper's hierarchy.
            var candidates = new[]
            {
                prefab.GrenadeItself != null ? prefab.GrenadeItself.gameObject : null,
                prefab.WeaponObject,
                prefab.gameObject
            };
            foreach (var root in candidates)
            {
                if (root != null && root.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer is MeshRenderer
                    && renderer.GetComponent<MeshFilter>()?.sharedMesh != null || renderer is SkinnedMeshRenderer skin && skin
                    .sharedMesh != null))
                {
                    return root;
                }
            }

            return null;
        }

        private void Grenade()
        {
            if (visuals.Count(visual => visual.Grenade) >= 2)
            {
                Log("stage=grenade visual-limit=2");
                return;
            }

            var model = grenadePrefabs.Where(prefab => prefab != null).Select(GrenadeRenderRoot).FirstOrDefault(root => root != null);
            if (model == null)
            {
                Log("stage=grenade waiting-for-loaded-model");
                return;
            }

            var target = Near(2.5f, 5);
            if (!NavMesh.SamplePosition(target, out var hit, 3, NavMesh.AllAreas))
            {
                return;
            }

            var start = Near(7, 12) + Vector3.up;
            var visual = CreateVisual(
                model.transform,
                model.GetComponentsInChildren<Renderer>(true),
                start,
                hit.position + Vector3.up * 0.08f,
                4,
                true,
                null
            );
            if (visual == null)
            {
                return;
            }

            Sound(Wire()._soundStorage.GetGrenadePinSound().GetClip(), start);
            grenadeCount++;
            Log("stage=grenade visual-only position=" + hit.position);
        }

        private void Decoy()
        {
            if (visuals.Count(v => !v.Grenade && !v.Crowd && !v.Music) >= 2)
            {
                Log("stage=decoy another-visual-active");
                return;
            }

            var donors = world!.RegisteredPlayers.OfType<Player>().Where(p => p.IsAI && p.HealthController?.IsAlive == true
                && p.PlayerBody != null && p.HandsController is Player.FirearmController).ToArray();
            if (donors.Length == 0)
            {
                return;
            }

            var donor = donors[Random.Range(0, donors.Length)];
            var local = Plugin.Local!;
            var camera = PsychWorldScares.ViewCamera;
            var forward = camera != null ? camera.transform.forward : local.LookDirection;
            forward.y = 0;
            if (forward.sqrMagnitude < .1f)
            {
                return;
            }

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            for (int attempt = 0; attempt < 18; attempt++)
            {
                float range = Random.Range(4f, 14f);
                var anchor = local.Position + forward * range + right * Random.Range(-range * .28f, range * .28f);
                if (!NavMesh.SamplePosition(anchor, out var start, 2, NavMesh.AllAreas) || Mathf.Abs(start.position.y - local
                    .Position.y) > 2)
                {
                    continue;
                }

                if (!PsychWorldScares.Visible(local.Transform.Original, start.position + Vector3.up * 1.4f, 20))
                {
                    continue;
                }

                // Random heading permits approach, retreat and diagonal crossings as well
                // as lateral travel. Native navmesh and sight checks still reject walls.
                var heading = DecoyMotionPolicy.Direction(Random.value);
                var travel = forward * (float)heading.Forward + right * (float)heading.Right;
                var endPoint = start.position + travel * Random.Range(1.5f, 4f);
                if (!NavMesh.SamplePosition(endPoint, out var end, 1.2f, NavMesh.AllAreas) || !PsychWorldScares.Visible(local
                    .Transform.Original, end.position + Vector3.up * 1.4f, 20))
                {
                    continue;
                }

                if (NavMesh.Raycast(start.position, end.position, out _, NavMesh.AllAreas))
                {
                    continue;
                }

                var v = CreateVisual(donor.Transform.Original, ActorRenderers(donor), start.position, end.position, 4, false, donor);
                if (v != null)
                {
                    v.FadeAt = Now + 2.75f;
                    v.FadeDuration = 1.25f;
                    decoyCount++;
                    Log("stage=decoy visible-front=true donor=" + donor.Profile.Info.Settings.Role);
                }

                return;
            }

            Log("stage=decoy no-visible-front-placement");
        }

        private bool TryMusicDecoy()
        {
            var donors = world!.RegisteredPlayers.OfType<Player>().Where(p => p.IsAI && p.HealthController?.IsAlive == true
                && p.PlayerBody != null && p.HandsController is Player.FirearmController && p.AIData?.BotOwner != null).ToArray();
            if (donors.Length == 0)
            {
                return false;
            }

            var local = Plugin.Local!;
            var camera = PsychWorldScares.ViewCamera;
            Vector3 forward = camera != null ? camera.transform.forward : local.LookDirection;
            forward.y = 0;
            if (forward.sqrMagnitude < .1f)
            {
                return false;
            }

            forward.Normalize();
            if (music.Length == 0)
            {
                Log("stage=music-decoy no-loaded-music");
                return false;
            }

            Vector3 hitPosition;
            if (!MusicPoint(out hitPosition))
            {
                Log("stage=music-decoy no-visible-front-placement");
                return false;
            }

            var idle = donors.Where(p => p.AIData.BotOwner.Mover.IsMoving == false).ToArray();
            var donor = idle.Length > 0 ? idle[Random.Range(0, idle.Length)] : donors[Random.Range(0, donors.Length)];
            var renderers = new List<Renderer>();
            donor.PlayerBody.GetRenderersNonAlloc(renderers);
            var visual = CreateVisual(
                donor.Transform.Original,
                ActorRenderers(donor),
                hitPosition,
                hitPosition,
                PsychMusicPolicy.WaitSeconds,
                false,
                donor
            );
            if (visual == null)
            {
                return false;
            }

            visual.Music = visual.Beckon = true;
            var facing = local.Position - hitPosition;
            facing.y = 0;
            if (facing.sqrMagnitude > .1f)
            {
                visual.Root.transform.rotation = Quaternion.LookRotation(facing);
            }

            musicVisual = visual;
            musicApproached = -1;
            decoyCount++;
            Log("stage=music-decoy-created position=" + hitPosition + " radiusMetres=" + PsychMusicPolicy.ApproachMetres);
            return true;
        }

        private void TickMusic()
        {
            if (musicVisual == null)
            {
                return;
            }

            if (musicApproached < 0)
            {
                if (musicVisual.Root == null || Now >= musicVisual.Born + musicVisual.Life)
                {
                    StopMusic();
                    return;
                }

                if ((Plugin.Local!.Position - musicVisual.Start).sqrMagnitude > PsychMusicPolicy.ApproachMetres * PsychMusicPolicy
                    .ApproachMetres)
                {
                    return;
                }

                Vector3 ear = PsychWorldScares.ViewCamera != null ? PsychWorldScares.ViewCamera.transform.position : Plugin.Local
                    .Position + Vector3.up * 1.6f;
                if (!PsychWorldScares.ClearSight(musicVisual.Root.transform, musicVisual.Start + Vector3.up * 1.4f))
                {
                    return;
                }

                musicRoot = new GameObject("RR_PhantomJukeboxAudio");
                musicRoot.transform.position = musicVisual.Start + Vector3.up;
                musicSource = musicRoot.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 1;
                musicSource.dopplerLevel = 0;
                musicSource.rolloffMode = AudioRolloffMode.Linear;
                musicSource.minDistance = 2;
                musicSource.maxDistance = 8;
                musicSource.volume = 0;
                if (Singleton<BetterAudio>.Instantiated)
                {
                    var native = Singleton<BetterAudio>.Instance.GetSource(BetterAudio.AudioSourceGroupType.Weaponry);
                    if (native != null)
                    {
                        musicSource.outputAudioMixerGroup = native.source1.outputAudioMixerGroup;
                        native.Release();
                    }
                }

                musicSource.clip = music[Random.Range(0, music.Length)];
                musicSource.Play();
                if (musicSource.clip.length > 40)
                {
                    musicSource.time = Random.Range(10f, musicSource.clip.length - 20);
                }

                musicApproached = Now;
                musicVisual.Life = Now - musicVisual.Born + PsychMusicPolicy.ListenSeconds + PsychMusicPolicy.FadeSeconds;
                musicVisual.FadeAt = Now + PsychMusicPolicy.ListenSeconds;
                musicVisual.FadeDuration = (float)PsychMusicPolicy.FadeSeconds;
                Log("stage=music-decoy-playing track=" + musicSource.clip.name);
            }

            double elapsed = Now - musicApproached;
            if (musicSource != null)
            {
                musicSource.volume = .65f * (float)PsychMusicPolicy.Gain(elapsed);
            }

            if (elapsed >= PsychMusicPolicy.ListenSeconds + PsychMusicPolicy.FadeSeconds)
            {
                Log("stage=music-decoy-faded seconds=5");
                StopMusic();
            }
        }

        private void StopMusic()
        {
            if (musicRoot != null)
            {
                Object.Destroy(musicRoot);
            }

            musicRoot = null;
            musicSource = null;
            musicVisual = null;
            musicApproached = -1;
        }

        internal void LateTick()
        {
            if (active)
            {
                worldScares.LateTick(Plugin.Local);
                medical.LateTick();
                TickGunSounds();
            }
        }

        internal void ManualDoor(Door door)
        {
            if (active)
            {
                previousRoom = false;
                lastRoomPosition = Plugin.Local!.Position;
            }
        }

        private Visual? CreateVisual(Transform source, IEnumerable<Renderer> renderers, Vector3 start, Vector3 end,
             float life, bool grenade, Player? actor)
        {
            var v = new Visual
            {
                Root = new GameObject("RR_PhantomVisual"),
                SourceRoot = source,
                Actor = actor,
                Start = start,
                End = end,
                Born = Now,
                Life = life,
                Grenade = grenade
            };
            v.Root.transform.position = start;
            try
            {
                var heldRoot = (actor?.HandsController as Player.FirearmController)?.ControllerGameObject.transform;
                foreach (var renderer in renderers.Where(r => r != null && (heldRoot == null || !r.transform.IsChildOf(heldRoot)))
                    .Distinct().Take(actor != null ? 32 : 6))
                {
                    var part = new GameObject("mesh");
                    part.transform.SetParent(v.Root.transform, false);
                    Renderer copy;
                    if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        Transform Bone(Transform bone)
                        {
                            if (bone == null)
                            {
                                return v.Root.transform;
                            }

                            if (v.Bones.TryGetValue(bone, out var existing))
                            {
                                return existing;
                            }

                            var created = new GameObject("visual_bone").transform;
                            created.SetParent(v.Root.transform, false);
                            v.Bones[bone] = created;
                            return created;
                        }

                        var skinned = part.AddComponent<SkinnedMeshRenderer>();
                        skinned.sharedMesh = skin.sharedMesh;
                        skinned.bones = skin.bones.Select(Bone).ToArray();
                        if (skin.rootBone != null)
                        {
                            skinned.rootBone = Bone(skin.rootBone);
                        }

                        skinned.localBounds = actor != null ? new Bounds(Vector3.up, new Vector3(3, 4, 3)) : skin.localBounds;
                        skinned.updateWhenOffscreen = true;
                        copy = skinned;
                    }
                    else if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh is Mesh mesh)
                    {
                        part.AddComponent<MeshFilter>().sharedMesh = mesh;
                        copy = part.AddComponent<MeshRenderer>();
                    }
                    else
                    {
                        Object.Destroy(part);
                        continue;
                    }

                    copy.sharedMaterials = renderer.sharedMaterials;
                    copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    copy.receiveShadows = false;
                    v.Parts.Add(new Part { Source = renderer, Copy = part });
                }

                if (v.Parts.Count == 0)
                {
                    Destroy(v);
                    return null;
                }

                visuals.Add(v);
                UpdateVisual(v);
                if (actor != null)
                {
                    AddAssembledWeapon(v, actor, renderers);
                }

                return v;
            }
            catch
            {
                Destroy(v);
                throw;
            }
        }

        private void UpdateVisual(Visual v)
        {
            ApplyVisualFade(v);
            float elapsed = Now - v.Born;
            float t = Mathf.Clamp01(elapsed / (v.Grenade ? 1.15f : v.Life));
            v.Root.transform.position = v.Falling ? v.Start + v.Velocity * elapsed + Vector3.down * (3.5f * elapsed * elapsed)
                : Vector3.Lerp(v.Start, v.End, t) + (v.Grenade ? Vector3.up * Mathf.Sin(t * Mathf.PI) * 2 : Vector3.zero);
            if (v.Falling)
            {
                v.Root.transform.rotation = Quaternion.Euler(elapsed * 210, elapsed * 170, elapsed * 90);
            }

            Vector3 direction = v.End - v.Start;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.1f)
            {
                v.Root.transform.rotation = Quaternion.LookRotation(v.ReverseFacing ? -direction : direction);
                if (v.Aircraft)
                {
                    // Local aircraft axes: pitch X, yaw Y, roll Z. Cosmetic mesh
                    // transforms only; native airdrops/flight physics are absent.
                    v.Root.transform.rotation *= Quaternion.Euler(v.AircraftRotation + v.AircraftSpin * elapsed);
                }
            }

            if (v.Grenade)
            {
                v.Root.transform.rotation = Quaternion.Euler(elapsed * 500, elapsed * 220, 0);
            }

            bool updatePose = !v.Music && !v.Static && Now >= v.PoseAt;
            if (updatePose)
            {
                v.PoseAt = Now + 0.1f;
            }

            if (updatePose)
            {
                foreach (var pair in v.Bones)
                {
                    if (pair.Key == null)
                    {
                        continue;
                    }

                    pair.Value.localPosition = v.SourceRoot.InverseTransformPoint(pair.Key.position);
                    pair.Value.localRotation = Quaternion.Inverse(v.SourceRoot.rotation) * pair.Key.rotation;
                    pair.Value.localScale = pair.Key.lossyScale;
                }
            }

            if (updatePose)
            {
                foreach (var part in v.Parts)
                {
                    if (part.HeldWeapon || part.Source == null)
                    {
                        continue;
                    }

                    part.Copy.transform.localPosition = v.SourceRoot.InverseTransformPoint(part.Source.transform.position);
                    part.Copy.transform.localRotation = Quaternion.Inverse(v.SourceRoot.rotation) * part.Source.transform.rotation;
                    var s = part.Source.transform.lossyScale;
                    var p = v.SourceRoot.lossyScale;
                    part.Copy.transform.localScale = new Vector3(s.x / p.x, s.y / p.y, s.z / p.z);
                }
            }

            if (v.Beckon)
            {
                Beckon(v);
            }

            UpdateWeaponAnchor(v);
            if (!v.Grenade && !v.Music && !v.Static && elapsed > 1 && Now >= v.VisibilityAt)
            {
                v.VisibilityAt = Now + 0.25f;
                var camera = PsychWorldScares.ViewCamera;
                if (camera != null)
                {
                    var point = camera.WorldToViewportPoint(v.Root.transform.position + Vector3.up);
                    if (point.z < 0 || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1 || !PsychWorldScares.ClearSight(v.Root
                        .transform, v.Root.transform.position + Vector3.up))
                    {
                        v.Life = Mathf.Min(v.Life, elapsed + 0.1f);
                    }
                }
            }

            if (v.Grenade && elapsed > 1.2f && !v.Bounced)
            {
                v.Bounced = true;
                Attempt("grenade-bounce", () => Sound(Wire()._soundStorage.GetDefuseCollisionSound().GetClip(), v.End, 0.8f));
            }

            if (v.Grenade && elapsed > 3.6f)
            {
                if (effects != null)
                {
                    var effect = effects.EffectsArray.FirstOrDefault(e => e.Name != null && (e.Name.IndexOf("grenade", StringComparison
                        .OrdinalIgnoreCase) >= 0 || e.Name.Equals("explosion", StringComparison.OrdinalIgnoreCase)));
                    if (effect != null)
                    {
                        effects.EmitGrenade(effect.Name, v.End, Vector3.up, 0.8f);
                    }
                }

                v.Life = elapsed;
                Log("stage=grenade-dissolved visual-effects-only");
            }
        }

        private static void Destroy(Visual v)
        {
            foreach (var part in v.Parts)
            {
                if (part.OwnedMesh != null)
                {
                    Object.Destroy(part.OwnedMesh);
                }
            }

            foreach (var material in v.FadeMaterials)
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }

            v.FadeMaterials.Clear();
            if (v.Root != null)
            {
                Object.Destroy(v.Root);
            }
        }
    }

    [HarmonyPatch(typeof(Door), nameof(Door.Interact), new[] { typeof(InteractionResult) })]
    internal static class PsychManualDoor
    {
        private static void Prefix(Door __instance, InteractionResult interactionResult)
        {
            if (interactionResult.InteractionType == EInteractionType.Open && Plugin.Local != null && __instance
                .InteractingPlayer?.ProfileId == Plugin.Local.ProfileId)
            {
                SchizophreniaDirector.Current?.ManualDoor(__instance);
            }
        }
    }
}
