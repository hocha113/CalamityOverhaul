using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RVulcaniteLance : ItemOverride
    {
        internal static int index;
        public override int TargetID => ModContent.ItemType<VulcaniteLance>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<VulcaniteLanceHeld>();
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
        public override int TargetID => ModContent.ItemType<VulcaniteLance>();
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
                Particle smoke = new MediumMistParticle(smokePos, smokeVel, new Color(255, 50, 50), Color.DimGray
                    , Smoketype ? Main.rand.NextFloat(0.4f, 0.75f) : Main.rand.NextFloat(1.5f, 2f), 220 - Main.rand.Next(50), 0.1f);
                GeneralParticleHandler.SpawnParticle(smoke);
                return true;
            }
            if (Time % (2 * UpdateRate) == 0 && Time < maxSwingTime / 2) {
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
            int projType = ModContent.ProjectileType<BrimstoneFireFriendly>();
            Projectile projectile = Projectile.NewProjectileDirect(Source, ShootSpanPos, AbsolutelyShootVelocity.RotatedByRandom(0.2f)
                , projType, (int)(Projectile.damage * 0.8), Projectile.knockBack * 0.85f, Projectile.owner);
            projectile.DamageType = DamageClass.Melee;
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
