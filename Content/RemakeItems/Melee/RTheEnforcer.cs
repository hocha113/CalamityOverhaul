﻿using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheEnforcer : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TheEnforcer>();
        public override int ProtogenesisID => ModContent.ItemType<TheEnforcerEcType>();
        public override string TargetToolTipItemName => "TheEnforcerEcType";
        public override void SetDefaults(Item item) => TheEnforcerEcType.SetDefaultsFunc(item);
    }
}
