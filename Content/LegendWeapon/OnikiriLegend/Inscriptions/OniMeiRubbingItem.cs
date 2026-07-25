using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 錾样/拓本载体：入包即 Unlock 所持；装刀只在改铭台。<br/>
    /// 图标为拓片反白（墨面留白字，金阶鎏金），复用 <see cref="OniMeiGlyph"/> 笔画库。<br/>
    /// 名册可送铭均有物品形态；拓本不消耗，匣上点选仅作样板凿铭
    /// </summary>
    internal abstract class OniMeiRubbingItem : ModItem, ILocalizedModType
    {
        public override string LocalizationCategory => "Items";

        /// <summary>绑定铭 Key，须与 <see cref="OniMeiDefinition.Key"/> 一致</summary>
        public abstract string MeiKey { get; }

        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Dictionary<string, int> keyToType = [];

        public override void SetStaticDefaults() {
            keyToType[MeiKey] = Type;
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(silver: 50);
            if (OniMeiRegistry.TryGet(MeiKey, out OniMeiDefinition def) && def.IsGoldTier) {
                Item.rare = ItemRarityID.Yellow;
            }
        }

        public override void UpdateInventory(Player player) {
            OniMeiOwned.Unlock(player, MeiKey);
        }

        /// <summary>Key → 錾样物品 Type；未注册返回 0</summary>
        public static int ItemTypeForKey(string key)
            => key != null && keyToType.TryGetValue(key, out int type) ? type : 0;

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            DrawRubbing(spriteBatch, position, 28f * scale, drawColor.A / 255f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            Vector2 center = Item.Center - Main.screenPosition;
            //暗处可寻:alpha 兜底 + 一点烛暖背光
            float a = MathHelper.Max(lightColor.A / 255f, 0.35f);
            OniBrush.DrawBacklight(spriteBatch, center, 30f * scale, OnikiriUITheme.CandleWarm, a * 0.28f);
            DrawRubbing(spriteBatch, center, 28f * scale, a);
            return false;
        }

        private void DrawRubbing(SpriteBatch sb, Vector2 center, float size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            bool gold = OniMeiRegistry.TryGet(MeiKey, out OniMeiDefinition def) && def.IsGoldTier;
            //rim 提亮:暗底上先立住轮廓
            Color rim = gold
                ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.55f)
                : Color.Lerp(OnikiriUITheme.Seal, OnikiriUITheme.Bright, 0.40f);

            float g = size;
            sb.Draw(pixel, center + new Vector2(1.2f, 1.8f) * (g / 44f), src, new Color(8, 2, 5) * (alpha * 0.5f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, rim * (alpha * 0.95f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, OnikiriUITheme.Ink * (alpha * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.94f), SpriteEffects.None, 0f);

            //拓片反白:墨面留白字,亮笔画天然可读
            OniMeiGlyph.DrawRubbing(sb, MeiKey, center, g * 0.80f, alpha, gold, Main.GameUpdateCount * 0.02f);
        }
    }

    internal sealed class OniMeiRubbingOnikiri : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiOnikiri);
    }

    internal sealed class OniMeiRubbingHigekiri : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiHigekiri);
    }

    internal sealed class OniMeiRubbingTessetsu : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiTessetsu);
    }

    internal sealed class OniMeiRubbingKyushu : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKyushu);
    }

    internal sealed class OniMeiRubbingShishinoko : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiShishinoko);
    }

    internal sealed class OniMeiRubbingIkiai : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiIkiai);
    }

    internal sealed class OniMeiRubbingKyoko : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKyoko);
    }

    internal sealed class OniMeiRubbingTomokiri : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiTomokiri);
    }

    internal sealed class OniMeiRubbingKarikiri : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKarikiri);
    }

    internal sealed class OniMeiRubbingMokukiri : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiMokukiri);
    }

    internal sealed class OniMeiRubbingKazehi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKazehi);
    }

    internal sealed class OniMeiRubbingKogehi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKogehi);
    }

    internal sealed class OniMeiRubbingKanhi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKanhi);
    }

    internal sealed class OniMeiRubbingChihi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiChihi);
    }

    internal sealed class OniMeiRubbingTodohi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiTodohi);
    }

    internal sealed class OniMeiRubbingShiorihi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiShiorihi);
    }

    internal sealed class OniMeiRubbingShiohi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiShiohi);
    }

    internal sealed class OniMeiRubbingFudo : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiFudo);
    }

    internal sealed class OniMeiRubbingShibori : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiShibori);
    }

    internal sealed class OniMeiRubbingChinmei : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiChinmei);
    }

    internal sealed class OniMeiRubbingAshidome : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiAshidome);
    }

    internal sealed class OniMeiRubbingKurikara : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKurikara);
    }

    internal sealed class OniMeiRubbingYoen : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiYoen);
    }
}
