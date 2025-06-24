﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 钨弓
    /// </summary>
    internal class RTungstenBow : CWRItemOverride
    {
        public override int TargetID => ItemID.TungstenBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<TungstenBowHeldProj>();
    }
}
