using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;

namespace RaidRoulette.TestingClient
{
    internal sealed partial class SchizophreniaDirector
    {
        private bool fakeJamAiming;
        private float nextFakeJam;
        private void TickFakeJam()
        {
            bool aiming = Plugin.Local?.HandsController is Player.FirearmController firearm && firearm.IsAiming;
            bool entered = aiming && !fakeJamAiming;
            fakeJamAiming = aiming;
            if (active && !pauseScares && entered && Now >= nextFakeJam && UnityEngine.Random.value < .05f)
            {
                FakeJamNotice();
            }
        }

        private void ResetFakeJam()
        {
            fakeJamAiming = false;
            nextFakeJam = 0;
        }

        private void FakeJamNotice()
        {
            if (!active || Plugin.Local?.HandsController is not Player.FirearmController firearm || firearm
                .Item is not Weapon weapon || weapon.MalfState.State != Weapon.EMalfunctionState.None
                || !Singleton<NotificationManager>.Instantiated)
            {
                return;
            }

            // This is the native localized alert object, not a malfunction event.
            // Never write MalfState or invoke the firearm's malfunction operation:
            // shots, ammo, reloads and real malfunction handling remain untouched.
            NotificationManager.DisplayNotification(new MalfunctionOccurredNotification(Weapon.EMalfunctionState.Jam));
            nextFakeJam = Now + 180f;
            Log("stage=fake-jam notification-only=true weapon-state-unchanged=true");
        }
    }
}
