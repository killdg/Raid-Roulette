using System;
using EFT;
using UnityEngine;

namespace RaidRoulette.TestingClient
{
    // Optional adapters: no actors or behavior changes exist unless a campaign supplies them.
    public static class CampaignActors
    {
        public static Func<IPlayer?, bool>? Civilian;
        public static Func<bool>? CivilianContext;
        public static Func<string, string, bool>? CivilianKill;
        public static Func<BotMover, bool>? RouteOwned;
        public static Func<BotOwner, Vector3, bool>? RouteAllowed;
        public static Func<BotOwner?, IPlayer?, bool>? HostileOwner;
        public static Func<BotsGroup, IPlayer?, bool>? HostileGroup;
        public static bool IsCivilian(IPlayer? p) => Civilian?.Invoke(p) == true;
        public static bool Active => CivilianContext?.Invoke() == true;

        public static bool CanTarget(BotOwner? owner, IPlayer? target) => HostileOwner?.Invoke(owner, target) == true;
        public static bool CanTarget(BotsGroup group, IPlayer? target) => HostileGroup?.Invoke(group, target) == true;
        public static void Clear()
        {
            Civilian = null;
            CivilianContext = null;
            CivilianKill = null;
            RouteOwned = null;
            RouteAllowed = null;
            HostileOwner = null;
            HostileGroup = null;
        }
    }
}
