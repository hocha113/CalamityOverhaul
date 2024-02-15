using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RBlazingPhantomBlade : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "BlazingPhantomBlade";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.MaxUpdates = 2;
            Projectile.aiStyle = 18;
            Projectile.alpha = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20 * Projectile.MaxUpdates;
            Projectile.timeLeft = 180 * Projectile.MaxUpdates;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, 0.6f, 0f, 0f);
            Projectile.rotation += 0.4f;
            NPC target = Projectile.Center.InPosClosestNPC(900);
            if (Projectile.ai[0] == 0) {
                if (Projectile.ai[1] == 0) {
                    Projectile.velocity *= 0.98f;
                    if (Projectile.timeLeft < 150) {
                        Projectile.velocity = Projectile.velocity.UnitVector() * 7;
                        Projectile.ai[1] = 1;
                    }
                }
                if (Projectile.ai[1] == 1 && target != null) {
                    Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    Projectile.velocity *= 1.005f;
                }
            }
            if (Projectile.ai[0] == 1) {
                if (Projectile.ai[1] == 0) {
                    Projectile.velocity *= 0.995f;
                    if (Projectile.timeLeft < 100) {
                        Projectile.ai[1] = 1;
                    }
                }
                if (Projectile.ai[1] == 1 && target != null) {
                    Projectile.ChasingBehavior(target.Center, Projectile.velocity.Length());
                    Projectile.velocity *= 1.008f;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            if (Projectile.timeLeft < 85) {
                byte b = (byte)(Projectile.timeLeft * 3);
                byte alpha = (byte)(100f * (b / 255f));
                return new Color(b, b, b, alpha);
            }

            return new Color(255, 255, 255, 100);
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0 && Projectile.ai[0] == 0) {
                int type = ModContent.ProjectileType<HyperBlade>();
                for (int i = 0; i < 3; i++) {
                    Vector2 offsetvr = CWRUtils.GetRandomVevtor(-97.5f, -82.5f, 360);
                    Vector2 spanPos = target.Center + offsetvr;
                    Projectile.NewProjectile(
                        Projectile.parent(),
                        spanPos,
                        -offsetvr.UnitVector() * 12,
                        type,
                        Projectile.damage / 2,
                        0,
                        Projectile.owner
                        );
                }
            }

            target.AddBuff(24, 180);
        }
    }
}
