using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using Comfort.Common;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
using WindowsManagerUtilities;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed class PsychGlassIllusion
    {
        private sealed class Pane
        {
            internal WindowBreaker Window = null!;
            internal Renderer Renderer = null!;
            internal bool ForcedOff;
            internal WindowsManager? Manager;
            internal ComputeBuffer? Buffer;
            internal int Index;
        }

        private sealed class Shard
        {
            internal GameObject Root = null!;
            internal Mesh Mesh = null!;
            internal Vector3 Start;
            internal Vector3 Velocity;
            internal Vector3 Spin;
            internal Quaternion Rotation;
            internal float Born;
        }

        private readonly List<WindowBreaker> windows = new();
        private readonly HashSet<WindowBreaker> knownWindows = new();
        private float nextRegistryRefresh;
        private readonly List<Pane> panes = new();
        private readonly List<Shard> shards = new();
        private readonly Queue<Pane> pending = new();
        private readonly Collider[] overlap = new Collider[128];
        private float restoreAt;
        private static readonly System.Reflection.FieldInfo Indices = AccessTools.Field(typeof(WindowsManager), "_breakerIdInstanceId");
        private static readonly System.Reflection.FieldInfo Data = AccessTools.Field(typeof(WindowsManager), "_renderData");
        private static readonly System.Reflection.FieldInfo Enables = AccessTools.Field(typeof(WindowsManager), "_instancesEnable");
        private static readonly int[] Hidden =
        {
            0
        };
        private static readonly int[] Shown =
        {
            1
        };
        internal void Add(WindowBreaker window)
        {
            if (knownWindows.Add(window))
            {
                windows.Add(window);
            }
        }

        private static Bounds BoundsFor(WindowBreaker w) => w.ObstructiveCollider != null ? w.ObstructiveCollider.bounds
            : w.GlassBallisticCollider?.GetComponent<Collider>()is Collider collider ? collider.bounds : w.Renderer.bounds;
        internal int NearbyIntactCount(Player player)
        {
            RefreshRegistry();
            var origin = player.Position + Vector3.up * 1.4f;
            int count = 0;
            foreach (var window in windows)
            {
                if (window == null || window.Renderer == null)
                {
                    continue;
                }

                var point = BoundsFor(window).ClosestPoint(origin);
                if ((point - origin).sqrMagnitude < 9.3f && !window.IsDamaged && PsychWorldScares.ClearSight(window.transform,
                     point) && ++count >= 2)
                {
                    return count;
                }
            }

            return count;
        }

        private void RefreshRegistry()
        {
            if (Time.unscaledTime < nextRegistryRefresh || !Singleton<GameWorld>.Instantiated)
            {
                return;
            }

            nextRegistryRefresh = Time.unscaledTime + 10;
            foreach (var window in Singleton<GameWorld>.Instance.Windows._iteration)
            {
                if (window != null)
                {
                    Add(window);
                }
            }
        }

        internal bool Trigger(Player player, bool nearbyOnly = false)
        {
            if (panes.Count > 0)
            {
                return false;
            }

            RefreshRegistry();
            var origin = PsychWorldScares.ViewCamera != null ? PsychWorldScares.ViewCamera.transform.position : player
                .Position + Vector3.up * 1.4f;
            var camera = PsychWorldScares.ViewCamera;
            const float coneRange = GlassScarePolicy.ForwardRange;
            bool InCone(WindowBreaker w)
            {
                var point = BoundsFor(w).ClosestPoint(origin);
                var delta = point - origin;
                return (nearbyOnly ? delta.sqrMagnitude < 9.3f : GlassScarePolicy.InForwardCone(delta.sqrMagnitude, camera != null
                    ? Vector3.Dot(camera.transform.forward, delta.normalized) : 1)) && PsychWorldScares.ClearSight(w.transform, point);
            }

            int found = Physics.OverlapSphereNonAlloc(origin, GlassScarePolicy.Radius, overlap, Physics.DefaultRaycastLayers,
                 QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                var window = overlap[i].GetComponentInParent<WindowBreaker>();
                if (window != null)
                {
                    Add(window);
                }

                overlap[i] = null!;
            }

            var eligible = windows.Where(w => w != null && !w.IsDamaged && w.Renderer != null && InCone(w)).OrderBy(w =>
                 (BoundsFor(w).ClosestPoint(origin) - origin).sqrMagnitude).Take(GlassScarePolicy.MaxPanes).ToArray();
            foreach (var window in eligible)
            {
                var renderer = window.Renderer;
                var pane = new Pane
                {
                    Window = window,
                    Renderer = renderer,
                    ForcedOff = renderer.forceRenderingOff
                };
                var manager = WindowsManager.Instance;
                // Some maps draw glass through a shared GPU buffer, not Renderer.enabled.
                // Borrow only that pane's visual enable bit; never call BreakWindow/Break.
                if (manager != null && manager.IsActive && Indices.GetValue(manager)is Dictionary<string, int> indices && indices
                    .TryGetValue(window.Id, out int index))
                {
                    var buffer = (Data.GetValue(manager) as GeometryComputeBuffers)?._instancesEnable;
                    var states = Enables.GetValue(manager) as List<int>;
                    if (buffer == null || !buffer.IsValid() || index < 0 || index >= buffer.count || states == null || index >= states
                        .Count || states[index] != 1)
                    {
                        continue;
                    }

                    buffer.SetData(Hidden, 0, index, 1);
                    pane.Manager = manager;
                    pane.Buffer = buffer;
                    pane.Index = index;
                }
                else if (!renderer.enabled)
                {
                    continue;
                }

                renderer.forceRenderingOff = true;
                panes.Add(pane);
                pending.Enqueue(pane);
            }

            if (panes.Count == 0)
            {
                Plugin.Trace("[RR-PSYCH] stage=glass no-supported-intact-panes-in-forward-cone cached=" + windows.Count
                    + " intact=" + windows.Count(w => w != null && !w.IsDamaged) + " candidates=" + eligible.Length);
                return false;
            }

            restoreAt = Time.unscaledTime + GlassScarePolicy.Duration;
            Plugin.Trace($"[RR-PSYCH] stage=glass-hidden panes={panes.Count} forwardConeMetres={coneRange} duration={GlassScarePolicy.Duration} collisionUnchanged=true");
            return true;
        }

        internal void Tick()
        {
            if (panes.Count == 0)
            {
                return;
            }

            if (Time.unscaledTime >= restoreAt)
            {
                Restore();
                return;
            }

            // Spread cosmetic mesh creation over frames instead of rebuilding every pane
            // in one frame. Native window meshes and colliders are never modified.
            if (pending.Count > 0)
            {
                var pane = pending.Dequeue();
                if (pane.Window != null && !pane.Window.IsDamaged)
                {
                    try
                    {
                        CreateShards(pane);
                        if (panes.IndexOf(pane) < 4)
                        {
                            var point = pane.Renderer.bounds.center;
                            pane.Window.PlayHitSound(in point);
                        }
                    }
                    catch (Exception e)
                    {
                        Plugin.Trace("[RR-PSYCH] stage=glass-shards " + e.GetBaseException().Message);
                    }
                }
            }

            foreach (var pane in panes)
            {
                if (pane.Renderer != null && pane.Window != null)
                {
                    if (pane.Window.IsDamaged)
                    {
                        pane.Renderer.forceRenderingOff = pane.ForcedOff;
                    }
                    else
                    {
                        pane.Renderer.forceRenderingOff = true;
                    }
                }
            }

            foreach (var shard in shards)
            {
                if (shard.Root == null)
                {
                    continue;
                }

                float t = Time.unscaledTime - shard.Born;
                if (t > 2.2f)
                {
                    Object.Destroy(shard.Root);
                    continue;
                }

                shard.Root.transform.position = shard.Start + shard.Velocity * t + Vector3.down * (4.9f * t * t);
                shard.Root.transform.rotation = shard.Rotation * Quaternion.Euler(shard.Spin * t);
            }
        }

        private void CreateShards(Pane pane)
        {
            var source = pane.Renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (source == null)
            {
                return;
            }

            var bounds = source.bounds;
            var size = bounds.size;
            int thin = size.x < size.y ? (size.x < size.z ? 0 : 2) : (size.y < size.z ? 1 : 2);
            int x = (thin + 1) % 3;
            int y = (thin + 2) % 3;
            if (size[x] < .05f || size[y] < .05f)
            {
                return;
            }

            var material = pane.Window.Material ?? pane.Renderer.sharedMaterial;
            if (material == null)
            {
                return;
            }

            var t = pane.Renderer.transform;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    Vector3 Point(float u, float v)
                    {
                        var p = bounds.center;
                        p[x] = bounds.min[x] + size[x] * u;
                        p[y] = bounds.min[y] + size[y] * v;
                        return t.TransformPoint(p);
                    }

                    float u = col / 2f;
                    float v = row / 3f;
                    var a = Point(u, v);
                    var b = Point(u + .5f, v);
                    var c = Point(u + .5f, v + 1f / 3);
                    var d = Point(u, v + 1f / 3);
                    Triangle(a, b, c, new Vector2(u, v), new Vector2(u + .5f, v), new Vector2(u + .5f, v + 1f / 3), material);
                    Triangle(a, c, d, new Vector2(u, v), new Vector2(u + .5f, v + 1f / 3), new Vector2(u, v + 1f / 3), material);
                }
            }
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Material material)
        {
            var centre = (a + b + c) / 3;
            var mesh = new Mesh
            {
                name = "RR_PhantomGlassShard"
            };
            mesh.vertices = new[]
            {
                a - centre,
                b - centre,
                c - centre,
                a - centre,
                c - centre,
                b - centre
            };
            mesh.triangles = new[]
            {
                0,
                1,
                2,
                3,
                4,
                5
            };
            mesh.uv = new[]
            {
                ua,
                ub,
                uc,
                ua,
                uc,
                ub
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var root = new GameObject("RR_PhantomGlassShard");
            root.transform.position = centre;
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shards.Add(new Shard { Root = root, Mesh = mesh, Start = centre, Rotation = root.transform.rotation, Born = Time
                .unscaledTime, Velocity = new Vector3(Random.Range(-.6f, .6f), Random.Range(.2f, .8f), Random.Range(-.6f, .6f)),
                 Spin = Random.insideUnitSphere * 160 });
        }

        private void Restore()
        {
            foreach (var pane in panes)
            {
                try
                {
                    if (pane.Renderer != null)
                    {
                        pane.Renderer.forceRenderingOff = pane.ForcedOff;
                    }

                    if (pane.Window != null && GlassScarePolicy.RestoreIntact(pane.Window.IsDamaged) && pane.Manager != null && pane
                        .Manager == WindowsManager.Instance && pane.Buffer != null && pane.Buffer.IsValid() && (Data.GetValue(pane
                        .Manager) as GeometryComputeBuffers)?._instancesEnable == pane.Buffer && Indices.GetValue(pane
                        .Manager)is Dictionary<string, int> indices && indices.GetValueOrDefault(pane.Window.Id, -1) == pane.Index)
                    {
                        pane.Buffer.SetData(Shown, 0, pane.Index, 1);
                    }
                }
                catch (Exception e)
                {
                    Plugin.Trace("[RR-PSYCH] stage=glass-restore " + e.GetBaseException().Message);
                }
            }

            foreach (var shard in shards)
            {
                if (shard.Root != null)
                {
                    Object.Destroy(shard.Root);
                }

                if (shard.Mesh != null)
                {
                    Object.Destroy(shard.Mesh);
                }
            }

            if (panes.Count > 0)
            {
                Plugin.Trace("[RR-PSYCH] stage=glass-restored genuineDamagePreserved=true");
            }

            panes.Clear();
            pending.Clear();
            shards.Clear();
            restoreAt = 0;
        }

        internal void Stop()
        {
            Restore();
            windows.Clear();
            knownWindows.Clear();
            nextRegistryRefresh = 0;
            Array.Clear(overlap, 0, overlap.Length);
        }
    }
}
