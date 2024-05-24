using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Extras
{
    internal class GraniteRifle : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "GraniteRifle";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 25;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 12;
            Item.shootSpeed = 16;
            Item.rare = ItemRarityID.Orange;
            Item.value = Terraria.Item.buyPrice(0, 0, 13, 5);
            Item.useAmmo = AmmoID.Bullet;
            Item.UseSound = CWRSound.Gun_Rifle_Shoot;
            Item.SetCartridgeGun<GraniteRifleHeldProj>(10);
        }
    }
}
