using CalamityMod.Items;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 黄金之鹰
    /// </summary>
    internal class GoldenEagleEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "GoldenEagle";
        public override void SetDefaults() {
            Item.damage = 85;
            Item.DamageType = DamageClass.Ranged;
            Item.noMelee = true;
            Item.width = 46;
            Item.height = 30;
            Item.useTime = 19;
            Item.useAnimation = 19;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3f;
            Item.value = CalamityGlobalItem.Rarity11BuyPrice;
            Item.rare = ItemRarityID.Purple;
            Item.UseSound = SoundID.Item41;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<GoldenEagleHelProj>();//非常重要，关联手持弹幕
        }
    }
}
