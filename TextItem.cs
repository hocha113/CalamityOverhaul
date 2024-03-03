using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal class TextItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";

        public override bool IsLoadingEnabled(Mod mod) {
            return true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 9999;
            Item.DamageType = DamageClass.Default;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            if (player.PressKey(false) && Main.GameUpdateCount % 10 == 0) {
                player.QuickSpawnItem(player.parent(), Main.HoverItem, 1);
            }
        }

        public override void HoldItem(Player player) {
        }

        public override bool? UseItem(Player player) {
            Projectile.NewProjectile(player.parent(), player.Center, Vector2.Zero,
                ModContent.ProjectileType<EndSkillEffectStart>(), 1000, 0, player.whoAmI, 0, player.Center.X, player.Center.Y);
            return true;
        }
    }
}
