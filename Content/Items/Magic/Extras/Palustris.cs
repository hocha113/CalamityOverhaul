using Terraria.ModLoader;
using Terraria.ID;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class Palustris : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Palustris";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 9;
            Item.useTime = Item.useAnimation = 22;
            Item.DamageType = DamageClass.Magic;
            Item.rare = ItemRarityID.Orange;
            Item.value = Terraria.Item.buyPrice(0, 0, 1, 25);
            Item.UseSound = SoundID.Item20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<PalustrisOrb>();
            Item.shootSpeed = 8;
            Item.SetHeldProj<PalustrisHeldProj>();
        }
    }
}
