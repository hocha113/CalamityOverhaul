using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.RebelBladeProj;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class RebelBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.shootSpeed = 9;
            Item.crit = 8;
            Item.damage = 186;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.knockBack = 6;
            Item.value = Item.buyPrice(0, 83, 55, 0);
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.shoot = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            Item.CWR().isHeldItem = true;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) => player.itemLocation = player.GetPlayerStabilityCenter();

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override void HoldItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeBack>()] == 0
                && Main.myPlayer == player.whoAmI
                && player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeSlash>()] == 0
                && !player.PressKey()) {
                Projectile.NewProjectileDirect(player.parent(), player.Center
                    , Vector2.Zero, ModContent.ProjectileType<RebelBladeBack>(), 0, 0, player.whoAmI);
            }
        }

        public override bool? UseItem(Player player) {
            Item.noMelee = false;
            Item.noUseGraphic = false;
            if (player.altFunctionUse == 2) {
                Item.noMelee = true;
                Item.noUseGraphic = true;
            }
            return base.UseItem(player);
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse != 2) {
                Projectile.NewProjectile(source, player.MountedCenter, new Vector2(player.direction, 0f)
                , ModContent.ProjectileType<RebelBladeSlash>()
                , damage, knockback, player.whoAmI, player.direction * player.gravDir
                , player.itemAnimationMax, player.GetAdjustedItemScale(Item));
                return false;
            }
            return true;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) => HitEffect(player, target);

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) => HitEffect(player, target);

        private void HitEffect(Player player, Entity target) {
            for (int i = 0; i < 3; i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(player.GetSource_FromThis(), spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, player.whoAmI);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            Dust dust = Dust.NewDustDirect(hitbox.TopLeft(), hitbox.Width, hitbox.Height, DustID.FireworkFountain_Yellow, 0, 0, 55);
            dust.noGravity = true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.LunarBar, 10)
                .AddIngredient(ItemID.SoulofMight, 15)
                .AddIngredient(ItemID.SoulofLight, 15)
                .AddIngredient(ItemID.SoulofNight, 15)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
