using System.Linq;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        // A visual-only invitation on the phantom's copied bones. Do not call
        // ShowGesture on the donor: that would change a real bot's behavior.
        private void Beckon(Visual visual)
        {
            if (visual.GesturePose.Count == 0)
            {
                foreach (var pair in visual.Bones)
                {
                    visual.GesturePose[pair.Key] = (pair.Value.localPosition, pair.Value.localRotation);
                }
            }

            var upper = visual.Bones.Keys.FirstOrDefault(bone => bone != null && bone.name == "Base HumanLUpperarm");
            var elbow = visual.Bones.Keys.FirstOrDefault(bone => bone != null && bone.name == "Base HumanLForearm1");
            var palm = visual.Bones.Keys.FirstOrDefault(bone => bone != null && bone.name == "Base HumanLPalm");
            if (upper == null || elbow == null || palm == null)
            {
                return;
            }

            var shoulderPoint = visual.GesturePose[upper].Position;
            var elbowPoint = visual.GesturePose[elbow].Position;
            var palmPoint = visual.GesturePose[palm].Position;
            float wave = Mathf.Sin((Now - visual.Born) * 3.5f);
            var newElbow = shoulderPoint + new Vector3(-0.35f, 0.1f, 0.9f).normalized * Vector3.Distance(shoulderPoint, elbowPoint);
            var newPalm = newElbow + new Vector3(-0.1f, 0.8f, 0.2f + wave * 0.4f).normalized * Vector3.Distance(elbowPoint, palmPoint);
            var upperRotation = Quaternion.FromToRotation(elbowPoint - shoulderPoint, newElbow - shoulderPoint);
            var forearmRotation = Quaternion.FromToRotation(palmPoint - elbowPoint, newPalm - newElbow);
            foreach (var pair in visual.Bones)
            {
                if (pair.Key == null || !visual.GesturePose.TryGetValue(pair.Key, out var pose))
                {
                    continue;
                }

                if (pair.Key == elbow || pair.Key.IsChildOf(elbow))
                {
                    pair.Value.localPosition = newElbow + forearmRotation * (pose.Position - elbowPoint);
                    pair.Value.localRotation = forearmRotation * pose.Rotation;
                }
                else if (pair.Key == upper || pair.Key.IsChildOf(upper))
                {
                    pair.Value.localPosition = shoulderPoint + upperRotation * (pose.Position - shoulderPoint);
                    pair.Value.localRotation = upperRotation * pose.Rotation;
                }
            }
        }
    }
}
