using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Magic;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 死亡冰雹
    /// </summary>
    internal class DeathhailStaff : EctypeItem
    {
        public new string LocalizationCategory => "Items.Weapons.Magic";

        public override string Texture => CWRConstant.Cay_Wap_Magic + "DeathhailStaff";

        public override void SetStaticDefaults() {
            Item.staff[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 328;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 16;
            Item.width = 80;
            Item.height = 84;
            Item.useTime = 11;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 4f;
            Item.value = CalamityGlobalItem.Rarity14BuyPrice;
            Item.rare = ModContent.RarityType<DarkBlue>();
            Item.UseSound = SoundID.Item12;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DeathhailBeam>();
            Item.shootSpeed = 18f;
            
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Magic/DeathhailStaffGlow").Value);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int myPlayer = Main.myPlayer;
            float shootSpeed = Item.shootSpeed;
            float baseKnockback = knockback;
            baseKnockback = player.GetWeaponKnockback(Item, baseKnockback);
            player.itemTime = Item.useTime;
            Vector2 vector = player.RotatedRelativePoint(player.MountedCenter, reverseRotation: true);
            _ = Main.MouseWorld - vector;
            float num = Main.mouseX + Main.screenPosition.X - vector.X;
            float num2 = Main.mouseY + Main.screenPosition.Y - vector.Y;
            if (player.gravDir == -1f) {
                num2 = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - vector.Y;
            }

            float num3 = (float)Math.Sqrt(num * num + num2 * num2);
            if ((float.IsNaN(num) && float.IsNaN(num2)) || (num == 0f && num2 == 0f)) {
                num = player.direction;
                num2 = 0f;
                num3 = shootSpeed;
            }
            else {
                num3 = shootSpeed / num3;
            }

            int num4 = 2;
            for (int i = 0; i < num4; i++) {
                vector = new Vector2(player.position.X + player.width * 0.5f + Main.rand.Next(91) * (0f - player.direction) + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
                vector.X = (vector.X + player.Center.X) / 2f + Main.rand.Next(-200, 201);
                vector.Y -= 100 * i;
                num = Main.mouseX + Main.screenPosition.X - vector.X;
                num2 = Main.mouseY + Main.screenPosition.Y - vector.Y;
                if (num2 < 0f) {
                    num2 *= -1f;
                }

                if (num2 < 20f) {
                    num2 = 20f;
                }

                num3 = (float)Math.Sqrt(num * num + num2 * num2);
                num3 = shootSpeed / num3;
                num *= num3;
                num2 *= num3;
                float speedX = num + Main.rand.Next(-50, 51) * 0.02f;
                float speedY = num2 + Main.rand.Next(-50, 51) * 0.02f;
                Projectile.NewProjectile(source, vector.X, vector.Y, speedX, speedY, type, damage, baseKnockback, myPlayer);
            }

            return false;
        }
    }
}
