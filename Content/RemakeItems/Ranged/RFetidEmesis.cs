﻿using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RFetidEmesis : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<FetidEmesis>();

        public override void SetDefaults(Item item) {
            item.damage = 129;
            item.DamageType = DamageClass.Ranged;
            item.width = 76;
            item.height = 46;
            item.useTime = item.useAnimation = 6;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3f;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 16f;
            item.useAmmo = AmmoID.Bullet;
            item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<FetidEmesisHeldProj>(120);
        }
    }
}
