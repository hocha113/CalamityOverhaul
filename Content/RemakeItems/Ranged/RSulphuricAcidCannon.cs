﻿using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSulphuricAcidCannon : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SulphuricAcidCannon>();

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<SulphuricAcidCannonHeldProj>(80);
            item.useAmmo = AmmoID.Bullet;
        }
    }
}
