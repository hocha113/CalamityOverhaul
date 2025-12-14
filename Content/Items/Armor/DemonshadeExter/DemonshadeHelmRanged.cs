using CalamityMod.Items.Armor.Demonshade;
using CalamityMod.Items.Materials;
using CalamityMod.Tiles.Furniture.CraftingStations;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Armor.DemonshadeExter
{
    [AutoloadEquip(EquipType.Head)]
    internal class DemonshadeHelmRanged : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item + "Armor/DemonshadeExter/DemonshadeHelmRanged";
        internal static int PType;
        internal static Asset<Texture2D> Hand;
        internal static readonly string TextureFrome = CWRConstant.Item + "Armor/DemonshadeExter/DemonshadeHelmRanged_Head_Frome";
        void ICWRLoader.SetupData() {
            PType = ModContent.ItemType<DemonshadeHelmRanged>();
            if (!Main.dedServ) {
                Hand = CWRUtils.GetT2DAsset(TextureFrome);
            }
        }
        void ICWRLoader.UnLoadData() {
            PType = 0;
            Hand = null;
        }
        public override bool IsLoadingEnabled(Mod mod) {
            return false;//TODO
        }
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.defense = 35;
            Item.value = 122000;
            Item.rare = CWRID.Rarity_HotPink;
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
