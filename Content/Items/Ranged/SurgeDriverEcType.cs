using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 煌辉御流炮
    /// </summary>
    internal class SurgeDriverEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SurgeDriver";
        public override void SetDefaults() {
            Item.damage = 140;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 164;
            Item.height = 58;
            Item.useTime = Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.channel = true;
            Item.knockBack = 8f;
            Item.value = CalamityGlobalItem.RarityVioletBuyPrice;
            Item.rare = ModContent.RarityType<Violet>();
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PrismEnergyBullet>();
            Item.shootSpeed = 11f;
            Item.useAmmo = AmmoID.Bullet;
            Item.UseSound = CommonCalamitySounds.LaserCannonSound;
            Item.SetHeldProj<SurgeDriverHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 98;
        }
    }
}
