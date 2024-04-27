using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class PlagueTaintedSMGEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "PlagueTaintedSMG";
        public override void SetDefaults() {
            Item.damage = 76;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 98;
            Item.height = 50;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.Calamity().donorItem = true;
            Item.UseSound = CWRSound.Gun_SMG_Shoot;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ModContent.ProjectileType<PlagueTaintedProjectile>();
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<PlagueTaintedSMGHeldProj>(90);
            Item.CWR().Scope = true;
        }
    }
}
