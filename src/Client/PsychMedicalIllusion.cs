using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Diz.Jobs;
using Diz.DependencyManager;
using Diz.Resources;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    // Test prototype: isolated Unity clip players on stripped cosmetic hierarchies.
    // No MedsController, animation event consumer or inventory item is created.
    internal sealed class PsychMedicalIllusion
    {
        private sealed class Rig
        {
            internal GameObject Root = null!;
            internal GameObject Model = null!;
            internal AnimationClip Clip = null!;
            internal float Born;
            internal bool Reported;
            internal int Index;
            internal Transform? LeftPalm;
            internal Transform? RightPalm;
            internal PlayableGraph Graph;
            internal AnimationClipPlayable Player;
            internal Quaternion ViewRotation;
            internal Quaternion InitialPalmRotation;
            internal float MaximumPalmMotion;
            internal Vector3 InitialPalmPosition;
            internal float MaximumPalmTravel;
        }

        private static readonly string[] Paths =
        {
            "assets/content/weapons/usable_items/item_meds_grizly/item_meds_grizly_container.bundle",
            "assets/content/weapons/usable_items/item_bandage/item_bandage_med_container.bundle",
            "assets/content/weapons/usable_items/item_syringe/item_syringe_container.bundle"
        };
        private DependencyGraph<IEasyBundle>.Token? clipToken;
        private AnimationClip[] medicalClips = Array.Empty<AnimationClip>();
        private readonly List<Rig> rigs = new();
        private WeaponPrefab[] prefabs = Array.Empty<WeaponPrefab>();
        private bool warming;
        private bool ready;
        private int epoch;
        private Player.MedsController? seen;
        private Player.MedsController? current;
        private EBodyPart part;
        private float retry;
        internal static PsychMedicalIllusion? Current;
        internal string Status => warming ? "Medical visuals: loading native assets" : ready
            ? "Medical visuals: ready; active rigs " + rigs.Count : "Medical visuals: assets not ready (see RR-MED-GHOST log)";

        internal async void Warm()
        {
            if (warming || ready)
            {
                return;
            }

            warming = true;
            int version = epoch;
            try
            {
                var keys = Paths.Select(path => new ResourceKey { path = path }).ToArray();
                var factory = Singleton<ObjectsFactory>.Instance;
                await factory.LoadBundlesAndCreatePools(
                    ObjectsFactory.PoolsCategory.Raid,
                    ObjectsFactory.AssemblyType.Local,
                    keys,
                    JobYieldPriority.General,
                    null,
                    factory.PoolsCancellationToken
                );
                if (version != epoch)
                {
                    return;
                }

                var easy = Singleton<IEasyAssets>.Instance;
                var clipPaths = Paths.Select(path => path.Substring(0, path.LastIndexOf('/')) + "/client_assets.bundle").ToArray();
                var retained = easy.Retain(clipPaths);
                clipToken = retained;
                await retained.LoadingJob;
                if (version != epoch)
                {
                    retained.Release();
                    return;
                }

                medicalClips = clipPaths.SelectMany(path => easy.System.GetNode(path).Data.Assets ?? Array.Empty<UnityEngine
                    .Object>()).OfType<AnimationClip>().ToArray();
                Plugin.Trace("[RR-MED-GHOST] explicit-native-clips=" + string.Join(",", medicalClips.Select(c => c.name)));
                prefabs = keys.Select(key => Singleton<IEasyAssets>.Instance.GetAsset<WeaponPrefab>(key)).ToArray();
                ready = prefabs.All(p => p != null && p.WeaponObject != null) && new[]
                {
                    "grizly_use_s",
                    "item_bandage_loop_1",
                    "item_syringe_use"
                }.All(name => medicalClips.Any(c => c.name == name));
                Plugin.Trace("[RR-MED-GHOST] assets-ready=" + ready + " usablePrefabs=" + prefabs.Length);
            }
            catch (Exception e)
            {
                Plugin.Trace("[RR-MED-GHOST] asset-error " + e.GetBaseException().Message);
            }
            finally
            {
                if (version == epoch)
                {
                    warming = false;
                }
            }
        }

        internal void Tick(Player player)
        {
            var meds = player.HandsController as Player.MedsController;
            if (current != null && (meds != current || meds.CurrentOperation == null || meds.CurrentOperation._finished || meds
                .CurrentOperation._bodyPartQueue?.Count == 0 || player.HealthController.GetBodyPartHealth(part).Current > 0))
            {
                Clear();
            }

            if (meds == null)
            {
                seen = null;
                return;
            }

            string template = meds.Item.TemplateId;
            bool surgery = template == "5d02778e86f774203e7dedbe" || template == "5d02797c86f774203f38e30a";
            EBodyPart body = EBodyPart.Common;
            if (surgery && meds != seen && ready && Time.unscaledTime >= retry && meds.CurrentOperation?._bodyPartQueue?
                .TryPeek(out body) == true && body != EBodyPart.Common && player.HealthController.GetBodyPartHealth(body).Current <= 0)
            {
                seen = meds;
                current = meds;
                part = body;
                Current = this;
                retry = Time.unscaledTime + 1;
                try
                {
                    Create(meds);
                }
                catch (Exception e)
                {
                    Plugin.Trace("[RR-MED-GHOST] rig-error " + e.GetBaseException().Message);
                    Clear();
                }
            }
        }

        internal void LateTick()
        {
            foreach (var rig in rigs)
            {
                if (rig.Root == null || !rig.Root.activeInHierarchy)
                {
                    continue;
                }

                if (rig.Graph.IsValid())
                {
                    double time = (Time.unscaledTime - rig.Born + rig.Index * .13f) % Mathf.Max(.1f, rig.Clip.length);
                    rig.Player.SetTime(time);
                    rig.Graph.Evaluate(0);
                }

                if (rig.RightPalm != null)
                {
                    rig.MaximumPalmMotion = Mathf.Max(
                        rig.MaximumPalmMotion,
                        Quaternion.Angle(rig.InitialPalmRotation, Quaternion.Inverse(rig.Model.transform.rotation) * rig.RightPalm.rotation)
                    );
                    rig.MaximumPalmTravel = Mathf.Max(
                        rig.MaximumPalmTravel,
                        Vector3.Distance(rig.InitialPalmPosition, rig.Model.transform.InverseTransformPoint(rig.RightPalm.position))
                    );
                }

                // Native held-item space is not camera space. Align the actual sampled palm
                // midpoint, rather than the mesh bounds (which include shoulders behind the eye).
                var view = PsychWorldScares.ViewCamera;
                if (view != null && rig.LeftPalm != null && rig.RightPalm != null)
                {
                    var palmCentre = (rig.LeftPalm.position + rig.RightPalm.position) * .5f;
                    rig.Root.transform.rotation = view.transform.rotation * rig.ViewRotation;
                    palmCentre = (rig.LeftPalm.position + rig.RightPalm.position) * .5f;
                    var anchor = MedicalViewPolicy.Anchor(rig.Index);
                    var visibleCentre = view.transform.TransformPoint(new Vector3(anchor.X, anchor.Y, anchor.Z));
                    rig.Root.transform.position += visibleCentre - palmCentre;
                }

                if (!rig.Reported && Time.unscaledTime - rig.Born > 2f)
                {
                    rig.Reported = true;
                    var camera = PsychWorldScares.ViewCamera;
                    var arms = rig.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "cosmetic_arms").ToArray();
                    foreach (var arm in arms)
                    {
                        var viewport = camera != null ? camera.WorldToViewportPoint(arm.bounds.center) : Vector3.zero;
                        Plugin.Trace("[RR-MED-GHOST] visibility clip=" + rig.Clip.name + " active=" + arm.gameObject.activeInHierarchy
                            + " enabled=" + arm.enabled + " layer=" + arm.gameObject.layer + " cameraMask=" + (camera != null ? camera
                            .cullingMask : 0) + " viewport=" + viewport + " bounds=" + arm.bounds + " modelLocal=" + rig.Model.transform
                            .localPosition + " sampledPalmMotionDegrees=" + rig.MaximumPalmMotion.ToString("0.00")
                            + " sampledPalmTravelMetres=" + rig.MaximumPalmTravel.ToString("0.000") + " graphValid=" + rig.Graph.IsValid());
                    }
                }
            }
        }

        private void Create(Player.MedsController real)
        {
            var arms = Plugin.Local!.PlayerBody.BodySkins.TryGetValue(EBodyModelPart.Hands, out var skin) ? skin.GetRenderers()
                .OfType<SkinnedMeshRenderer>().Where(r => r.sharedMesh != null).GroupBy(r => r.sharedMaterials.FirstOrDefault()?
                .name ?? r.name).Select(g => g.OrderByDescending(r => r.sharedMesh.vertexCount).First()).ToArray() : Array
                .Empty<SkinnedMeshRenderer>();
            Plugin.Trace("[RR-MED-GHOST] player-hand-skin-renderers=" + arms.Length);
            int armCopies = 0;
            for (int i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[i];
                string wanted = i == 0 ? "grizly_use_s" : i == 1 ? "item_bandage_loop_1" : "item_syringe_use";
                var clip = medicalClips.FirstOrDefault(c => c.name == wanted);
                if (clip == null)
                {
                    continue;
                }

                var root = new GameObject("RR_PhantomMedicalRig");
                root.SetActive(false);
                root.transform.SetParent(real.ControllerGameObject.transform, false);
                root.transform.localPosition = new Vector3((i - 1) * .12f, i == 1 ? .04f : -.035f, .03f);
                root.transform.localScale = Vector3.one * MedicalViewPolicy.Scale;
                var model = Object.Instantiate(prefab.WeaponObject, root.transform, false);
                var originalAnimator = model.GetComponent<Animator>();
                var avatar = originalAnimator != null ? originalAnimator.avatar : null;
                // Match WeaponPrefab.CreateObjectInstance: editor-space root offsets are not a hand pose.
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                var rig = new Rig
                {
                    Root = root,
                    Model = model,
                    Clip = clip,
                    Born = Time.unscaledTime,
                    Index = i,
                    ViewRotation = PsychWorldScares.ViewCamera != null ? Quaternion.Inverse(PsychWorldScares.ViewCamera.transform
                        .rotation) * root.transform.rotation : Quaternion.identity
                };
                rigs.Add(rig);
                // Inactive parent prevents initialization; remove scripts before activation.
                foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (script != null)
                    {
                        Object.DestroyImmediate(script);
                    }
                }

                foreach (var component in model.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component is not Transform && component is not MeshFilter
                        && component is not MeshRenderer && component is not SkinnedMeshRenderer)
                    {
                        Object.DestroyImmediate(component);
                    }
                }

                var bones = model.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                rig.LeftPalm = bones.TryGetValue("Base HumanLPalm", out var leftPalm) ? leftPalm : null;
                rig.RightPalm = bones.TryGetValue("Base HumanRPalm", out var rightPalm) ? rightPalm : null;
                foreach (var source in arms)
                {
                    int exact = source.bones.Count(b => b != null && bones.ContainsKey(b.name));
                    if (exact < source.bones.Length / 2)
                    {
                        Plugin.Trace("[RR-MED-GHOST] arm-bone-map rejected matches=" + exact + "/" + source.bones.Length);
                        continue;
                    }

                    Transform Map(Transform bone)
                    {
                        var b = bone;
                        while (b != null)
                        {
                            if (bones.TryGetValue(b.name, out var found))
                            {
                                return found;
                            }

                            b = b.parent;
                        }

                        return model.transform;
                    }

                    var hand = new GameObject("cosmetic_arms");
                    hand.transform.SetParent(model.transform, false);
                    hand.layer = source.gameObject.layer;
                    var copy = hand.AddComponent<SkinnedMeshRenderer>();
                    copy.sharedMesh = source.sharedMesh;
                    copy.sharedMaterials = source.sharedMaterials;
                    copy.bones = source.bones.Select(Map).ToArray();
                    if (source.rootBone != null && bones.TryGetValue(source.rootBone.name, out var rb))
                    {
                        copy.rootBone = rb;
                    }

                    copy.localBounds = source.localBounds;
                    copy.updateWhenOffscreen = true;
                    armCopies++;
                }

                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.gameObject.layer = arms.FirstOrDefault()?.gameObject.layer ?? real.ControllerGameObject.layer;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                model.SetActive(true);
                root.SetActive(true);
                // Use an independent clip graph, not a real medical controller.
                // Animation events and IK are disabled, so this cannot apply healing,
                // consume items or dispatch native medical animation callbacks.
                var animator = model.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                rig.Graph = PlayableGraph.Create("RR_CosmeticMedicalClip");
                rig.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                rig.Player = AnimationClipPlayable.Create(rig.Graph, clip);
                rig.Player.SetApplyFootIK(false);
                rig.Player.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(rig.Graph, "CosmeticHands", animator);
                output.SetSourcePlayable(rig.Player);
                rig.Graph.Play();
                rig.Graph.Evaluate(0);
                if (rig.RightPalm != null)
                {
                    rig.InitialPalmRotation = Quaternion.Inverse(model.transform.rotation) * rig.RightPalm.rotation;
                    rig.InitialPalmPosition = model.transform.InverseTransformPoint(rig.RightPalm.position);
                }

                Plugin.Trace("[RR-MED-GHOST] clip=" + clip.name + " seconds=" + clip.length.ToString("0.0"));
            }

            Plugin.Trace("[RR-MED-GHOST] forced-surgery-test rigs=" + rigs.Count + " extraArmRenderers=" + armCopies
                + " realItemOnly=true");
        }

        internal void Cancel(Player.MedsController controller)
        {
            if (controller == current)
            {
                Clear();
            }
        }

        private void Clear()
        {
            foreach (var rig in rigs)
            {
                if (rig.Graph.IsValid())
                {
                    rig.Graph.Destroy();
                }

                if (rig.Root != null)
                {
                    rig.Root.SetActive(false);
                    rig.Root.transform.SetParent(null, false);
                    Object.Destroy(rig.Root);
                }
            }

            rigs.Clear();
            current = null;
            if (Current == this)
            {
                Current = null;
            }
        }

        internal void Stop()
        {
            epoch++;
            Clear();
            clipToken?.Release();
            clipToken = null;
            medicalClips = Array.Empty<AnimationClip>();
            seen = null;
            ready = warming = false;
            prefabs = Array.Empty<WeaponPrefab>();
        }
    }

    [HarmonyPatch]
    internal static class PsychMedicalCleanup
    {
        private static IEnumerable<MethodBase> TargetMethods() => typeof(Player.MedsController.MedsInHandsOperation)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(m => m
            .Name is "Remove" or "HideWeapon" or "Finish").Concat(typeof(Player.MedsController).GetMethods(BindingFlags
            .Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).Where(m => m.Name is "Drop" or "Destroy"));
        private static void Prefix(object __instance)
        {
            var controller = __instance as Player.MedsController ?? (__instance as Player.MedsController.MedsInHandsOperation)?._controller;
            if (controller != null)
            {
                PsychMedicalIllusion.Current?.Cancel(controller);
            }
        }
    }
}
