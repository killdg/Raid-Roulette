using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using JsonType;
using EFT.Ballistics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private readonly HashSet<string> recentVictims = new();
        private readonly Queue<float> recentKills = new();
        private Vector3? musicCorpseAnchor;
        private float glassProbeAt;
        private float runningGlassCooldown;
        private Vector3 runningProbePosition;
        private float runningProbeTime;
        private void PsychKilled(Player victim, IPlayer killer, DamageInfo damage, EBodyPart part)
        {
            if (!active || pauseScares || !victim.IsAI || killer?.ProfileId != Plugin.Local?.ProfileId || !recentVictims
                .Add(victim.ProfileId))
            {
                return;
            }

            while (recentKills.Count > 0 && Now - recentKills.Peek() > 30)
            {
                recentKills.Dequeue();
            }

            recentKills.Enqueue(Now);
            if (recentKills.Count < 2)
            {
                return;
            }

            recentKills.Clear();
            bool won = Random.value < .35f;
            Log("stage=jukebox-double-kill won=" + won);
            if (won && musicVisual == null)
            {
                musicCorpseAnchor = victim.Position;
                musicAttempts = 3;
                musicAttemptAt = Now + 1;
            }
        }

        private bool MusicPoint(out Vector3 point)
        {
            if (musicCorpseAnchor is Vector3 anchor)
            {
                for (int i = 0; i < 12; i++)
                {
                    var offset = Random.insideUnitCircle.normalized * Random.Range(2f, 4f);
                    var target = anchor + new Vector3(offset.x, 0, offset.y);
                    if (NavMesh.SamplePosition(target, out var hit, .75f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - anchor.y) < 1)
                    {
                        point = hit.position;
                        musicCorpseAnchor = null;
                        return true;
                    }
                }

                point = default;
                return false;
            }

            return FrontPoint(4, 12, out point);
        }

        private bool RearPoint(out Vector3 point)
        {
            var local = Plugin.Local!;
            var camera = PsychWorldScares.ViewCamera;
            var forward = camera != null ? camera.transform.forward : local.LookDirection;
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < 12; i++)
            {
                var target = local.Position - forward * Random.Range(4f, 13f) + right * Random.Range(-6f, 6f);
                if (NavMesh.SamplePosition(target, out var hit, .7f, NavMesh.AllAreas) && Mathf.Abs(hit.position.y - local.Position
                    .y) < 1 && PsychWorldScares.ClearSight(local.Transform.Original, hit.position + Vector3.up * 1.4f))
                {
                    point = hit.position;
                    return true;
                }
            }

            point = default;
            return false;
        }

        private void TickNewEncounters()
        {
            var local = Plugin.Local!;
            foreach (var visual in visuals.Where(v => v.RearCrowd && float.IsPositiveInfinity(v.FadeAt)))
            {
                if (PsychWorldScares.Visible(local.Transform.Original, visual.Start + Vector3.up * 1.4f, 20))
                {
                    visual.FadeAt = Now + 1.25f;
                    visual.FadeDuration = 2;
                    visual.Life = Now - visual.Born + 3.25f;
                }
            }

            if (pauseScares)
            {
                return;
            }

            // Flaming-head prototype removed from the release candidate.
            if (Now < glassProbeAt)
            {
                return;
            }

            glassProbeAt = Now + .75f;
            float elapsed = Now - runningProbeTime;
            float speed = elapsed > 0 && elapsed < 2 ? Vector3.Distance(local.Position, runningProbePosition) / elapsed : 0;
            runningProbeTime = Now;
            runningProbePosition = local.Position;
            if (speed > 3 && Now >= runningGlassCooldown && worldScares.Glass.NearbyIntactCount(local) >= 2)
            {
                // A failed roll also consumes the encounter cooldown, preventing repeated rolls every frame.
                runningGlassCooldown = Now + 30;
                bool won = Random.value < .50f;
                Log("stage=running-glass roll=" + won);
                if (won)
                {
                    worldScares.Glass.Trigger(local, true);
                }
            }
        }

        private static void ApplyVisualFade(Visual visual)
        {
            if (Now < visual.FadeAt || visual.FadeDuration <= 0)
            {
                return;
            }

            if (visual.FadeMaterials.Count == 0)
            {
                var shader = Shader.Find("Standard");
                if (shader == null)
                {
                    return;
                }

                var copies = new Dictionary<Material, Material>();
                foreach (var renderer in visual.Root.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                    {
                        if (source == null)
                        {
                            return source;
                        }

                        if (copies.TryGetValue(source, out var existing))
                        {
                            return existing;
                        }

                        var copy = new Material(shader);
                        copy.mainTexture = source.mainTexture;
                        copy.color = source.HasProperty("_Color") ? source.color : Color.white;
                        // Standard's transparent blend mode is applied only to owned phantom copies.
                        copy.SetFloat("_Mode", 3);
                        copy.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        copy.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        copy.SetInt("_ZWrite", 0);
                        copy.EnableKeyword("_ALPHABLEND_ON");
                        copy.renderQueue = 3000;
                        copies[source] = copy;
                        visual.FadeMaterials.Add(copy);
                        return copy;
                    }).ToArray();
                }
            }

            float alpha = PsychFadePolicy.Alpha(Now - visual.FadeAt, visual.FadeDuration);
            foreach (var material in visual.FadeMaterials)
            {
                var color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }

        private void ClearNewEncounters()
        {
            recentKills.Clear();
            recentVictims.Clear();
            musicCorpseAnchor = null;
            runningProbeTime = glassProbeAt = runningGlassCooldown = 0;
        }
    }
}
