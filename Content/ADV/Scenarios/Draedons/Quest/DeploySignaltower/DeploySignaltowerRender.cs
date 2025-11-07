using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltower
{
    internal class DeploySignaltowerRender : RenderHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D DeploySignaltowerShow;//大小宽512高768，用于ADV任务介绍场景中展示信号塔的图片

        //图片展示动画状态
        private static bool showingImage = false;
        private static float imageFadeProgress = 0f;
        private static float imageScaleProgress = 0f;
        private static float imageRotation = 0f;
        private static float imageGlowIntensity = 0f;

        //动画参数
        private const float ImageFadeSpeed = 0.06f;
        private const float ImageScaleSpeed = 0.08f;
        private const float ImageBaseScale = 1.8f;
        private const float ImageMaxScale = 2.2f;

        //图片位置偏移，在玩家头顶右侧
        private static Vector2 imageOffset = new Vector2(280f, -240f);

        //科技光效粒子
        private static readonly List<TechParticle> techParticles = new();
        private static int particleSpawnTimer = 0;

        //全息投影效果
        private static float hologramFlicker = 0f;
        private static float scanLineProgress = 0f;

        public override void UpdateBySystem(int index) {
            if (!showingImage) {
                return;
            }

            //更新图片展示动画
            UpdateImageAnimation();

            //更新科技粒子
            UpdateTechParticles();

            //更新全息效果
            hologramFlicker += 0.08f;
            scanLineProgress += 0.035f;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (scanLineProgress > 1f) scanLineProgress -= 1f;
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (showingImage && imageFadeProgress > 0.01f) {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                DrawTowerImage(spriteBatch, main);
                Main.spriteBatch.End();
            }
        }

        /// <summary>
        /// 更新图片展示动画状态
        /// </summary>
        private static void UpdateImageAnimation() {
            //淡入和缩放
            if (imageFadeProgress < 1f) {
                imageFadeProgress = Math.Min(imageFadeProgress + ImageFadeSpeed, 1f);
            }
            if (imageScaleProgress < 1f) {
                imageScaleProgress = Math.Min(imageScaleProgress + ImageScaleSpeed, 1f);
            }

            //光效脉冲
            imageGlowIntensity = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f) * 0.5f + 0.5f;

            //生成科技粒子
            particleSpawnTimer++;
            if (particleSpawnTimer >= 4) {
                particleSpawnTimer = 0;
                SpawnTechParticle();
            }
        }

        /// <summary>
        /// 绘制信号塔图片
        /// </summary>
        private static void DrawTowerImage(SpriteBatch spriteBatch, Main main) {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;
            if (DeploySignaltowerShow == null) return;

            //计算世界坐标位置
            Vector2 worldPos = player.Center + imageOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            //计算缩放
            float easedScale = CWRUtils.EaseOutBack(imageScaleProgress);
            float scale = MathHelper.Lerp(ImageBaseScale, ImageMaxScale, easedScale);

            //计算透明度
            float alpha = imageFadeProgress * 0.88f;

            //全息闪烁效果
            float flicker = (float)Math.Sin(hologramFlicker * 1.8f) * 0.15f + 0.85f;
            alpha *= flicker;

            Color techColor = new Color(80, 200, 255);

            //绘制外层科技光晕
            for (int i = 0; i < 3; i++) {
                float glowScale = scale * (1.15f + i * 0.08f);
                float glowAlpha = alpha * (0.25f - i * 0.06f) * imageGlowIntensity;
                spriteBatch.Draw(
                    DeploySignaltowerShow,
                    screenPos,
                    null,
                    techColor * glowAlpha,
                    imageRotation,
                    DeploySignaltowerShow.Size() * 0.5f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //绘制六角网格叠加
            DrawHexagonalGrid(spriteBatch, screenPos, DeploySignaltowerShow.Size() * scale, alpha * 0.3f);

            //绘制扫描线效果
            DrawScanLines(spriteBatch, screenPos, DeploySignaltowerShow.Size() * scale, alpha);

            //绘制主图片，带轻微的科技色调
            Color mainColor = Color.Lerp(Color.White, techColor, 0.15f);
            spriteBatch.Draw(
                DeploySignaltowerShow,
                screenPos,
                null,
                mainColor * alpha,
                imageRotation,
                DeploySignaltowerShow.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f
            );

            //绘制数据流粒子
            DrawTechParticles(spriteBatch, screenPos);

            //绘制边框投影效果
            DrawHologramBorder(spriteBatch, screenPos, DeploySignaltowerShow.Size() * scale, alpha, techColor);
        }

        /// <summary>
        /// 绘制六角网格效果
        /// </summary>
        private static void DrawHexagonalGrid(SpriteBatch sb, Vector2 center, Vector2 size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            int gridRows = 10;
            float gridHeight = size.Y / gridRows;

            for (int row = 0; row < gridRows; row++) {
                float t = row / (float)gridRows;
                float y = center.Y - size.Y * 0.5f + row * gridHeight;
                float phase = hologramFlicker + t * MathHelper.Pi;
                float brightness = (float)Math.Sin(phase) * 0.5f + 0.5f;

                Color gridColor = new Color(60, 180, 255) * (alpha * brightness);
                sb.Draw(
                    pixel,
                    new Vector2(center.X - size.X * 0.5f + 10f, y),
                    new Rectangle(0, 0, 1, 1),
                    gridColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(size.X - 20f, 1f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制扫描线效果
        /// </summary>
        private static void DrawScanLines(SpriteBatch sb, Vector2 center, Vector2 size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //主扫描线
            float scanY = center.Y - size.Y * 0.5f + scanLineProgress * size.Y;
            Color scanColor = new Color(80, 220, 255) * (alpha * 0.6f);

            for (int i = -2; i <= 2; i++) {
                float offsetY = scanY + i * 2.5f;
                float intensity = 1f - Math.Abs(i) * 0.25f;
                sb.Draw(
                    pixel,
                    new Vector2(center.X - size.X * 0.5f, offsetY),
                    new Rectangle(0, 0, 1, 1),
                    scanColor * intensity,
                    0f,
                    Vector2.Zero,
                    new Vector2(size.X, 2f),
                    SpriteEffects.None,
                    0f
                );
            }

            //额外的随机扫描线
            int extraLines = 3;
            for (int i = 0; i < extraLines; i++) {
                float lineProgress = (scanLineProgress + i * 0.33f) % 1f;
                float lineY = center.Y - size.Y * 0.5f + lineProgress * size.Y;
                float lineAlpha = (float)Math.Sin(lineProgress * MathHelper.Pi) * alpha * 0.25f;

                sb.Draw(
                    pixel,
                    new Vector2(center.X - size.X * 0.5f, lineY),
                    new Rectangle(0, 0, 1, 1),
                    new Color(60, 180, 255) * lineAlpha,
                    0f,
                    Vector2.Zero,
                    new Vector2(size.X, 1f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制全息投影边框
        /// </summary>
        private static void DrawHologramBorder(SpriteBatch sb, Vector2 center, Vector2 size, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Rectangle borderRect = new Rectangle(
                (int)(center.X - size.X * 0.5f),
                (int)(center.Y - size.Y * 0.5f),
                (int)size.X,
                (int)size.Y
            );

            float borderAlpha = alpha * 0.7f;
            Color borderColor = color * borderAlpha;

            //绘制四边
            sb.Draw(pixel, new Rectangle(borderRect.X, borderRect.Y, borderRect.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(borderRect.X, borderRect.Bottom - 2, borderRect.Width, 2), borderColor * 0.7f);
            sb.Draw(pixel, new Rectangle(borderRect.X, borderRect.Y, 2, borderRect.Height), borderColor * 0.85f);
            sb.Draw(pixel, new Rectangle(borderRect.Right - 2, borderRect.Y, 2, borderRect.Height), borderColor * 0.85f);

            //绘制四角装饰
            DrawCornerTech(sb, new Vector2(borderRect.X + 8, borderRect.Y + 8), color * borderAlpha, -MathHelper.PiOver2);
            DrawCornerTech(sb, new Vector2(borderRect.Right - 8, borderRect.Y + 8), color * borderAlpha, 0f);
            DrawCornerTech(sb, new Vector2(borderRect.X + 8, borderRect.Bottom - 8), color * borderAlpha, MathHelper.Pi);
            DrawCornerTech(sb, new Vector2(borderRect.Right - 8, borderRect.Bottom - 8), color * borderAlpha, MathHelper.PiOver2);
        }

        /// <summary>
        /// 绘制角落科技装饰
        /// </summary>
        private static void DrawCornerTech(SpriteBatch sb, Vector2 pos, Color color, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float size = 8f;
            sb.Draw(
                pixel,
                pos,
                new Rectangle(0, 0, 1, 1),
                color,
                rotation,
                new Vector2(0.5f),
                new Vector2(size, size * 0.2f),
                SpriteEffects.None,
                0f
            );
            sb.Draw(
                pixel,
                pos,
                new Rectangle(0, 0, 1, 1),
                color * 0.7f,
                rotation + MathHelper.PiOver2,
                new Vector2(0.5f),
                new Vector2(size, size * 0.2f),
                SpriteEffects.None,
                0f
            );
        }

        #region 科技粒子系统
        private class TechParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public Color Color;

            public TechParticle(Vector2 pos, Color color) {
                Position = pos;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(0.3f, 1.2f);
                Velocity = angle.ToRotationVector2() * speed;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(40f, 80f);
                Size = Main.rand.NextFloat(1.5f, 3.5f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Color = color;
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.96f;
                Rotation += 0.04f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                if (pixel == null) return;

                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                float alpha = fade * 0.75f;

                sb.Draw(
                    pixel,
                    Position,
                    new Rectangle(0, 0, 1, 1),
                    Color * alpha,
                    Rotation,
                    new Vector2(0.5f),
                    new Vector2(Size * 2f, Size * 0.3f),
                    SpriteEffects.None,
                    0f
                );
                sb.Draw(
                    pixel,
                    Position,
                    new Rectangle(0, 0, 1, 1),
                    Color * (alpha * 0.8f),
                    Rotation + MathHelper.PiOver2,
                    new Vector2(0.5f),
                    new Vector2(Size * 2f, Size * 0.3f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static void SpawnTechParticle() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;
            if (DeploySignaltowerShow == null) return;

            Vector2 worldPos = player.Center + imageOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            //在图片边缘生成粒子
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(30f, 60f);
            Vector2 spawnPos = screenPos + angle.ToRotationVector2() * distance;

            Color particleColor = new Color(80, 200, 255);
            techParticles.Add(new TechParticle(spawnPos, particleColor));
        }

        private static void UpdateTechParticles() {
            for (int i = techParticles.Count - 1; i >= 0; i--) {
                if (techParticles[i].Update()) {
                    techParticles.RemoveAt(i);
                }
            }

            //限制粒子数量
            while (techParticles.Count > 25) {
                techParticles.RemoveAt(0);
            }
        }

        private static void DrawTechParticles(SpriteBatch spriteBatch, Vector2 center) {
            foreach (var particle in techParticles) {
                particle.Draw(spriteBatch);
            }
        }
        #endregion

        /// <summary>
        /// 注册展示效果
        /// </summary>
        internal static void RegisterShowEffect() {
            //重置状态
            showingImage = false;
            imageFadeProgress = 0f;
            imageScaleProgress = 0f;
            imageRotation = 0f;
            imageGlowIntensity = 0f;
            techParticles.Clear();
            particleSpawnTimer = 0;
            hologramFlicker = 0f;
            scanLineProgress = 0f;
        }

        /// <summary>
        /// 显示信号塔图片
        /// </summary>
        internal static void ShowTowerImage() {
            showingImage = true;
            imageFadeProgress = 0f;
            imageScaleProgress = 0f;

            //播放全息投影音效
            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.6f,
                Pitch = 0.3f,
                MaxInstances = 2
            });

            //播放额外的科技感音效
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy with {
                Volume = 0.4f,
                Pitch = 0.5f,
                MaxInstances = 1
            });
        }

        /// <summary>
        /// 隐藏信号塔图片
        /// </summary>
        internal static void HideTowerImage() {
            showingImage = false;
        }

        /// <summary>
        /// 清理资源，当场景结束时调用
        /// </summary>
        internal static void Cleanup() {
            showingImage = false;
            imageFadeProgress = 0f;
            imageScaleProgress = 0f;
            techParticles.Clear();
        }
    }
}
