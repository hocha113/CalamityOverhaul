using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HalleysInfernoEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalleysInferno";
        public override void SetDefaults() {
            Item.damage = 444;
            Item.knockBack = 5.5f;
            Item.DamageType = DamageClass.Ranged;
            Item.useTime = 5;
            Item.useAnimation = 25;
            Item.reuseDelay = 39;
            Item.useLimitPerAnimation = 5;
            Item.autoReuse = true;
            Item.useAmmo = AmmoID.Gel;
            Item.shootSpeed = 12f;
            Item.shoot = ModContent.ProjectileType<HalleysComet>();
            Item.width = 84;
            Item.height = 34;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.UseSound = HalleysInferno.ShootSound;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.SetHeldProj<HalleysInfernoHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().CartridgeEnum = CartridgeUIEnum.JAR;
            Item.CWR().AmmoCapacity = 26;
        }
    }
}
