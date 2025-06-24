﻿using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAvalanche : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Avalanche>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<AvalancheHeld>();
    }
}
