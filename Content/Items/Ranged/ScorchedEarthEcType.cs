using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ScorchedEarthEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "ScorchedEarth";
        public static SoundStyle ShootSound = new("CalamityMod/Sounds/Item/ScorchedEarthShot", 3);
        public override void SetDefaults() {
            Item.damage = 350;
            Item.DamageType = DamageClass.Ranged;
            Item.useTime = 88;
            Item.useAnimation = 32;
            Item.reuseDelay = 60;
            Item.useLimitPerAnimation = 4;
            Item.width = 104;
            Item.height = 44;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 8.7f;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.autoReuse = true;
            Item.shootSpeed = 12.6f;
            Item.shoot = ModContent.ProjectileType<ScorchedEarthRocket>();
            Item.useAmmo = AmmoID.Rocket;
            Item.Calamity().donorItem = true;
            Item.SetCartridgeGun<ScorchedEarthHeldProj>(4);
        }
    }
}
