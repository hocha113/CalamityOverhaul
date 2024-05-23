using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class HighExplosiveBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/HESHBox";
        public override void SetAmmoBox() {
            maxFrameNum = 9;
            drawOffsetPos = new Microsoft.Xna.Framework.Vector2(0, 2);
        }
        public override bool ClickBehavior(Player player, CWRItems cwr) {
            _ = SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
            _ = SoundEngine.PlaySound(CWRSound.DeploymentSound_AP with { Volume = 0.4f }, Projectile.Center);
            cwr.SpecialAmmoState = SpecialAmmoStateEnum.highExplosive;
            return true;
        }
    }
}
