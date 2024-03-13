using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityMod.Projectiles.Ranged;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class InfinityEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Infinity";
        public override void SetDefaults() {
            Item.damage = 120;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 56;
            Item.height = 24;
            Item.useTime = 2;
            Item.useAnimation = 18;
            Item.reuseDelay = 6;
            Item.useLimitPerAnimation = 9;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.UseSound = SoundID.Item31;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ChargedBlast>();
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<InfinityHeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 900;
            Item.CWR().Scope = true;
        }
    }
}
