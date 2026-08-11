using CalamityOverhaul.Content.Cyberwares.UIs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI;

namespace CalamityOverhaul.Content.NPCs.Victors.UIs
{
    /// <summary>对话/诊所 HUD 原语，开放边无闭合盒</summary>
    internal static class VictorUIStyle
    {
        private static Texture2D Px => VaultAsset.placeholder2.Value;
        private static Texture2D Glow => CWRAsset.SoftGlow?.Value;

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

        /// <summary>铂金银铜价；rightAlign 时 pos.X 为右界</summary>
        public static void DrawPrice(SpriteBatch sb, Vector2 pos, long value, float alpha, float scale, bool rightAlign, string freeText = "FREE") {
            Texture2D px = Px;
            if (value <= 0) {
                Vector2 fs = FontAssets.MouseText.Value.MeasureString(freeText) * scale;
                Utils.DrawBorderString(sb, freeText, new Vector2(rightAlign ? pos.X - fs.X : pos.X, pos.Y), CyberwareTheme.AccentGold * alpha, scale);
                return;
            }

            int[] amounts = [
                (int)(value / 1000000L),
                (int)(value / 10000L % 100L),
                (int)(value / 100L % 100L),
                (int)(value % 100L),
            ];
            int[] coinItems = [Terraria.ID.ItemID.PlatinumCoin, Terraria.ID.ItemID.GoldCoin, Terraria.ID.ItemID.SilverCoin, Terraria.ID.ItemID.CopperCoin];

            float totalW = 0f;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                Main.instance.LoadItem(coinItems[i]);
                Texture2D coin = TextureAssets.Item[coinItems[i]].Value;
                Vector2 ns = FontAssets.MouseText.Value.MeasureString(amounts[i].ToString()) * scale;
                totalW += ns.X + 2f + coin.Width * 0.7f + 8f;
            }

            float x = rightAlign ? pos.X - totalW : pos.X;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0) {
                    continue;
                }
                string num = amounts[i].ToString();
                Vector2 ns = FontAssets.MouseText.Value.MeasureString(num) * scale;
                Utils.DrawBorderString(sb, num, new Vector2(x, pos.Y), Color.White * alpha, scale);
                x += ns.X + 2f;
                Main.instance.LoadItem(coinItems[i]);
                Texture2D coin = TextureAssets.Item[coinItems[i]].Value;
                sb.Draw(coin, new Vector2(x, pos.Y - 1f), null, Color.White * alpha, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
                x += coin.Width * 0.7f + 8f;
            }
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
                        case Terraria.ID.ItemID.CopperCoin: total += it.stack; break;
                        case Terraria.ID.ItemID.SilverCoin: total += it.stack * 100L; break;
                        case Terraria.ID.ItemID.GoldCoin: total += it.stack * 10000L; break;
                        case Terraria.ID.ItemID.PlatinumCoin: total += it.stack * 1000000L; break;
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

        public static string Trim(string s, int max) {
            s ??= "???";
            return s.Length > max ? s[..(max - 1)] + "…" : s;
        }

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
    }
}
