using InnoVault.RenderHandles;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltower
{
    internal class DeploySignaltowerRender : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        [VaultLoaden(CWRConstant.ADV + "Draedon/")]
        public static Texture2D DeploySignaltowerShow;//大小宽512高768，用于ADV任务介绍场景中展示信号塔的图片

        public override bool Active => showingImage || imageFadeProgress > 0f;

        //图片展示动画状态
        private static bool showingImage = false;
        private static float imageFadeProgress = 0f;
        private static float imageScaleProgress = 0f;
        private static float imageRotation = 0f;
        private static float imageGlowIntensity = 0f;

        //动画参数
        private const float ImageFadeSpeed = 0.06f;
        private const float ImageScaleSpeed = 0.08f;
        private const float ImageBaseScale = 0.6f;
        private const float ImageMaxScale = 0.7f;

        //图片位置偏移，在屏幕右侧，相对于屏幕中心
        private static Vector2 GetImageScreenPos() {
            //定位到屏幕垂直居中偏上
            return new Vector2(Main.screenWidth / 2, Main.screenHeight * 0.35f);
        }

        //信息框参数
        private const float InfoBoxWidth = 280f;
        private const float InfoBoxHeight = 400f;
        private static Vector2 GetInfoBoxScreenPos() {
            //信息框在图片左侧
            Vector2 imagePos = GetImageScreenPos();
            return new Vector2(imagePos.X - 320f, imagePos.Y);
        }

        //科技光效粒子
        private static readonly List<TechParticle> techParticles = new();
        private static int particleSpawnTimer = 0;

        //全息投影效果
        private static float hologramFlicker = 0f;
        private static float scanLineProgress = 0f;

        //数据流效果
        private static readonly List<DataStream> dataStreams = new();
        private static int dataStreamTimer = 0;
        private static float dataUpdateTimer = 0f;
        private static readonly List<string> currentDataLines = new();

        //科技数据文本库
        private static readonly string[] techDataTemplates = [
            "QUANTUM_ENTANGLE: {0}%",
            "SIGNAL_STRENGTH: {0} dBm",
            "FREQUENCY: {0} GHz",
            "UPLINK_RATE: {0} Tb/s",
            "NODE_COUNT: {0}/∞",
            "LATENCY: {0} ps",
            "ENERGY_OUTPUT: {0} ZJ",
            "STABILITY: {0}%",

            "SYNC_STATUS: {0}",
            "COHERENCE: {0}%",

            "PHASE_ALIGN: {0}°",
            "BANDWIDTH: {0} PHz",

            "ERROR_RATE: {0}e-{1}",

            "DIMENSION: {0}D",
            "PROTOCOL: QEP-{0}",
        ];

        //数据显示状态
        private const int maxDataLines = 12;

        public override void LogicUpdate() {
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

            //更新数据流
            UpdateDataStreams();

            //更新数据文本
            UpdateDataText();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showingImage && imageFadeProgress > 0.01f) {
                //先绘制信息框
                DrawInfoBox(spriteBatch);

                //再绘制信号塔图片
                DrawTowerImage(spriteBatch);
            }
        }

        /// <summary>
        /// 更新数据文本
        /// </summary>
        private static void UpdateDataText() {
            dataUpdateTimer += 0.05f;
            
            //每隔一段时间更新一些数据行
            if (dataUpdateTimer >= 1f) {
                dataUpdateTimer = 0f;
                
                //随机更新几行数据
                int linesToUpdate = Main.rand.Next(2, 5);
                for (int i = 0; i < linesToUpdate && currentDataLines.Count < maxDataLines; i++) {
                    string newLine = GenerateRandomDataLine();
                    if (currentDataLines.Count >= maxDataLines) {
                        currentDataLines.RemoveAt(0);
                    }
                    currentDataLines.Add(newLine);
                }
            }
        }

        /// <summary>
        /// 生成随机科技数据行
        /// </summary>
        private static string GenerateRandomDataLine() {
            string template = techDataTemplates[Main.rand.Next(techDataTemplates.Length)];
            
            if (template.Contains("{0}") && template.Contains("{1}")) {
                //错误率格式
                return string.Format(template, Main.rand.Next(1, 10), Main.rand.Next(12, 25));
            }
            else if (template.Contains("{0}")) {
                //根据不同类型生成不同数据
                if (template.Contains("SYNC_STATUS")) {
                    string[] statuses = { "ACTIVE", "STABLE", "OPTIMAL", "LOCKED" };
                    return string.Format(template, statuses[Main.rand.Next(statuses.Length)]);
                }
                else if (template.Contains("PROTOCOL")) {
                    return string.Format(template, Main.rand.Next(7, 13).ToString("X2"));
                }
                else if (template.Contains("LATENCY")) {
                    return string.Format(template, Main.rand.NextFloat(0.1f, 5.0f).ToString("F2"));
                }
                else if (template.Contains("FREQUENCY")) {
                    return string.Format(template, Main.rand.NextFloat(300f, 999f).ToString("F1"));
                }
                else if (template.Contains("UPLINK")) {
                    return string.Format(template, Main.rand.NextFloat(10f, 99f).ToString("F1"));
                }
                else if (template.Contains("NODE_COUNT")) {
                    return string.Format(template, Main.rand.Next(1, 100));
                }
                else if (template.Contains("DIMENSION")) {
                    return string.Format(template, Main.rand.Next(3, 12));
                }
                else {
                    return string.Format(template, Main.rand.Next(85, 100));
                }
            }
            
            return template;
        }

        /// <summary>
        /// 绘制信息框
        /// </summary>
        private static void DrawInfoBox(SpriteBatch sb) {
            Vector2 boxPos = GetInfoBoxScreenPos();
            Rectangle boxRect = new Rectangle(
                (int)(boxPos.X - InfoBoxWidth * 0.5f),
                (int)(boxPos.Y - InfoBoxHeight * 0.5f),
                (int)InfoBoxWidth,
                (int)InfoBoxHeight
            );

            float alpha = imageFadeProgress * 0.85f;
            float flicker = (float)Math.Sin(hologramFlicker * 1.5f) * 0.12f + 0.88f;
            alpha *= flicker;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color techColor = new Color(80, 200, 255);

            //绘制背景渐变
            DrawInfoBoxBackground(sb, boxRect, alpha);

            //绘制边框
            DrawInfoBoxBorder(sb, boxRect, alpha, techColor);

            //绘制标题栏
            DrawInfoBoxHeader(sb, boxRect, alpha, techColor);

            //绘制数据流粒子
            DrawInfoBoxDataStreams(sb, boxRect, alpha);

            //绘制数据文本
            DrawInfoBoxDataText(sb, boxRect, alpha, techColor);

            //绘制扫描线
            DrawInfoBoxScanLines(sb, boxRect, alpha);
        }

        /// <summary>
        /// 绘制信息框背景
        /// </summary>
        private static void DrawInfoBoxBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //渐变背景
            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)((t + 1f / segments) * rect.Height);
                
                float pulse = (float)Math.Sin(hologramFlicker * 0.8f + t * 1.5f) * 0.5f + 0.5f;
                Color dark = new Color(8, 15, 25);
                Color mid = new Color(15, 25, 40);
                Color blend = Color.Lerp(dark, mid, pulse * 0.5f);
                
                sb.Draw(pixel, new Rectangle(rect.X, y1, rect.Width, Math.Max(1, y2 - y1)),
                    new Rectangle(0, 0, 1, 1), blend * (alpha * 0.92f));
            }
        }

        /// <summary>
        /// 绘制信息框边框
        /// </summary>
        private static void DrawInfoBoxBorder(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color borderColor = techColor * (alpha * 0.75f);
            
            //外边框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), borderColor * 0.7f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), borderColor * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), borderColor * 0.85f);

            //内发光
            Color glowColor = techColor * (alpha * 0.15f);
            sb.Draw(pixel, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, 1), glowColor);
            sb.Draw(pixel, new Rectangle(rect.X + 2, rect.Y + 2, 1, rect.Height - 4), glowColor);

            //角落装饰
            DrawCornerTech(sb, new Vector2(rect.X + 8, rect.Y + 8), techColor * (alpha * 0.8f), -MathHelper.PiOver2);
            DrawCornerTech(sb, new Vector2(rect.Right - 8, rect.Y + 8), techColor * (alpha * 0.8f), 0f);
            DrawCornerTech(sb, new Vector2(rect.X + 8, rect.Bottom - 8), techColor * (alpha * 0.8f), MathHelper.Pi);
            DrawCornerTech(sb, new Vector2(rect.Right - 8, rect.Bottom - 8), techColor * (alpha * 0.8f), MathHelper.PiOver2);
        }

        /// <summary>
        /// 绘制信息框标题栏
        /// </summary>
        private static void DrawInfoBoxHeader(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //标题栏背景
            Rectangle headerRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, 32);
            sb.Draw(pixel, headerRect, new Rectangle(0, 0, 1, 1), new Color(20, 35, 55) * (alpha * 0.6f));

            //标题文本
            string title = "QUANTUM SIGNAL TOWER";
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(title) * 0.55f;
            Vector2 titlePos = new Vector2(
                headerRect.X + (headerRect.Width - titleSize.X) * 0.5f,
                headerRect.Y + (headerRect.Height - titleSize.Y) * 0.5f
            );

            //标题发光
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1f;
                Utils.DrawBorderString(sb, title, titlePos + offset, techColor * (alpha * 0.4f), 0.55f);
            }
            Utils.DrawBorderString(sb, title, titlePos, Color.White * alpha, 0.55f);

            //标题栏底部分隔线
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom, headerRect.Width, 1),
                techColor * (alpha * 0.5f));
        }

        /// <summary>
        /// 绘制信息框数据流
        /// </summary>
        private static void DrawInfoBoxDataStreams(SpriteBatch sb, Rectangle rect, float alpha) {
            foreach (var stream in dataStreams) {
                //只绘制在信息框内的数据流
                if (stream.Position.X >= rect.X && stream.Position.X <= rect.Right &&
                    stream.Position.Y >= rect.Y && stream.Position.Y <= rect.Bottom) {
                    stream.Draw(sb, alpha * 0.7f);
                }
            }
        }

        /// <summary>
        /// 绘制信息框数据文本
        /// </summary>
        private static void DrawInfoBoxDataText(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            if (currentDataLines.Count == 0) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 textPos = new Vector2(rect.X + 12, rect.Y + 44);
            float lineHeight = font.MeasureString("A").Y * 0.48f + 4f;

            for (int i = 0; i < currentDataLines.Count && i < maxDataLines; i++) {
                string line = currentDataLines[i];
                float lineAlpha = alpha * (0.7f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i * 0.3f) * 0.3f);
                
                //添加闪烁的乱码效果
                if (Main.rand.NextBool(30)) {
                    int glitchPos = Main.rand.Next(line.Length);
                    StringBuilder sb2 = new StringBuilder(line);
                    sb2[glitchPos] = "█▓▒░■□"[Main.rand.Next(6)];
                    line = sb2.ToString();
                }

                Color lineColor = Color.Lerp(techColor, Color.White, 0.4f);
                Utils.DrawBorderString(sb, line, textPos, lineColor * lineAlpha, 0.48f);
                textPos.Y += lineHeight;
            }
        }

        /// <summary>
        /// 绘制信息框扫描线
        /// </summary>
        private static void DrawInfoBoxScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float scanY = rect.Y + 40 + scanLineProgress * (rect.Height - 40);
            Color scanColor = new Color(80, 220, 255) * (alpha * 0.25f);

            sb.Draw(pixel, new Vector2(rect.X + 4, scanY), new Rectangle(0, 0, 1, 1),
                scanColor, 0f, Vector2.Zero, new Vector2(rect.Width - 8, 2f), SpriteEffects.None, 0f);
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
        private static void DrawTowerImage(SpriteBatch spriteBatch) {
            if (DeploySignaltowerShow == null) return;

            Vector2 screenPos = GetImageScreenPos();

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
            if (DeploySignaltowerShow == null) return;

            Vector2 screenPos = GetImageScreenPos();

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

        #region 数据流系统
        private class DataStream
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public char Character;

            public DataStream(Vector2 pos, Color color) {
                Position = pos;
                Velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.5f, 3f));
                Life = 0f;
                MaxLife = Main.rand.NextFloat(40f, 90f);
                Size = Main.rand.NextFloat(0.6f, 1.0f);
                Color = color;
                Character = "0123456789ABCDEF"[Main.rand.Next(16)];
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float baseAlpha) {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                float alpha = fade * baseAlpha;

                Utils.DrawBorderString(sb, Character.ToString(), Position, Color * alpha, Size * 0.45f);
            }
        }

        private static void UpdateDataStreams() {
            dataStreamTimer++;
            if (dataStreamTimer >= 8 && dataStreams.Count < 30) {
                dataStreamTimer = 0;
                
                //在信息框内生成数据流
                Vector2 boxPos = GetInfoBoxScreenPos();
                Rectangle boxRect = new Rectangle(
                    (int)(boxPos.X - InfoBoxWidth * 0.5f),
                    (int)(boxPos.Y - InfoBoxHeight * 0.5f),
                    (int)InfoBoxWidth,
                    (int)InfoBoxHeight
                );

                Vector2 spawnPos = new Vector2(
                    Main.rand.NextFloat(boxRect.X + 10, boxRect.Right - 10),
                    boxRect.Y + 40
                );

                Color streamColor = new Color(80, 200, 255);
                dataStreams.Add(new DataStream(spawnPos, streamColor));
            }

            for (int i = dataStreams.Count - 1; i >= 0; i--) {
                if (dataStreams[i].Update()) {
                    dataStreams.RemoveAt(i);
                }
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
            dataStreams.Clear();
            dataStreamTimer = 0;
            dataUpdateTimer = 0f;
            currentDataLines.Clear();
        }

        /// <summary>
        /// 显示信号塔图片
        /// </summary>
        internal static void ShowTowerImage() {
            showingImage = true;
            imageFadeProgress = 0f;
            imageScaleProgress = 0f;
            
            //初始化数据行
            currentDataLines.Clear();
            for (int i = 0; i < 8; i++) {
                currentDataLines.Add(GenerateRandomDataLine());
            }

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
            dataStreams.Clear();
            currentDataLines.Clear();
        }
    }
}
