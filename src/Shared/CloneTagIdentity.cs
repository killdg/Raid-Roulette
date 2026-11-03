using System;

namespace RaidRoulette.Shared
{
    public static class CloneTagIdentity
    {
        public const string Prefix = "c10e";
        public static string NewId() => Prefix + Guid.NewGuid().ToString("N").Substring(0, 20);
        public static bool IsClone(string id) => id != null && id.Length == 24 && id.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
