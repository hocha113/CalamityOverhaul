using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue.Extras
{
    internal class GraniteDart : ModItem
    {
        public override string Texture => CWRConstant.Item + "Rogue/GraniteDart";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.value = Item.buyPrice(0, 0, 5, 0);
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Item.damage = 12;
            Item.knockBack = 2.2f;
            Item.shootSpeed = 5f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.shoot = ModContent.ProjectileType<GraniteDartHeld>();
        }
    }
}
