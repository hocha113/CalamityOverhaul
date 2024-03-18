using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 翡翠之潮
    /// </summary>
    internal class GreentideEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Greentide";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 95;
            Item.DamageType = DamageClass.Melee;
            Item.width = 62;
            Item.height = 62;
            Item.scale = 1.5f;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7;
            Item.value = CalamityGlobalItem.Rarity7BuyPrice;
            Item.rare = ItemRarityID.Lime;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GreenWater>();
            Item.shootSpeed = 18f;
            
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            Item.useAnimation = Item.useTime = 20;
            Item.scale = 1f;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 24;
                Item.scale = 1.5f;
            }

            return base.UseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(source, Main.MouseWorld + new Vector2(Main.rand.Next(-12, 12), Main.rand.Next(322, 382))
                    , new Vector2(Main.rand.NextFloat(-2, 2), Main.rand.Next(-19, -16)), type
                , damage / 2, knockback, Main.myPlayer, 0f, Main.rand.Next(3));
            }
            return false;
        }

        public static void OnHitSpanProj(Item item, Player player, float knockback) {
            Terraria.DataStructures.IEntitySource source = player.GetSource_ItemUse(item);
            int i = Main.myPlayer;
            float projSpeed = item.shootSpeed;
            float playerKnockback = knockback;
            playerKnockback = player.GetWeaponKnockback(item, playerKnockback);
            player.itemTime = item.useTime;
            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            float mouseXDist = Main.mouseX - Main.screenPosition.X - realPlayerPos.X;
            float mouseYDist = Main.mouseY - Main.screenPosition.Y - realPlayerPos.Y;
            if (player.gravDir == -1f) {
                mouseYDist = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - realPlayerPos.Y;
            }
            float mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
            if ((float.IsNaN(mouseXDist) && float.IsNaN(mouseYDist)) || (mouseXDist == 0f && mouseYDist == 0f)) {
                mouseXDist = player.direction;
            }
            else {
                mouseDistance = projSpeed / mouseDistance;
            }

            for (int j = 0; j < 3; j++) {
                realPlayerPos = new Vector2(player.position.X + (player.width * 0.5f) + (float)(Main.rand.Next(201) * -(float)player.direction) + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
                realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-200, 201);
                realPlayerPos.Y -= 100 * j;
                mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X;
                mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
                if (mouseYDist < 0f) {
                    mouseYDist *= -1f;
                }
                if (mouseYDist < 20f) {
                    mouseYDist = 20f;
                }
                mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
                mouseDistance = projSpeed / mouseDistance;
                mouseXDist *= mouseDistance;
                mouseYDist *= mouseDistance;
                float speedX4 = mouseXDist;
                float speedY5 = mouseYDist + (Main.rand.Next(-180, 181) * 0.02f);
                int greenWaterDamage = player.CalcIntDamage<MeleeDamageClass>(item.damage);
                _ = Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, speedX4, speedY5, ModContent.ProjectileType<GreenWater>(), greenWaterDamage, playerKnockback, i, 0f, Main.rand.Next(10));
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2)
                OnHitSpanProj(Item, player, hit.Knockback);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2)
                OnHitSpanProj(Item, player, hurtInfo.Knockback);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            int randomDust = Main.rand.Next(2);
            randomDust = randomDust == 0 ? 33 : 89;
            if (Main.rand.NextBool(4)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, randomDust);
            }
        }
    }
}
