using CalamityMod.Buffs.DamageOverTime;
using CalamityMod;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityMod.Projectiles.Melee;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Common;
using System;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RSausageMakerSpear : BaseSpearProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "Spears/SausageMakerSpear";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<SausageMakerEcType>();
        private Item sausageMaker => Main.player[Projectile.owner].ActiveItem();
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
        }

        public override float InitialSpeed => 3f;
        public override float ReelbackSpeed => 1.1f;
        public override float ForwardSpeed => 0.95f;
        public override Action<Projectile> EffectBeforeReelback => delegate {
            sausageMaker.initialize();
            sausageMaker.CWR().ai[0]++;
            if (sausageMaker.CWR().ai[0] > 5) {
                Lighting.AddLight(Projectile.Center, Color.DarkRed.ToVector3() * 2);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity * 0.5f, Projectile.velocity
                    , ModContent.ProjectileType<BloodBall>(), Projectile.damage, Projectile.knockBack * 0.85f, Projectile.owner);
                sausageMaker.CWR().ai[0] = 0;
            }
        };
        public override void ExtraBehavior() {
            if (Main.rand.NextBool(5))
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, 5, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<BurningBlood>(), 240);
            if (Projectile.owner == Main.myPlayer) {
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity * 1.2f, ModContent.ProjectileType<Blood2>(), Projectile.damage / 2, Projectile.knockBack * 0.5f, Projectile.owner, 0f, 0f);
                }
            }
        }
    }
}
