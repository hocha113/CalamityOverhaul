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

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
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

        //信息框参数
        private const float InfoBoxWidth = 320f;//增加宽度以容纳更多数据
        private const float InfoBoxHeight = 460f;//增加高度

        //科技光效粒子
        private static readonly List<TechParticle> techParticles = new();
        private static int particleSpawnTimer = 0;

        //全息投影效果
        private static float hologramFlicker = 0f;
        private static float scanLineProgress = 0f;

        //数据流效果
        private static readonly List<DataStream> dataStreams = new();
        private static readonly List<MatrixRain> matrixRains = new();//矩阵雨效果
        private static int dataStreamTimer = 0;
        private static float dataUpdateTimer = 0f;
        private static readonly List<DataLine> currentDataLines = new();//使用结构化数据行

        //科技数据文本
        private static readonly string[] techDataTemplates = [
            "QE_SYNC: {0}%",
            "SIG_PWR: {0} dBm",
            "FREQ_BAND: {0} GHz",
            "UPLINK: {0} Tb/s",
            "NODE: {0}/∞",
            "LAT: {0} ps",
            "E_OUT: {0} ZJ",
            "STBL: {0}%",

            "SYNC: {0}",
            "COH: {0}%",

            "PHASE: {0}°",
            "BW: {0} PHz",

            "ERR: {0}e-{1}",

            "DIM: {0}D",
            "PROTO: QEP-{0}",

            "TEMP: {0}K",
            "QBIT: {0}",

            "ENT_RATE: {0}%",
            "FLUX: {0} Φ₀",
            "DECO_TIME: {0} μs",
        ];

        //十六进制数据块
        private static readonly List<HexDataBlock> hexBlocks = new();
        private static int hexBlockTimer = 0;

        //数据显示状态
        private const int maxDataLines = 15;
        private const int maxMatrixColumns = 22;//矩阵雨列数

        //数据行结构
        private class DataLine
        {
            public string Text;
            public float GlitchProgress;//乱码进度
            public int UpdateTimer;
            public Color BaseColor;
            public float FlickerPhase;

            public DataLine(string text) {
                Text = text;
                GlitchProgress = 0f;
                UpdateTimer = Main.rand.Next(30, 90);
                BaseColor = new Color(80 + Main.rand.Next(40), 180 + Main.rand.Next(60), 240 + Main.rand.Next(15));
                FlickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        //图片位置偏移，在屏幕垂直居中
        private static Vector2 GetImageScreenPos() {
            return new Vector2(Main.screenWidth / 2, Main.screenHeight * 0.35f);
        }

        private static Vector2 GetInfoBoxScreenPos() {
            //信息框在图片左侧
            Vector2 imagePos = GetImageScreenPos();
            return new Vector2(imagePos.X - 340f, imagePos.Y);
        }

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

            //更新数据流（多层）
            UpdateDataStreams();
            UpdateMatrixRain();
            UpdateHexBlocks();

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
        /// 更新数据文本，带乱码效果
        /// </summary>
        private static void UpdateDataText() {
            dataUpdateTimer += 0.08f;//加快更新速度

            //快速刷新数据行
            if (dataUpdateTimer >= 0.5f) {
                dataUpdateTimer = 0f;

                //移除旧数据，添加新数据，制造洪流感
                if (currentDataLines.Count >= maxDataLines) {
                    currentDataLines.RemoveAt(0);
                }

                currentDataLines.Add(new DataLine(GenerateRandomDataLine()));
            }

            //更新每行的乱码和闪烁效果
            foreach (var line in currentDataLines) {
                line.UpdateTimer--;
                if (line.UpdateTimer <= 0) {
                    line.UpdateTimer = Main.rand.Next(20, 60);
                    line.GlitchProgress = Main.rand.NextFloat(0.3f, 0.9f);
                }
                else {
                    line.GlitchProgress *= 0.92f;//乱码逐渐消退
                }

                line.FlickerPhase += 0.15f;
            }
        }

        /// <summary>
        /// 生成随机科技数据行
        /// </summary>
        private static string GenerateRandomDataLine() {
            string template = techDataTemplates[Main.rand.Next(techDataTemplates.Length)];

            if (template.Contains("{0}") && template.Contains("{1}")) {
                return string.Format(template, Main.rand.Next(1, 10), Main.rand.Next(12, 25));
            }
            else if (template.Contains("{0}")) {
                if (template.Contains("SYNC")) {
                    string[] statuses = { "ACTIVE", "LOCKED", "STABLE", "OPTIMAL", "SYNC", "READY" };
                    return string.Format(template, statuses[Main.rand.Next(statuses.Length)]);
                }
                else if (template.Contains("PROTO")) {
                    return string.Format(template, Main.rand.Next(7, 256).ToString("X2"));
                }
                else if (template.Contains("LAT")) {
                    return string.Format(template, Main.rand.NextFloat(0.01f, 9.99f).ToString("F2"));
                }
                else if (template.Contains("FREQ") || template.Contains("BW")) {
                    return string.Format(template, Main.rand.NextFloat(100f, 999.9f).ToString("F1"));
                }
                else if (template.Contains("UPLINK")) {
                    return string.Format(template, Main.rand.NextFloat(5f, 99.9f).ToString("F1"));
                }
                else if (template.Contains("NODE")) {
                    return string.Format(template, Main.rand.Next(1, 256));
                }
                else if (template.Contains("DIM")) {
                    return string.Format(template, Main.rand.Next(3, 26));
                }
                else if (template.Contains("TEMP")) {
                    return string.Format(template, Main.rand.NextFloat(0.01f, 4.5f).ToString("F2"));
                }
                else if (template.Contains("QBIT")) {
                    return string.Format(template, Main.rand.Next(128, 2048));
                }
                else {
                    return string.Format(template, Main.rand.Next(82, 100));
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

            //绘制背景矩阵雨
            DrawMatrixRain(sb, boxRect, alpha * 0.4f);

            //绘制十六进制数据块
            DrawHexDataBlocks(sb, boxRect, alpha * 0.5f);

            //绘制背景渐变
            DrawInfoBoxBackground(sb, boxRect, alpha);

            //绘制边框
            DrawInfoBoxBorder(sb, boxRect, alpha, techColor);

            //绘制标题栏
            DrawInfoBoxHeader(sb, boxRect, alpha, techColor);

            //绘制数据流粒子
            DrawInfoBoxDataStreams(sb, boxRect, alpha);

            //绘制数据文本
            DrawInfoBoxDataTextEnhanced(sb, boxRect, alpha, techColor);

            //绘制扫描线
            DrawInfoBoxScanLines(sb, boxRect, alpha);

            //绘制数据波动可视化
            DrawDataWaveform(sb, boxRect, alpha, techColor);
        }

        /// <summary>
        /// 绘制信息框背景
        /// </summary>
        private static void DrawInfoBoxBackground(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //深色半透明背景，不要完全遮挡底层效果
            sb.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), new Color(5, 10, 18) * (alpha * 0.75f));
        }

        /// <summary>
        /// 绘制信息框边框
        /// </summary>
        private static void DrawInfoBoxBorder(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Color borderColor = techColor * (alpha * 0.85f);

            //外边框
            sb.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, 3), borderColor);
            sb.Draw(pixel, new Rectangle(rect.X - 1, rect.Bottom - 2, rect.Width + 2, 3), borderColor * 0.7f);
            sb.Draw(pixel, new Rectangle(rect.X - 1, rect.Y - 1, 3, rect.Height + 2), borderColor * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y - 1, 3, rect.Height + 2), borderColor * 0.85f);

            //内发光
            float pulse = (float)Math.Sin(hologramFlicker * 2f) * 0.5f + 0.5f;
            Color glowColor = techColor * (alpha * 0.25f * pulse);
            sb.Draw(pixel, new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, 1), glowColor);
            sb.Draw(pixel, new Rectangle(rect.X + 3, rect.Y + 3, 1, rect.Height - 6), glowColor);

            //角落装饰
            DrawCornerTechEnhanced(sb, new Vector2(rect.X + 10, rect.Y + 10), techColor * (alpha * 0.9f), -MathHelper.PiOver2);
            DrawCornerTechEnhanced(sb, new Vector2(rect.Right - 10, rect.Y + 10), techColor * (alpha * 0.9f), 0f);
            DrawCornerTechEnhanced(sb, new Vector2(rect.X + 10, rect.Bottom - 10), techColor * (alpha * 0.9f), MathHelper.Pi);
            DrawCornerTechEnhanced(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), techColor * (alpha * 0.9f), MathHelper.PiOver2);
        }

        /// <summary>
        /// 绘制增强版角落装饰
        /// </summary>
        private static void DrawCornerTechEnhanced(SpriteBatch sb, Vector2 pos, Color color, float rotation) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //主线
            float size = 12f;
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, rotation,
                new Vector2(0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.7f, rotation + MathHelper.PiOver2,
                new Vector2(0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0f);

            //次级装饰线
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.5f, rotation,
                new Vector2(0.5f), new Vector2(size * 0.6f, size * 0.15f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制信息框标题栏
        /// </summary>
        private static void DrawInfoBoxHeader(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //标题栏背景
            Rectangle headerRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, 32);
            sb.Draw(pixel, headerRect, new Rectangle(0, 0, 1, 1), new Color(15, 28, 45) * (alpha * 0.7f));

            //标题文本
            string title = "◢ QUANTUM SIGNAL TOWER ◣";
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 titleSize = font.MeasureString(title) * 0.5f;
            Vector2 titlePos = new Vector2(
                headerRect.X + (headerRect.Width - titleSize.X) * 0.5f,
                headerRect.Y + (headerRect.Height - titleSize.Y) * 0.5f
            );

            //标题发光
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.2f;
                Utils.DrawBorderString(sb, title, titlePos + offset, techColor * (alpha * 0.5f), 0.5f);
            }
            Utils.DrawBorderString(sb, title, titlePos, Color.White * alpha, 0.5f);

            //标题栏底部分隔线
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom, headerRect.Width, 2),
                techColor * (alpha * 0.6f));
            sb.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom + 3, headerRect.Width, 1),
                techColor * (alpha * 0.3f));
        }

        /// <summary>
        /// 绘制信息框数据流
        /// </summary>
        private static void DrawInfoBoxDataStreams(SpriteBatch sb, Rectangle rect, float alpha) {
            foreach (var stream in dataStreams) {
                if (stream.Position.X >= rect.X && stream.Position.X <= rect.Right &&
                    stream.Position.Y >= rect.Y && stream.Position.Y <= rect.Bottom) {
                    stream.Draw(sb, alpha * 0.6f);
                }
            }
        }

        /// <summary>
        /// 绘制增强版数据文本
        /// </summary>
        private static void DrawInfoBoxDataTextEnhanced(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            if (currentDataLines.Count == 0) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 textPos = new Vector2(rect.X + 14, rect.Y + 48);
            float lineHeight = font.MeasureString("A").Y * 0.45f + 3f;

            for (int i = 0; i < currentDataLines.Count && i < maxDataLines; i++) {
                var dataLine = currentDataLines[i];
                string line = dataLine.Text;

                //动态生成乱码
                if (dataLine.GlitchProgress > 0.01f) {
                    StringBuilder glitched = new StringBuilder();
                    for (int c = 0; c < line.Length; c++) {
                        if (Main.rand.NextFloat() < dataLine.GlitchProgress * 0.3f) {
                            glitched.Append("█▓▒░■□◢◣▪▫"[Main.rand.Next(10)]);
                        }
                        else {
                            glitched.Append(line[c]);
                        }
                    }
                    line = glitched.ToString();
                }

                //计算透明度和颜色
                float flicker = (float)Math.Sin(dataLine.FlickerPhase) * 0.4f + 0.6f;
                float lineAlpha = alpha * flicker;

                //渐变消失效果
                float fadeOut = 1f - (i / (float)maxDataLines) * 0.6f;
                lineAlpha *= fadeOut;

                //动态颜色
                Color lineColor = Color.Lerp(dataLine.BaseColor, Color.White, 0.3f);

                //绘制发光背景
                if (i == currentDataLines.Count - 1) {//最新的一行高亮
                    Rectangle highlightRect = new Rectangle(
                        (int)textPos.X - 6,
                        (int)textPos.Y - 2,
                        rect.Width - 20,
                        (int)lineHeight + 2
                    );
                    sb.Draw(VaultAsset.placeholder2.Value, highlightRect,
                        techColor * (alpha * 0.15f));
                }

                Utils.DrawBorderString(sb, line, textPos, lineColor * lineAlpha, 0.45f);
                textPos.Y += lineHeight;
            }
        }

        /// <summary>
        /// 绘制信息框扫描线
        /// </summary>
        private static void DrawInfoBoxScanLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //主扫描线
            float scanY = rect.Y + 42 + scanLineProgress * (rect.Height - 42);
            for (int layer = 0; layer < 3; layer++) {
                float offset = layer * 1.5f;
                Color scanColor = new Color(80, 220, 255) * (alpha * (0.35f - layer * 0.08f));
                sb.Draw(pixel, new Vector2(rect.X + 4, scanY + offset), new Rectangle(0, 0, 1, 1),
                    scanColor, 0f, Vector2.Zero, new Vector2(rect.Width - 8, 2f + layer), SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制数据波动可视化
        /// </summary>
        private static void DrawDataWaveform(SpriteBatch sb, Rectangle rect, float alpha, Color techColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //在底部绘制波形
            int barCount = 16;
            float barWidth = (rect.Width - 20) / (float)barCount;
            float baseY = rect.Bottom - 30;

            for (int i = 0; i < barCount; i++) {
                float phase = hologramFlicker * 3f + i * 0.4f;
                float height = (float)Math.Sin(phase) * 15f + 18f;
                float barX = rect.X + 10 + i * barWidth;

                Rectangle barRect = new Rectangle(
                    (int)barX,
                    (int)(baseY - height),
                    (int)(barWidth - 2),
                    (int)height
                );

                float barAlpha = (height / 33f) * alpha * 0.5f;
                sb.Draw(pixel, barRect, Color.Lerp(techColor, Color.White, 0.3f) * barAlpha);
            }
        }

        #region 矩阵雨效果
        private class MatrixRain
        {
            public float X;
            public float Y;
            public float Speed;
            public float Length;
            public char[] Characters;
            public float[] CharAlphas;
            public int UpdateTimer;

            public MatrixRain(float x, float startY, float speed) {
                X = x;
                Y = startY;
                Speed = speed;
                Length = Main.rand.Next(8, 16);
                Characters = new char[(int)Length];
                CharAlphas = new float[(int)Length];
                UpdateTimer = 0;

                for (int i = 0; i < Length; i++) {
                    Characters[i] = "0123456789ABCDEFX█▓▒"[Main.rand.Next(19)];
                    CharAlphas[i] = 1f - (i / Length);
                }
            }

            public bool Update(Rectangle bounds) {
                Y += Speed;
                UpdateTimer++;

                if (UpdateTimer >= 4) {
                    UpdateTimer = 0;
                    for (int i = 0; i < Length; i++) {
                        if (Main.rand.NextBool(5)) {
                            Characters[i] = "0123456789ABCDEFX█▓▒"[Main.rand.Next(19)];
                        }
                    }
                }

                return Y - Length * 12 > bounds.Bottom;
            }

            public void Draw(SpriteBatch sb, float baseAlpha) {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                for (int i = 0; i < Length; i++) {
                    float charY = Y - i * 12;
                    float alpha = CharAlphas[i] * baseAlpha;
                    Color color = i == 0 ? new Color(200, 255, 200) : new Color(80, 200, 120);
                    Utils.DrawBorderString(sb, Characters[i].ToString(),
                        new Vector2(X, charY), color * alpha, 0.4f);
                }
            }
        }

        private static void UpdateMatrixRain() {
            //生成新的矩阵雨
            if (Main.rand.NextBool(3) && matrixRains.Count < maxMatrixColumns) {
                Vector2 boxPos = GetInfoBoxScreenPos();
                Rectangle boxRect = new Rectangle(
                    (int)(boxPos.X - InfoBoxWidth * 0.5f),
                    (int)(boxPos.Y - InfoBoxHeight * 0.5f),
                    (int)InfoBoxWidth,
                    (int)InfoBoxHeight
                );

                float x = boxRect.X + Main.rand.NextFloat(10, boxRect.Width - 10);
                float speed = Main.rand.NextFloat(1.5f, 3.5f);
                matrixRains.Add(new MatrixRain(x, boxRect.Y, speed));
            }

            //更新矩阵雨
            Vector2 boxPos2 = GetInfoBoxScreenPos();
            Rectangle bounds = new Rectangle(
                (int)(boxPos2.X - InfoBoxWidth * 0.5f),
                (int)(boxPos2.Y - InfoBoxHeight * 0.5f),
                (int)InfoBoxWidth,
                (int)InfoBoxHeight
            );

            for (int i = matrixRains.Count - 1; i >= 0; i--) {
                if (matrixRains[i].Update(bounds)) {
                    matrixRains.RemoveAt(i);
                }
            }
        }

        private static void DrawMatrixRain(SpriteBatch sb, Rectangle rect, float alpha) {
            foreach (var rain in matrixRains) {
                rain.Draw(sb, alpha);
            }
        }
        #endregion

        #region 十六进制数据块
        private class HexDataBlock
        {
            public Vector2 Position;
            public string Data;
            public float Life;
            public float MaxLife;
            public float Alpha;

            public HexDataBlock(Vector2 pos) {
                Position = pos;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 120f);
                Alpha = Main.rand.NextFloat(0.3f, 0.7f);

                //生成4x2的十六进制块
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 8; i++) {
                    sb.Append(Main.rand.Next(256).ToString("X2"));
                    if (i == 3) sb.Append("\n");
                    else if (i < 7) sb.Append(" ");
                }
                Data = sb.ToString();
            }

            public bool Update() {
                Life++;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float baseAlpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                Utils.DrawBorderString(sb, Data, Position,
                    new Color(100, 180, 200) * (Alpha * fade * baseAlpha), 0.35f);
            }
        }

        private static void UpdateHexBlocks() {
            hexBlockTimer++;
            if (hexBlockTimer >= 15 && hexBlocks.Count < 8) {
                hexBlockTimer = 0;

                Vector2 boxPos = GetInfoBoxScreenPos();
                Rectangle boxRect = new Rectangle(
                    (int)(boxPos.X - InfoBoxWidth * 0.5f),
                    (int)(boxPos.Y - InfoBoxHeight * 0.5f),
                    (int)InfoBoxWidth,
                    (int)InfoBoxHeight
                );

                Vector2 spawnPos = new Vector2(
                    Main.rand.NextFloat(boxRect.X + 15, boxRect.Right - 80),
                    Main.rand.NextFloat(boxRect.Y + 50, boxRect.Bottom - 60)
                );

                hexBlocks.Add(new HexDataBlock(spawnPos));
            }

            for (int i = hexBlocks.Count - 1; i >= 0; i--) {
                if (hexBlocks[i].Update()) {
                    hexBlocks.RemoveAt(i);
                }
            }
        }

        private static void DrawHexDataBlocks(SpriteBatch sb, Rectangle rect, float alpha) {
            foreach (var block in hexBlocks) {
                block.Draw(sb, alpha);
            }
        }
        #endregion

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
                Velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(2f, 4f));
                Life = 0f;
                MaxLife = Main.rand.NextFloat(30f, 70f);
                Size = Main.rand.NextFloat(0.5f, 0.9f);
                Color = color;
                Character = "0123456789ABCDEFX"[Main.rand.Next(17)];
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

                Utils.DrawBorderString(sb, Character.ToString(), Position, Color * alpha, Size * 0.4f);
            }
        }

        private static void UpdateDataStreams() {
            dataStreamTimer++;
            if (dataStreamTimer >= 5 && dataStreams.Count < 40) {//增加数量和生成频率
                dataStreamTimer = 0;

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
            matrixRains.Clear();
            hexBlocks.Clear();
            dataStreamTimer = 0;
            dataUpdateTimer = 0f;
            currentDataLines.Clear();
            hexBlockTimer = 0;
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
            for (int i = 0; i < 10; i++) {
                currentDataLines.Add(new DataLine(GenerateRandomDataLine()));
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
            matrixRains.Clear();
            hexBlocks.Clear();
            currentDataLines.Clear();
        }
    }
}
