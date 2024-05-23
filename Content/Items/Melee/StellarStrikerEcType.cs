using CalamityMod;
using CalamityMod.Items;
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
    /// 月炎之锋
    /// </summary>
    internal class StellarStrikerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "StellarStriker";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 90;
            Item.height = 100;
            Item.scale = 1.5f;
            Item.damage = 480;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.knockBack = 7.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Red;
            Item.shoot = ProjectileID.LunarFlare;
            Item.shootSpeed = 12f;

        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool? UseItem(Player player) {
            Item.useAnimation = Item.useTime = 15;
            Item.scale = 1f;
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 20;
                Item.scale = 1.5f;
            }

            return base.UseItem(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            SoundEngine.PlaySound(SoundID.Item88, player.Center);
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<StellarStrikerBeam>()
                , damage / 3, knockback, Main.myPlayer, 0f, Main.rand.Next(3));
            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2)
                SpawnFlares(Item, player, Item.knockBack, Item.damage, hit.Crit);
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.whoAmI == Main.myPlayer && player.altFunctionUse == 2) {
                SpawnFlares(Item, player, Item.knockBack, Item.damage, true);
            }
        }

        public static void SpawnFlares(Item item, Player player, float knockback, int damage, bool crit) {
            IEntitySource source = player.GetSource_ItemUse(item);
            _ = SoundEngine.PlaySound(SoundID.Item88, player.Center);
            int i = Main.myPlayer;
            float cometSpeed = item.shootSpeed;
            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            float mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X;
            float mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
            if (player.gravDir == -1f) {
                mouseYDist = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - realPlayerPos.Y;
            }
            float mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
            if ((float.IsNaN(mouseXDist) && float.IsNaN(mouseYDist)) || (mouseXDist == 0f && mouseYDist == 0f)) {
                _ = (float)player.direction;
            }
            else {
                _ = cometSpeed / mouseDistance;
            }

            if (crit) {
                damage /= 2;
            }

            for (int j = 0; j < 2; j++) {
                realPlayerPos = new Vector2(player.Center.X + (float)(Main.rand.Next(201) * -(float)player.direction)
                    + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
                realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-200, 201);
                realPlayerPos.Y -= 100 * j;
                mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X + (Main.rand.Next(-40, 41) * 0.03f);
                mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
                if (mouseYDist < 0f) {
                    mouseYDist *= -1f;
                }
                if (mouseYDist < 20f) {
                    mouseYDist = 20f;
                }
                mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
                mouseDistance = cometSpeed / mouseDistance;
                mouseXDist *= mouseDistance;
                mouseYDist *= mouseDistance;
                float speedX = mouseXDist;
                float speedY = mouseYDist + (Main.rand.Next(-80, 81) * 0.02f);
                int proj = Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, speedX, speedY
                    , ProjectileID.LunarFlare, (int)(damage * 0.5), knockback, i, 0f, Main.rand.Next(3));
                if (proj.WithinBounds(Main.maxProjectiles)) {
                    Main.projectile[proj].DamageType = DamageClass.Melee;
                }
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                _ = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Vortex);
            }
        }
    }
}
