﻿using CalamityMod.Items;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class NeutronStarMuzzleBrake : ModItem
    {
        public override string Texture => CWRConstant.Item + "MuzzleBrakeIV";
        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 14));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.value = CalamityGlobalItem.Rarity12BuyPrice * 8;
            Item.rare = ModContent.RarityType<Turquoise>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            CWRPlayer modplayer = player.CWR();
            modplayer.LoadMuzzleBrake = true;
            modplayer.LoadMuzzleBrakeLevel = 4;
            modplayer.PressureIncrease = 0;
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            return incomingItem.type != ModContent.ItemType<ElementMuzzleBrake>() 
                && incomingItem.type != ModContent.ItemType<PrecisionMuzzleBrake>() 
                && incomingItem.type != ModContent.ItemType<SimpleMuzzleBrake>();
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                .AddIngredient<ElementMuzzleBrake>()
                .AddIngredient<ElementalQuiver>()
                .AddIngredient<DaawnlightSpiritOrigin>()
                .AddIngredient<BlackMatterStick>(5)
                .AddTile(ModContent.TileType<DarkMatterCompressor>())
                .Register();
        }
    }
}
