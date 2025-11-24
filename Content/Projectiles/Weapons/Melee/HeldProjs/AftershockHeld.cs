using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class AftershockHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Aftershock>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Aftershock_Bar";

        private bool canShoot2;
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            IgnoreImpactBoxSize = true;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 40;
            drawTrailCount = 6;
            Length = 44;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            canShoot2 = true;
            shootSengs = 0.6f;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 1) {
                return;
            }
            if (canShoot2) {
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity + new Vector2(0, -Main.rand.Next(0, 6))
                    , ModContent.ProjectileType<MeleeFossilShard>(), Projectile.damage / 3, Projectile.knockBack, Owner.whoAmI);
                }
            }
        }

        public override bool PreInOwner() {
            Projectile.DamageType = Item.DamageType;
            if (Projectile.ai[0] == 1) {
                distanceToOwner = 40;
                SwingData.ler1_UpLengthSengs = 0.018f;
                SwingData.ler1_UpSizeSengs = 0.036f;
                SwingData.minClampLength = 70;
                SwingData.maxClampLength = 90;
            }
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f, phase0MeleeSizeIncrement: 0, phase2MeleeSizeIncrement: 0);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            canShoot2 = false;
            if (Projectile.ai[0] == 1 && Projectile.numHits == 0) {
                target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 300);
                Vector2 destination = target.Center;
                Vector2 position = destination - (Vector2.UnitY * (destination.Y - Main.screenPosition.Y + 80f)) + (Vector2.UnitX * Main.rand.Next(-160, 161));
                Vector2 velocity = (destination - position).SafeNormalize(Vector2.UnitY) * ShootSpeed * Main.rand.NextFloat(0.9f, 1.1f);
                int rockDamage = Projectile.damage;
                Projectile.NewProjectile(Source, position, velocity, ModContent.ProjectileType<AftershockRock>()
                    , rockDamage, hit.Knockback, Owner.whoAmI, 0f, Main.rand.Next(10), target.Center.Y);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            canShoot2 = false;
            if (Projectile.ai[0] == 1 && Projectile.numHits == 0) {
                target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 300);
                Vector2 destination = target.Center;
                Vector2 position = destination - (Vector2.UnitY * (destination.Y - Main.screenPosition.Y + 80f)) + (Vector2.UnitX * Main.rand.Next(-160, 161));
                Vector2 velocity = (destination - position).SafeNormalize(Vector2.UnitY) * ShootSpeed * Main.rand.NextFloat(0.9f, 1.1f);
                int rockDamage = Projectile.damage;
                Projectile.NewProjectile(Source, position, velocity, ModContent.ProjectileType<AftershockRock>()
                    , rockDamage, info.Knockback, Owner.whoAmI, 0f, Main.rand.Next(10), target.Center.Y);
            }
        }
    }
}
