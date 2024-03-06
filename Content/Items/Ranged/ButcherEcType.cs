using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ButcherEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Butcher";
        public override void SetDefaults() {
            Item.damage = 15;
            Item.width = 20;
            Item.height = 12;
            Item.useTime = 40;
            Item.useAnimation = 40;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 1f;
            Item.value = CalamityGlobalItem.Rarity4BuyPrice;
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item38;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ButcherGun>();
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<ButcherHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 58;
        }
    }
}
