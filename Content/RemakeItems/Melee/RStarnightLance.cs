using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RStarnightLance : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<StarnightLance>();
        internal static int index;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<StarnightLanceHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(item, player, source, position, velocity, type, damage, knockback);
        }
        public static bool ShootFunc(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (++index > 3) {
                index = 0;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, index);
            return false;
        }
    }

    internal class StarnightLanceHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StarnightLance>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Gel_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 52;
            SwingData.baseSwingSpeed = 6;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            if (Projectile.ai[0] == 0) {
                if (Time == 0) {
                    OtherMeleeSize = 1.4f;
                }

                SwingData.baseSwingSpeed = 10;
                SwingAIType = SwingAITypeEnum.Down;

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.025f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }
                return true;
            }

            StabBehavior(initialLength: 60, lifetime: 26, scaleFactorDenominator: 220f, minLength: 40, maxLength: 100, canDrawSlashTrail: true);
            return false;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 0) {
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity * 3,
                ModContent.ProjectileType<StarnightBeam>(), (int)(Projectile.damage * 0.8), Projectile.knockBack * 0.85f, Projectile.owner);
        }

        public override void MeleeEffect() {
            Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                , DustID.WaterCandle, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn2, 120);
            if (Projectile.ai[0] == 0 && Projectile.numHits == 0) {
                for (int i = 0; i < 5; i++) {
                    Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity.RotatedBy(MathHelper.TwoPi / 5f * i) / 6,
                    ModContent.ProjectileType<StarnightBeam>(), (int)(Projectile.damage * 0.8), Projectile.knockBack * 0.85f, Projectile.owner);
                }
                Projectile.numHits++;
            }
        }
    }
}
