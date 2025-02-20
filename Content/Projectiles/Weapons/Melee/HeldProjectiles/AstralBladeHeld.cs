using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class AstralBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<AstralBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AstralBlade_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            Projectile.width = Projectile.height = 52;
            distanceToOwner = 14;
            drawTrailBtommWidth = 40;
            drawTrailTopWidth = 40;
            drawTrailCount = 8;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 5;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ModContent.ProjectileType<AstralBall>()
                , Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
        }

        public override bool PreInOwner() {
            int dustType = Main.rand.NextBool() ? ModContent.DustType<AstralOrange>() : ModContent.DustType<AstralBlue>();
            Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
            if (d != null) {
                d.customData = 0.03f;
            }
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);

            if (hit.Crit) {
                Projectile star = CalamityUtils.ProjectileBarrage(Source, Owner.Center, target.Center
                        , Main.rand.NextBool(), 800f, 800f, 800f, 800f, 10f, ModContent.ProjectileType<AstralStar>()
                        , (int)(hit.Damage * 0.4), 1f, Owner.whoAmI, true);
                if (star.whoAmI.WithinBounds(Main.maxProjectiles)) {
                    star.DamageType = DamageClass.Melee;
                    star.ai[0] = 3f;
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
        }
    }
}
