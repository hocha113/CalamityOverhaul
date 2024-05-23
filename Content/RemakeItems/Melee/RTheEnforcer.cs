using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheEnforcer : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.TheEnforcer>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override string TargetToolTipItemName => "TheEnforcerEcType";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override void SetDefaults(Item item) {
            item.width = 100;
            item.height = 100;
            item.scale = 1f;
            item.damage = 690;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 17;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 17;
            item.useTurn = true;
            item.knockBack = 9f;
            item.UseSound = SoundID.Item20;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.shoot = ModContent.ProjectileType<EssenceFlames>();
            item.shootSpeed = 2;
        }

        public override void UseAnimation(Item item, Player player) {
            item.noUseGraphic = false;
            item.UseSound = SoundID.Item20;
            if (player.altFunctionUse == 2) {
                item.noUseGraphic = true;
                item.UseSound = SoundID.Item84;
            }
            if (Main.myPlayer == player.whoAmI) {
                int types = ModContent.ProjectileType<TheEnforcerBeam>();
                Vector2 vector2 = player.Center.To(Main.MouseWorld).UnitVector() * 3;
                Vector2 position = player.Center;
                Projectile.NewProjectile(
                    player.parent(), position, vector2
                    , types
                    , (int)(item.damage * 1.25f)
                    , item.knockBack
                    , player.whoAmI, player.altFunctionUse == 2 ? 1 : 0);
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity * 5, ModContent.ProjectileType<EssenceStar>(), (int)(damage * 0.75f), knockback, player.whoAmI, 0f, Main.rand.Next(3));
            }
            else {
                _ = player.RotatedRelativePoint(player.MountedCenter, true);
                for (int i = 0; i < 4; i++) {
                    Vector2 realPlayerPos = new Vector2(player.position.X + (player.width * 0.5f) + (float)(Main.rand.Next(1358) * -(float)player.direction)
                        + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y);
                    realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-350, 351);
                    realPlayerPos.Y -= 100 * i;
                    Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, 0f, 0f, type, damage / 4, knockback, player.whoAmI, 0f, Main.rand.Next(3));
                }
            }
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return false;
        }
    }
}
