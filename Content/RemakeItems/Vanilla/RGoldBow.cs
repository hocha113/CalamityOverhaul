﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 金弓
    /// </summary>
    internal class RGoldBow : BaseRItem
    {
        public override int TargetID => ItemID.GoldBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_GoldBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<GoldBowHeldProj>();
    }
}
