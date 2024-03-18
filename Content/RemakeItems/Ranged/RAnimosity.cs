using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAnimosity : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Animosity>();
        public override int ProtogenesisID => ModContent.ItemType<AnimosityEcType>();
        public override string TargetToolTipItemName => "AnimosityEcType";
        public override void SetDefaults(Item item) {
            item.damage = 33;
            item.DamageType = DamageClass.Ranged;
            item.width = 70;
            item.height = 18;
            item.useTime = 4;
            item.useAnimation = 12;
            item.reuseDelay = 15;
            item.useLimitPerAnimation = 3;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 2f;
            item.value = CalamityGlobalItem.Rarity7BuyPrice;
            item.rare = ItemRarityID.Lime;
            item.UseSound = SoundID.Item31;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 11f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<AnimosityHeldProj>();
        }
    }
}
