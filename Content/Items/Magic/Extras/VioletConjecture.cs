using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Extras
{
    internal class VioletConjecture : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "VioletConjecture";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 22;
            Item.damage = 9;
            Item.DamageType = DamageClass.Magic;
            Item.rare = ItemRarityID.Orange;
            Item.value = Terraria.Item.buyPrice(0, 0, 1, 25);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<VioletConjectureOrb>();
            Item.shootSpeed = 8;
            Item.UseSound = SoundID.Item20;
            Item.SetHeldProj<VioletConjectureHeldProj>();
        }
    }
}
