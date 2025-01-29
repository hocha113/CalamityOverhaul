using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RDeepcoreGK2 : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<DeepcoreGK2>();
 
        public override void SetDefaults(Item item) {
            item.damage = 45;
            item.ArmorPenetration = 15;
            item.DamageType = DamageClass.Ranged;
            item.noMelee = true;
            item.width = 142;
            item.height = 64;
            item.useTime = item.useAnimation = 14;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 7f;
            item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            item.rare = ItemRarityID.Pink;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shoot = ProjectileID.Bullet;
            item.shootSpeed = 20f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().donorItem = true;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<DeepcoreGK2HeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 220;
            item.CWR().Scope = true;
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
