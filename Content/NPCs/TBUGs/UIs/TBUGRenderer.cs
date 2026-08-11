using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.TBUGs.UIs
{
    /// <summary>
    /// TBUG 界面绘制原语。拐角只有切角一种语言，发光只靠亮笔叠宽，
    /// 暗部一律靠着色器底或紧贴投影——不做同心放大的假羽化
    /// </summary>
    internal static class TBUGRenderer
    {
        public static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static Texture2D Glow => CWRAsset.SoftGlow?.Value;
        private static DynamicSpriteFont Font => FontAssets.MouseText.Value;
        private static readonly Rectangle One = new(0, 0, 1, 1);

        #region 基础

        public static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float len = diff.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, start, One, color, diff.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(len, thickness), SpriteEffects.None, 0f);
        }

        /// <summary>亮笔：三道递增宽度递减亮度，只用于冷光线条</summary>
        public static void DrawGlowLine(SpriteBatch sb, Vector2 start, Vector2 end, float thickness, Color color) {
            DrawLine(sb, start, end, thickness * 3.2f, color * 0.14f);
            DrawLine(sb, start, end, thickness * 1.8f, color * 0.28f);
            DrawLine(sb, start, end, thickness, color);
        }

        #endregion

        #region 切角面

        /// <summary>切角实心填充；ch=0 退化为普通矩形</summary>
        public static void FillChamfer(SpriteBatch sb, Rectangle r, Color color, int ch = TBUGTheme.Chamfer) {
            if (r.Width <= 0 || r.Height <= 0) {
                return;
            }
            ch = Math.Min(ch, Math.Min(r.Width, r.Height) / 2);
            if (ch <= 0) {
                sb.Draw(Pixel, r, One, color);
                return;
            }
            //主体：让开上下切角带
            sb.Draw(Pixel, new Rectangle(r.X, r.Y + ch, r.Width, r.Height - ch * 2), One, color);
            //上下切角带逐行内缩
            for (int i = 0; i < ch; i++) {
                int inset = ch - i;
                sb.Draw(Pixel, new Rectangle(r.X + inset, r.Y + i, r.Width - inset * 2, 1), One, color);
                sb.Draw(Pixel, new Rectangle(r.X + inset, r.Bottom - 1 - i, r.Width - inset * 2, 1), One, color);
            }
        }

        /// <summary>切角描边；四条直边让开角，四条斜边补角</summary>
        public static void DrawChamferFrame(SpriteBatch sb, Rectangle r, Color color,
            float thickness = 1.6f, int ch = TBUGTheme.Chamfer, bool glow = false) {
            if (r.Width <= ch * 2 || r.Height <= ch * 2) {
                return;
            }
            float l = r.Left, t = r.Top, rt = r.Right, b = r.Bottom;
            Span<Vector2> pts = [
                new(l + ch, t), new(rt - ch, t),
                new(l + ch, b), new(rt - ch, b),
                new(l, t + ch), new(l, b - ch),
                new(rt, t + ch), new(rt, b - ch),
            ];
            //四条直边
            Stroke(sb, pts[0], pts[1], thickness, color, glow);
            Stroke(sb, pts[2], pts[3], thickness, color, glow);
            Stroke(sb, pts[4], pts[5], thickness, color, glow);
            Stroke(sb, pts[6], pts[7], thickness, color, glow);
            //四条斜角
            Stroke(sb, pts[4], pts[0], thickness, color, glow);
            Stroke(sb, pts[1], pts[6], thickness, color, glow);
            Stroke(sb, pts[5], pts[2], thickness, color, glow);
            Stroke(sb, pts[3], pts[7], thickness, color, glow);
        }

        private static void Stroke(SpriteBatch sb, Vector2 a, Vector2 b, float th, Color c, bool glow) {
            if (glow) {
                DrawGlowLine(sb, a, b, th, c);
            }
            else {
                DrawLine(sb, a, b, th, c);
            }
        }

        /// <summary>紧贴投影，位移不放大——放大只会摞出方块黑层</summary>
        public static void DrawDropShadow(SpriteBatch sb, Rectangle r, float alpha, int ch = TBUGTheme.Chamfer) {
            Rectangle s = r;
            s.Offset(3, 4);
            FillChamfer(sb, s, Color.Black * (0.5f * alpha), ch);
        }

        #endregion

        #region 文字

        /// <summary>冷光描边文字，四向亮笔垫底</summary>
        public static void DrawGlowText(SpriteBatch sb, string text, Vector2 pos,
            Color color, Color glowColor, float scale, float radius = 1.25f) {
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.PiOver2 * i;
                Vector2 off = new(MathF.Cos(ang) * radius, MathF.Sin(ang) * radius);
                Utils.DrawBorderString(sb, text, pos + off, glowColor, scale);
            }
            Utils.DrawBorderString(sb, text, pos, color, scale);
        }

        public static void DrawText(SpriteBatch sb, string text, Vector2 pos, Color color, float scale)
            => Utils.DrawBorderString(sb, text, pos, color, scale);

        public static Vector2 Measure(string text, float scale)
            => string.IsNullOrEmpty(text) ? Vector2.Zero : Font.MeasureString(text) * scale;

        /// <summary>按像素宽截断补省略号；放不下的长物品名交给它，不许溢出面板</summary>
        public static string TrimToWidth(string s, float scale, float maxPx) {
            if (string.IsNullOrEmpty(s) || Measure(s, scale).X <= maxPx) {
                return s;
            }
            int keep = s.Length - 1;
            while (keep > 1 && Measure(string.Concat(s.AsSpan(0, keep), "…"), scale).X > maxPx) {
                keep--;
            }
            return string.Concat(s.AsSpan(0, keep), "…");
        }

        /// <summary>按像素宽换行，返回非空行</summary>
        public static List<string> WrapLines(string text, float scale, float pixelWidth, int maxLines = 16) {
            List<string> result = [];
            if (string.IsNullOrWhiteSpace(text) || pixelWidth < 8f) {
                return result;
            }
            string[] raw = VaultUtils.WrapTextArray(text, Font, (int)(pixelWidth / scale), maxLines, out _);
            foreach (string line in raw) {
                if (!string.IsNullOrWhiteSpace(line)) {
                    result.Add(line);
                }
            }
            return result;
        }

        #endregion

        #region 面板底

        /// <summary>
        /// TBUGTerminalPanel.fx 终端玻璃底；着色器缺失回退为三档暗底 + 顶部冷光
        /// </summary>
        /// <param name="mode">0 主窗 1 悬停浮层（噪声与网格更弱）</param>
        public static void DrawGlassPanel(SpriteBatch sb, Rectangle rect, float alpha, int mode = 0) {
            if (rect.Width < 4 || rect.Height < 4 || alpha < 0.01f) {
                return;
            }
            Effect effect = EffectLoader.TBUGTerminalPanel?.Value;
            if (effect == null) {
                FillChamfer(sb, rect, TBUGTheme.Deep * (0.96f * alpha));
                sb.Draw(Pixel, new Rectangle(rect.X + TBUGTheme.Chamfer, rect.Y,
                    rect.Width - TBUGTheme.Chamfer * 2, 1), One, TBUGTheme.Blue * (0.5f * alpha));
                return;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(alpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uChamfer"]?.SetValue((float)TBUGTheme.Chamfer);
            effect.Parameters["uMode"]?.SetValue(mode);
            ShaderQuad(sb, effect, rect);
        }

        private static void ShaderQuad(SpriteBatch sb, Effect effect, Rectangle dest) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, dest, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>顶栏：提示符 + 标题 + 闪烁块光标，返回标题栏底边 Y</summary>
        public static int DrawPromptHeader(SpriteBatch sb, Rectangle panel, float alpha,
            float timer, string prompt, string title) {
            int headerH = 38;
            float y = panel.Y + 9f;
            float x = panel.X + 18f;

            DrawGlowText(sb, prompt, new Vector2(x, y),
                TBUGTheme.Blue * alpha, TBUGTheme.Blue * (alpha * 0.22f), TBUGTheme.FontTitle);
            x += Measure(prompt, TBUGTheme.FontTitle).X + 10f;
            DrawText(sb, title, new Vector2(x, y), TBUGTheme.Text * alpha, TBUGTheme.FontTitle);
            x += Measure(title, TBUGTheme.FontTitle).X + 6f;

            if ((int)(timer * 1.6f) % 2 == 0) {
                sb.Draw(Pixel, new Rectangle((int)x, (int)y + 4, 9, 18), One, TBUGTheme.Blue * (alpha * 0.9f));
            }

            int divY = panel.Y + headerH;
            DrawRule(sb, panel.X + 14, panel.Right - 14, divY, TBUGTheme.Line * alpha, TBUGTheme.Blue * (alpha * 0.55f));
            return divY;
        }

        /// <summary>分隔线：整条暗结构线 + 左段主色引导，方向感统一从左起</summary>
        public static void DrawRule(SpriteBatch sb, int left, int right, int y, Color baseColor, Color leadColor) {
            int w = right - left;
            if (w <= 0) {
                return;
            }
            sb.Draw(Pixel, new Rectangle(left, y, w, 1), One, baseColor);
            sb.Draw(Pixel, new Rectangle(left, y, Math.Min(64, w), 1), One, leadColor);
        }

        /// <summary>底部状态栏</summary>
        public static void DrawStatusBar(SpriteBatch sb, Rectangle panel, float alpha,
            float timer, string status, bool error) {
            int barTop = panel.Bottom - 30;
            DrawRule(sb, panel.X + 14, panel.Right - 14, barTop, TBUGTheme.Line * (alpha * 0.8f),
                TBUGTheme.Blue * (alpha * 0.4f));

            Color dot = error ? TBUGTheme.Danger : TBUGTheme.Blue;
            float blink = MathF.Sin(timer * 3f) > 0f ? 1f : 0.35f;
            sb.Draw(Pixel, new Rectangle(panel.X + 18, barTop + 12, 5, 5), One, dot * (alpha * blink));
            DrawText(sb, status, new Vector2(panel.X + 30, barTop + 5f),
                (error ? TBUGTheme.Danger : TBUGTheme.TextDim) * alpha, TBUGTheme.FontMicro);
        }

        #endregion

        #region 关闭钮

        public static Rectangle GetCloseRect(Rectangle panel) => new(panel.Right - 40, panel.Y + 8, 24, 22);

        public static void DrawClose(SpriteBatch sb, Rectangle panel, float alpha, bool hover) {
            Rectangle r = GetCloseRect(panel);
            Color c = hover ? TBUGTheme.Danger : TBUGTheme.TextDim;
            FillChamfer(sb, r, (hover ? TBUGTheme.Rise : TBUGTheme.Panel) * (alpha * 0.85f), 4);
            DrawChamferFrame(sb, r, c * (alpha * 0.8f), 1.2f, 4);
            Vector2 mid = r.Center.ToVector2();
            const float s = 4.5f;
            DrawLine(sb, mid + new Vector2(-s, -s), mid + new Vector2(s, s), 1.6f, c * alpha);
            DrawLine(sb, mid + new Vector2(s, -s), mid + new Vector2(-s, s), 1.6f, c * alpha);
        }

        #endregion

        #region 货币

        /// <summary>铂金银铜价；rightAlign 时 pos.X 为右界。返回占用宽度</summary>
        public static float DrawPrice(SpriteBatch sb, Vector2 pos, long value, float alpha,
            float scale, bool rightAlign, Color numberColor) {
            int[] amounts = [
                (int)(value / 1000000L),
                (int)(value / 10000L % 100L),
                (int)(value / 100L % 100L),
                (int)(value % 100L),
            ];
            int[] coins = [ItemID.PlatinumCoin, ItemID.GoldCoin, ItemID.SilverCoin, ItemID.CopperCoin];
            const float coinScale = 0.85f;

            float total = 0f;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                Main.instance.LoadItem(coins[i]);
                total += Measure(amounts[i].ToString(), scale).X + 3f
                    + TextureAssets.Item[coins[i]].Value.Width * coinScale + 9f;
            }

            float x = rightAlign ? pos.X - total : pos.X;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                string num = amounts[i].ToString();
                DrawText(sb, num, new Vector2(x, pos.Y), numberColor * alpha, scale);
                x += Measure(num, scale).X + 3f;
                Main.instance.LoadItem(coins[i]);
                Texture2D coin = TextureAssets.Item[coins[i]].Value;
                sb.Draw(coin, new Vector2(x, pos.Y - 1f), null, Color.White * alpha, 0f,
                    Vector2.Zero, coinScale, SpriteEffects.None, 0f);
                x += coin.Width * coinScale + 9f;
            }
            return total;
        }

        public static float MeasurePrice(long value, float scale) {
            int[] amounts = [
                (int)(value / 1000000L),
                (int)(value / 10000L % 100L),
                (int)(value / 100L % 100L),
                (int)(value % 100L),
            ];
            int[] coins = [ItemID.PlatinumCoin, ItemID.GoldCoin, ItemID.SilverCoin, ItemID.CopperCoin];
            float total = 0f;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                Main.instance.LoadItem(coins[i]);
                total += Measure(amounts[i].ToString(), scale).X + 3f
                    + TextureAssets.Item[coins[i]].Value.Width * 0.85f + 9f;
            }
            return total;
        }

        public static long CountCoins(Player p) {
            long total = 0;
            void Add(Item[] inv) {
                if (inv == null) {
                    return;
                }
                foreach (Item it in inv) {
                    if (it == null || it.IsAir) {
                        continue;
                    }
                    switch (it.type) {
                        case ItemID.CopperCoin: total += it.stack; break;
                        case ItemID.SilverCoin: total += it.stack * 100L; break;
                        case ItemID.GoldCoin: total += it.stack * 10000L; break;
                        case ItemID.PlatinumCoin: total += it.stack * 1000000L; break;
                    }
                }
            }
            Add(p.inventory);
            Add(p.bank?.item);
            Add(p.bank2?.item);
            Add(p.bank3?.item);
            Add(p.bank4?.item);
            return total;
        }

        #endregion

        #region 物品

        public static void DrawItemIcon(SpriteBatch sb, int type, Vector2 center, float box, float alpha) {
            Main.instance.LoadItem(type);
            Texture2D tex = TextureAssets.Item[type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[type] != null
                ? Main.itemAnimations[type].GetFrame(tex)
                : tex.Bounds;
            float maxDim = Math.Max(frame.Width, frame.Height);
            float scale = maxDim > box ? box / maxDim : 1f;
            sb.Draw(tex, center, frame, Color.White * alpha, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        #endregion

        #region 悬停介绍框

        /// <summary>
        /// 光标介绍框：按内容测量后成框，四边钳制在屏内。
        /// 结构 = 标题行(名+右上角标) / 正文若干行 / 底部价格行
        /// </summary>
        public static void DrawCursorPanel(SpriteBatch sb, Vector2 cursor, float alpha,
            string title, Color titleColor, IReadOnlyList<string> body,
            string tag, Color tagColor, long price, Color priceColor, string priceLabel) {
            if (alpha < 0.02f) {
                return;
            }

            const float pad = 15f;
            const float minW = 250f;
            const float maxW = 430f;
            float lineH = Measure("A", TBUGTheme.FontBody).Y + 5f;

            //宽度取标题行、最长正文行、价格行三者的最大值
            float tagBlockW = string.IsNullOrEmpty(tag) ? 0f : Measure(tag, TBUGTheme.FontMicro).X + 22f;
            float titleW = Measure(title, TBUGTheme.FontTitle).X + tagBlockW;
            float widest = titleW;
            if (body != null) {
                foreach (string l in body) {
                    widest = MathF.Max(widest, Measure(l, TBUGTheme.FontBody).X);
                }
            }
            float priceRowW = Measure(priceLabel, TBUGTheme.FontLabel).X + 12f
                + MeasurePrice(price, TBUGTheme.FontLabel);
            widest = MathF.Max(widest, priceRowW);

            float contentW = MathHelper.Clamp(widest, minW - pad * 2f, maxW - pad * 2f);
            //超长物品名裁到宽度上限内，不许压过右上角标或捅出面板
            string fitTitle = TrimToWidth(title, TBUGTheme.FontTitle, contentW - tagBlockW);
            float titleH = Measure(fitTitle, TBUGTheme.FontTitle).Y + 8f;
            int bodyCount = body?.Count ?? 0;
            float bodyH = bodyCount > 0 ? bodyCount * lineH + 8f : 0f;
            float priceH = Measure("A", TBUGTheme.FontLabel).Y + 12f;

            float panelW = contentW + pad * 2f;
            float panelH = pad * 1.2f + titleH + bodyH + priceH + pad * 0.6f;

            Vector2 pos = cursor + new Vector2(22f, 20f);
            pos.X = MathHelper.Clamp(pos.X, 8f, TBUGTheme.UIScreenW - panelW - 8f);
            pos.Y = MathHelper.Clamp(pos.Y, 8f, TBUGTheme.UIScreenH - panelH - 8f);
            Rectangle rect = new((int)pos.X, (int)pos.Y, (int)panelW, (int)panelH);

            DrawDropShadow(sb, rect, alpha);
            DrawGlassPanel(sb, rect, alpha, mode: 1);
            DrawChamferFrame(sb, rect, TBUGTheme.Blue * (alpha * 0.85f), 1.5f, TBUGTheme.Chamfer, glow: true);

            float y = rect.Y + pad * 0.8f;
            DrawGlowText(sb, fitTitle, new Vector2(rect.X + pad, y),
                titleColor * alpha, titleColor * (alpha * 0.25f), TBUGTheme.FontTitle);
            if (!string.IsNullOrEmpty(tag)) {
                Vector2 tagSize = Measure(tag, TBUGTheme.FontMicro);
                DrawText(sb, tag, new Vector2(rect.Right - pad - tagSize.X, y + 5f),
                    tagColor * alpha, TBUGTheme.FontMicro);
            }
            y += titleH;

            if (bodyCount > 0) {
                DrawRule(sb, rect.X + (int)pad, rect.Right - (int)pad, (int)y - 3,
                    TBUGTheme.Line * alpha, TBUGTheme.BlueDim * alpha);
                y += 5f;
                foreach (string l in body) {
                    DrawText(sb, l, new Vector2(rect.X + pad, y), TBUGTheme.TextDim * alpha, TBUGTheme.FontBody);
                    y += lineH;
                }
                y += 3f;
            }

            DrawRule(sb, rect.X + (int)pad, rect.Right - (int)pad, (int)y,
                TBUGTheme.Line * alpha, TBUGTheme.BlueDim * alpha);
            y += 6f;
            DrawText(sb, priceLabel, new Vector2(rect.X + pad, y), TBUGTheme.TextDim * alpha, TBUGTheme.FontLabel);
            DrawPrice(sb, new Vector2(rect.Right - pad, y), price, alpha,
                TBUGTheme.FontLabel, rightAlign: true, priceColor);
        }

        #endregion

        #region 命令按钮

        /// <summary>
        /// 横排命令按钮：切角底 + 左侧序号键帽 + 文本。返回按钮矩形宽度
        /// </summary>
        public static void DrawCommandButton(SpriteBatch sb, Rectangle rect, string key, string text,
            float hoverT, float alpha, Color accent) {
            FillChamfer(sb, rect, Color.Lerp(TBUGTheme.Panel, TBUGTheme.Rise, hoverT) * (alpha * 0.9f), 5);
            DrawChamferFrame(sb, rect, Color.Lerp(TBUGTheme.Line, accent, 0.35f + 0.65f * hoverT) * alpha,
                1.4f, 5, glow: hoverT > 0.5f);

            //键帽：底边一道亮条表示"可按"
            sb.Draw(Pixel, new Rectangle(rect.X + 5, rect.Bottom - 3, rect.Width - 10, 2), One,
                accent * (alpha * (0.25f + 0.75f * hoverT)));

            float keyW = Measure(key, TBUGTheme.FontMicro).X;
            DrawText(sb, key, new Vector2(rect.X + 12f, rect.Y + 9f),
                accent * (alpha * (0.6f + 0.4f * hoverT)), TBUGTheme.FontMicro);

            Vector2 textSize = Measure(text, TBUGTheme.FontLabel);
            DrawText(sb, text, new Vector2(rect.X + 20f + keyW, rect.Y + (rect.Height - textSize.Y) * 0.5f),
                Color.Lerp(TBUGTheme.Text, TBUGTheme.Ice, hoverT) * alpha, TBUGTheme.FontLabel);
        }

        /// <summary>命令按钮宽度 = 键号 + 文本 + 内边距</summary>
        public static int MeasureCommandButton(string key, string text)
            => (int)(Measure(key, TBUGTheme.FontMicro).X + Measure(text, TBUGTheme.FontLabel).X) + 44;

        #endregion

        #region 商店格

        /// <summary>
        /// 商店格：切角底 + 图标 + 底部价条。悬停整格抬起并亮边，买不起压暗且价格转报错色
        /// </summary>
        public static void DrawShopCell(SpriteBatch sb, Rectangle cell, int itemType, long price,
            bool affordable, float hoverT, float alpha) {
            float lift = hoverT * 3f;
            Rectangle r = new(cell.X, cell.Y - (int)lift, cell.Width, cell.Height);

            if (hoverT > 0.02f) {
                DrawDropShadow(sb, r, alpha * hoverT * 0.8f, 5);
            }
            FillChamfer(sb, r, Color.Lerp(TBUGTheme.Panel, TBUGTheme.Rise, hoverT) * (alpha * 0.95f), 5);

            Color edge = affordable
                ? Color.Lerp(TBUGTheme.Line, TBUGTheme.Blue, 0.3f + 0.7f * hoverT)
                : Color.Lerp(TBUGTheme.Line, TBUGTheme.Danger, 0.25f + 0.45f * hoverT);
            DrawChamferFrame(sb, r, edge * alpha, 1.4f, 5, glow: hoverT > 0.4f);

            //价条：格底一条独立暗带，把图标区和价格分开
            int stripH = 24;
            Rectangle strip = new(r.X + 2, r.Bottom - stripH - 2, r.Width - 4, stripH);
            sb.Draw(Pixel, strip, One, TBUGTheme.Void * (alpha * 0.7f));
            sb.Draw(Pixel, new Rectangle(strip.X, strip.Y, strip.Width, 1), One, TBUGTheme.Line * (alpha * 0.8f));

            float iconAlpha = alpha * (affordable ? 1f : 0.45f);
            Vector2 iconCenter = new(r.Center.X, r.Y + (r.Height - stripH) * 0.5f + 2f);
            DrawItemIcon(sb, itemType, iconCenter, 40f, iconAlpha);

            Color priceColor = affordable ? TBUGTheme.Amber : TBUGTheme.Danger;
            //铂金档四组币会超出格宽，超了就降一档字号
            float priceScale = TBUGTheme.FontMicro;
            float priceW = MeasurePrice(price, priceScale);
            if (priceW > strip.Width - 8f) {
                priceScale *= 0.8f;
                priceW = MeasurePrice(price, priceScale);
            }
            DrawPrice(sb, new Vector2(strip.Center.X - priceW * 0.5f, strip.Y + 3f), price,
                alpha, priceScale, rightAlign: false, priceColor);

            //悬停角标：右上角一小段亮切角，明确"当前选中的是这一格"
            if (hoverT > 0.05f) {
                Color tick = TBUGTheme.Ice * (alpha * hoverT);
                DrawLine(sb, new Vector2(r.Right - 14, r.Y + 1), new Vector2(r.Right - 1, r.Y + 1), 2f, tick);
                DrawLine(sb, new Vector2(r.Right - 1, r.Y + 1), new Vector2(r.Right - 1, r.Y + 14), 2f, tick);
            }
        }

        #endregion

        #region 扫描氛围

        /// <summary>面板内缓慢下行的扫描亮线，配合着色器底给"没静止"的观感</summary>
        public static void DrawScanSweep(SpriteBatch sb, Rectangle rect, float alpha, float timer) {
            float t = timer * 0.16f % 1f;
            int y = rect.Y + (int)(t * rect.Height);
            float fade = MathF.Sin(t * MathHelper.Pi);
            sb.Draw(Pixel, new Rectangle(rect.X + 2, y, rect.Width - 4, 1), One,
                TBUGTheme.Blue * (alpha * 0.10f * fade));
            sb.Draw(Pixel, new Rectangle(rect.X + 2, y + 2, rect.Width - 4, 1), One,
                TBUGTheme.Blue * (alpha * 0.05f * fade));
            if (Glow != null) {
                Color g = TBUGTheme.Blue * (alpha * 0.05f * fade);
                g.A = 0;
                sb.Draw(Glow, new Vector2(rect.Center.X, y), null, g, 0f,
                    Glow.Size() * 0.5f, new Vector2(rect.Width / 120f, 0.06f), SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}
