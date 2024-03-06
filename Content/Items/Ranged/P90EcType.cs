using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class P90EcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "P90";
        public override void SetDefaults() {
            Item.damage = 5;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 60;
            Item.height = 28;
            Item.useTime = Item.useAnimation = 2;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1.5f;
            Item.value = CalamityGlobalItem.Rarity4BuyPrice;
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item11 with { Volume = 0.6f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<P90Round>();
            Item.shootSpeed = 9f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 580;
            Item.SetHeldProj<P90HeldProj>();
        }
    }
}
