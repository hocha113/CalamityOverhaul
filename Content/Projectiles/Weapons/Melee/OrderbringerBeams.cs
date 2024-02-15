using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class OrderbringerBeams : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "OrderbringerBeam";
        public new string LocalizationCategory => "Projectiles.Melee";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.extraUpdates = 2;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.DamageType = DamageClass.Melee;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (Projectile.ai[1] == 0f) {
                Projectile.ai[1] = 1f;
                if (Projectile.IsOwnedByLocalPlayer())
                    SpanProj();
                _ = SoundEngine.PlaySound(SoundID.Item60, Projectile.position);
            }

            if (Projectile.localAI[0] == 0f) {
                Projectile.scale -= 0.02f;
                Projectile.alpha += 30;
                if (Projectile.alpha >= 250) {
                    Projectile.alpha = 255;
                    Projectile.localAI[0] = 1f;
                }
            }
            else if (Projectile.localAI[0] == 1f) {
                Projectile.scale += 0.02f;
                Projectile.alpha -= 30;
                if (Projectile.alpha <= 0) {
                    Projectile.alpha = 0;
                    Projectile.localAI[0] = 0f;
                }
            }

            Lighting.AddLight((int)Projectile.Center.X / 16, (int)Projectile.Center.Y / 16, Main.DiscoR / 200f, Main.DiscoG / 200f, Main.DiscoB / 200f);

            int rainbow = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RainbowTorch, 0f, 0f, 100, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 0.6f);
            Main.dust[rainbow].noGravity = true;
            Main.dust[rainbow].velocity *= 0.5f;
            Main.dust[rainbow].velocity += Projectile.velocity * 0.1f;

            CalamityUtils.HomeInOnNPC(Projectile, !Projectile.tileCollide, 200f, 10f, 20f);
        }

        public void SpanProj() {
            for (int i = 0; i < 3; i++) {
                Vector2 pos = Projectile.Center + Projectile.velocity.GetNormalVector() * Main.rand.Next(-160, 160);
                Vector2 vr = pos.To(Main.MouseWorld).UnitVector() * 10;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, vr, ModContent.ProjectileType<OrderbringerBeams2>()
                    , Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 0f);
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB, Projectile.alpha);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                target.CWR().OrderbringerOnHitNum += 1;
                if (target.CWR().OrderbringerOnHitNum > 13) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero, ModContent.ProjectileType<OrderbringerProj>()
                    , Projectile.damage * 3, Projectile.knockBack, Projectile.owner, 0f, 0f, target.whoAmI);
                    target.CWR().OrderbringerOnHitNum = 0;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 115) {
                return false;
            }
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }

        public override void OnKill(int timeLeft) {
            _ = SoundEngine.PlaySound(SoundID.Item122, Projectile.Center);
            for (int i = 0; i < 2; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.RainbowTorch, 0f, 0f, 100, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.5f);
                Main.dust[dust].noGravity = true;
            }
            for (int j = 0; j < 20; j++) {
                int deathRainbow = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RainbowTorch, 0f, 0f, 100, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 2.5f);
                Main.dust[deathRainbow].noGravity = true;
                Main.dust[deathRainbow].velocity *= 3f;
                deathRainbow = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RainbowTorch, 0f, 0f, 100, new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), 1.5f);
                Main.dust[deathRainbow].velocity *= 2f;
                Main.dust[deathRainbow].noGravity = true;
            }
        }
    }
}
