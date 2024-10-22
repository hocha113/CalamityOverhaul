using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.AmmoBoxs
{
    internal class DragonBreathBoxProj : BaseAmmoBox
    {
        public override string Texture => CWRConstant.Item + "Placeable/DBCBox";
        public override void SetAmmoBox() {
            maxFrameNum = 1;
            drawOffsetPos = new Vector2(0, -18);
        }

        public override void ClockFrame() {
            if (++Projectile.frameCounter > 6 && Projectile.frame <= 20) {
                Projectile.frame++;
                Projectile.frameCounter = 0;
            }
            if (Projectile.frame >= 20) {
                Projectile.frame = 20;
            }
        }

        public override bool ClickBehavior(Player player, CWRItems cwr) {
            _ = SoundEngine.PlaySound(CWRSound.loadTheRounds, Projectile.Center);
            _ = SoundEngine.PlaySound(CWRSound.DeploymentSound_AP with { Volume = 0.4f }, Projectile.Center);
            cwr.SpecialAmmoState = SpecialAmmoStateEnum.dragonBreath;
            return true;
        }
    }
}
