using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class KarasawaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Karasawa";
        public override void SetDefaults() {
            Item.width = 94;
            Item.height = 44;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 2400;
            Item.knockBack = 12f;
            Item.useTime = 52;
            Item.useAnimation = 52;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = Karasawa.FireSound;
            Item.noMelee = true;
            Item.value = CalamityGlobalItem.Rarity14BuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.Calamity().donorItem = true;
            Item.shoot = ModContent.ProjectileType<KarasawaShot>();
            Item.shootSpeed = 1f;
            Item.useAmmo = AmmoID.Bullet;
            Item.SetHeldProj<InfinityHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 72;
        }
    }
}
