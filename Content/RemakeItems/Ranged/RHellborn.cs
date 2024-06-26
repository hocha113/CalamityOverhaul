﻿using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHellborn : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Hellborn>();
        public override int ProtogenesisID => ModContent.ItemType<HellbornEcType>();
        public override string TargetToolTipItemName => "HellbornEcType";
        public override void SetDefaults(Item item) {
            item.damage = 20;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 34;
            item.useAnimation = item.useTime = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 2f;
            item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            item.rare = ItemRarityID.Pink;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.noMelee = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetCartridgeGun<HellbornHeldProj>(80);
        }
    }
}
