﻿using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs.Vanilla;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RBubbleGun : CWRItemOverride
    {
        public override int TargetID => ItemID.BubbleGun;
        public override bool IsVanilla => true;
        public override void SetDefaults(Item item) {
            item.damage = 52;
            item.SetHeldProj<BubbleGunHeldProj>();
        }
    }
}
