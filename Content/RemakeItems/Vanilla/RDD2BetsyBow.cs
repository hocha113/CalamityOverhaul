﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 空中祸害
    /// </summary>
    internal class RDD2BetsyBow : BaseRItem
    {
        public override int TargetID => ItemID.DD2BetsyBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_DD2BetsyBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<DD2BetsyBowHeldProj>();
    }
}
