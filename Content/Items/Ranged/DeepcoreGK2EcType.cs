using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class DeepcoreGK2EcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DeepcoreGK2";
        public override void SetDefaults() {
            Item.damage = 45;
            Item.ArmorPenetration = 15;
            Item.DamageType = DamageClass.Ranged;
            Item.noMelee = true;
            Item.width = 142;
            Item.height = 64;
            Item.useTime = Item.useAnimation = 14;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item38;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().donorItem = true;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<DeepcoreGK2HeldProj>();
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 220;
            Item.CWR().Scope = true;
        }
    }
}
