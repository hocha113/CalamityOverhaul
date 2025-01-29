using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RTyrannysEnd : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<TyrannysEnd>();
        public override void SetDefaults(Item item) {
            item.damage = 1500;
            item.knockBack = 9.5f;
            item.DamageType = DamageClass.Ranged;
            item.useTime = 60;
            item.useAnimation = 60;
            item.shoot = ProjectileID.BulletHighVelocity;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.autoReuse = true;
            item.width = 150;
            item.height = 48;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.UseSound = CommonCalamitySounds.LargeWeaponFireSound;
            item.value = CalamityGlobalItem.RarityVioletBuyPrice;
            item.rare = ModContent.RarityType<Violet>();
            item.Calamity().donorItem = true;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().hasHeldNoCanUseBool = true;
            item.CWR().heldProjType = ModContent.ProjectileType<TyrannysEndHeldProj>();
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 5;
            item.CWR().Scope = true;
        }
    }
}
