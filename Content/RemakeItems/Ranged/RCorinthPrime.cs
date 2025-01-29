using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RCorinthPrime : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<CorinthPrime>();
 
        public override void SetDefaults(Item item) {
            item.damage = 140;
            item.DamageType = DamageClass.Ranged;
            item.width = 106;
            item.height = 42;
            item.useTime = 24;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 8f;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.Calamity().donorItem = true;
            item.UseSound = SoundID.Item38;
            item.autoReuse = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.shoot = ModContent.ProjectileType<RealmRavagerBullet>();
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<CorinthPrimeHeldProj>();
        }
    }
}
