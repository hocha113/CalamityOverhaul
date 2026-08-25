using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 沉锚镣环：不溺者的头号奖励，直接回答 L4 的通行痛点（水）。
    /// 水中移动不再受阻（保留惯性）、呼吸时间 +60%、获得游泳能力。
    /// 首杀必掉/复杀 25%，结算在 DungeonworldBossRecords.ServerSettleKill。
    /// 贴图借原版镣铐，绘制期重染水藻绿与怨灵掉落区分（零新画像素）
    /// </summary>
    internal class UndrownedShackleCharm : UndrownedModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Shackle;

        /// <summary>水藻绿重染色</summary>
        private static readonly Color KelpTint = new(128, 178, 150);

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 2);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            //水中不减速且保留惯性 + 游泳 + 呼吸时间 +60%
            player.ignoreWater = true;
            player.accFlipper = true;
            player.breathMax += player.breathMax * 3 / 5;
        }

        //==================== 重染绘制（背包与落地都染水藻绿）====================

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            spriteBatch.Draw(tex, position, frame, drawColor.MultiplyRGB(KelpTint), 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI) {
            Texture2D tex = TextureAssets.Item[Item.type]?.Value;
            if (tex == null) {
                return true;
            }
            spriteBatch.Draw(tex, Item.Center - Main.screenPosition, null,
                lightColor.MultiplyRGB(KelpTint), rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
