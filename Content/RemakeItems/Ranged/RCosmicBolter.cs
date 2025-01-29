﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCosmicBolter : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<VernalBolter>();
 
        public override void SetDefaults(Item item) => item.SetHeldProj<CosmicBolterHeldProj>();
    }
}
