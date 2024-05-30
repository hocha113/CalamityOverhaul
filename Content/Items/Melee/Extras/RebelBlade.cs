using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class RebelBlade : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Item.width = Item.height = 54;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.damage = 186;
            Item.useTime = Item.useAnimation = 15;
            Item.knockBack = 6;
            Item.rare = ItemRarityID.Lime;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.shootSpeed = 9;
            Item.shoot = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            Item.crit = 8;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame) {
            player.itemLocation = player.GetPlayerStabilityCenter();
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] == 0;
        }

        public override bool? UseItem(Player player) {
            Item.noMelee = false;
            Item.noUseGraphic = false;
            Item.shoot = ProjectileID.None;
            if (player.altFunctionUse == 2) {
                Item.noMelee = true;
                Item.noUseGraphic = true;
                Item.shoot = ModContent.ProjectileType<RebelBladeFlyAttcke>();
            }
            return base.UseItem(player);
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < Main.rand.Next(3, 5); i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(player.GetSource_FromThis(), spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, player.whoAmI);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            for (int i = 0; i < Main.rand.Next(3, 5); i++) {
                Vector2 spwanPos = target.position + new Vector2(target.width * Main.rand.NextFloat(), target.height * Main.rand.NextFloat());
                Projectile.NewProjectile(player.GetSource_FromThis(), spwanPos, Vector2.Zero
                    , ModContent.ProjectileType<RebelBladeOrb>(), Item.damage / 5, 0, player.whoAmI);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            Dust dust = Dust.NewDustDirect(hitbox.TopLeft(), hitbox.Width, hitbox.Height, DustID.FireworkFountain_Yellow, 0, 0, 55);
            dust.noGravity = true;
        }
    }
}
