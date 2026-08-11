using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Presentation.Skins.Kikasa;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 鬼雨叙事皮肤的调试触发物品：使用即播放 <see cref="FirstMetShenyo"/>，可反复触发。
    /// 测试用，皮肤正式接入剧情流程后删除
    /// </summary>
    internal class ShenyoStyleDemoItem : ModItem
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 24;
            Item.rare = ItemRarityID.Blue;
            Item.maxStack = 1;
        }

        public override bool? UseItem(Player player) {
            //闸同其余触发器:演出占线时不重复入场
            if (player.whoAmI == Main.myPlayer && !NarrativeTriggerGate.IsBusy) {
                NarrativeRouter.Begin<FirstMetShenyo>();
            }
            return true;
        }

        //测试物品要能在任何存档立刻拿到:一块木头徒手合成
        public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Wood).Register();

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position,
            Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            KikasaPanelDraw.DrawUmbrellaGlyph(spriteBatch, position, 26f * scale, drawColor.A / 255f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            Vector2 center = Item.Center - Main.screenPosition;
            //暗处可寻:alpha 兜底
            float a = MathHelper.Max(lightColor.A / 255f, 0.35f);
            KikasaPanelDraw.DrawUmbrellaGlyph(spriteBatch, center, 26f * scale, a, rotation);
            return false;
        }
    }
}
