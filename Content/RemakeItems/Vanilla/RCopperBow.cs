﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铜弓
    /// </summary>
    internal class RCopperBow : CWRItemOverride
    {
        public override int TargetID => ItemID.CopperBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<CopperBowHeldProj>();
    }
}
