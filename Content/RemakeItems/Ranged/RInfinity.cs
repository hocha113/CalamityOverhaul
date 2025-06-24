﻿using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RInfinity : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<Infinity>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => true;
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 105;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = 2;
            Item.useAnimation = 18;
            Item.reuseDelay = 6;
            Item.useLimitPerAnimation = 9;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.UseSound = SoundID.Item31;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ChargedBlast>();
            Item.shootSpeed = 6f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<InfinityHeldProj>(900);
            Item.CWR().Scope = true;
        }
    }
}
