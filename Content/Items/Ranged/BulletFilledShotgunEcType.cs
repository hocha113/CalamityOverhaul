using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 多发霰弹枪
    /// </summary>
    internal class BulletFilledShotgunEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "BulletFilledShotgun";
        public override void SetDefaults() {
            Item.damage = 1;
            Item.knockBack = 0f;
            Item.useTime = Item.useAnimation = 45;
            Item.DamageType = DamageClass.Ranged;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.useAmmo = AmmoID.Bullet;
            Item.shootSpeed = 9f;
            Item.shoot = ModContent.ProjectileType<BouncingShotgunPellet>();
            Item.width = 64;
            Item.height = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item38;
            Item.value = CalamityGlobalItem.Rarity3BuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.Calamity().donorItem = true;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<BulletFilledShotgunHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 8;
        }
    }
}
