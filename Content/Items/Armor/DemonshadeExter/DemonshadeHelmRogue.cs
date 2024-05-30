using CalamityMod.Items.Armor.Demonshade;
using CalamityMod.Items.Materials;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityMod;

namespace CalamityOverhaul.Content.Items.Armor.DemonshadeExter
{
    [AutoloadEquip(EquipType.Head)]
    internal class DemonshadeHelmRogue : ModItem, ISetupData
    {
        public override string Texture => CWRConstant.Item + "Armor/DemonshadeExter/DemonshadeHelmRogue";
        internal static int PType;
        internal static Asset<Texture2D> Hand;
        internal readonly static string TextureFrome = CWRConstant.Item + "Armor/DemonshadeExter/DemonshadeHelmRogue_Head_Frome";
        void ISetupData.SetupData() {
            PType = ModContent.ItemType<DemonshadeHelmRogue>();
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

        public override void UpdateArmorSet(Player player) {
            var modPlayer = player.Calamity();
            modPlayer.rogueStealthMax += 2f;
            modPlayer.wearingRogueArmor = true;
        }

        public override void ArmorSetShadows(Player player) {
            player.armorEffectDrawShadow = true;
            player.armorEffectDrawOutlines = true;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage<ThrowingDamageClass>() += 0.3f;
            player.GetCritChance<ThrowingDamageClass>() += 20;
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<ShadowspecBar>(12).
                AddTile<DraedonsForge>().
                Register();
        }
    }
}
