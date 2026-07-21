using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>面板，着色器底/装饰/标题/故障/关闭钮</summary>
    internal class CyberPanelRenderer
    {
        #region 动画状态

        private float scanLinePhase;
        private float glitchTimer;
        private float glitchIntensity;
        private float nextGlitchTime;

        #endregion

        #region 公共方法

        public void TriggerGlitch(float intensity) {
            glitchIntensity = MathHelper.Clamp(intensity, 0, 1);
        }

        public void Update() {
            scanLinePhase += 0.025f;
            if (scanLinePhase > MathHelper.TwoPi) scanLinePhase -= MathHelper.TwoPi;

            if (glitchIntensity > 0) glitchIntensity -= 0.02f;
            glitchTimer += 0.016f;
            if (glitchTimer > nextGlitchTime) {
                glitchTimer = 0;
                nextGlitchTime = 2f + Main.rand.NextFloat(4f);
                glitchIntensity = MathHelper.Clamp(0.15f + Main.rand.NextFloat(0.2f), 0, 1);
            }
        }

        /// <summary>
        /// CyberwarePanel.fx 面板底层
        /// <br/>mode 0=主面板 1=侧栏；bodyRadius&lt;=1 无光场
        /// </summary>
        /// <param name="bodyLocalCenter">人体中心面板局部坐标</param>
        /// <param name="bodyRadius">光场半径，&lt;=1 无光场</param>
        /// <param name="mode">0 主面板 1 侧栏</param>
        public static void DrawShaderBackground(SpriteBatch sb, float alpha, Rectangle panelRect,
            Vector2 bodyLocalCenter, float bodyRadius, int mode) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            //着色器缺失降级 BgPanel
            if (EffectLoader.CyberwarePanel?.Value == null) {
                sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.BgPanel * (alpha * 0.95f));
                return;
            }

            Effect effect = EffectLoader.CyberwarePanel.Value;
            float time = (float)Main.GameUpdateCount / 60f;

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(alpha * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(panelRect.Width, panelRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue(CyberwareTheme.ShaderEdgePad);
            effect.Parameters["uBodyCenter"]?.SetValue(bodyLocalCenter);
            effect.Parameters["uBodyRadius"]?.SetValue(bodyRadius);
            effect.Parameters["uMode"]?.SetValue(mode);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>四角括号+顶脉冲+边线</summary>
        public static void DrawFrameDecor(SpriteBatch sb, float alpha, Rectangle panelRect, float globalTimer) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            //顶部边框带脉冲
            float borderPulse = MathF.Sin(globalTimer * 2f) * 0.15f + 0.85f;
            Color topBorder = CyberwareTheme.Accent * (alpha * 0.8f * borderPulse);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2),
                new Rectangle(0, 0, 1, 1), topBorder);
            //底边
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.6f));
            //左边
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.5f));
            //右边
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.5f));

            //四角双层括号+斜切
            Color cornerColor = CyberwareTheme.Accent * (alpha * 0.9f);
            Color cornerDim = cornerColor * 0.5f;
            Color cornerInner = CyberwareTheme.Accent * (alpha * 0.25f);
            int cL = 28, cL2 = 14, cInset = 6;
            //左上 外+内层
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, cL, 2), new Rectangle(0, 0, 1, 1), cornerColor);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 2, cL), new Rectangle(0, 0, 1, 1), cornerColor);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelRect.X + cL, panelRect.Y + 1),
                new Vector2(panelRect.X + cL + 5, panelRect.Y + 6), 1f, cornerColor * 0.4f);
            sb.Draw(px, new Rectangle(panelRect.X + cInset, panelRect.Y + cInset, cL2, 1), new Rectangle(0, 0, 1, 1), cornerInner);
            sb.Draw(px, new Rectangle(panelRect.X + cInset, panelRect.Y + cInset, 1, cL2), new Rectangle(0, 0, 1, 1), cornerInner);
            //右上
            sb.Draw(px, new Rectangle(panelRect.Right - cL, panelRect.Y, cL, 2), new Rectangle(0, 0, 1, 1), cornerColor);
            sb.Draw(px, new Rectangle(panelRect.Right - 2, panelRect.Y, 2, cL), new Rectangle(0, 0, 1, 1), cornerColor);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelRect.Right - cL, panelRect.Y + 1),
                new Vector2(panelRect.Right - cL - 5, panelRect.Y + 6), 1f, cornerColor * 0.4f);
            sb.Draw(px, new Rectangle(panelRect.Right - cInset - cL2, panelRect.Y + cInset, cL2, 1), new Rectangle(0, 0, 1, 1), cornerInner);
            sb.Draw(px, new Rectangle(panelRect.Right - cInset - 1, panelRect.Y + cInset, 1, cL2), new Rectangle(0, 0, 1, 1), cornerInner);
            //左下
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 2, cL, 2), new Rectangle(0, 0, 1, 1), cornerDim);
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - cL, 2, cL), new Rectangle(0, 0, 1, 1), cornerDim);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelRect.X + cL, panelRect.Bottom - 1),
                new Vector2(panelRect.X + cL + 5, panelRect.Bottom - 6), 1f, cornerDim * 0.4f);
            sb.Draw(px, new Rectangle(panelRect.X + cInset, panelRect.Bottom - cInset - 1, cL2, 1), new Rectangle(0, 0, 1, 1), cornerInner * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.X + cInset, panelRect.Bottom - cInset - cL2, 1, cL2), new Rectangle(0, 0, 1, 1), cornerInner * 0.7f);
            //右下
            sb.Draw(px, new Rectangle(panelRect.Right - cL, panelRect.Bottom - 2, cL, 2), new Rectangle(0, 0, 1, 1), cornerDim);
            sb.Draw(px, new Rectangle(panelRect.Right - 2, panelRect.Bottom - cL, 2, cL), new Rectangle(0, 0, 1, 1), cornerDim);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelRect.Right - cL, panelRect.Bottom - 1),
                new Vector2(panelRect.Right - cL - 5, panelRect.Bottom - 6), 1f, cornerDim * 0.4f);
            sb.Draw(px, new Rectangle(panelRect.Right - cInset - cL2, panelRect.Bottom - cInset - 1, cL2, 1), new Rectangle(0, 0, 1, 1), cornerInner * 0.7f);
            sb.Draw(px, new Rectangle(panelRect.Right - cInset - 1, panelRect.Bottom - cInset - cL2, 1, cL2), new Rectangle(0, 0, 1, 1), cornerInner * 0.7f);

            //顶边脉冲亮点
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float pulsePos = globalTimer * 0.35f % 1f;
            int pulseX = panelRect.X + (int)(pulsePos * panelRect.Width);
            sb.Draw(px, new Rectangle(pulseX - 20, panelRect.Y, 40, 2),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.EdgeGlow * (alpha * 0.5f));
            if (glow != null) {
                Color pulseGlow = CyberwareTheme.EdgeGlow * (alpha * 0.25f);
                pulseGlow.A = 0;
                sb.Draw(glow, new Vector2(pulseX, panelRect.Y), null, pulseGlow, 0,
                    glow.Size() / 2, new Vector2(0.5f, 0.08f), SpriteEffects.None, 0);
            }
        }

        /// <summary>标题栏/版本/底状态栏</summary>
        public void DrawTitleAndDecor(SpriteBatch sb, float alpha, Rectangle panelRect, Vector2 panelCenter,
            float globalTimer, string title, string statusText) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            //标题栏深色底
            int headerH = 26;
            sb.Draw(px, new Rectangle(panelRect.X + 2, panelRect.Y + 2, panelRect.Width - 4, headerH),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.SectionBg * (alpha * 0.9f));

            //标题栏底部分割线
            int divY = panelRect.Y + headerH + 3;
            Color divBright = CyberwareTheme.Accent * (alpha * 0.45f);
            sb.Draw(px, new Rectangle(panelRect.X + 10, divY, panelRect.Width - 20, 1),
                new Rectangle(0, 0, 1, 1), divBright);

            //分割线中央菱形缺口装饰
            int notchW = 8;
            sb.Draw(px, new Rectangle((int)panelCenter.X - notchW, divY - 2, notchW * 2, 5),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.BgPanel * alpha);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelCenter.X - notchW, divY),
                new Vector2(panelCenter.X, divY - 2), 1f, divBright);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelCenter.X, divY - 2),
                new Vector2(panelCenter.X + notchW, divY), 1f, divBright);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelCenter.X - notchW, divY),
                new Vector2(panelCenter.X, divY + 2), 1f, divBright * 0.5f);
            CyberwareTheme.DrawLine(sb, px, new Vector2(panelCenter.X, divY + 2),
                new Vector2(panelCenter.X + notchW, divY), 1f, divBright * 0.5f);

            //标题文字
            float titleScale = 0.72f * CyberwareTheme.FontScale;
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * titleScale;
            Vector2 titlePos = new(panelCenter.X - titleSize.X / 2f, panelRect.Y + 7);
            Color titleColor = CyberwareTheme.Accent * (alpha * 0.95f);
            Utils.DrawBorderString(sb, title, titlePos, titleColor, titleScale);

            //标题两侧对称装饰线+尖括号
            float sideY = titlePos.Y + titleSize.Y * 0.45f;
            Color sideColor = CyberwareTheme.Accent * (alpha * 0.35f);
            float gapFromTitle = 10f;
            float sideLineLen = 35f;
            //左侧
            float lsx = titlePos.X - gapFromTitle - sideLineLen;
            sb.Draw(px, new Rectangle((int)lsx, (int)sideY, (int)sideLineLen, 1),
                new Rectangle(0, 0, 1, 1), sideColor);
            CyberwareTheme.DrawLine(sb, px, new Vector2(lsx - 6, sideY - 4),
                new Vector2(lsx, sideY), 1f, sideColor * 0.8f);
            CyberwareTheme.DrawLine(sb, px, new Vector2(lsx - 6, sideY + 4),
                new Vector2(lsx, sideY), 1f, sideColor * 0.8f);
            //右侧
            float rsx = titlePos.X + titleSize.X + gapFromTitle;
            sb.Draw(px, new Rectangle((int)rsx, (int)sideY, (int)sideLineLen, 1),
                new Rectangle(0, 0, 1, 1), sideColor);
            CyberwareTheme.DrawLine(sb, px, new Vector2(rsx + sideLineLen + 6, sideY - 4),
                new Vector2(rsx + sideLineLen, sideY), 1f, sideColor * 0.8f);
            CyberwareTheme.DrawLine(sb, px, new Vector2(rsx + sideLineLen + 6, sideY + 4),
                new Vector2(rsx + sideLineLen, sideY), 1f, sideColor * 0.8f);

            //版本号
            Color verColor = CyberwareTheme.TextDim * (alpha * 0.5f);
            Utils.DrawBorderString(sb, "v2.077",
                new Vector2(panelRect.Right - 100, panelRect.Y + 10), verColor, 0.42f * CyberwareTheme.FontScale);

            //底部状态栏独立背景区
            int footerH = 22;
            int footerTop = panelRect.Bottom - footerH;
            sb.Draw(px, new Rectangle(panelRect.X + 2, footerTop, panelRect.Width - 4, footerH - 2),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.SectionBg * (alpha * 0.75f));

            //底部双线分割
            sb.Draw(px, new Rectangle(panelRect.X + 10, footerTop - 2, panelRect.Width - 20, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * (alpha * 0.25f));
            sb.Draw(px, new Rectangle(panelRect.X + 10, footerTop, panelRect.Width - 20, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * (alpha * 0.15f));

            //运行状态指示灯和文字
            float bottomTextY = footerTop + 4;
            float statusPulse = MathF.Sin(globalTimer * 3f) > 0 ? 1f : 0.4f;
            Color statusDot = new Color(50, 255, 80) * (alpha * statusPulse);
            sb.Draw(px, new Vector2(panelRect.X + 10, bottomTextY + 2), new Rectangle(0, 0, 1, 1),
                statusDot, 0, Vector2.Zero, 4f, SpriteEffects.None, 0);
            Utils.DrawBorderString(sb, statusText, new Vector2(panelRect.X + 22, bottomTextY - 2),
                CyberwareTheme.TextDim * alpha, 0.44f * CyberwareTheme.FontScale);

            //右下角滚动数据标签
            string dataTag = $"NET::0x{(int)(globalTimer * 100) % 0xFFFF:X4}";
            Utils.DrawBorderString(sb, dataTag, new Vector2(panelRect.Right - 130, bottomTextY - 2),
                CyberwareTheme.AccentCyan * (alpha * 0.35f), 0.40f * CyberwareTheme.FontScale);
        }

        /// <summary>随机故障色块</summary>
        public void DrawGlitchEffect(SpriteBatch sb, float alpha, Rectangle panelRect) {
            if (glitchIntensity <= 0.01f) return;
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) return;

            float intensity = glitchIntensity * alpha;
            int glitchLines = (int)(3 + intensity * 8);
            for (int i = 0; i < glitchLines; i++) {
                int y = panelRect.Y + Main.rand.Next(panelRect.Height);
                int h = 1 + Main.rand.Next(3);
                int offsetX = Main.rand.Next(-8, 9);
                Color gc = Main.rand.NextBool() ? CyberwareTheme.Accent : CyberwareTheme.AccentCyan;
                gc *= intensity * 0.3f;
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

            Rectangle btnRect = GetCloseButtonRect(panelRect);
            float hov = isHovered ? 1f : 0f;

            Color bgColor = Color.Lerp(CyberwareTheme.BgDark, CyberwareTheme.Accent, hov * 0.28f) * (alpha * 0.92f);
            sb.Draw(px, btnRect, new Rectangle(0, 0, 1, 1), bgColor);

            Color borderColor = Color.Lerp(CyberwareTheme.Border, CyberwareTheme.Accent, hov * 0.9f) * alpha;
            sb.Draw(px, new Rectangle(btnRect.X, btnRect.Y, btnRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor);
            sb.Draw(px, new Rectangle(btnRect.X, btnRect.Bottom - 1, btnRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
            sb.Draw(px, new Rectangle(btnRect.X, btnRect.Y, 1, btnRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            sb.Draw(px, new Rectangle(btnRect.Right - 1, btnRect.Y, 1, btnRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);

            Color xColor = Color.Lerp(CyberwareTheme.TextDim, CyberwareTheme.Accent, hov) * alpha;
            Vector2 center = btnRect.Center.ToVector2();
            float s = 4.5f;
            CyberwareTheme.DrawLine(sb, px, center + new Vector2(-s, -s), center + new Vector2(s, s), 1.6f, xColor);
            CyberwareTheme.DrawLine(sb, px, center + new Vector2(s, -s), center + new Vector2(-s, s), 1.6f, xColor);
        }

        #endregion
    }
}
