using System;

namespace RaidRoulette.TestingClient
{
    public sealed class InteriorScareGate
    {
        private double since = -1;
        public void Reset() => since = -1;
        public bool Tick(double now, bool enclosed)
        {
            if (!enclosed || double.IsNaN(now) || double.IsInfinity(now))
            {
                Reset();
                return false;
            }

            if (since < 0 || now < since)
            {
                since = now;
            }

            return now - since >= 2;
        }
    }

    public static class PsychMusicPolicy
    {
        public const float ApproachMetres = 3.048f;
        public const float ListenSeconds = 7;
        public const float FadeSeconds = 5;
        public const float WaitSeconds = 90;
        public static double Gain(double sinceApproach)
        {
            if (double.IsNaN(sinceApproach) || sinceApproach < 0)
            {
                return 0;
            }

            if (sinceApproach < ListenSeconds)
            {
                return Math.Min(1, sinceApproach / .4);
            }

            return Math.Max(0, 1 - (sinceApproach - ListenSeconds) / FadeSeconds);
        }
    }

    public static class PsychScarePolicy
    {
        public static double DeathNoticeDelay(double seconds) => double.IsNaN(seconds) || double.IsInfinity(seconds)
            || seconds <= 0 ? 5 : Math.Max(.1, seconds - .5);
        public static (float Min, float Max) SoundRange(double roll) => roll < .2 ? (0.8f, 2.5f) : roll < .65 ? (8f, 22f) : (35f, 75f);
        public static double WhisperAt(double duration, double roll)
        {
            if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 5)
            {
                return double.PositiveInfinity;
            }

            return Math.Max(0, Math.Min(1, roll)) * (duration - 5);
        }
    }

    public static class GlassScarePolicy
    {
        public const float Radius = 3.048f;
        public const float Duration = 4;
        public const float ForwardRange = 12;
        public static bool InForwardCone(double distanceSquared, double facingDot) => !double.IsNaN(distanceSquared)
            && !double.IsNaN(facingDot) && distanceSquared >= 0 && distanceSquared <= ForwardRange * ForwardRange
            && (distanceSquared < .25 || facingDot >= .25);
        public const int MaxPanes = 12;
        public static bool RestoreIntact(bool genuinelyDamaged) => !genuinelyDamaged;
    }

    public static class PsychInjuryPolicy
    {
        public static double Boost(bool light, bool heavy, bool fracture, bool blacked) => (light ? .10 : 0) + (heavy
            ? .15 : 0) + (fracture ? .20 : 0) + (blacked ? .25 : 0);
        public static double Interval(double baseline, double boost) => baseline / (1 + System.Math.Max(0, System.Math.Min(.70, boost)));
    }

    public sealed class RapidSoundPolicy
    {
        private double last = -100;
        public int Count { get; private set; }

        public bool Shot(double now)
        {
            if (now - last > .20 || now < last)
            {
                Count = 0;
            }

            last = now;
            return ++Count > 10;
        }

        public void Reset()
        {
            last = -100;
            Count = 0;
        }
    }

    public static class DecoyMotionPolicy
    {
        public static (double Forward, double Right) Direction(double roll)
        {
            double angle = Math.Max(0, Math.Min(1, roll)) * Math.PI * 2;
            return (Math.Cos(angle), Math.Sin(angle));
        }
    }
}

namespace RaidRoulette.TestingClient
{
    public static class MedicalViewPolicy
    {
        public const float Scale = .45f;
        // Keep small, independent work areas below the centre of the view.
        // Depth leaves space between the camera and the cloned sleeve meshes.
        public static (float X, float Y, float Z) Anchor(int index)
        {
            return index == 1 ? (0f, -.46f, 1.05f) : index == 0 ? (-.28f, -.37f, .95f) : (.28f, -.37f, .95f);
        }
    }
}

namespace RaidRoulette.TestingClient
{
    public static class PsychFadePolicy
    {
        public static float Alpha(float elapsed, float duration)
        {
            if (float.IsNaN(elapsed) || float.IsNaN(duration) || duration <= 0)
            {
                return 0;
            }

            return 1 - Math.Max(0, Math.Min(1, elapsed / duration));
        }

        public static float ColdStrength(float secondsRemaining)
        {
            if (float.IsNaN(secondsRemaining))
            {
                return 0;
            }

            return Math.Max(0, Math.Min(1, Math.Min((10 - secondsRemaining) / 2, secondsRemaining / 2)));
        }
    }
}
