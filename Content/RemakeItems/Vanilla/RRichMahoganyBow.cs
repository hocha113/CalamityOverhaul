﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 红木弓
    /// </summary>
    internal class RRichMahoganyBow : CWRItemOverride
    {
        public override int TargetID => ItemID.RichMahoganyBow;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) => item.SetHeldProj<RichMahoganyBowHeldProj>();
    }
}
