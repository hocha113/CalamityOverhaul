using CalamityMod.Items;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class Helstorm : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Helstorm";
        public override void SetDefaults() {
            Item.damage = 31;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 50;
            Item.height = 24;
            Item.useTime = 7;
            Item.useAnimation = 7;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2.5f;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 11.5f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<HelstormHeldProj>();
        }
    }
}
