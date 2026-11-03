using System;
using System.Collections.Generic;
using System.Diagnostics;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    // Brightness only: do not change lamp power state, enabled state, destruction,
    // GameObject activation, quest switches, baked lightmaps or player equipment.
    internal sealed class LightningLightFlicker
    {
        private interface ITarget
        {
            int Id { get; }

            void Apply(float gain);
            void Restore();
        }

        private sealed class Scalar(Object owner, int id, Func<float> read, Action<float> write) : ITarget
        {
            public int Id => id;

            private bool owned;
            private float native;
            private float applied;
            public void Apply(float gain)
            {
                if (owner == null)
                {
                    return;
                }

                float value = read();
                if (!owned || Mathf.Abs(value - applied) > 0.0001f)
                {
                    native = value;
                }

                float next = native * gain;
                if (Mathf.Abs(next - value) > 0.0001f)
                {
                    write(next);
                }

                applied = next;
                owned = true;
            }

            public void Restore()
            {
                if (owned && owner != null && Mathf.Abs(read() - applied) < 0.0001f && Mathf.Abs(native - applied) > 0.0001f)
                {
                    write(native);
                }

                owned = false;
            }
        }

        private sealed class Colored(Object owner, int id, Func<Color> read, Action<Color> write) : ITarget
        {
            public int Id => id;

            private bool owned;
            private Color native;
            private Color applied;
            public void Apply(float gain)
            {
                if (owner == null)
                {
                    return;
                }

                var value = read();
                if (!owned || value != applied)
                {
                    native = value;
                }

                var next = new Color(native.r * gain, native.g * gain, native.b * gain, native.a);
                if (value != next)
                {
                    write(next);
                }

                applied = next;
                owned = true;
            }

            public void Restore()
            {
                if (owned && owner != null && read() == applied && native != applied)
                {
                    write(native);
                }

                owned = false;
            }
        }

        private readonly List<ITarget> targets = new();
        private readonly HashSet<int> ids = new();
        private readonly HashSet<int> drivenLights = new();
        private readonly HashSet<int> emissionRenderers = new();
        private readonly Stack<Transform> pending = new();
        private readonly HashSet<int> scenes = new();
        private bool initialized;
        private float strikeAt = -100;
        private int nodes;
        private int lamps;
        private static readonly System.Reflection.FieldInfo AreaMultiplier = AccessTools
            .Field(typeof(CullingAdvancedLightObject), "_intensityMultiplier");
        private static readonly int EmissionVisibility = Shader.PropertyToID("_EmissionVisibility");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        internal void Discover()
        {
            if (!initialized)
            {
                initialized = true;
                SceneManager.sceneLoaded += SceneLoaded;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Enqueue(SceneManager.GetSceneAt(i));
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            var clock = Stopwatch.StartNew();
            int budget = 0;
            while (pending.Count > 0 && budget++ < 96)
            {
                var t = pending.Pop();
                if (t == null)
                {
                    continue;
                }

                if (t.name.StartsWith("ROT_", StringComparison.Ordinal) || t.GetComponent<Player>() != null || t
                    .GetComponent<Camera>() != null)
                {
                    continue;
                }

                for (int i = 0; i < t.childCount; i++)
                {
                    pending.Push(t.GetChild(i));
                }

                try
                {
                    Inspect(t);
                }
                catch (Exception e)
                {
                    Plugin.Trace("[RR-LIGHTS] skipped node=" + t.name + " reason=" + e.GetBaseException().Message);
                }

                nodes++;
                if (budget % 8 == 0 && clock.Elapsed.TotalMilliseconds >= 1.5)
                {
                    break;
                }
            }

            if (pending.Count == 0)
            {
                Plugin.Trace($"[RR-LIGHTS] cache-ready nodes={nodes} lamps={lamps} controllableOutputs={targets.Count}; map lights only");
            }
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => Enqueue(scene);
        private void Enqueue(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !scenes.Add(scene.handle))
            {
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                pending.Push(root.transform);
            }
        }

        private void Add(ITarget target)
        {
            if (ids.Add(target.Id))
            {
                targets.Add(target);
            }
        }

        private void Driven(int id)
        {
            drivenLights.Add(id);
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i].Id == id)
                {
                    targets[i].Restore();
                    targets.RemoveAt(i);
                    ids.Remove(id);
                }
            }
        }

        private void Inspect(Transform t)
        {
            var culling = t.GetComponent<CullingLightObject>();
            if (culling != null && culling._light != null && culling._light.type != LightType.Directional)
            {
                Driven(culling._light.GetInstanceID());
                Add(new Scalar(
                        culling,
                        culling.GetInstanceID(),
                        () => culling.IntensityMultiplier,
                        v =>
                {
                    culling.IntensityMultiplier = v;
                    culling.UpdateIntensity();
                }
                    ));
            }

            var areaCulling = t.GetComponent<CullingAdvancedLightObject>();
            if (areaCulling != null && areaCulling._light != null && AreaMultiplier != null)
            {
                Driven(areaCulling._light.GetInstanceID());
                Add(new Scalar(
                        areaCulling,
                        areaCulling.GetInstanceID(),
                        () => (float)AreaMultiplier.GetValue(areaCulling),
                        v =>
                {
                    areaCulling.IntensityMultiplier = v;
                    areaCulling.UpdateIntensity();
                }
                    ));
            }

            var light = t.GetComponent<Light>();
            if (light != null && light.type != LightType.Directional && !drivenLights.Contains(light.GetInstanceID()))
            {
                Add(new Scalar(light, light.GetInstanceID(), () => light.intensity, v => light.intensity = v));
            }

            var area = t.GetComponent<BaseLight>();
            if (area != null && !drivenLights.Contains(area.GetInstanceID()))
            {
                Add(new Scalar(area, area.GetInstanceID(), () => area.m_Intensity, v => area.m_Intensity = v));
            }

            var custom = t.GetComponent<AdvancedLight>();
            if (custom != null && custom.LightType != AdvancedLight.LightTypeEnum.Directional)
            {
                Add(new Colored(custom, custom.GetInstanceID(), () => custom.Color, v => custom.Color = v));
            }

            var lamp = t.GetComponent<LampController>();
            if (lamp != null)
            {
                lamps++;
                foreach (var entry in lamp._materialsWithEmission ?? Array.Empty<MaterialEmission>())
                {
                    RegisterEmission(entry);
                }

                foreach (var entry in lamp._legacyMaterialsWithEmission ?? Array.Empty<MaterialColor>())
                {
                    RegisterEmission(entry);
                }
            }

            // Some window/neon/sign meshes have no LampController. Inspect only named
            // fixtures, not every material in the map, and only touch emission channels.
            string name = t.name;
            if (name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("lamp", StringComparison
                .OrdinalIgnoreCase) >= 0 || name.IndexOf("neon", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("window",
                 StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("sign", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var renderer = t.GetComponent<MeshRenderer>();
                if (renderer != null && !emissionRenderers.Contains(renderer.GetInstanceID()))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != null && (material.HasProperty(EmissionVisibility) || material.HasProperty(EmissionColor)))
                        {
                            RegisterEmission(renderer, material, null);
                            break;
                        }
                    }
                }
            }
        }

        private void RegisterEmission(MaterialData entry)
        {
            if (entry?.Renderer == null)
            {
                return;
            }

            var material = entry.Material;
            if (material == null)
            {
                return;
            }

            RegisterEmission(entry.Renderer, material, entry is MaterialColor colorEntry ? colorEntry.ParemeterName : null);
        }

        private void RegisterEmission(Renderer renderer, Material material, string? customColor)
        {
            if (emissionRenderers.Contains(renderer.GetInstanceID()))
            {
                return;
            }

            // Preserve unrelated renderer properties; no shared material is modified.
            var block = new MaterialPropertyBlock();
            if (string.IsNullOrEmpty(customColor) && material.HasProperty(EmissionVisibility))
            {
                emissionRenderers.Add(renderer.GetInstanceID());
                float fallback = material.GetFloat(EmissionVisibility);
                Add(new Scalar(
                        renderer,
                        renderer.GetInstanceID(),
                        () =>
                {
                    renderer.GetPropertyBlock(block);
                    return block.HasFloat(EmissionVisibility) ? block.GetFloat(EmissionVisibility) : fallback;
                },
                        v =>
                {
                    renderer.GetPropertyBlock(block);
                    block.SetFloat(EmissionVisibility, v);
                    renderer.SetPropertyBlock(block);
                }
                    ));
            }
            else if (!string.IsNullOrEmpty(customColor) || material.HasProperty(EmissionColor))
            {
                int property = string.IsNullOrEmpty(customColor) ? EmissionColor : Shader.PropertyToID(customColor);
                if (!material.HasProperty(property))
                {
                    return;
                }

                emissionRenderers.Add(renderer.GetInstanceID());
                var fallback = material.GetColor(property);
                Add(new Colored(
                        renderer,
                        renderer.GetInstanceID(),
                        () =>
                {
                    renderer.GetPropertyBlock(block);
                    return block.HasColor(property) ? block.GetColor(property) : fallback;
                },
                        v =>
                {
                    renderer.GetPropertyBlock(block);
                    block.SetColor(property, v);
                    renderer.SetPropertyBlock(block);
                }
                    ));
            }
        }

        internal int OutputCount => targets.Count;

        internal void RestoreOutputs()
        {
            foreach (var target in targets)
            {
                try
                {
                    target.Restore();
                }
                catch (Exception error)
                {
                    Plugin.Trace("[RR-LIGHTS] restore skipped=" + error.GetBaseException().Message);
                }
            }
        }

        internal void RegisterNode(Transform t) => Inspect(t);
        internal void Drive(float gain)
        {
            foreach (var target in targets)
            {
                target.Apply(gain);
            }
        }

        internal void Strike()
        {
            if (!TODSkyProvider.IsAvailable || !LightFlickerPolicy.Night(TODSkyProvider.Instance.Cycle.Hour))
            {
                return;
            }

            Stop();
            strikeAt = Time.unscaledTime;
            Plugin.Trace($"[RR-LIGHTS] night-strike outputs={targets.Count} lamps={lamps} discoveryPending={pending.Count}");
        }

        internal void LateTick()
        {
            if (strikeAt < 0)
            {
                return;
            }

            float elapsed = Time.unscaledTime - strikeAt;
            if (elapsed >= LightFlickerPolicy.Duration)
            {
                Stop();
                return;
            }

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var target = targets[i];
                try
                {
                    target.Apply((float)LightFlickerPolicy.Gain(elapsed, (target.Id & 255) / 255d * 0.12));
                }
                catch (Exception e)
                {
                    try
                    {
                        target.Restore();
                    }
                    catch
                    {
                    }

                    targets.RemoveAt(i);
                    ids.Remove(target.Id);
                    Plugin.Trace("[RR-LIGHTS] removed unavailable output=" + e.GetBaseException().Message);
                }
            }
        }

        internal void Stop()
        {
            if (strikeAt < 0)
            {
                return;
            }

            foreach (var target in targets)
            {
                try
                {
                    target.Restore();
                }
                catch (Exception e)
                {
                    Plugin.Trace("[RR-LIGHTS] restore skipped=" + e.GetBaseException().Message);
                }
            }

            strikeAt = -100;
        }

        internal void Dispose()
        {
            Stop();
            if (initialized)
            {
                SceneManager.sceneLoaded -= SceneLoaded;
            }

            targets.Clear();
            ids.Clear();
            drivenLights.Clear();
            emissionRenderers.Clear();
            pending.Clear();
            scenes.Clear();
            initialized = false;
            nodes = lamps = 0;
        }
    }
}
