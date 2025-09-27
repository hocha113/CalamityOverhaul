﻿using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class DecaySubstance : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/DecaySubstance";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 5));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }
        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 13);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_DecaySubstance;
        }
    }
}
