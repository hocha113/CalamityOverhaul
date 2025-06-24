﻿using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBrimstoneSword : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<BrimstoneSword>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<BrimstoneSwordHeld>();
        public override bool? AltFunctionUse(Item item, Player player) => false;
        public override bool? CanUseItem(Item item, Player player) => player.altFunctionUse != 2;
        public override bool? On_UseItem(Item item, Player player) => false;
        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => type = ModContent.ProjectileType<BrimstoneSwordHeld>();
        public override bool? On_UseAnimation(Item item, Player player) => false;
    }
}
