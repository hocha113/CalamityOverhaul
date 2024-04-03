using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class NapalmBombBox : BaseAmmoBox
    {
        public override bool ClickBehavior(Player player, CWRItems cwr) {
            _ = SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
            _ = SoundEngine.PlaySound(CWRSound.DeploymentSound_Fire with { Volume = 0.4f }, Projectile.Center);
            cwr.AmmoCapacityInNapalmBomb = true;
            return true;
        }
    }
}
