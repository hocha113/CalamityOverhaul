﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTheHive : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TheHive>();
        public override int ProtogenesisID => ModContent.ItemType<TheHiveEcType>();
        public override string TargetToolTipItemName => "TheHiveEcType";
        public override bool CanLoad() => false;//TODO:在当前版本暂时移除
        public override void SetDefaults(Item item) => item.SetCartridgeGun<TheHiveHeldProj>(28);
    }
}
