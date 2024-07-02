using CalamityMod;
using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HellbornEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Hellborn";
        public override void SetDefaults() {
            Item.damage = 20;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 62;
            Item.height = 34;
            Item.useAnimation = Item.useTime = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 2f;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetCartridgeGun<HellbornHeldProj>(80);
        }
    }
}
