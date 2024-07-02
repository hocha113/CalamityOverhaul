using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class CorinthPrimeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "CorinthPrime";
        public override void SetDefaults() {
            Item.damage = 140;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 106;
            Item.height = 42;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 8f;
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.Calamity().donorItem = true;
            Item.UseSound = SoundID.Item38;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ModContent.ProjectileType<RealmRavagerBullet>();
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<CorinthPrimeHeldProj>();
        }
    }
}
