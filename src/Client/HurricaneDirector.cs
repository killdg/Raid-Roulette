using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Weather;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    // Cosmetic pooled meshes: no colliders, Rigidbody components, damage or loot.
    internal sealed class HurricaneDirector
    {
        private sealed class Piece
        {
            internal GameObject Root = null!;
            internal MeshRenderer Renderer = null!;
            internal Vector3 Velocity;
            internal Vector3 Spin;
            internal Vector3 Checked;
            internal float Age;
            internal float Life;
            internal bool Live;
            internal bool Blocked;
            internal int Kind;
            internal float Tint;
        }

        internal static bool Active;
        internal static bool ExtraDrops;
        internal static Vector2 Wind;
        internal static float Gust;
        internal static float Storm;
        private GameObject? root;
        private readonly List<Piece> pieces = new();
        private readonly Material[] materials = new Material[3];
        private readonly Mesh[] meshes = new Mesh[3];
        private readonly Texture2D[] textures = new Texture2D[3];
        private readonly MaterialPropertyBlock color = new();
        private AudioSource? windSound;
        private AudioClip? windClip;
        private float[]? windSamples;
        private float nextSpawn;
        private float nextWindStart;
        private float nextLog;
        private float nightBlend;
        private int scan;
        private bool assetsReady;
        private bool assetsFailed;
        private bool litMaterials;
        private Vector2 baseline;
        private WindController? controller;
        private static readonly int TerrainMask = LayerMask.GetMask("Terrain");
        private static readonly int ObstacleMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "LowPolyCollider",
             "DoorLowPolyCollider", "TransparentCollider");
        internal void Tick(float strength, double elapsed, double exposure, int requested, bool extraDrops)
        {
            var player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player == null)
            {
                return;
            }

            if (assetsReady && root == null)
            {
                Dispose();
            }

            Active = true;
            ExtraDrops = extraDrops;
            Storm = strength;
            Gust = (float)HurricanePolicy.Gust(elapsed);
            var weather = WeatherController.Instance;
            if (controller == null)
            {
                controller = weather.WindController;
                baseline = weather.WeatherCurve.Wind;
            }

            var direction = baseline.sqrMagnitude > 0.001f ? baseline.normalized : new Vector2(0.8f, 0.6f);
            // Slow direction changes rather than a new random heading each frame.
            float turn = Mathf.Sin((float)elapsed * 0.035f) * 0.22f;
            direction = new Vector2(
                direction.x * Mathf.Cos(turn) - direction.y * Mathf.Sin(turn),
                direction.x * Mathf.Sin(turn) + direction.y * Mathf.Cos(turn)
            );
            Wind = direction * (float)HurricanePolicy.Wind(strength, Gust);
            if (!assetsReady && !assetsFailed)
            {
                CreateAssets();
            }

            if (!assetsReady)
            {
                return;
            }

            int limit = HurricanePolicy.Limit(requested);
            bool night = TODSkyProvider.IsAvailable && HurricanePolicy.NightDebris(TODSkyProvider.Instance.Cycle.Hour);
            nightBlend = Mathf.MoveTowards(nightBlend, night ? 1 : 0, Time.unscaledDeltaTime / 3);
            // Two allocations per frame during warmup; never build a whole pool in one frame.
            for (int n = 0; n < 2 && night && pieces.Count < limit; n++)
            {
                CreatePiece(pieces.Count);
            }

            WindAudio(strength, exposure);
            if (!night && nightBlend <= 0)
            {
                foreach (var p in pieces)
                {
                    if (p.Live)
                    {
                        Recycle(p);
                    }
                }

                return;
            }

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float now = Time.unscaledTime;
            Vector3 flow = new Vector3(direction.x, 0, direction.y);
            int rays = HurricanePolicy.RayBudget;
            bool outside = RainController.IsCameraUnderRain;
            var camera = Camera.main;
            int large = 0;
            foreach (var p in pieces)
            {
                if (p.Live && p.Kind == 1)
                {
                    large++;
                }
            }

            if (night && strength > 0.15f && now >= nextSpawn && pieces.Count > 0)
            {
                nextSpawn = now + Mathf.Lerp(0.45f, 0.08f, strength) * (1 - 0.55f * Gust);
                foreach (var p in pieces)
                {
                    if (p.Live || pieces.IndexOf(p) >= limit || p.Kind == 1 && (large >= HurricanePolicy.MaxLarge || Gust < 0.3f))
                    {
                        continue;
                    }

                    // World-aligned cells spread the field across streets rather than a
                    // narrow wind tunnel centered on the player. Existing pieces stay put
                    // in world space while the player moves or enters shelter.
                    Vector3 anchor = new Vector3(Mathf.Floor(player.Position.x / 40) * 40 + 20, 0, Mathf.Floor(player.Position
                        .z / 40) * 40 + 20);
                    var pos = anchor + new Vector3(Random.Range(-85f, 85f), 0, Random.Range(-85f, 85f));
                    float horizontalDistance = Vector2.Distance(new Vector2(player.Position.x, player.Position.z), new Vector2(pos.x,
                         pos.z));
                    if (horizontalDistance < 35 || horizontalDistance > 110)
                    {
                        break;
                    }

                    // Probe from a fixed world elevation, never from the player's floor.
                    // Terrain wins over roofs; mesh-only maps fall back to their top surface.
                    var probe = new Vector3(pos.x, 1000, pos.z);
                    rays--;
                    if (Physics.Raycast(probe, Vector3.down, out var ground, 2000, TerrainMask, QueryTriggerInteraction.Ignore))
                    {
                        pos.y = (float)HurricanePolicy.DebrisHeight(ground.point.y, Random.value);
                        rays--;
                        if (Physics.Raycast(pos, Vector3.up, 24, ObstacleMask, QueryTriggerInteraction.Ignore))
                        {
                            break;
                        }
                    }
                    else
                    {
                        rays--;
                        if (!Physics.Raycast(probe, Vector3.down, out ground, 2000, ObstacleMask, QueryTriggerInteraction.Ignore))
                        {
                            break;
                        }

                        pos.y = (float)HurricanePolicy.DebrisHeight(ground.point.y, Random.value);
                    }

                    if (camera != null)
                    {
                        var viewport = camera.WorldToViewportPoint(pos);
                        if (viewport.z > 0 && viewport.x > -.1f && viewport.x < 1.1f && viewport.y > -.1f && viewport.y < 1.1f)
                        {
                            break;
                        }
                    }

                    p.Root.transform.position = pos;
                    p.Root.transform.rotation = Random.rotation;
                    p.Checked = pos;
                    p.Velocity = flow * Random.Range(3f, 7f) + Vector3.up * Random.Range(-0.2f, 0.7f);
                    p.Spin = Random.insideUnitSphere * (p.Kind == 1 ? 90 : 220);
                    p.Age = 0;
                    p.Life = Random.Range(20f, 34f);
                    p.Blocked = false;
                    p.Tint = Random.Range(0.55f, 0.85f);
                    p.Live = true;
                    p.Root.SetActive(true);
                    break;
                }
            }

            foreach (var p in pieces)
            {
                if (!p.Live)
                {
                    continue;
                }

                p.Age += dt;
                float distance = Vector3.Distance(p.Root.transform.position, player.Position);
                if (!p.Blocked && p.Age > p.Life - 4 && distance < 100 && camera != null)
                {
                    var view = camera.WorldToViewportPoint(p.Root.transform.position);
                    if (view.z > 0 && view.x > -.1f && view.x < 1.1f && view.y > -.1f && view.y < 1.1f)
                    {
                        p.Life = p.Age + 4;
                    }
                }

                if (p.Age >= p.Life || distance > 150)
                {
                    Recycle(p);
                    continue;
                }

                float swirl = Mathf.Sin(p.Age * 3.1f + p.Life);
                var target = flow * (4 + 7 * strength + 8 * Gust) + Vector3.up * (swirl * 1.2f + (p.Kind == 1 ? -0.7f : 0.15f));
                p.Velocity = Vector3.Lerp(p.Velocity, target, dt * 0.8f);
                if (!p.Blocked)
                {
                    p.Root.transform.position += p.Velocity * dt;
                    p.Root.transform.Rotate(p.Spin * dt, Space.Self);
                }

                float light = litMaterials ? 1 : Mathf.Clamp(RenderSettings.ambientLight.maxColorComponent * RenderSettings
                    .ambientIntensity, 0.08f, 0.5f);
                float tint = p.Tint * light;
                color.SetColor(
                    "_Color",
                    new Color(
                        tint,
                        tint * 0.96f,
                        tint * 0.87f,
                        (float)(HurricanePolicy.Fade(p.Age, p.Life) * HurricanePolicy.DistanceFade(distance) * nightBlend)
                    )
                );
                p.Renderer.SetPropertyBlock(color);
            }

            // Shared budget includes spawn probes. Round-robin checks catch walls/roofs
            // without a map scan or one raycast for every piece each frame.
            int visited = 0;
            while (rays > 0 && visited++ < pieces.Count && pieces.Count > 0)
            {
                var p = pieces[scan++ % pieces.Count];
                if (!p.Live)
                {
                    continue;
                }

                rays--;
                if (p.Blocked)
                {
                    continue;
                }

                if (Physics.Linecast(p.Checked, p.Root.transform.position, out var hit, ObstacleMask, QueryTriggerInteraction.Ignore))
                {
                    p.Root.transform.position = hit.point + hit.normal * 0.03f;
                    p.Blocked = true;
                    p.Age = Mathf.Max(p.Age, p.Life - 1.2f);
                }
                else
                {
                    p.Checked = p.Root.transform.position;
                }
            }

            if (now >= nextLog)
            {
                nextLog = now + 30;
                int live = 0;
                foreach (var p in pieces)
                {
                    if (p.Live)
                    {
                        live++;
                    }
                }

                Plugin.Trace($"[ROT-HURRICANE] gust={Gust:0.00} wind={Wind.magnitude:0.00} debris={live}/{pieces.Count} outside={outside} extraDrops={extraDrops}");
            }
        }

        private void CreatePiece(int index)
        {
            int kind = index % 9 == 0 ? 1 : index % 3 == 0 ? 2 : 0;
            var go = new GameObject("ROT_Debris_" + index);
            go.transform.SetParent(root!.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = meshes[kind];
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[kind];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            go.transform.localScale = Vector3.one * (kind == 1 ? Random.Range(0.45f, 0.8f) : kind == 2 ? Random.Range(0.08f,
                 0.16f) : Random.Range(0.18f, 0.35f));
            go.SetActive(false);
            pieces.Add(new() { Root = go, Renderer = renderer, Kind = kind });
        }

        private void CreateAssets()
        {
            Shader? shader = null;
            foreach (var name in new[]
            {
                "Legacy Shaders/Transparent/Diffuse",
                "Standard",
                "Sprites/Default",
                "Unlit/Transparent Cutout"
            }

            )
            {
                var candidate = Shader.Find(name);
                if (candidate != null && candidate.isSupported)
                {
                    shader = candidate;
                    break;
                }
            }

            if (shader == null)
            {
                assetsFailed = true;
                Plugin.Trace("[ROT-HURRICANE] no supported debris shader; visual debris unavailable");
                return;
            }

            litMaterials = shader.name != "Sprites/Default" && shader.name != "Unlit/Transparent Cutout";
            root = new GameObject("ROT_Hurricane");
            for (int k = 0; k < 3; k++)
            {
                textures[k] = DebrisArt.Texture(k);
                materials[k] = new Material(shader)
                {
                    name = "ROT_DebrisMaterial_" + k,
                    mainTexture = textures[k]
                };
                materials[k].color = Color.white;
                if (shader.name == "Standard")
                {
                    materials[k].SetFloat("_Mode", 2);
                    materials[k].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    materials[k].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    materials[k].SetInt("_ZWrite", 0);
                    materials[k].EnableKeyword("_ALPHABLEND_ON");
                    materials[k].renderQueue = 3000;
                    materials[k].SetFloat("_Glossiness", 0);
                    materials[k].SetFloat("_Metallic", 0);
                    materials[k].DisableKeyword("_EMISSION");
                }

                // A folded sheet, duplicated triangles so both faces are visible.
                meshes[k] = new Mesh
                {
                    name = "ROT_FoldedDebris_" + k
                };
                meshes[k].vertices = new[]
                {
                    new Vector3(-0.5f, -0.7f, 0),
                    new Vector3(0, -0.7f, 0.13f),
                    new Vector3(0.5f, -0.7f, 0),
                    new Vector3(-0.5f, 0.7f, 0),
                    new Vector3(0, 0.7f, 0.13f),
                    new Vector3(0.5f, 0.7f, 0)
                };
                meshes[k].uv = new[]
                {
                    new Vector2(0, 0),
                    new Vector2(.5f, 0),
                    new Vector2(1, 0),
                    new Vector2(0, 1),
                    new Vector2(.5f, 1),
                    new Vector2(1, 1)
                };
                meshes[k].triangles = new[]
                {
                    0,
                    1,
                    3,
                    1,
                    4,
                    3,
                    1,
                    2,
                    4,
                    2,
                    5,
                    4,
                    3,
                    1,
                    0,
                    3,
                    4,
                    1,
                    4,
                    2,
                    1,
                    4,
                    5,
                    2
                };
                meshes[k].RecalculateNormals();
                meshes[k].RecalculateBounds();
            }

            assetsReady = true;
            Plugin.Trace("[ROT-HURRICANE] procedural weathered debris ready; shader=" + shader.name + " lit=" + litMaterials
                + "; no colliders or external assets");
        }

        private void WindAudio(float strength, double exposure)
        {
            if (root == null)
            {
                return;
            }

            if (windSound == null)
            {
                var audioObject = new GameObject("ROT_HurricaneWind");
                audioObject.transform.SetParent(root.transform, false);
                windSound = audioObject.AddComponent<AudioSource>();
                windSound.playOnAwake = false;
                windSound.loop = true;
            }

            if (windClip == null || windClip.samples <= 0)
            {
                if (windClip != null)
                {
                    Object.Destroy(windClip);
                }

                windSamples ??= DebrisArt.WindSamples();
                windClip = AudioClip.Create("ROT_WindGust", windSamples.Length, 1, 16000, false);
                if (!windClip.SetData(windSamples, 0))
                {
                    return;
                }

                windSound.clip = windClip;
            }

            if (!WeatherAudioOutput.Attach(windSound))
            {
                return;
            }

            windSound.volume = strength * (0.10f + 0.22f * Gust) * Mathf.Lerp(0.15f, 1, (float)exposure);
            if (!windSound.isPlaying && Time.unscaledTime >= nextWindStart)
            {
                nextWindStart = Time.unscaledTime + 2;
                windSound.Play();
            }
        }

        private static void Recycle(Piece p)
        {
            p.Live = false;
            if (p.Root != null)
            {
                p.Root.SetActive(false);
            }
        }

        internal void Stop()
        {
            if (Active && controller != null)
            {
                controller.SetWind(WeatherController.Instance != null ? WeatherController.Instance.WeatherCurve.Wind : baseline);
            }

            ExtraDrops = false;
            Active = false;
            Storm = 0;
            Gust = 0;
            nightBlend = 0;
            controller = null;
            foreach (var p in pieces)
            {
                if (p.Live)
                {
                    Recycle(p);
                }
            }

            if (windSound != null)
            {
                windSound.Stop();
            }
        }

        internal void Dispose()
        {
            Stop();
            if (root != null)
            {
                Object.Destroy(root);
            }

            foreach (var m in materials)
            {
                if (m != null)
                {
                    Object.Destroy(m);
                }
            }

            foreach (var m in meshes)
            {
                if (m != null)
                {
                    Object.Destroy(m);
                }
            }

            foreach (var t in textures)
            {
                if (t != null)
                {
                    Object.Destroy(t);
                }
            }

            if (windClip != null)
            {
                Object.Destroy(windClip);
            }

            pieces.Clear();
            assetsFailed = false;
            assetsReady = false;
            root = null;
        }
    }

    [HarmonyPatch(typeof(WeatherController), nameof(WeatherController.ControlWind))]
    internal static class HurricaneWind
    {
        private static bool Prefix(WeatherController __instance)
        {
            if (!HurricaneDirector.Active)
            {
                return true;
            }

            __instance.WindController.SetWind(HurricaneDirector.Wind);
            return false;
        }
    }

    // Scope the native drop-spawn delay to this Update only. Native glasses/visor
    // state, fade lifetimes and render targets remain owned by the game.
    [HarmonyPatch(typeof(RainScreenDropsPlacer), nameof(RainScreenDropsPlacer.Update))]
    internal static class HurricaneVisorRain
    {
        private static void Prefix(RainScreenDropsPlacer __instance, out float? __state)
        {
            __state = null;
            if (!HurricaneDirector.Active || !HurricaneDirector.ExtraDrops || !RainController.IsCameraUnderRain)
            {
                return;
            }

            __state = __instance._rainDropsDelay;
            __instance._rainDropsDelay = Mathf.Max(
                0.015f,
                __instance._rainDropsDelay / (float)HurricanePolicy.DropRate(HurricaneDirector.Storm, HurricaneDirector.Gust)
            );
        }

        private static Exception? Finalizer(Exception? __exception, RainScreenDropsPlacer __instance, float? __state)
        {
            if (__state.HasValue)
            {
                __instance._rainDropsDelay = __state.Value;
            }

            return __exception;
        }
    }

    [HarmonyPatch(typeof(WeatherController), nameof(WeatherController.SetCloudWind))]
    internal static class HurricaneCloudWind
    {
        private static void Prefix(ref float layerASpeed)
        {
            if (HurricaneDirector.Active)
            {
                layerASpeed = Mathf.Max(layerASpeed, HurricaneDirector.Wind.magnitude * 1.8f);
            }
        }
    }
}
