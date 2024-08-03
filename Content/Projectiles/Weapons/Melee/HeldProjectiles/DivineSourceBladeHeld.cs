using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class DivineSourceBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DivineSourceBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "DragonRage_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 112;
            canDrawSlashTrail = true;
            drawTrailCount = 34;
            distanceToOwner = -20;
            drawTrailTopWidth = 86;
            ownerOrientationLock = true;
            SwingData.starArg = 70;
            SwingData.baseSwingSpeed = 5.25f;
            unitOffsetDrawZkMode = 16;
            Length = 124;
            SwingAIType = SwingAITypeEnum.UpAndDown;
        }

        public override void UpdateCaches() {
            if (Time < 2) {
                return;
            }

            for (int i = drawTrailCount - 1; i > 0; i--) {
                oldRotate[i] = oldRotate[i - 1];
                oldDistanceToOwner[i] = oldDistanceToOwner[i - 1];
                oldLength[i] = oldLength[i - 1];
            }

            oldRotate[0] = safeInSwingUnit.RotatedBy(MathHelper.ToRadians(-8 * Projectile.spriteDirection)).ToRotation();
            oldDistanceToOwner[0] = distanceToOwner;
            oldLength[0] = Projectile.height * Projectile.scale;
        }

        public override void Shoot() {
            int types = ModContent.ProjectileType<DivineSourceBeam>();
            Vector2 vector2 = Owner.Center.To(Main.MouseWorld).UnitVector() * 3;
            Vector2 position = Owner.Center;
            Projectile.NewProjectile(
                Owner.parent(), position, vector2, types
                , (int)(Item.damage * 1.25f)
                , Item.knockBack
                , Owner.whoAmI);
            int type = ModContent.ProjectileType<DivineSourceBladeProjectile>();
            Projectile proj = Projectile.NewProjectileDirect(Source, ShootSpanPos, ShootVelocity.UnitVector() * 22, type, Item.damage / 2, 0, Owner.whoAmI);
            proj.SetArrowRot();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerratomereSlashCreator>(),
                Projectile.damage / 3, 0, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathHelper.TwoPi));
                Main.projectile[proj].timeLeft = 30;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.numHits == 0) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<TerratomereSlashCreator>(),
                Projectile.damage / 3, 0, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathHelper.TwoPi));
                Main.projectile[proj].timeLeft = 30;
            }
        }
    }
}
