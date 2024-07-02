using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SeasSearingEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SeasSearing";
        public override void SetDefaults() {
            Item.damage = 40;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 88;
            Item.height = 44;
            Item.useTime = 5;
            Item.useAnimation = 10;
            Item.reuseDelay = 16;
            Item.useLimitPerAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5f;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ModContent.ProjectileType<SeasSearingBubble>();
            Item.shootSpeed = 13f;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.SetHeldProj<SeasSearingHeldProj>();
        }
    }
}
