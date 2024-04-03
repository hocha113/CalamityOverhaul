using CalamityOverhaul.Common;
using Terraria.Audio;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class ArmourPiercerBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HEATBox";
        public override void SetAmmoBox() {
            maxFrameNum = 38;
        }
        public override bool ClickBehavior(Player player, CWRItems cwr) {
            _ = SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
            _ = SoundEngine.PlaySound(CWRSound.DeploymentSound_AP with { Volume = 0.4f }, Projectile.Center);
            cwr.SpecialAmmoState = SpecialAmmoStateEnum.armourPiercer;
            return true;
        }
    }
}
