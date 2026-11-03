using System;
using System.Linq;
using Comfort.Common;
using EFT;
using Diz.Resources;
using EFT.SynchronizableObjects;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private AirplaneView? planeAsset;
        private AirplaneView? PlaneAsset()
        {
            if (planeAsset != null)
            {
                return planeAsset;
            }

            if (!Singleton<IEasyAssets>.Instantiated)
            {
                return null;
            }

            try
            {
                var prefab = Singleton<IEasyAssets>.Instance.GetAsset<AirplaneSynchronizableObject>(InGameBundles
                    .SynchronizableObjectPath[SynchronizableObjectType.AirPlane]);
                planeAsset = prefab ? prefab._views.Values.FirstOrDefault(v => v != null) : null;
            }
            catch (Exception e)
            {
                Log("stage=air-assets-unavailable " + e.GetBaseException().Message);
            }

            return planeAsset;
        }

        private bool OpenSky()
        {
            return Plugin.Local != null && Plugin.Local.Environment == EnvironmentType.Outdoor && !Physics.Raycast(
                Plugin.Local.Position + Vector3.up * 1.8f,
                Vector3.up,
                300,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        private enum PhantomFlight
        {
            Normal,
            UpsideDown,
            BarrelRoll,
            Tumble,
            Blob
        }

        private void FastPlane()
        {
            FastPlaneDirection(Random.value < .5f, (PhantomFlight)Random.Range(0, 5));
        }

        private void FastPlaneDirection(bool reverse, PhantomFlight flight = PhantomFlight.Normal)
        {
            if (!OpenSky())
            {
                return;
            }

            var asset = PlaneAsset();
            if (asset == null)
            {
                Log("stage=plane no-cached-native-prefab");
                return;
            }

            var forward = Plugin.Local!.LookDirection;
            forward.y = 0;
            if (forward.sqrMagnitude < .1f)
            {
                return;
            }

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var centre = Plugin.Local.Position + forward * 120 + Vector3.up * 150;
            var v = CreateVisual(
                asset.transform,
                asset.GetComponentsInChildren<MeshRenderer>(true),
                centre - right * 2133,
                centre + right * 2133,
                32,
                false,
                null
            );
            if (v == null)
            {
                return;
            }

            v.Static = true;
            v.ReverseFacing = reverse;
            v.Aircraft = true;
            if (flight == PhantomFlight.UpsideDown)
            {
                v.AircraftRotation = new Vector3(0, 0, 180);
            }
            else if (flight == PhantomFlight.BarrelRoll)
            {
                v.AircraftSpin = new Vector3(0, 0, 72);
            }
            else if (flight == PhantomFlight.Tumble)
            {
                v.AircraftSpin = new Vector3(45, 60, 80);
            }
            else if (flight == PhantomFlight.Blob)
            {
                // Seven overlapping render-only planes share one travel vector,
                // lifetime and birth time. Use one engine source below, not seven
                // stacked sounds. Clones have no native aircraft scripts/colliders.
                var rotations = new[]
                {
                    new Vector3(0, 180, 0),
                    new Vector3(90, 0, 0),
                    new Vector3(-90, 0, 0),
                    new Vector3(0, 0, 180),
                    new Vector3(0, 0, 90),
                    new Vector3(35, 140, 45)
                };
                for (int index = 0; index < rotations.Length; index++)
                {
                    Vector3 offset = Random.insideUnitSphere * 3f;
                    var extra = CreateVisual(
                        asset.transform,
                        asset.GetComponentsInChildren<MeshRenderer>(true),
                        v.Start + offset,
                        v.End + offset,
                        v.Life,
                        false,
                        null
                    );
                    if (extra == null)
                    {
                        continue;
                    }

                    extra.Static = extra.Aircraft = true;
                    extra.Born = v.Born;
                    extra.ReverseFacing = reverse;
                    extra.AircraftRotation = rotations[index];
                    if (index == rotations.Length - 1)
                    {
                        extra.AircraftSpin = new Vector3(0, 0, 36);
                    }

                    UpdateVisual(extra);
                }
            }

            UpdateVisual(v);
            // Time the display near the middle of the flyby, so attention is already
            // drawn upward. Fireworks are no longer a separate pool event.
            scheduled.Add((Now + 14, () => AirFirework()));
            if (asset.soundClip?.Clip != null)
            {
                var audio = v.Root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.clip = asset.soundClip.Clip;
                audio.loop = true;
                audio.spatialBlend = 1;
                audio.minDistance = 60;
                audio.maxDistance = 600;
                audio.volume = .55f;
                audio.dopplerLevel = 0;
                audio.Play();
            }

            Log("stage=phantom-plane style=" + flight + " reverse=" + reverse + " duration=32 distance=4266 noDrop=true");
        }

        private Material? glowMaterial;
        private Texture2D? glowTexture;
        private Material? FlareMaterial()
        {
            if (glowMaterial != null)
            {
                return glowMaterial;
            }

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader
                .Find("GUI/Text Shader");
            if (shader == null)
            {
                Log("stage=particle-shader-unavailable");
                return null;
            }

            glowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color[1024];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
                    pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - distance), 2));
                }
            }

            glowTexture.SetPixels(pixels);
            glowTexture.Apply();
            glowMaterial = new Material(shader)
            {
                mainTexture = glowTexture,
                renderQueue = 3000
            };
            Log("stage=particle-material shader=" + shader.name);
            return glowMaterial;
        }

        private void StopAirAssets()
        {
            if (glowMaterial != null)
            {
                Object.Destroy(glowMaterial);
            }

            if (glowTexture != null)
            {
                Object.Destroy(glowTexture);
            }

            glowMaterial = null;
            glowTexture = null;
        }

        private void AirFirework()
        {
            if (!OpenSky())
            {
                return;
            }

            var forward = Plugin.Local!.LookDirection;
            forward.y = 0;
            if (forward.sqrMagnitude < .1f)
            {
                return;
            }

            forward.Normalize();
            var centre = Plugin.Local.Position + forward * 55 + Vector3.up * 45;
            for (int i = 0; i < 4; i++)
            {
                var at = centre + new Vector3(Random.Range(-10f, 10f), Random.Range(-3f, 8f), Random.Range(-5f, 5f));
                if (!PsychWorldScares.ClearSight(Plugin.Local.Transform.Original, at))
                {
                    continue;
                }

                if (i == 0)
                {
                    FireworkBurst(at);
                }
                else
                {
                    var captured = at;
                    scheduled.Add((Now + i * .65f, () => FireworkBurst(captured)));
                }
            }
        }

        private void FireworkBurst(Vector3 at)
        {
            Burst(at, Color.HSVToRGB(Random.value, .9f, 1), true);
            if (fireworkSounds.Length > 0)
            {
                var clip = fireworkSounds[Random.Range(0, fireworkSounds.Length)];
                QueueAudioPreparation(clip);
                Sound(clip, at, .75f, 120);
                Log("stage=firework-audio-play clip=" + clip.name + " state=" + clip.loadState);
            }
            else
            {
                Log("stage=firework-audio missing-clips discovery-in-progress=" + (audioBundles == null
                    || audioBundleIndex < audioBundles.Length));
            }
        }

        private void Burst(Vector3 at, Color color, bool firework)
        {
            var material = FlareMaterial();
            if (material == null)
            {
                Log("stage=phantom-particles no-cached-flare-material");
                return;
            }

            var root = new GameObject("RR_PhantomSkyParticles");
            root.transform.position = at;
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1;
            main.startLifetime = firework ? 1.7f : 4;
            main.startSpeed = firework ? 7 : 3;
            main.startSize = firework ? .18f : .65f;
            main.startColor = color;
            main.gravityModifier = firework ? .25f : .08f;
            main.maxParticles = 128;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(firework ? 96 : 12)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .15f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }
            );
            fade.color = gradient;
            var collision = ps.collision;
            collision.enabled = false;
            var triggers = ps.trigger;
            triggers.enabled = false;
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ps.Play();
            sounds.Add((root, Now + 6));
            Log("stage=" + (firework ? "phantom-firework" : "phantom-sky-flares") + " cosmeticOnly=true position=" + at);
        }
    }
}
