﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 金弓
    /// </summary>
    internal class RGoldBow : CWRItemOverride
    {
        public override int TargetID => ItemID.GoldBow;
        public override bool IsVanilla => true;

        public override void SetDefaults(Item item) => item.SetHeldProj<GoldBowHeldProj>();
    }
}
