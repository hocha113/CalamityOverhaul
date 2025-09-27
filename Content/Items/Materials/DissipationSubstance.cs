﻿using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class DissipationSubstance : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/DissipationSubstance";
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 64;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 7));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
        }
        public override void SetDefaults() {
            Item.width = Item.height = 25;
            Item.maxStack = 999;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 43);
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_DissipationSubstance;
        }
    }
}
