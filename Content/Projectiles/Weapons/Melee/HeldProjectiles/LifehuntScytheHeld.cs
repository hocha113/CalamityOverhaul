using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class LifehuntScytheHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<LifehuntScythe>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail4";
        public override string gradientTexturePath => CWRConstant.ColorBar + "UltimusCleaver_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 82;
            canDrawSlashTrail = true;
            drawTrailTopWidth = 40;
            drawTrailHighlight = false;
            drawTrailBtommWidth = 10;
            distanceToOwner = 30;
            OtherMeleeSize = 1.24f;
            Length = 50;
            unitOffsetDrawZkMode = 6;
            IgnoreImpactBoxSize = true;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 0) {
                return;
            }
            int max = (int)Projectile.ai[1];
            for (int i = 0; i < max; i++) {
                Vector2 ver = (MathHelper.TwoPi / max * i).ToRotationVector2().RotatedBy(ToMouseA);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center + ver * 63, ver * (13 + max),
                ModContent.ProjectileType<SickleLife>(), Projectile.damage / 2, Projectile.knockBack / 2, Projectile.owner);
            }
        }

        public override bool PreInOwnerUpdate() {
            OtherMeleeSize += 0.005f;
            if (Time == 0) {
                SoundEngine.PlaySound(SoundID.Item71, Owner.Center);
            }
            if (Projectile.ai[0] == 1) {
                SwingData.baseSwingSpeed = 9;
                SwingAIType = SwingAITypeEnum.Down;
            }
            return base.PreInOwnerUpdate();
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(4))
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.CursedTorch);
        }
    }
}
