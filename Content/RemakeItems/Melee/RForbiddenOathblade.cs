﻿using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RForbiddenOathblade : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<ForbiddenOathblade>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<ForbiddenOathbladeHeld>();
    }
}
