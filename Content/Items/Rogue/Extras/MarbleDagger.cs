using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class MarbleDagger : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/MarbleDagger";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 36;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.damage = 12;
            Item.knockBack = 2.2f;
            Item.shootSpeed = 13f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.shoot = ModContent.ProjectileType<MarbleDaggerHeld>();
        }
    }
}
