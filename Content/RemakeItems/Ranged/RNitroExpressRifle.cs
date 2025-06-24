﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RNitroExpressRifle : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<NitroExpressRifle>();
        public override void SetDefaults(Item item) => item.SetCartridgeGun<NitroExpressRifleHeldProj>(8);
    }
}
