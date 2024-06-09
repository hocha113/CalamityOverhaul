﻿using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria.ModLoader;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class ThrowingBrickEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Rogue + "ThrowingBrick";
        public override void SetDefaults() {
            Item.SetCalamitySD<ThrowingBrick>();
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<ThrowingBrickHeld>();
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 20;
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 6;
    }
}