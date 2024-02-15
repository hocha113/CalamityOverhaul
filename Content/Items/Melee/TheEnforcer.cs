using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 暴政
    /// </summary>
    internal class TheEnforcer : EctypeItem
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
                for (int i = 0; i < 3; i++) {
                    Vector2 realPlayerPos = new Vector2(player.position.X + (player.width * 0.5f) + (float)(Main.rand.Next(1358) * -(float)player.direction)
                        + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y);
                    realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-350, 351);
                    realPlayerPos.Y -= 100 * i;
                    Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, 0f, 0f, type, damage / 5, knockback, player.whoAmI, 0f, Main.rand.Next(3));
                }
            }
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return false;
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            var source = player.GetSource_ItemUse(Item);
            SoundEngine.PlaySound(SoundID.Item73, player.Center);
            int j = Main.myPlayer;
            float flameSpeed = 3f;
            player.itemTime = Item.useTime;
            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            float mouseXDist = Main.mouseX + Main.screenPosition.X + realPlayerPos.X;
            float mouseYDist = Main.mouseY + Main.screenPosition.Y + realPlayerPos.Y;
            if (player.gravDir == -1f) {
                mouseYDist = Main.screenPosition.Y + Main.screenHeight + Main.mouseY + realPlayerPos.Y;
            }
            float mouseDistance = (float)Math.Sqrt(mouseXDist * mouseXDist + mouseYDist * mouseYDist);
            if ((float.IsNaN(mouseXDist) && float.IsNaN(mouseYDist)) || (mouseXDist == 0f && mouseYDist == 0f)) {
                mouseXDist = player.direction;
                mouseYDist = 0f;
                mouseDistance = flameSpeed;
            }
            else {
                mouseDistance = flameSpeed / mouseDistance;
            }

            int essenceDamage = player.CalcIntDamage<MeleeDamageClass>(0.25f * Item.damage);
            for (int i = 0; i < 5; i++) {
                realPlayerPos = new Vector2(player.position.X + player.width * 0.5f + (Main.rand.Next(401) * -(float)player.direction) + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y);
                realPlayerPos.X = (realPlayerPos.X + player.Center.X) / 2f + Main.rand.Next(-400, 401);
                realPlayerPos.Y -= 100 * i;
                mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X;
                mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
                if (mouseYDist < 0f) {
                    mouseYDist *= -1f;
                }
                if (mouseYDist < 20f) {
                    mouseYDist = 20f;
                }
                mouseDistance = (float)Math.Sqrt(mouseXDist * mouseXDist + mouseYDist * mouseYDist);
                mouseDistance = flameSpeed / mouseDistance;
                Projectile.NewProjectile(source, realPlayerPos, Vector2.Zero, ModContent.ProjectileType<EssenceFlame2>(), essenceDamage, 0f, i, 0f, Main.rand.Next(3));
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3))
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, 173);
        }
    }
}
