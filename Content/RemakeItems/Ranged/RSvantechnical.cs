﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSvantechnical : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Svantechnical>();
        public override int ProtogenesisID => ModContent.ItemType<SvantechnicalEcType>();
        public override string TargetToolTipItemName => "SvantechnicalEcType";
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<SvantechnicalHeldProj>(280);
            item.CWR().Scope = true;
        }
    }
}
