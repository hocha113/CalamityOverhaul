﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 肌腱弓
    /// </summary>
    internal class RTendonBow : CWRItemOverride
    {
        public override int TargetID => ItemID.TendonBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<TendonBowHeldProj>();
    }
}
