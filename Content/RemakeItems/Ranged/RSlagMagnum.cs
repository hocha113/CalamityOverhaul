﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSlagMagnum : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<SlagMagnum>();
        public override void SetDefaults(Item item) {
            item.damage = 58;
            item.UseSound = CWRSound.Gun_Magnum_Shoot with { Volume = 0.35f };
            item.SetCartridgeGun<SlagMagnumHeldProj>(8);
        }
    }
}
