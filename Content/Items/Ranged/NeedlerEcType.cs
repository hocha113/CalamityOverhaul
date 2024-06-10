using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class NeedlerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Needler";
        public override void SetDefaults() {
            Item.damage = 40;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 44;
            Item.height = 26;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5.5f;
            Item.value = CalamityGlobalItem.RarityLightRedBuyPrice;
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item108;
            Item.autoReuse = true;
            Item.shootSpeed = 9f;
            Item.shoot = ModContent.ProjectileType<NeedlerProj>();
            Item.useAmmo = AmmoID.Bullet;
            Item.CWR().HasCartridgeHolder = true;
            Item.CWR().AmmoCapacity = 50;
            Item.SetHeldProj<NeedlerHeldProj>();
        }
    }
}
