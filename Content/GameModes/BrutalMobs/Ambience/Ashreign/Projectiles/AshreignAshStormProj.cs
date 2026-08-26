using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign.Projectiles
{
    /// <summary>
    /// 烬暴·过境红霾墙。权威端在目标上风 1500px 生成，恒速横扫（velocity 随生成包同步，
    /// 各端一切表现由位置+速度确定性推演，不吃本地计时器）。
    /// 预告=远处红霾墙可见逼近 + 风声转烈（逼近耗时 ≥340 帧，远超 45 帧公平线）；
    /// 过境=视野烬幕+轻推+每秒微量灼伤（逐玩家结算在 AshreignPlayer，
    /// 躲入建筑/障碍后免疫）；散去=尾缘淡出。
    /// 与 DuneStorm 风堑（阵风推挤）、Frostveil 风雪墙的差异：带持续灼伤，且上风实体墙可靠免疫。
    /// 墙体本身无接触判定（hostile=false），暗层用真 alpha 的 Fog 承载
    /// </summary>
    internal class AshreignAshStormProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>烬幕带半宽（像素），带内即暴露候选</summary>
        internal const float HalfWidth = 380f;
        private const int TotalLife = 780;
        private const int FadeInFrames = 40;
        private const int FadeOutFrames = 60;

        /// <summary>存在包络 0~1（首尾淡入淡出；静态口供氛围层与玩家结算同读）</summary>
        internal static float Envelope(Projectile proj) {
            int elapsed = TotalLife - proj.timeLeft;
            float fadeIn = MathHelper.Clamp(elapsed / (float)FadeInFrames, 0f, 1f);
            float fadeOut = MathHelper.Clamp(proj.timeLeft / (float)FadeOutFrames, 0f, 1f);
            return Math.Min(fadeIn, fadeOut);
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Main.dedServ || Main.gamePaused) {
                return;
            }
            float envelope = Envelope(Projectile);
            if (envelope < 0.05f) {
                return;
            }

            //只在带与屏幕相交时花粒子预算
            float cx = Projectile.Center.X;
            float screenLeft = Main.screenPosition.X;
            float screenRight = screenLeft + Main.screenWidth;
            if (cx + HalfWidth + 240f < screenLeft || cx - HalfWidth - 240f > screenRight) {
                return;
            }
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;

            //横掠灰烬流（带内密集横飞的烬屑，事件期短时上探约 80/s）
            if (Main.rand.NextFloat() < 0.9f * envelope) {
                Vector2 pos = RandomPointInBand(cx, screenLeft, screenRight);
                Dust ash = Dust.NewDustPerfect(pos, DustID.Ash,
                    new Vector2(dir * Main.rand.NextFloat(7f, 12f), Main.rand.NextFloat(-0.5f, 0.5f)),
                    (int)(150 - 60 * envelope), default, Main.rand.NextFloat(0.9f, 1.4f));
                ash.noGravity = true;
            }
            //浑浊烟团
            if (Main.rand.NextFloat() < 0.2f * envelope) {
                Vector2 pos = RandomPointInBand(cx, screenLeft, screenRight);
                Dust smoke = Dust.NewDustPerfect(pos, DustID.Smoke,
                    new Vector2(dir * Main.rand.NextFloat(4f, 7f), Main.rand.NextFloat(-0.4f, 0.4f)),
                    140, default, Main.rand.NextFloat(1.1f, 1.7f));
                smoke.noGravity = true;
            }
            //横曳火星（速度拉伸成丝）
            if (Main.rand.NextFloat() < 0.25f * envelope) {
                Vector2 pos = RandomPointInBand(cx, screenLeft, screenRight);
                PRTLoader.NewParticle<PRT_DefEmber>(pos,
                    new Vector2(dir * Main.rand.NextFloat(6f, 10f), Main.rand.NextFloat(-0.6f, 0.6f)),
                    Ashreign.EmberWarm, Main.rand.NextFloat(0.32f, 0.52f))
                    ?.Configure(Main.rand.Next(20, 32), 0f, 0.99f);
            }
        }

        private static Vector2 RandomPointInBand(float cx, float screenLeft, float screenRight) {
            float left = Math.Max(cx - HalfWidth, screenLeft - 100f);
            float right = Math.Min(cx + HalfWidth, screenRight + 100f);
            return new Vector2(Main.rand.NextFloat(left, right),
                Main.screenPosition.Y + Main.rand.NextFloat(-60f, Main.screenHeight + 60f));
        }

        public override bool PreDraw(ref Color lightColor) {
            float envelope = Envelope(Projectile);
            if (envelope < 0.02f) {
                return false;
            }
            float cx = Projectile.Center.X;
            float dir = Projectile.velocity.X >= 0f ? 1f : -1f;
            float time = (float)Main.timeForVisualEffects * 0.016f;
            float seed = Projectile.identity * 2.39f;

            float screenTop = Main.screenPosition.Y;
            float screenLeft = Main.screenPosition.X;
            float screenRight = screenLeft + Main.screenWidth;

            //==== 烬幕暗纱：Fog 真 alpha 染暗（加色物理上画不出暗层）====
            //四列纵深：前缘偏红（红霾墙），核心最浓，尾缘稀薄
            Texture2D fog = CWRAsset.Fog.Value;
            Vector2 fogOrigin = fog.Size() * 0.5f;
            ReadOnlySpan<float> colOffset = [0.75f, 0.25f, -0.35f, -0.8f];
            ReadOnlySpan<float> colAlpha = [0.30f, 0.38f, 0.30f, 0.20f];
            ReadOnlySpan<float> colLead = [1f, 0.35f, 0.1f, 0f];

            for (int c = 0; c < 4; c++) {
                float colX = cx + dir * HalfWidth * colOffset[c];
                if (colX < screenLeft - 460f || colX > screenRight + 460f) {
                    continue;
                }
                Color colTint = Color.Lerp(Ashreign.AshDark, Ashreign.HazeRed, colLead[c] * 0.55f);
                int row = 0;
                for (float y = screenTop - 140f; y < screenTop + Main.screenHeight + 160f; y += 300f, row++) {
                    float wobX = MathF.Sin(time * 0.7f + seed + row * 2.1f + c * 1.3f) * 46f;
                    float wobY = MathF.Sin(time * 0.5f + seed * 1.7f + row * 1.4f + c) * 22f;
                    float sizeJit = 2.5f + 0.5f * MathF.Sin(seed + row * 3.7f + c * 2.2f);
                    float rot = MathF.Sin(seed * 0.6f + row + c) * 0.6f + time * 0.05f * (c % 2 == 0 ? 1f : -1f);
                    Main.EntitySpriteDraw(fog,
                        new Vector2(colX + wobX, y + wobY) - Main.screenPosition, null,
                        colTint * (colAlpha[c] * envelope), rot, fogOrigin, sizeJit,
                        SpriteEffects.None, 0);
                }
            }

            //==== 幕内暖芯：两根竖向余光柱（A=0 加色，火在灰里闷烧）====
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            for (int g = 0; g < 2; g++) {
                float gx = cx + dir * HalfWidth * (0.3f - 0.6f * g)
                    + MathF.Sin(time * 0.9f + seed + g * 2.6f) * 60f;
                if (gx < screenLeft - 400f || gx > screenRight + 400f) {
                    continue;
                }
                Main.EntitySpriteDraw(glowTex,
                    new Vector2(gx, screenTop + Main.screenHeight * 0.55f) - Main.screenPosition, null,
                    new Color(176, 62, 26, 0) * (0.20f * envelope), 0f, glowTex.Size() * 0.5f,
                    new Vector2(5.5f, 10f), SpriteEffects.None, 0);
            }

            //==== 横掠速度线：随机截条横扫（A=0 加色，黑底图禁 AlphaBlend 原样画）====
            Texture2D lines = CWRAsset.SpeedLines01.Value;
            for (int s = 0; s < 4; s++) {
                //沿带内循环推进，位置由时间确定性推演
                float cycle = 2f * HalfWidth;
                float local = (time * 260f + s * 537f) % cycle;
                float sx = cx - dir * HalfWidth + dir * local;
                if (sx < screenLeft - 700f || sx > screenRight + 700f) {
                    continue;
                }
                int srcY = (int)(MathF.Abs(MathF.Sin(seed + s * 4.9f)) * 1000f);
                float sy = screenTop + (0.12f + 0.76f * MathF.Abs(MathF.Sin(seed * 1.3f + s * 2.7f)))
                    * Main.screenHeight;
                var src = new Rectangle(0, srcY, lines.Width, 14);
                Main.EntitySpriteDraw(lines, new Vector2(sx, sy) - Main.screenPosition, src,
                    new Color(196, 122, 82, 0) * (0.26f * envelope), 0f,
                    new Vector2(src.Width * 0.5f, 7f), new Vector2(1.5f, 0.6f),
                    dir >= 0f ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }
            return false;
        }
    }
}
