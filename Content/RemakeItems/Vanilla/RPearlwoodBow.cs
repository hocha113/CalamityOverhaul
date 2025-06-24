﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 珍珠木弓
    /// </summary>
    internal class RPearlwoodBow : CWRItemOverride
    {
        public override int TargetID => ItemID.PearlwoodBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<PearlwoodBowHeldProj>();
    }
}
