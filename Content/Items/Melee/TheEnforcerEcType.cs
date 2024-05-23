using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 暴政
    /// </summary>
    internal class TheEnforcerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheEnforcer";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 100;
            Item.height = 100;
            Item.scale = 1f;
            Item.damage = 690;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 17;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 17;
            Item.useTurn = true;
            Item.knockBack = 9f;
            Item.UseSound = SoundID.Item20;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.shoot = ModContent.ProjectileType<EssenceFlames>();
            Item.shootSpeed = 2;

        }

        public override void UseAnimation(Player player) {
            Item.noUseGraphic = false;
            Item.UseSound = SoundID.Item20;
            if (player.altFunctionUse == 2) {
                Item.noUseGraphic = true;
                Item.UseSound = SoundID.Item84;
            }
            if (Main.myPlayer == player.whoAmI) {
                int types = ModContent.ProjectileType<TheEnforcerBeam>();
                Vector2 vector2 = player.Center.To(Main.MouseWorld).UnitVector() * 3;
                Vector2 position = player.Center;
                Projectile.NewProjectile(
                    player.parent(), position, vector2
                    , types
                    , (int)(Item.damage * 1.25f)
                    , Item.knockBack
                    , player.whoAmI, player.altFunctionUse == 2 ? 1 : 0);
            }
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/TheEnforcerGlow").Value);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
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

        public override bool AltFunctionUse(Player player) {
            return false;
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3))
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.ShadowbeamStaff);
        }
    }
}
