﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 钨弓
    /// </summary>
    internal class RTungstenBow : BaseRItem
    {
        public override int TargetID => ItemID.TungstenBow;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_TungstenBow_Text";
        public override void SetDefaults(Item item) => item.SetHeldProj<TungstenBowHeldProj>();
    }
}
