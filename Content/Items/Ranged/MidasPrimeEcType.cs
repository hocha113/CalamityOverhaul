using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class MidasPrimeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "MidasPrime";
        public override void SetDefaults() {
            Item.damage = 81;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 23;
            Item.height = 8;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2.25f;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = new("CalamityMod/Sounds/Item/CrackshotColtShot") { Volume = 0.5f, PitchVariance = 0.1f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarksmanShot>();
            Item.useAmmo = AmmoID.Bullet;
            Item.shootSpeed = 14f;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<MidasPrimeHeldProj>(30);
        }
    }
}
