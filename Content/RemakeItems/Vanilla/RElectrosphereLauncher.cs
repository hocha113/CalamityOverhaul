﻿using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    internal class RElectrosphereLauncher : CWRItemOverride
    {
        public override int TargetID => ItemID.ElectrosphereLauncher;

        public override void SetDefaults(Item item) {
            item.SetCartridgeGun<ElectrosphereLauncherHeldProj>(40);
            item.useTime = 48;
            item.damage = 40;
        }
        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) => false;
    }
}
