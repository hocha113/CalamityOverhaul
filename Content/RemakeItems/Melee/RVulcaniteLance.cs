using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RVulcaniteLance : CWRItemOverride
    {
        internal static int index;
        public override void SetDefaults(Item item) {
            item.damage = 120;
            item.SetKnifeHeld<VulcaniteLanceHeld>();
        }
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

    internal class VulcaniteLanceHeld : BaseKnife
    {
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 40;
            drawTrailTopWidth = 10;
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

                SwingData.baseSwingSpeed = 14;
                SwingAIType = SwingAITypeEnum.Down;

                if (Time < maxSwingTime / 3) {
                    OtherMeleeSize += 0.025f / SwingMultiplication;
                }
                else {
                    OtherMeleeSize -= 0.005f / SwingMultiplication;
                }

                bool Smoketype = Main.rand.NextBool();
                Vector2 smokePos = Projectile.Center + Main.rand.NextVector2Circular(Projectile.width * 0.5f, Projectile.height * 0.5f);
                Vector2 smokeVel = AbsolutelyShootVelocity.UnitVector() * Main.rand.NextFloat(0.2f, 2f) * MathHelper.Clamp(Projectile.height * 0.1f, 1f, 10f);
                CWRRef.SpawnMediumMistParticle(smokePos, smokeVel, Smoketype);
                return true;
            }
            if (Time % 2 == 0 && Time < maxSwingTime / 2) {
                canShoot = true;
            }
            StabBehavior(initialLength: 60, lifetime: 26, scaleFactorDenominator: 220f, minLength: 60, maxLength: 120, canDrawSlashTrail: true);
            return false;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 0) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item34, Projectile.Center);
            Projectile projectile = Projectile.NewProjectileDirect(Source, Owner.Center
                , AbsolutelyShootVelocity.RotatedByRandom(0.1f) * Main.rand.NextFloat(0.8f, 1.6f)
                , ProjectileID.Flames, Projectile.damage / 2, Projectile.knockBack * 0.85f, Projectile.owner);
            projectile.DamageType = DamageClass.Melee;
            projectile.netUpdate = true;
        }

        public override void MeleeEffect() {
            Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                , DustID.Flare, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 240);
        }
    }
}
