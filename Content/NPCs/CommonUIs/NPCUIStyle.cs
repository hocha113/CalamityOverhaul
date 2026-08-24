using CalamityOverhaul.Content.Cyberwares.UIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;

namespace CalamityOverhaul.Content.NPCs.CommonUIs
{
    /// <summary>
    /// 城镇特殊 NPC（维克托/TBUG）共用的对话与商店 HUD 原语，开放边无闭合盒。
    /// 皮肤统一跟随义体家族（<see cref="CyberwareTheme"/>），别再为单个 NPC 另起色板
    /// </summary>
    internal static class NPCUIStyle
    {
        private static Texture2D Px => VaultAsset.placeholder2.Value;
        private static Texture2D Glow => CWRAsset.SoftGlow?.Value;

        #region UI 空间坐标

        //UIHandle 的 Update/Draw 跑在 UIScale 空间，逻辑帧里是原始后台缓冲尺寸，
        //跨语境布局一律走这组换算，禁止直接读 Main.screenWidth/Height
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        #endregion

        #region 文字

        public static Vector2 Measure(string text, float scale)
            => string.IsNullOrEmpty(text) ? Vector2.Zero : FontAssets.MouseText.Value.MeasureString(text) * scale;

        public static string Trim(string s, int max) {
            s ??= "???";
            return s.Length > max ? s[..(max - 1)] + "…" : s;
        }

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
            string[] raw = VaultUtils.WrapTextArray(text, FontAssets.MouseText.Value, (int)(pixelWidth / scale), maxLines, out _);
            foreach (string line in raw) {
                if (!string.IsNullOrWhiteSpace(line)) {
                    result.Add(line);
                }
            }
            return result;
        }

        #endregion

        #region 结构件

        /// <summary>四角 L 角标</summary>
        public static void DrawCorners(SpriteBatch sb, Rectangle r, Color c, int len, int th) {
            Texture2D px = Px;
            //左上
            sb.Draw(px, new Rectangle(r.X, r.Y, len, th), c);
            sb.Draw(px, new Rectangle(r.X, r.Y, th, len), c);
            //右上
            sb.Draw(px, new Rectangle(r.Right - len, r.Y, len, th), c);
            sb.Draw(px, new Rectangle(r.Right - th, r.Y, th, len), c);
            //左下
            sb.Draw(px, new Rectangle(r.X, r.Bottom - th, len, th), c);
            sb.Draw(px, new Rectangle(r.X, r.Bottom - len, th, len), c);
            //右下
            sb.Draw(px, new Rectangle(r.Right - len, r.Bottom - th, len, th), c);
            sb.Draw(px, new Rectangle(r.Right - th, r.Bottom - len, th, len), c);
        }

        /// <summary>竖向发光分隔，中亮端淡</summary>
        public static void DrawVDivider(SpriteBatch sb, int x, int top, int bottom, Color c) {
            Texture2D px = Px;
            int h = bottom - top;
            if (h <= 0) {
                return;
            }
            const int seg = 16;
            for (int i = 0; i < seg; i++) {
                float t = i / (float)(seg - 1);
                float a = MathF.Sin(t * MathHelper.Pi);
                sb.Draw(px, new Rectangle(x, top + (int)(h * t), 2, h / seg + 1), c * a);
            }
        }

        /// <summary>横向发光分隔，中亮端淡</summary>
        public static void DrawHDivider(SpriteBatch sb, int left, int right, int y, Color c) {
            Texture2D px = Px;
            int w = right - left;
            if (w <= 0) {
                return;
            }
            const int seg = 20;
            for (int i = 0; i < seg; i++) {
                float t = i / (float)(seg - 1);
                float a = MathF.Sin(t * MathHelper.Pi);
                sb.Draw(px, new Rectangle(left + (int)(w * t), y, w / seg + 1, 1), c * a);
            }
        }

