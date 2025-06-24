﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RShredder : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Shredder>();
        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ShredderHeldProj>(300);
            item.CWR().Scope = true;
        }
    }
}
