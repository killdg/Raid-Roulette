namespace RaidRoulette.Shared
{
    public static class GuardedPolicy
    {
        // Conventional boss guards. Specialized named companions and lighthouse
        // snipers are excluded from this first independent-guard implementation.
        public static readonly string[] Roles =
        {
            "followerBully",
            "followerGluharAssault",
            "followerGluharScout",
            "followerGluharSecurity",
            "followerKojaniy",
            "followerSanitar",
            "followerBoar",
            "followerBoarClose1",
            "followerBoarClose2",
            "followerKolontayAssault",
            "followerKolontaySecurity"
        };
    }
}
