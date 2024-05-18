using CalamityOverhaul.Common.Effects;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class GrandGuardianBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Melee + "GrandGuardianGlow";
        NPC hitnpc;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
        public override void SetDefaults() {
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.Size = new Vector2(36);
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 380;
            Projectile.extraUpdates = 3;
            Projectile.alpha = 0;
        }

        public override void AI() {
            Projectile.ai[1] = (float)Math.Abs(Math.Sin(Projectile.timeLeft * 0.05f));
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.localAI[0] <= 0) {
                if (Projectile.ai[2] > 0) {
                    Projectile.penetrate = 1;
                    Projectile.extraUpdates = 5;
                    Projectile.alpha += 15;
                }
                else {
                    Projectile.alpha = (int)(155 + Projectile.ai[1] * 100);
                    if (Projectile.ai[0] > 20) {
                        Projectile.velocity *= 0.98f;
                    }
                }
            }
            else {
                if (!hitnpc.active) {
                    Projectile.Kill();
                    return;
                }
                Projectile.extraUpdates = 0;
                Projectile.position += hitnpc.velocity;
                float sengs = 0.9f;
                if (hitnpc.width > 180) {
                    sengs += 0.01f;
                }
                if (hitnpc.width > 280) {
                    sengs += 0.01f;
                }
                if (hitnpc.width > 320) {
                    sengs += 0.01f;
                }
                if (hitnpc.width > 340) {
                    sengs += 0.01f;
                }
                Projectile.velocity *= sengs;
            }
            Projectile.ai[0]++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.localAI[0] == 0) {
                hitnpc = target;
                Projectile.timeLeft = 90;
                if (!target.active) {
                    hitnpc = null;
                    Projectile.localAI[0] = 0;
                    return;
                }
                Projectile.localAI[0]++;
            }
        }

        public override void OnKill(int timeLeft) {
            int inc;
            for (int i = 4; i < 31; i = inc + 1) {
                Vector2 vector = Projectile.rotation.ToRotationVector2() * 13;
                float oldXPos = vector.X * (30f / i);
                float oldYPos = vector.Y * (30f / i);
                int killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 8, 8
                    , DustID.FireworkFountain_Blue, vector.X, vector.Y, 100, default, 1.8f);
                Main.dust[killDust].noGravity = true;
                Dust dust2 = Main.dust[killDust];
                dust2.velocity *= 0.5f;
                dust2.color = Color.Blue;
                dust2.shader = EffectsRegistry.StreamerDustShader;
                dust2.shader.UseColor(dust2.color);
                killDust = Dust.NewDust(new Vector2(Projectile.oldPosition.X - oldXPos, Projectile.oldPosition.Y - oldYPos), 8, 8
                    , DustID.FireworkFountain_Blue, vector.X, vector.Y, 100, default, 1.4f);
                dust2 = Main.dust[killDust];
                dust2.velocity *= 0.05f;
                dust2.noGravity = true;
                inc = i;
            }
            Projectile.Explode(explosionSound: SoundID.Item14 with { Pitch = 0.6f });
        }

        public override bool PreDraw(ref Color color) {
            Texture2D mainValue = Projectile.T2DValue();
            Main.spriteBatch.Draw(mainValue, Projectile.Center - Main.screenPosition, null
                , Color.White with { G = (byte)(Projectile.ai[1] * 255) } * (Projectile.alpha / 255f)
                , Projectile.rotation + MathHelper.PiOver4, mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
