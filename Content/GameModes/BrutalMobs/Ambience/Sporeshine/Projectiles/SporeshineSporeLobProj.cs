using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine.Projectiles
{
    /// <summary>
    /// 孢子团：巨菇喷发抛出的发光孢囊（「巨菇喷发」的飞行段）。
    /// ai[0]=档位。飞行本身无判定（可见即可躲），触地即碎，
    /// 由权威端在落点绽开 <see cref="SporeshineSporeFogProj"/>；伤害值随身携带转交雾区
    /// </summary>
    internal class SporeshineSporeLobProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>抛物重力（蓄胀体解算弹道时共用同一常数）</summary>
        internal const float Gravity = 0.34f;
        /// <summary>落点雾区全局并发上限</summary>
        private const int FogCap = 4;
        /// <summary>寿命保险（正常在此之前早已触地）</summary>
        private const int LifeFrames = 260;
        /// <summary>出生穿行帧数（先飞离菌盖再开碰撞，防原地碎裂）</summary>
        private const int LaunchGraceFrames = 8;

        private static readonly Color DeepSpore = new(24, 46, 88);
        private static readonly Color BrightSpore = new(110, 215, 255);

        private int Tier => Math.Clamp((int)Projectile.ai[0], 1, 3);
        private int Age => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = false;//飞行段无判定，落地成雾才咬人
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Age == LaunchGraceFrames) {
                Projectile.tileCollide = true;
            }
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + Gravity, 15f);
            Projectile.rotation += 0.09f * MathF.Sign(Projectile.velocity.X);

            if (VaultUtils.isServer) {
                return;
            }
            //拖尾孢尘（≤1 粒/2 帧）
            if (Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.GlowingMushroom, -Projectile.velocity * 0.08f, 130, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, BrightSpore.ToVector3() * 0.32f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float rot = Projectile.velocity.ToRotation();

            //暗色孢囊本体（真 alpha，实体感）+ 速度拉伸的辉光 + 白星核
            Main.EntitySpriteDraw(fog, center, null, DeepSpore * 0.65f, Projectile.rotation,
                fog.Size() * 0.5f, 0.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, BrightSpore with { A = 0 } * 0.55f, rot,
                glow.Size() * 0.5f, new Vector2(0.5f + speed * 0.045f, 0.42f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, center, null, BrightSpore with { A = 0 } * 0.7f,
                Projectile.rotation * 1.4f, star.Size() * 0.5f, 0.075f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            //落点绽雾只在权威端（生成后原生同步）；并发超限则只留碎裂演出
            if (Main.netMode != NetmodeID.MultiplayerClient
                && SporeshinePlayer.CountActive(ModContent.ProjectileType<SporeshineSporeFogProj>()) < FogCap) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                    Projectile.Center - new Vector2(0f, 12f), Vector2.Zero,
                    ModContent.ProjectileType<SporeshineSporeFogProj>(),
                    Projectile.damage, 0f, Main.myPlayer, Tier);
            }

            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GlowingMushroom,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f) - new Vector2(0f, 1f),
                    110, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
