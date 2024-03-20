using CalamityMod.Items;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AnimosityEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Animosity";
        public override void SetDefaults() {
            Item.damage = 33;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 70;
            Item.height = 18;
            Item.useTime = 4;
            Item.useAnimation = 12;
            Item.reuseDelay = 15;
            Item.useLimitPerAnimation = 3;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.value = CalamityGlobalItem.Rarity7BuyPrice;
            Item.rare = ItemRarityID.Lime;
            Item.UseSound = SoundID.Item31;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 11f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<AnimosityHeldProj>(55);
        }
    }
}
