using CalamityOverhaul.Content.ADV.DialogueBoxs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 芯片读取动画渲染器
    /// 在阿波利娅读取授权芯片时，在对话框上方左侧显示一个星流风格的全息数据面板
    /// </summary>
    internal class ChipReadRender : UIHandle
    {
        public override bool Active => active || fadeProgress > 0.01f;
        public override float RenderPriority => 0.88f;

        #region 状态

        private static bool active;
        private static float fadeProgress;
        private static float hologramFlicker;
        private static float scanLineProgress;
        private static float readProgress;
        private static float shimmerTimer;

        private const float PanelWidth = 260f;
        private const float PanelHeight = 150f;
        private const float FadeSpeed = 0.04f;
        private const float ReadDuration = 120f;

        //星流风格配色
        private static readonly Color BorderGold = new(220, 180, 80);
        private static readonly Color TextGold = new(255, 245, 220);
        private static readonly Color HeaderGold = new(255, 210, 100);
        private static readonly Color DataWarm = new(230, 200, 140);
        private static readonly Color ConfirmGreen = new(140, 255, 180);
        private static readonly Color BgDeep = new(10, 8, 22);

        private static readonly List<DataLine> dataLines = [];
        private static float dataUpdateTimer;

        private static readonly string[] chipDataTemplates = [
            "AUTH: DRAEDON-{0}",
            "RANK: {0}",
            "CLEARANCE: LV-{0}",
            "IDENT: {0}",
            "SIGNAL: {0}%",
            "VERIFY: {0}",
            "KEY: {0}",
            "PROTO: SFC-{0}",
            "ORIGIN: {0}",
            "STATUS: {0}",
        ];

        private static readonly string[] verifyStatuses = [
            "VALIDATING...",
            "DECRYPTING...",
            "MATCHING...",
            "CONFIRMED",
            "AUTHORIZED",
        ];

        #endregion

        #region 生命周期

        internal static void Activate() {
            active = true;
            fadeProgress = 0f;
            hologramFlicker = 0f;
            scanLineProgress = 0f;
            readProgress = 0f;
            shimmerTimer = 0f;
            dataUpdateTimer = 0f;
            dataLines.Clear();

            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with {
                Volume = 0.35f,
                Pitch = 0.4f,
                MaxInstances = 1
            });
        }

        internal static void Deactivate() {
            active = false;
        }

        private static void ForceCleanup() {
            active = false;
            fadeProgress = 0f;
            dataLines.Clear();
        }

        #endregion

        #region 定位

        /// <summary>
        /// 面板位置：对话框上方偏左，不遮挡右侧立绘
        /// </summary>
        private static Vector2 GetPanelCenter() {
            var box = DialogueUIRegistry.Current;
            if (box != null) {
                var rect = box.GetPanelRect();
                //对话框左上方
                return new Vector2(rect.X + PanelWidth * 0.5f + 20f, rect.Y - PanelHeight * 0.5f - 16f);
            }
            //降级位置
            return new Vector2(Main.screenWidth * 0.25f, Main.screenHeight * 0.55f);
        }

        #endregion

        #region 更新

        public override void LogicUpdate() {
            if (!active && fadeProgress <= 0.01f) return;

            hologramFlicker += 0.08f;
            scanLineProgress += 0.03f;
            shimmerTimer += 0.035f;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (scanLineProgress > 1f) scanLineProgress -= 1f;
            if (shimmerTimer > MathHelper.TwoPi) shimmerTimer -= MathHelper.TwoPi;

            if (active) {
                fadeProgress = MathF.Min(fadeProgress + FadeSpeed, 1f);
                readProgress = MathF.Min(readProgress + 1f, ReadDuration);
            }
            else {
                fadeProgress = MathF.Max(fadeProgress - FadeSpeed * 1.5f, 0f);
                if (fadeProgress <= 0.01f) {
                    ForceCleanup();
                    return;
                }
            }

            UpdateData();
        }

        private static void UpdateData() {
            dataUpdateTimer += 0.12f;

            if (dataUpdateTimer >= 1f) {
                dataUpdateTimer = 0f;

                if (dataLines.Count >= 6) {
                    dataLines.RemoveAt(0);
                }

                dataLines.Add(new DataLine(GenerateDataLine()));
            }

            for (int i = dataLines.Count - 1; i >= 0; i--) {
                var line = dataLines[i];
                line.Timer--;
                if (line.Timer <= 0) {
                    line.Timer = Main.rand.Next(15, 40);
                    line.GlitchAmount = Main.rand.NextFloat(0.2f, 0.7f);
                }
                else {
                    line.GlitchAmount *= 0.9f;
                }
            }
        }

        private static string GenerateDataLine() {
            float t = readProgress / ReadDuration;

            if (t > 0.85f) {
                return Main.rand.Next(3) switch {
                    0 => ">> AUTHORIZATION CONFIRMED",
                    1 => ">> IDENTITY VERIFIED",
                    _ => ">> STAR FLEET: SOLO UNIT"
                };
            }

            string template = chipDataTemplates[Main.rand.Next(chipDataTemplates.Length)];
            string value = template switch {
                _ when template.Contains("AUTH") => $"DRAEDON-{Main.rand.Next(1000, 9999):X4}",
                _ when template.Contains("RANK") => $"OPERATIVE-{Main.rand.Next(1, 9)}",
                _ when template.Contains("CLEARANCE") => $"LV-{Main.rand.Next(5, 9)}",
                _ when template.Contains("SIGNAL") => $"{Main.rand.Next(70, 100)}%",
                _ when template.Contains("VERIFY") => verifyStatuses[Main.rand.Next(verifyStatuses.Length)],
                _ when template.Contains("KEY") => $"{Main.rand.Next(0x1000, 0xFFFF):X4}-{Main.rand.Next(0x1000, 0xFFFF):X4}",
                _ when template.Contains("ORIGIN") => "TERRA",
                _ when template.Contains("STATUS") => t > 0.5f ? "PROCESSING" : "READING",
                _ => $"{Main.rand.Next(100, 999)}"
            };

            return string.Format(template, value);
        }

        #endregion

        #region 绘制

        public override void Draw(SpriteBatch sb) {
            if (fadeProgress <= 0.01f) return;

            float alpha = fadeProgress;
            float flicker = MathF.Sin(hologramFlicker * 1.5f) * 0.04f + 0.96f;
            alpha *= flicker;

            Vector2 panelPos = GetPanelCenter();

            DrawPanel(sb, panelPos, alpha);
            DrawScanLine(sb, panelPos, alpha);
            DrawDataLines(sb, panelPos, alpha);
            DrawProgressBar(sb, panelPos, alpha);
        }

        private static void DrawPanel(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float ease = VaultUtils.EaseOutCubic(fadeProgress);
            float w = PanelWidth * ease;
            float h = PanelHeight * ease;

            //阴影
            Rectangle shadowRect = new((int)(pos.X - w / 2 + 4), (int)(pos.Y - h / 2 + 5), (int)w, (int)h);
            sb.Draw(px, shadowRect, new Rectangle(0, 0, 1, 1), new Color(5, 0, 15) * (0.5f * alpha));

            //面板背景——深空色
            Rectangle bgRect = new((int)(pos.X - w / 2), (int)(pos.Y - h / 2), (int)w, (int)h);
            sb.Draw(px, bgRect, new Rectangle(0, 0, 1, 1), BgDeep * (0.88f * alpha));

            //金色边框
            Color border = BorderGold * (0.65f * alpha);
            int b = 2;
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, b), new Rectangle(0, 0, 1, 1), border);
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Bottom - b, bgRect.Width, b), new Rectangle(0, 0, 1, 1), border * 0.7f);
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Y, b, bgRect.Height), new Rectangle(0, 0, 1, 1), border * 0.85f);
            sb.Draw(px, new Rectangle(bgRect.Right - b, bgRect.Y, b, bgRect.Height), new Rectangle(0, 0, 1, 1), border * 0.85f);

            //角落装饰——星流风格的金色角标
            int cornerLen = 8;
            Color cornerColor = HeaderGold * (0.8f * alpha);
            //左上
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Y, cornerLen, b + 1), new Rectangle(0, 0, 1, 1), cornerColor);
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Y, b + 1, cornerLen), new Rectangle(0, 0, 1, 1), cornerColor);
            //右上
            sb.Draw(px, new Rectangle(bgRect.Right - cornerLen, bgRect.Y, cornerLen, b + 1), new Rectangle(0, 0, 1, 1), cornerColor);
            sb.Draw(px, new Rectangle(bgRect.Right - b - 1, bgRect.Y, b + 1, cornerLen), new Rectangle(0, 0, 1, 1), cornerColor);
            //左下
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Bottom - b - 1, cornerLen, b + 1), new Rectangle(0, 0, 1, 1), cornerColor * 0.7f);
            sb.Draw(px, new Rectangle(bgRect.X, bgRect.Bottom - cornerLen, b + 1, cornerLen), new Rectangle(0, 0, 1, 1), cornerColor * 0.7f);
            //右下
            sb.Draw(px, new Rectangle(bgRect.Right - cornerLen, bgRect.Bottom - b - 1, cornerLen, b + 1), new Rectangle(0, 0, 1, 1), cornerColor * 0.7f);
            sb.Draw(px, new Rectangle(bgRect.Right - b - 1, bgRect.Bottom - cornerLen, b + 1, cornerLen), new Rectangle(0, 0, 1, 1), cornerColor * 0.7f);

            //标题
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string header = "✦ CHIP READOUT ✦";
            Vector2 headerSize = font.MeasureString(header) * 0.35f;
            Vector2 headerPos = new(pos.X - headerSize.X * 0.5f, pos.Y - h / 2 + 6);
            //标题辉光
            sb.DrawString(font, header, headerPos + new Vector2(0, 1), HeaderGold * (alpha * 0.15f), 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
            sb.DrawString(font, header, headerPos, HeaderGold * alpha, 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);

            //标题下方分割线
            float divY = pos.Y - h / 2 + 22;
            float divLeft = pos.X - w / 2 + 6;
            float divRight = pos.X + w / 2 - 6;
            for (int x = (int)divLeft; x < (int)divRight; x++) {
                float t = (x - divLeft) / (divRight - divLeft);
                float lineAlpha = MathF.Sin(t * MathHelper.Pi) * 0.7f;
                sb.Draw(px, new Rectangle(x, (int)divY, 1, 1), new Rectangle(0, 0, 1, 1), BorderGold * (lineAlpha * alpha));
            }
        }

        private static void DrawScanLine(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float ease = VaultUtils.EaseOutCubic(fadeProgress);
            float h = PanelHeight * ease;
            float w = PanelWidth * ease;

            float scanY = pos.Y - h / 2 + scanLineProgress * h;
            Color scanColor = HeaderGold * (0.1f * alpha);
            sb.Draw(px, new Rectangle((int)(pos.X - w / 2), (int)scanY, (int)w, 1), new Rectangle(0, 0, 1, 1), scanColor);
        }

        private static void DrawDataLines(SpriteBatch sb, Vector2 pos, float alpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float ease = VaultUtils.EaseOutCubic(fadeProgress);
            float w = PanelWidth * ease;
            float h = PanelHeight * ease;
            float startY = pos.Y - h / 2 + 26;
            float startX = pos.X - w / 2 + 8;

            for (int i = 0; i < dataLines.Count; i++) {
                var line = dataLines[i];
                float lineY = startY + i * 15;
                if (lineY > pos.Y + h / 2 - 22) break;

                string text = line.Text;
                if (line.GlitchAmount > 0.1f) {
                    text = ApplyGlitch(text, line.GlitchAmount);
                }

                float lineAlpha = alpha * (1f - i * 0.04f);
                Color c = line.Text.Contains(">>")
                    ? ConfirmGreen * lineAlpha
                    : DataWarm * lineAlpha;

                sb.DrawString(font, text, new Vector2(startX, lineY), c, 0f, Vector2.Zero, 0.3f, SpriteEffects.None, 0f);
            }
        }

        private static void DrawProgressBar(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float ease = VaultUtils.EaseOutCubic(fadeProgress);
            float w = PanelWidth * ease;
            float h = PanelHeight * ease;

            float barW = w - 16;
            float barH = 5;
            float barX = pos.X - w / 2 + 8;
            float barY = pos.Y + h / 2 - 14;

            //背景
            sb.Draw(px, new Rectangle((int)barX, (int)barY, (int)barW, (int)barH),
                new Rectangle(0, 0, 1, 1), new Color(15, 12, 30) * (0.8f * alpha));

            //进度——金色，完成时变绿
            float t = readProgress / ReadDuration;
            Color barColor = t > 0.85f ? ConfirmGreen : HeaderGold;
            sb.Draw(px, new Rectangle((int)barX, (int)barY, (int)(barW * t), (int)barH),
                new Rectangle(0, 0, 1, 1), barColor * (0.85f * alpha));

            //进度条辉光
            if (t > 0.01f && t < 1f) {
                float glowX = barX + barW * t;
                sb.Draw(px, new Rectangle((int)(glowX - 2), (int)(barY - 1), 4, (int)barH + 2),
                    new Rectangle(0, 0, 1, 1), barColor * (0.3f * alpha));
            }

            //百分比文字
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string pct = $"{(int)(t * 100)}%";
            sb.DrawString(font, pct, new Vector2(barX + barW + 4, barY - 2),
                TextGold * (alpha * 0.8f), 0f, Vector2.Zero, 0.28f, SpriteEffects.None, 0f);
        }

        private static string ApplyGlitch(string text, float amount) {
            char[] chars = text.ToCharArray();
            int glitchCount = (int)(chars.Length * amount * 0.3f);
            for (int i = 0; i < glitchCount; i++) {
                int idx = Main.rand.Next(chars.Length);
                chars[idx] = (char)Main.rand.Next(33, 126);
            }
            return new string(chars);
        }

        #endregion

        private class DataLine(string text)
        {
            public readonly string Text = text;
            public float GlitchAmount = Main.rand.NextFloat(0.3f, 0.8f);
            public int Timer = Main.rand.Next(10, 30);
        }
    }
}