        /// <summary>分区标题，左块+标题+右虚线</summary>
        public static void DrawSectionHeader(SpriteBatch sb, Rectangle r, string label, Color accent, float alpha, float fontScale) {
            Texture2D px = Px;
            sb.Draw(px, new Rectangle(r.X, r.Y + 2, 4, r.Height - 4), accent * (alpha * 0.9f));
            sb.Draw(px, new Rectangle(r.X + 6, r.Y + 1, 2, r.Height - 2), accent * (alpha * 0.4f));
            Utils.DrawBorderString(sb, label, new Vector2(r.X + 14, r.Y + (r.Height - FontAssets.MouseText.Value.MeasureString(label).Y * fontScale) / 2f),
                accent * alpha, fontScale);
            float textW = FontAssets.MouseText.Value.MeasureString(label).X * fontScale;
            int dashStart = r.X + 20 + (int)textW;
            for (int x = dashStart; x < r.Right - 4; x += 8) {
                sb.Draw(px, new Rectangle(x, r.Y + r.Height / 2, 4, 1), accent * (alpha * 0.35f));
            }
        }

        /// <summary>命令行，返回悬停 slide</summary>
        public static int DrawCommandRow(SpriteBatch sb, Rectangle rect, Color accent, float hoverT, float alpha, bool separator = true) {
            Texture2D px = Px;
            int slide = (int)(hoverT * 6f);
            Rectangle r = new(rect.X + slide, rect.Y, rect.Width - slide, rect.Height);

            //悬停左亮右淡
            if (hoverT > 0.001f) {
                const int strips = 10;
                for (int i = 0; i < strips; i++) {
                    float t = i / (float)strips;
                    Color c = accent * (alpha * hoverT * 0.32f * (1f - t));
                    sb.Draw(px, new Rectangle(r.X + (int)(r.Width * t), r.Y, r.Width / strips + 1, r.Height), c);
                }
            }
            else {
                //空闲淡底
                sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, r.Height), CyberwareTheme.SlotInnerBg * (alpha * 0.35f));
            }

            //左强调条
            int barW = 3 + (int)(hoverT * 3f);
            sb.Draw(px, new Rectangle(r.X, r.Y, barW, r.Height), accent * (alpha * (0.55f + 0.45f * hoverT)));
            if (Glow != null && hoverT > 0.01f) {
                Color g = accent * (alpha * hoverT * 0.25f);
                g.A = 0;
                sb.Draw(Glow, new Vector2(r.X, r.Center.Y), null, g, 0f, Glow.Size() / 2f, new Vector2(0.12f, r.Height / 60f), SpriteEffects.None, 0f);
            }

            //底部分隔，悬停加宽
            if (separator) {
                int sepW = (int)((r.Width - barW - 8) * (0.45f + 0.55f * hoverT));
                sb.Draw(px, new Rectangle(r.X + barW + 6, r.Bottom - 1, sepW, 1), accent * (alpha * (0.2f + 0.35f * hoverT)));
            }

            return slide;
        }

        /// <summary>全息框，暗底+线+角标+扫描</summary>
        public static void DrawHoloFrame(SpriteBatch sb, Rectangle rect, Color accent, float alpha, float timer) {
            Texture2D px = Px;
            sb.Draw(px, rect, CyberwareTheme.SectionBg * (alpha * 0.92f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), accent * (alpha * 0.5f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), accent * (alpha * 0.5f));
            DrawCorners(sb, rect, accent * alpha, 16, 2);

            int sy = rect.Y + (int)(timer * 36f % rect.Height);
            sb.Draw(px, new Rectangle(rect.X + 2, sy, rect.Width - 4, 1), accent * (alpha * 0.16f));
            sb.Draw(px, new Rectangle(rect.X + 2, sy + 2, rect.Width - 4, 1), accent * (alpha * 0.08f));
        }

        #endregion

        #region 货币

        private const float CoinScale = 0.7f;

        /// <summary>铂金银铜价；rightAlign 时 pos.X 为右界。返回占用宽度</summary>
        public static float DrawPrice(SpriteBatch sb, Vector2 pos, long value, float alpha, float scale,
            bool rightAlign, string freeText = "FREE", Color? numberColor = null) {
            if (value <= 0) {
                Vector2 fs = FontAssets.MouseText.Value.MeasureString(freeText) * scale;
                Utils.DrawBorderString(sb, freeText, new Vector2(rightAlign ? pos.X - fs.X : pos.X, pos.Y), CyberwareTheme.AccentGold * alpha, scale);
                return fs.X;
            }

            int[] amounts = SplitCoins(value);
            int[] coinItems = [ItemID.PlatinumCoin, ItemID.GoldCoin, ItemID.SilverCoin, ItemID.CopperCoin];
            Color numColor = numberColor ?? Color.White;

            float totalW = MeasurePrice(value, scale);
            float x = rightAlign ? pos.X - totalW : pos.X;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                string num = amounts[i].ToString();
                Vector2 ns = FontAssets.MouseText.Value.MeasureString(num) * scale;
                Utils.DrawBorderString(sb, num, new Vector2(x, pos.Y), numColor * alpha, scale);
                x += ns.X + 2f;
                Main.instance.LoadItem(coinItems[i]);
                Texture2D coin = TextureAssets.Item[coinItems[i]].Value;
                sb.Draw(coin, new Vector2(x, pos.Y - 1f), null, Color.White * alpha, 0f, Vector2.Zero, CoinScale, SpriteEffects.None, 0f);
                x += coin.Width * CoinScale + 8f;
            }
            return totalW;
        }

        /// <summary>价签占用宽度，与 <see cref="DrawPrice"/> 同一份度量</summary>
        public static float MeasurePrice(long value, float scale) {
            if (value <= 0) {
                return 0f;
            }
            int[] amounts = SplitCoins(value);
            int[] coinItems = [ItemID.PlatinumCoin, ItemID.GoldCoin, ItemID.SilverCoin, ItemID.CopperCoin];
            float total = 0f;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                Main.instance.LoadItem(coinItems[i]);
                total += FontAssets.MouseText.Value.MeasureString(amounts[i].ToString()).X * scale + 2f
                    + TextureAssets.Item[coinItems[i]].Value.Width * CoinScale + 8f;
            }
            return total;
        }

        private static int[] SplitCoins(long value) => [
            (int)(value / 1000000L),
            (int)(value / 10000L % 100L),
            (int)(value / 100L % 100L),
            (int)(value % 100L),
        ];

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

        /// <summary>
        /// 走 <see cref="ItemSlot.DrawItemIcon"/>：内部过 ItemLoader.PreDrawInInventory/PostDrawInInventory，
        /// 占位贴图 + 自绘（SVG/特效）的物品才画得出来；裸 Draw 物品贴图只会画出占位像素
        /// </summary>
        public static void DrawItemIcon(SpriteBatch sb, int type, Vector2 center, float box, float alpha) {
            if (type <= ItemID.None
                || !ContentSamples.ItemsByType.TryGetValue(type, out Item sample) || sample == null) {
                return;
            }
            ItemSlot.DrawItemIcon(sample, ItemSlot.Context.InWorld, sb, center, 1f, box, Color.White * alpha);
        }

        #endregion

        #region 悬停介绍框

        public const float TipTitleScale = 0.8f;
        public const float TipBodyScale = 0.66f;
        public const float TipLabelScale = 0.66f;

        /// <summary>
        /// 光标介绍框（义体家族皮）：按内容测量后成框，四边钳制在屏内。
        /// 结构 = 标题行(名+右上角标) / 正文若干行 / 底部价格行 / 可选脚注行
        /// </summary>
        public static void DrawCursorPanel(SpriteBatch sb, Vector2 cursor, float alpha,
            string title, Color titleColor, IReadOnlyList<string> body,
            string tag, Color tagColor, long price, Color priceColor, string priceLabel,
            string footer = null, Color footerColor = default) {
            if (alpha < 0.02f) {
                return;
            }
            Texture2D px = Px;

            const float pad = 12f;
            const float minW = 230f;
            const float maxW = 430f;
            float lineH = Measure("A", TipBodyScale).Y + 4f;

            //宽度取标题行、最长正文行、价格行、脚注行四者的最大值
            float tagBlockW = string.IsNullOrEmpty(tag) ? 0f : Measure(tag, TipLabelScale).X + 20f;
            float widest = Measure(title, TipTitleScale).X + tagBlockW;
            if (body != null) {
                foreach (string l in body) {
                    widest = MathF.Max(widest, Measure(l, TipBodyScale).X);
                }
            }
            float priceRowW = Measure(priceLabel, TipLabelScale).X + 12f + MeasurePrice(price, TipLabelScale);
            widest = MathF.Max(widest, priceRowW);
            if (!string.IsNullOrEmpty(footer)) {
                widest = MathF.Max(widest, Measure(footer, TipLabelScale).X);
            }

            float contentW = MathHelper.Clamp(widest, minW - pad * 2f, maxW - pad * 2f);
            //超长物品名裁到宽度上限内，不许压过右上角标或捅出面板
            string fitTitle = TrimToWidth(title, TipTitleScale, contentW - tagBlockW);
            float titleH = Measure(fitTitle, TipTitleScale).Y + 7f;
            int bodyCount = body?.Count ?? 0;
            float bodyH = bodyCount > 0 ? bodyCount * lineH + 8f : 0f;
            float priceH = Measure("A", TipLabelScale).Y + 10f;
            float footerH = string.IsNullOrEmpty(footer) ? 0f : lineH + 2f;

            float panelW = contentW + pad * 2f;
            float panelH = pad + titleH + bodyH + priceH + footerH + pad * 0.6f;

            Vector2 pos = cursor + new Vector2(18f, 18f);
            pos.X = MathHelper.Clamp(pos.X, 8f, UIScreenW - panelW - 8f);
            pos.Y = MathHelper.Clamp(pos.Y, 8f, UIScreenH - panelH - 8f);
            Rectangle rect = new((int)pos.X, (int)pos.Y, (int)panelW, (int)panelH);

            //底：实底 + 上下暗角 + 顶缘主色线 + 切角缺口，与义体 Tooltip 同语汇
            sb.Draw(px, rect, CyberwareTheme.BgPanel * (alpha * 0.96f));
            for (int i = 0; i < 4; i++) {
                float fade = 1f - i / 4f;
                Color vig = CyberwareTheme.InnerShadow * (alpha * 0.5f * fade);
                sb.Draw(px, new Rectangle(rect.X, rect.Y + i, rect.Width, 1), vig);
                sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1 - i, rect.Width, 1), vig);
            }
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), CyberwareTheme.Accent * (alpha * 0.8f));
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), CyberwareTheme.Border * (alpha * 0.6f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), CyberwareTheme.Accent * (alpha * 0.4f));
            sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), CyberwareTheme.Accent * (alpha * 0.4f));
            const int cut = 3;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, cut, cut), CyberwareTheme.BgDark * alpha);
            sb.Draw(px, new Rectangle(rect.Right - cut, rect.Y, cut, cut), CyberwareTheme.BgDark * alpha);

            //慢速扫描线
            float scanY = rect.Y + Main.GlobalTimeWrappedHourly * 0.55f % 1f * rect.Height;
            sb.Draw(px, new Rectangle(rect.X + 1, (int)scanY, rect.Width - 2, 1), CyberwareTheme.Accent * (alpha * 0.06f));

            float y = rect.Y + pad * 0.8f;
            Utils.DrawBorderString(sb, fitTitle, new Vector2(rect.X + pad, y), titleColor * alpha, TipTitleScale);
            if (!string.IsNullOrEmpty(tag)) {
                Vector2 tagSize = Measure(tag, TipLabelScale);
                Utils.DrawBorderString(sb, tag, new Vector2(rect.Right - pad - tagSize.X, y + 4f), tagColor * alpha, TipLabelScale);
            }
            y += titleH;

            if (bodyCount > 0) {
                DrawHDivider(sb, rect.X + (int)pad, rect.Right - (int)pad, (int)y - 2, CyberwareTheme.Accent * (alpha * 0.35f));
                y += 5f;
                foreach (string l in body) {
                    Utils.DrawBorderString(sb, l, new Vector2(rect.X + pad, y), CyberwareTheme.TextNormal * alpha, TipBodyScale);
                    y += lineH;
                }
                y += 3f;
            }

            DrawHDivider(sb, rect.X + (int)pad, rect.Right - (int)pad, (int)y, CyberwareTheme.Accent * (alpha * 0.35f));
            y += 5f;
            Utils.DrawBorderString(sb, priceLabel, new Vector2(rect.X + pad, y), CyberwareTheme.TextDim * alpha, TipLabelScale);
            DrawPrice(sb, new Vector2(rect.Right - pad, y), price, alpha, TipLabelScale, rightAlign: true, numberColor: priceColor);

            if (!string.IsNullOrEmpty(footer)) {
                y += priceH - 4f;
                Color fc = footerColor == default ? CyberwareTheme.TextDim : footerColor;
                Utils.DrawBorderString(sb, footer, new Vector2(rect.X + pad, y), fc * alpha, TipLabelScale);
            }
        }

        #endregion
    }
}
