using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class ScorpioEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Scorpio";
        public override void SetDefaults() {
            Item.damage = 40;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 58;
            Item.height = 26;
            Item.useTime = 13;
            Item.useAnimation = 13;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 6.5f;
            Item.value = CalamityGlobalItem.Rarity10BuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item11;
            Item.autoReuse = true;
            Item.shootSpeed = 20f;
            Item.shoot = ModContent.ProjectileType<MiniRocket>();
            Item.useAmmo = AmmoID.Rocket;
            Item.SetCartridgeGun<ScorpioHeldProj>(5);
        }
    }
}
