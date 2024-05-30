using CalamityMod.Items.Armor.Demonshade;
using CalamityMod.Items.Materials;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;
using CalamityMod.Tiles.Furniture.CraftingStations;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace CalamityOverhaul.Content.Items.Armor.DemonshadeExter
{
    [AutoloadEquip(EquipType.Head)]
    internal class DemonshadeHelmRanged : ModItem, ISetupData
    {
        public override string Texture => CWRConstant.Item + "Armor/DemonshadeExter/DemonshadeHelmRanged";
        internal static int PType;
        internal static Asset<Texture2D> Hand;
        internal readonly static string TextureFrome = CWRConstant.Item + "Armor/DemonshadeExter/DemonshadeHelmRanged_Head_Frome";
        void ISetupData.SetupData() {
            PType = ModContent.ItemType<DemonshadeHelmRanged>();
            if (!Main.dedServ) {
                Hand = CWRUtils.GetT2DAsset(TextureFrome);
            }
        }
        void ISetupData.UnLoadData() {
            PType = 0;
            Hand = null;
        }
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.defense = 35;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.rare = ModContent.RarityType<HotPink>();
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) {
            return body.type == ModContent.ItemType<DemonshadeBreastplate>()
                && legs.type == ModContent.ItemType<DemonshadeGreaves>();
        }

        public override void UpdateArmorSet(Player player) { }

        public override void ArmorSetShadows(Player player) {
            player.armorEffectDrawShadow = true;
            player.armorEffectDrawOutlines = true;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage<RangedDamageClass>() += 0.35f;
            player.GetCritChance<RangedDamageClass>() += 20;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<ShadowspecBar>(12).
                AddTile<DraedonsForge>().
                Register();
        }
    }
}
