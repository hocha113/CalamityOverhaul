using CalamityOverhaul.Content.Projectiles.Weapons;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Materials
{
    internal class Pebble : ModItem
    {
        public override string Texture => CWRConstant.Item + "Materials/Pebble";
        public override void SetDefaults() {
            Item.damage = 12;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.maxStack = 999;
            Item.DamageType = DamageClass.Throwing;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.rare = ItemRarityID.White;
            Item.consumable = true;
            Item.noUseGraphic = true;
            Item.UseSound = SoundID.Item1;
            Item.shoot = ModContent.ProjectileType<PebbleProj>();
            Item.shootSpeed = 7;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) {
            crit -= 2;
        }
    }
}
