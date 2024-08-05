using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class AegisBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<AegisBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AegisBlade_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailCount = 4;
            distanceToOwner = 70;
            drawTrailTopWidth = 20;
            ownerOrientationLock = true;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 5.15f;
            ShootSpeed = 8;
        }

        public override bool PreInOwnerUpdate() {
            if (Projectile.ai[0] == 1) {
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                }
                SwingData.baseSwingSpeed = 15.5f;
            }
            if (Projectile.ai[0] == 2 || Projectile.ai[0] == 3) {
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                }
                SwingAIType = SwingAITypeEnum.UpAndDown;
                distanceToOwner = 60;
                drawTrailTopWidth = 60;
                SwingData.baseSwingSpeed = 5.55f;
                SwingData.ler1_UpSizeSengs = 0.102f;
                SwingData.minClampLength = 90;
                SwingData.maxClampLength = 110;
            }
            return true;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                return;
            }
            else if (Projectile.ai[0] == 2 || Projectile.ai[0] == 3) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Source, Owner.Center, ShootVelocity.RotatedBy((-1 + i) * 0.3f) * 1.25f
                , ModContent.ProjectileType<AegisBeams>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI, 1);
                }
                return;
            }
            Projectile.NewProjectile(Source, Owner.Center, ShootVelocity
                , ModContent.ProjectileType<AegisBeams>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
        }
    }
}
