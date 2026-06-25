using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Draedon.PQCDs.DraedonShops
{
    /// <summary>
    /// 嘉登交换终端的绘制原语：货币计数、价格币堆、货品图标与切角取景框。
    /// 面板外框/分隔线/扫描线等直接复用 <see cref="Content.Narrative.Presentation.Skins.Draedon.DraedonPanelDraw"/>，保持与对话皮肤同源
    /// </summary>
    internal static class DraedonShopStyle
    {
        private static Texture2D Px => VaultAsset.placeholder2.Value;

        private static readonly int[] CoinTiers = [ItemID.PlatinumCoin, ItemID.GoldCoin, ItemID.SilverCoin, ItemID.CopperCoin];

        /// <summary>统计玩家可用于购买的全部金币（背包 + 四类钱袋），与 <see cref="Player.BuyItem(int, int)"/> 的扣费范围一致</summary>
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

        /// <summary>把铜价拆成 铂/金/银/铜 四档，跳过为 0 的高档</summary>
        private static void SplitCoins(long value, Span<int> amounts) {
            amounts[0] = (int)(value / 1000000L);
            amounts[1] = (int)(value / 10000L % 100L);
            amounts[2] = (int)(value / 100L % 100L);
            amounts[3] = (int)(value % 100L);
        }

        /// <summary>测量价格币堆的像素宽度，便于右对齐布局</summary>
        public static float CoinsWidth(long value, float scale) {
            Span<int> amounts = stackalloc int[4];
            SplitCoins(value, amounts);
            float w = 0f;
            bool any = false;
            for (int i = 0; i < 4; i++) {
                if (amounts[i] <= 0 && !(i == 3 && !any)) {
                    continue;
                }
                any = true;
                Main.instance.LoadItem(CoinTiers[i]);
                Texture2D coin = TextureAssets.Item[CoinTiers[i]].Value;
                w += FontAssets.MouseText.Value.MeasureString(amounts[i].ToString()).X * scale + 2f + coin.Width * 0.62f * scale + 7f;
            }
            return w;
        }

        /// <summary>绘制 铂/金/银/铜 价格币堆；<paramref name="numberTint"/> 用于表达买得起/买不起</summary>
        public static void DrawCoins(SpriteBatch sb, Vector2 pos, long value, float alpha, float scale, Color numberTint, bool rightAlign = false) {
            Span<int> amounts = stackalloc int[4];
            SplitCoins(value, amounts);

            float x = rightAlign ? pos.X - CoinsWidth(value, scale) : pos.X;
            bool any = false;
            for (int i = 0; i < 4; i++) {
                //最低档铜币在总价为 0 或不足铜时仍要兜底显示一枚，避免空白
                if (amounts[i] <= 0 && !(i == 3 && !any)) {
                    continue;
                }
                any = true;

                string num = amounts[i].ToString();
                Vector2 ns = FontAssets.MouseText.Value.MeasureString(num) * scale;
                Utils.DrawBorderString(sb, num, new Vector2(x, pos.Y), numberTint * alpha, scale);
                x += ns.X + 2f;

                Main.instance.LoadItem(CoinTiers[i]);
                Texture2D coin = TextureAssets.Item[CoinTiers[i]].Value;
                sb.Draw(coin, new Vector2(x, pos.Y + ns.Y * 0.5f - coin.Height * 0.31f * scale), null, Color.White * alpha,
                    0f, Vector2.Zero, 0.62f * scale, SpriteEffects.None, 0f);
                x += coin.Width * 0.62f * scale + 7f;
            }
        }

        /// <summary>居中绘制一枚物品图标，按 <paramref name="box"/> 边长等比缩放</summary>
        public static void DrawItemIcon(SpriteBatch sb, int type, Vector2 center, float box, float alpha, float extraScale = 1f) {
            Main.instance.LoadItem(type);
            Texture2D tex = TextureAssets.Item[type]?.Value;
            if (tex == null) {
                return;
            }
            Rectangle frame = Main.itemAnimations[type] != null ? Main.itemAnimations[type].GetFrame(tex) : tex.Bounds;
            float maxDim = Math.Max(frame.Width, frame.Height);
            float scale = (maxDim > box ? box / maxDim : 1f) * extraScale;
            sb.Draw(tex, center, frame, Color.White * alpha, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>货品图标的切角取景框：暗底 + 青边 + 右上斜切 + 悬停辉光，呼应 DrawPortraitFrame 的语言</summary>
        public static void DrawIconFrame(SpriteBatch sb, Rectangle rect, float alpha, float hover, float pulse) {
            Texture2D px = Px;

            if (hover > 0.01f) {
                Rectangle glow = rect;
                glow.Inflate(3, 3);
                sb.Draw(px, glow, new Rectangle(0, 0, 1, 1), DraedonShopTheme.Glow * (alpha * 0.18f * hover));
            }

            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), DraedonShopTheme.Void * (alpha * 0.92f));

            Color edge = Color.Lerp(DraedonShopTheme.EdgeDim, DraedonShopTheme.EdgeBright, 0.35f + 0.45f * pulse + 0.2f * hover) * (alpha * 0.85f);
            const int bw = 1;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, bw), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - bw, rect.Width, bw), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - bw, rect.Y, bw, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);

            //右上斜切
            int cut = Math.Max(4, rect.Width / 5);
            for (int row = 0; row < cut; row++) {
                int segLen = cut - row;
                sb.Draw(px, new Rectangle(rect.Right - segLen - bw, rect.Y + row, segLen, 1), new Rectangle(0, 0, 1, 1),
                    DraedonShopTheme.Void * alpha);
                sb.Draw(px, new Rectangle(rect.Right - segLen - bw, rect.Y + row, 1, 1), new Rectangle(0, 0, 1, 1),
                    edge * (1f - (float)row / cut));
            }
        }

        /// <summary>裁剪过长字符串</summary>
        public static string Trim(string s, int max) {
            s ??= "???";
            return s.Length > max ? s[..(max - 1)] + "…" : s;
        }
    }
}
