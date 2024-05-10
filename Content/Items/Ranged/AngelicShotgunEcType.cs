using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class AngelicShotgunEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "AngelicShotgun";
        public override void SetDefaults() {
            Item.damage = 136;
            Item.knockBack = 3f;
            Item.DamageType = DamageClass.Ranged;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.width = 86;
            Item.height = 38;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.UseSound = SoundID.Item38;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.Calamity().donorItem = true;
            Item.shootSpeed = 12;
            Item.shoot = ModContent.ProjectileType<HallowPointRoundProj>();//TODO:临时替换HallowPointRoundProj弹幕，有待考证
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<AngelicShotgunHeldProj>(50);
        }
    }
}
