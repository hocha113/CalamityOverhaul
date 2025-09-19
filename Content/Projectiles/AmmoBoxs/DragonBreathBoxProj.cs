using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class DragonBreathBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/DBCBox";
        public override bool ClickBehavior(Player player, CWRItem cwr) {
            _ = SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
            _ = SoundEngine.PlaySound(CWRSound.DeploymentSound_AP with { Volume = 0.4f }, Projectile.Center);
            cwr.SpecialAmmoState = SpecialAmmoStateEnum.dragonBreath;
            return true;
        }
    }
}
