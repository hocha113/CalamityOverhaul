using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles
{
    /// <summary>体节等离子弹，替DeathLaser；ai[0]0红橙1猩红 ai[1]1=微加速</summary>
    internal class DestroyerBolt : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        private Color BaseColor => Projectile.ai[0] == 1f
            ? new Color(255, 62, 92)
            : new Color(255, 98, 36);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 9;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.alpha = 255;
        }

        public override void AI() {
            //首帧本端出膛声(服务端弹在客户首次AI播)
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (!VaultUtils.isServer) {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item33 with {
                        Volume = 0.32f,
                        Pitch = 0.25f + Main.rand.NextFloat(-0.12f, 0.12f),
                        MaxInstances = 8
                    }, Projectile.Center);
                }
            }

            //淡入
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 28;
                if (Projectile.alpha < 0) {
                    Projectile.alpha = 0;
                }
            }

            //微加速
            if (Projectile.ai[1] == 1f) {
                if (Projectile.localAI[0] == 0f) {
                    Projectile.localAI[0] = Projectile.velocity.Length();
                }
                float maxSpeed = Projectile.localAI[0] * 1.6f;
                if (Projectile.velocity.Length() < maxSpeed) {
                    Projectile.velocity *= 1.012f;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, BaseColor.ToVector3() * 0.62f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vel = -Projectile.velocity.SafeNormalize(Vector2.Zero)
                    .RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel, BaseColor,
                    Main.rand.NextFloat(0.6f, 1.0f)).Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 origin = glow.Size() / 2f;
            float fade = 1f - Projectile.alpha / 255f;
            Color core = BaseColor with { A = 0 };

            //拖尾光珠
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(glow, pos, null, core * (0.34f * t * fade), Projectile.rotation,
                    origin, new Vector2(1.1f, 0.5f) * t * 0.8f, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(glow, drawPos, null, core * (0.55f * fade), Projectile.rotation,
                origin, new Vector2(1.7f, 1.0f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core * (0.95f * fade), Projectile.rotation,
                origin, new Vector2(1.25f, 0.55f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 235, 200, 0) * (0.9f * fade), Projectile.rotation,
                origin, new Vector2(0.7f, 0.32f), SpriteEffects.None, 0);

            return false;
        }
    }
}
