using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>TBUG 终端窗口：着色器底 / 方括号边框 / 提示符标题 / 故障切片 / 关闭钮</summary>
    internal class TBUGPanelRenderer
    {
        #region 动画状态

        private float glitchTimer;
        private float glitchIntensity;
        private float nextGlitchTime = 3f;

        #endregion

        public void TriggerGlitch(float intensity) {
            glitchIntensity = MathHelper.Clamp(intensity, 0f, 1f);
        }

        public void Update() {
            if (glitchIntensity > 0f) {
                glitchIntensity -= 0.02f;
            }
            glitchTimer += 0.016f;
            if (glitchTimer > nextGlitchTime) {
                glitchTimer = 0f;
                nextGlitchTime = 2.5f + Main.rand.NextFloat(4.5f);
                glitchIntensity = MathHelper.Clamp(0.12f + Main.rand.NextFloat(0.22f), 0f, 1f);
            }
        }

        /// <summary>TBUGTerminalPanel.fx 面板底层，着色器缺失降级平铺</summary>
        public static void DrawShaderBackground(SpriteBatch sb, float alpha, Rectangle panelRect) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            if (EffectLoader.TBUGTerminalPanel?.Value == null) {
                sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), TBUGTheme.BgPanel * (alpha * 0.96f));
                return;
            }

            Effect effect = EffectLoader.TBUGTerminalPanel.Value;
            float time = (float)Main.GameUpdateCount / 60f;

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(panelRect.Width, panelRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue(TBUGTheme.ShaderEdgePad);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>方括号四角 + 细边线；全直角，不带斜切——终端不是战术 HUD</summary>
        public static void DrawFrameDecor(SpriteBatch sb, float alpha, Rectangle panelRect, float globalTimer) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;
            Rectangle one = new(0, 0, 1, 1);

            //顶边脉冲
            float pulse = MathF.Sin(globalTimer * 2f) * 0.12f + 0.88f;
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), one,
                TBUGTheme.Accent * (alpha * 0.7f * pulse));
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), one,
                TBUGTheme.Border * (alpha * 0.7f));
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), one,
                TBUGTheme.Border * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), one,
                TBUGTheme.Border * (alpha * 0.6f));

            //方括号角标：外亮内暗两层
            Color c = TBUGTheme.Accent * (alpha * 0.9f);
            Color cDim = TBUGTheme.AccentDim * (alpha * 0.6f);
            const int cL = 22, cL2 = 10, inset = 5;
            //左上
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, cL, 2), one, c);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 2, cL), one, c);
            sb.Draw(px, new Rectangle(panelRect.X + inset, panelRect.Y + inset, cL2, 1), one, cDim);
            sb.Draw(px, new Rectangle(panelRect.X + inset, panelRect.Y + inset, 1, cL2), one, cDim);
            //右上
            sb.Draw(px, new Rectangle(panelRect.Right - cL, panelRect.Y, cL, 2), one, c);
            sb.Draw(px, new Rectangle(panelRect.Right - 2, panelRect.Y, 2, cL), one, c);
            sb.Draw(px, new Rectangle(panelRect.Right - inset - cL2, panelRect.Y + inset, cL2, 1), one, cDim);
            sb.Draw(px, new Rectangle(panelRect.Right - inset - 1, panelRect.Y + inset, 1, cL2), one, cDim);
            //左下
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 2, cL, 2), one, c * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - cL, 2, cL), one, c * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.X + inset, panelRect.Bottom - inset - 1, cL2, 1), one, cDim * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.X + inset, panelRect.Bottom - inset - cL2, 1, cL2), one, cDim * 0.7f);
            //右下
            sb.Draw(px, new Rectangle(panelRect.Right - cL, panelRect.Bottom - 2, cL, 2), one, c * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - 2, panelRect.Bottom - cL, 2, cL), one, c * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - inset - cL2, panelRect.Bottom - inset - 1, cL2, 1), one, cDim * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - inset - 1, panelRect.Bottom - inset - cL2, 1, cL2), one, cDim * 0.7f);

            //顶边巡行亮点
            float pos = globalTimer * 0.30f % 1f;
            int pulseX = panelRect.X + (int)(pos * panelRect.Width);
            sb.Draw(px, new Rectangle(pulseX - 16, panelRect.Y, 32, 2), one, TBUGTheme.EdgeGlow * (alpha * 0.45f));
        }

        /// <summary>提示符标题行：tbug@rift:~$ TITLE ▊（闪烁光标）；返回标题行底 Y</summary>
        public int DrawPromptTitle(SpriteBatch sb, float alpha, Rectangle panelRect, float globalTimer, string title) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return panelRect.Y;
            Rectangle one = new(0, 0, 1, 1);

            const int headerH = 26;
            sb.Draw(px, new Rectangle(panelRect.X + 2, panelRect.Y + 2, panelRect.Width - 4, headerH), one,
                TBUGTheme.SectionBg * (alpha * 0.9f));

            float scale = 0.52f * TBUGTheme.FontScale;
            float textY = panelRect.Y + 7f;
            float x = panelRect.X + 12f;

            //提示符分三段着色：用户名亮绿、路径暗绿、$ 品红
            string user = "tbug@rift";
            string path = ":~";
            string mark = "$ ";
            Utils.DrawBorderString(sb, user, new Vector2(x, textY), TBUGTheme.Accent * (alpha * 0.9f), scale);
            x += FontAssets.MouseText.Value.MeasureString(user).X * scale;
            Utils.DrawBorderString(sb, path, new Vector2(x, textY), TBUGTheme.TextDim * alpha, scale);
            x += FontAssets.MouseText.Value.MeasureString(path).X * scale;
            Utils.DrawBorderString(sb, mark, new Vector2(x, textY), TBUGTheme.AccentErr * (alpha * 0.9f), scale);
            x += FontAssets.MouseText.Value.MeasureString(mark).X * scale;
            Utils.DrawBorderString(sb, title, new Vector2(x, textY), TBUGTheme.TextBright * alpha, scale);
            x += FontAssets.MouseText.Value.MeasureString(title).X * scale;

            //闪烁块光标
            if ((int)(globalTimer * 1.5f) % 2 == 0) {
                sb.Draw(px, new Rectangle((int)x + 4, (int)textY + 3, 7, 14), one, TBUGTheme.Accent * (alpha * 0.85f));
            }

            //标题栏底部分割线
            int divY = panelRect.Y + headerH + 3;
            sb.Draw(px, new Rectangle(panelRect.X + 8, divY, panelRect.Width - 16, 1), one,
                TBUGTheme.Accent * (alpha * 0.4f));
            return divY;
        }

        /// <summary>底部状态栏：状态灯 + 文本 + 右侧滚动地址</summary>
        public static void DrawStatusFooter(SpriteBatch sb, float alpha, Rectangle panelRect, float globalTimer, string statusText) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;
            Rectangle one = new(0, 0, 1, 1);

            const int footerH = 22;
            int footerTop = panelRect.Bottom - footerH;
            sb.Draw(px, new Rectangle(panelRect.X + 2, footerTop, panelRect.Width - 4, footerH - 2), one,
                TBUGTheme.SectionBg * (alpha * 0.75f));
            sb.Draw(px, new Rectangle(panelRect.X + 8, footerTop - 2, panelRect.Width - 16, 1), one,
                TBUGTheme.Accent * (alpha * 0.25f));

            float y = footerTop + 4f;
            float blink = MathF.Sin(globalTimer * 3f) > 0f ? 1f : 0.4f;
            sb.Draw(px, new Vector2(panelRect.X + 10, y + 2f), one,
                TBUGTheme.Accent * (alpha * blink), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
            Utils.DrawBorderString(sb, statusText, new Vector2(panelRect.X + 22, y - 2f),
                TBUGTheme.TextDim * alpha, 0.44f * TBUGTheme.FontScale);

            string addr = $"ERR::0x{(int)(globalTimer * 90f) % 0xFFFF:X4}";
            Utils.DrawBorderString(sb, addr, new Vector2(panelRect.Right - 120, y - 2f),
                TBUGTheme.AccentErr * (alpha * 0.35f), 0.40f * TBUGTheme.FontScale);
        }

        /// <summary>随机故障切片：横条错位，绿为主偶发品红</summary>
        public void DrawGlitchEffect(SpriteBatch sb, float alpha, Rectangle panelRect) {
            if (glitchIntensity <= 0.01f) return;
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float intensity = glitchIntensity * alpha;
            int lines = (int)(3 + intensity * 8);
            for (int i = 0; i < lines; i++) {
                int y = panelRect.Y + Main.rand.Next(panelRect.Height);
                int h = 1 + Main.rand.Next(3);
                int offsetX = Main.rand.Next(-9, 10);
                Color gc = Main.rand.NextBool(4) ? TBUGTheme.AccentErr : TBUGTheme.Accent;
                gc *= intensity * 0.28f;
                sb.Draw(px, new Rectangle(panelRect.X + offsetX, y, panelRect.Width, h),
                    new Rectangle(0, 0, 1, 1), gc);
            }
        }

        /// <summary>关闭钮矩形，交互复用</summary>
        public static Rectangle GetCloseButtonRect(Rectangle panelRect) {
            return new Rectangle(panelRect.Right - 34, panelRect.Y + 4, 24, 20);
        }

        /// <summary>标题栏关闭钮 X 图标</summary>
        public void DrawCloseButton(SpriteBatch sb, float alpha, Rectangle panelRect, bool isHovered) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;
            Rectangle one = new(0, 0, 1, 1);

            Rectangle btnRect = GetCloseButtonRect(panelRect);
            float hov = isHovered ? 1f : 0f;

            //悬停时按"报错"处理：品红化
            Color bgColor = Color.Lerp(TBUGTheme.BgDark, TBUGTheme.AccentErr, hov * 0.30f) * (alpha * 0.92f);
            sb.Draw(px, btnRect, one, bgColor);

            Color borderColor = Color.Lerp(TBUGTheme.Border, TBUGTheme.AccentErr, hov * 0.9f) * alpha;
            sb.Draw(px, new Rectangle(btnRect.X, btnRect.Y, btnRect.Width, 1), one, borderColor);
            sb.Draw(px, new Rectangle(btnRect.X, btnRect.Bottom - 1, btnRect.Width, 1), one, borderColor * 0.7f);
            sb.Draw(px, new Rectangle(btnRect.X, btnRect.Y, 1, btnRect.Height), one, borderColor * 0.8f);
            sb.Draw(px, new Rectangle(btnRect.Right - 1, btnRect.Y, 1, btnRect.Height), one, borderColor * 0.8f);

            Color xColor = Color.Lerp(TBUGTheme.TextDim, TBUGTheme.AccentErr, hov) * alpha;
            Vector2 center = btnRect.Center.ToVector2();
            const float s = 4.5f;
            TBUGTheme.DrawLine(sb, px, center + new Vector2(-s, -s), center + new Vector2(s, s), 1.6f, xColor);
            TBUGTheme.DrawLine(sb, px, center + new Vector2(s, -s), center + new Vector2(-s, s), 1.6f, xColor);
        }
    }
}
