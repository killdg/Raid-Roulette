using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private IEnumerable<Renderer> ActorRenderers(Player donor)
        {
            // Native enumeration puts equipment first; select complete body skins before gear.
            var result = new List<Renderer>();
            foreach (var pair in donor.PlayerBody.BodySkins)
            {
                if (pair.Key == EBodyModelPart.Hands)
                {
                    continue;
                }

                result.AddRange(pair.Value.GetRenderers().Where(r => r != null).GroupBy(r => string.Join("|", r.sharedMaterials
                    .Select(m => m != null ? m.name : ""))).Select(g => g.OrderByDescending(r => r is SkinnedMeshRenderer skin && skin
                    .sharedMesh != null ? skin.sharedMesh.vertexCount : 0).First()));
            }

            // Held weapons live under the hands controller, outside PlayerBody's clothing
            // renderer list. Borrow the donor's complete assembled weapon, including attachments.
            if (donor.HandsController is Player.FirearmController firearm)
            {
                var controller = firearm.ControllerGameObject;
                var selected = new HashSet<Renderer>();
                var lodRenderers = new HashSet<Renderer>();
                foreach (var group in controller.GetComponentsInChildren<LODGroup>(true))
                {
                    var levels = group.GetLODs();
                    foreach (var level in levels)
                    {
                        foreach (var renderer in level.renderers)
                        {
                            lodRenderers.Add(renderer);
                        }
                    }

                    if (levels.Length > 0)
                    {
                        foreach (var renderer in levels[0].renderers)
                        {
                            selected.Add(renderer);
                        }
                    }
                }

                // Far-away donor weapons may currently be culled. Use their first
                // LOD rather than copying only whatever is visible at the donor's location.
                result.AddRange(controller.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer is MeshRenderer
                    || renderer is SkinnedMeshRenderer).Where(renderer => ActiveWeaponBranch(renderer.transform, controller.transform))
                    .Where(renderer => renderer.gameObject.activeSelf || selected.Contains(renderer)).Where(renderer => renderer
                    .shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly).Where(renderer => !lodRenderers
                    .Contains(renderer) || selected.Contains(renderer)));
            }

            var equipment = new List<Renderer>();
            donor.PlayerBody.GetRenderersNonAlloc(equipment);
            result.AddRange(equipment.Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy));
            Log("stage=character-copy role=" + donor.Profile.Info.Settings.Role + " bodyFirstRenderers=" + result.Distinct().Count());
            return result.Distinct();
        }

        private bool FrontPoint(float min, float max, out Vector3 point)
        {
            point = default;
            var local = Plugin.Local!;
            var camera = PsychWorldScares.ViewCamera;
            var forward = camera != null ? camera.transform.forward : local.LookDirection;
            forward.y = 0;
            if (forward.sqrMagnitude < .1f)
            {
                return false;
            }

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float distance = Random.Range(min, max);
                var anchor = local.Position + forward * distance + right * Random.Range(-distance * .32f, distance * .32f);
                if (!NavMesh.SamplePosition(anchor, out var hit, 1.5f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - local
                    .Position.y) > 2)
                {
                    continue;
                }

                if (visuals.Any(v => v.Crowd && (v.Start - hit.position).sqrMagnitude < 1.5f))
                {
                    continue;
                }

                if (!PsychWorldScares.Visible(local.Transform.Original, hit.position + Vector3.up * 1.4f, max + 3))
                {
                    continue;
                }

                point = hit.position;
                return true;
            }

            return false;
        }

        private void GlassSound()
        {
            TryGlassSound();
        }

        private bool TryGlassSound()
        {
            if (glassSounds.Length > 0)
            {
                int before = sounds.Count;
                Sound(glassSounds[Random.Range(0, glassSounds.Length)], PhantomSoundPoint());
                return sounds.Count > before;
            }

            if (world == null)
            {
                return false;
            }

            var windows = world.Windows._iteration.Where(w => w != null && (w.transform.position - Plugin.Local!.Position)
                .sqrMagnitude < 3600).ToArray();
            if (windows.Length == 0)
            {
                Log("stage=glass-sound no-native-sound-source");
                return false;
            }

            var window = windows[Random.Range(0, windows.Length)];
            var at = PhantomSoundPoint();
            window.PlayHitSound(in at);
            return true;
        }
    }
}
