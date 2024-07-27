using CalamityOverhaul.Common.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class GrandGuardianBeam : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Melee + "GrandGuardianGlow";

        private NPC hitnpc;
        private Vector2 oldnpcPos;
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
                Vector2 npcUpdateVer = oldnpcPos.To(hitnpc.position);
                oldnpcPos = hitnpc.position;
                Projectile.position += npcUpdateVer;
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
            if (Projectile.localAI[0] == 0 && Projectile.ai[0] <= 60) {
                hitnpc = target;
                oldnpcPos = target.position;
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
            CWRDust.SplashDust(Projectile, 31, DustID.FireworkFountain_Blue, DustID.FireworkFountain_Blue, 13, Color.Blue, EffectLoader.StreamerDustShader);
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
