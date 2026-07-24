using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Items
{
    /// <summary>
    /// 錾样/拓本载体：入包即 Unlock 所持；装刀只在改铭台。<br/>
    /// 图标复用 <see cref="OniMeiGlyph"/>，与扇骨菱章同色系。<br/>
    /// 现有名册 8 铭均有物品形态，便于日后「换铭退旧样」对接
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
            Item.maxStack = 99;
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
            DrawRubbing(spriteBatch, position, 28f * scale, 1f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            Vector2 center = Item.Center - Main.screenPosition;
            float a = lightColor.A / 255f;
            DrawRubbing(spriteBatch, center, 28f * scale, a);
            return false;
        }

        private void DrawRubbing(SpriteBatch sb, Vector2 center, float size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 half = new(0.5f);
            bool gold = OniMeiRegistry.TryGet(MeiKey, out OniMeiDefinition def) && def.IsGoldTier;
            Color rim = gold
                ? Color.Lerp(OnikiriUITheme.GoldDeep, OnikiriUITheme.GoldInlay, 0.45f)
                : Color.Lerp(OnikiriUITheme.Deep, OnikiriUITheme.Bright, 0.35f);

            float g = size;
            sb.Draw(pixel, center + new Vector2(1.2f, 1.8f) * (g / 44f), src, new Color(8, 2, 5) * (alpha * 0.5f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, rim * (alpha * 0.9f),
                MathHelper.PiOver4, half, new Vector2(g * 1.06f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, OnikiriUITheme.Ink * (alpha * 0.97f),
                MathHelper.PiOver4, half, new Vector2(g * 0.96f), SpriteEffects.None, 0f);
            sb.Draw(pixel, center, src, OnikiriUITheme.Paper * (alpha * 0.16f),
                MathHelper.PiOver4, half, new Vector2(g * 0.82f), SpriteEffects.None, 0f);

            OniMeiGlyphStyle style = OniMeiGlyphStyle.Engraved(alpha);
            style.Inlay = gold ? 1f : 0f;
            style.Accent = gold ? OnikiriUITheme.GoldInlay : OnikiriUITheme.Bright;
            style.Lit = 0.15f;
            style.Time = (float)Main.GameUpdateCount * 0.02f;
            OniMeiGlyph.Draw(sb, MeiKey, center, g * 0.72f, style);
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

    internal sealed class OniMeiRubbingShishinoko : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiShishinoko);
    }

    internal sealed class OniMeiRubbingTomokiri : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiTomokiri);
    }

    internal sealed class OniMeiRubbingKazehi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKazehi);
    }

    internal sealed class OniMeiRubbingChihi : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiChihi);
    }

    internal sealed class OniMeiRubbingFudo : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiFudo);
    }

    internal sealed class OniMeiRubbingKurikara : OniMeiRubbingItem
    {
        public override string MeiKey => nameof(MeiKurikara);
    }
}
