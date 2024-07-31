using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Terraria.ModLoader;
using Terraria;
using Mono.Cecil;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Projectiles.Typeless;
using CalamityMod;
using CalamityMod.Dusts;
using Microsoft.Xna.Framework;

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
            drawTrailBtommMode = 40;
            trailTopWidth = 40;
            trailCount = 8;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 5;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void Shoot() {
            if (Projectile.numHits > 0) {
                return;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f))
                , ModContent.ProjectileType<AstralBall>(), (int)(Projectile.damage * 0.75f), Projectile.knockBack, Owner.whoAmI);
        }

        public Dust MeleeDustHelper(Player player, int dustType, float chancePerFrame, float minDistance
            , float maxDistance, float minRandRot = -0.2f, float maxRandRot = 0.2f, float minSpeed = 0.9f, float maxSpeed = 1.1f) {
            if (Main.rand.NextFloat(1f) < chancePerFrame) {
                float distance = Main.rand.NextFloat(minDistance, maxDistance);
                Vector2 offset = (safeInSwingUnit.ToRotation() - (MathHelper.PiOver4 * player.direction) 
                    + Main.rand.NextFloat(minRandRot, maxRandRot)).ToRotationVector2() * distance * player.direction;
                Vector2 pos = player.Center + offset;
                Vector2 vec = pos - player.Center;
                Dust d = Dust.NewDustPerfect(pos, dustType);
                vec.Normalize();
                d.velocity = vec * Main.rand.NextFloat(minSpeed, maxSpeed);
                return d;
            }
            return null;
        }

        public override bool PreInOwnerUpdate() {
            int dustType = Main.rand.NextBool() ? ModContent.DustType<AstralOrange>() : ModContent.DustType<AstralBlue>();
            Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType);
            if (d != null) {
                d.customData = 0.03f;
            }
            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
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
