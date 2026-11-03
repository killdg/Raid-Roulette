using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private static bool ActiveWeaponBranch(Transform renderer, Transform controller)
        {
            // activeSelf alone admits attachments inside disabled prefab branches.
            // Ignore only the renderer's own LOD visibility; its assembly ancestors
            // must belong to the active held weapon.
            for (var parent = renderer.parent; parent != null && parent != controller; parent = parent.parent)
            {
                if (!parent.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private void AddAssembledWeapon(Visual visual, Player actor, IEnumerable<Renderer> renderers)
        {
            if (actor.HandsController is not Player.FirearmController firearm)
            {
                return;
            }

            var source = firearm.ControllerGameObject.transform;
            var palm = visual.Bones.FirstOrDefault(pair => pair.Key != null && pair.Key.name == "Base HumanRPalm");
            if (palm.Key == null || palm.Value == null)
            {
                Log("stage=phantom-weapon no-right-palm; omitting-weapon-instead-of-floating-parts");
                return;
            }

            var anchor = new GameObject("RR_PhantomAssembledWeapon").transform;
            anchor.SetParent(visual.Root.transform, false);
            visual.WeaponRoot = anchor;
            visual.WeaponPalm = palm.Value;
            visual.WeaponPalmOffset = Quaternion.Inverse(palm.Key.rotation) * (source.position - palm.Key.position);
            visual.WeaponPalmRotation = Quaternion.Inverse(palm.Key.rotation) * source.rotation;
            UpdateWeaponAnchor(visual);
            int count = 0;
            foreach (var renderer in renderers.Where(renderer => renderer != null && renderer.transform.IsChildOf(source))
                .Distinct().Take(96))
            {
                var part = new GameObject("assembled_weapon_mesh");
                part.transform.SetParent(anchor, false);
                part.transform.localPosition = source.InverseTransformPoint(renderer.transform.position);
                part.transform.localRotation = Quaternion.Inverse(source.rotation) * renderer.transform.rotation;
                part.transform.localScale = renderer.transform.lossyScale;
                Mesh? mesh = null;
                bool owned = false;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                {
                    // Freeze the complete attachment in its current assembled pose.
                    // Copying its bones independently can separate the receiver,
                    // magazine and barrel when the phantom starts a gesture.
                    mesh = new Mesh
                    {
                        name = "RR_BakedWeaponPart"
                    };
                    skin.BakeMesh(mesh);
                    owned = true;
                }
                else if (renderer is MeshRenderer)
                {
                    mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                }

                if (mesh == null)
                {
                    Object.Destroy(part);
                    continue;
                }

                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var copy = part.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                visual.Parts.Add(new Part { Source = renderer, Copy = part, HeldWeapon = true, OwnedMesh = owned ? mesh : null });
                count++;
            }

            Log("stage=phantom-weapon assembledParts=" + count + " anchor=right-palm");
        }

        private static void UpdateWeaponAnchor(Visual visual)
        {
            if (visual.WeaponRoot == null || visual.WeaponPalm == null)
            {
                return;
            }

            visual.WeaponRoot.localPosition = visual.WeaponPalm.localPosition + visual.WeaponPalm.localRotation * visual.WeaponPalmOffset;
            visual.WeaponRoot.localRotation = visual.WeaponPalm.localRotation * visual.WeaponPalmRotation;
        }
    }
}
