using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HandheldTankEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HandheldTank";
        public override void SetDefaults() {
            Item.width = 110;
            Item.height = 46;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 740;
            Item.knockBack = 16f;
            Item.useTime = 84;
            Item.useAnimation = 84;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = HandheldTank.UseSound;
            Item.noMelee = true;
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.Calamity().donorItem = true;
            Item.shoot = ModContent.ProjectileType<HandheldTankShell>();
            Item.shootSpeed = 6f;
            Item.useAmmo = AmmoID.Rocket;
            Item.SetHeldProj<HandheldTankHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 12;
        }
    }
}
