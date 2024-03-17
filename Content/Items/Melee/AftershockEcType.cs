using CalamityMod;
using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 地炎阔刃
    /// </summary>
    internal class AftershockEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Aftershock";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void SetDefaults() {
            Item.damage = 65;
            Item.DamageType = DamageClass.Melee;
            Item.width = 54;
            Item.height = 58;
            Item.useTime = 28;
            Item.useAnimation = 25;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MeleeFossilShard>();
            Item.shootSpeed = 12f;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            Item.useTime = Item.useAnimation = 25;
            Item.scale = 1;
            if (player.altFunctionUse == 2) {
                Item.useTime = Item.useAnimation = 28;
                Item.scale = 1.5f;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                for (int i = 0; i < 3; i++) {
                    Vector2 pos = position;
                    pos.X += Main.rand.Next(-120, 120);
                    pos.Y += Main.rand.Next(-330, -303);
                    Vector2 vr = new(Main.rand.NextFloat(-0.1f, 0.1f) + (player.velocity.X * 0.1f), Main.rand.Next(-13, 7));
                    int proj = Projectile.NewProjectile(source, pos, vr, type, damage / 2, knockback);
                    Main.projectile[proj].scale = Main.rand.NextFloat(1, 1.5f);
                }
                return false;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vr = velocity + new Vector2(Math.Sign(velocity.X) * Main.rand.NextFloat(1, 7.2f), Main.rand.NextFloat(-6.3f, 0));
                int proj = Projectile.NewProjectile(source, position, vr, type, damage / 3, knockback);
                Main.projectile[proj].scale = Main.rand.NextFloat(1, 1.25f);
            }
            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 300);
                OnHitSpanProjFunc(Item, player, hit.Knockback);
            }

        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2) {
                target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 100);
                OnHitSpanProjFunc(Item, player, hurtInfo.Knockback);
            }
        }

        public static void OnHitSpanProjFunc(Item Item, Player player, float Knockback) {
            IEntitySource source = player.GetSource_ItemUse(Item);
            float rockSpeed = Item.shootSpeed;
            Vector2 realPlayerPos = player.RotatedRelativePoint(player.MountedCenter, true);
            float mouseXDist = Main.mouseX - Main.screenPosition.X - realPlayerPos.X;
            float mouseYDist = Main.mouseY - Main.screenPosition.Y - realPlayerPos.Y;
            if (player.gravDir == -1f) {
                mouseYDist = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - realPlayerPos.Y;
            }
            float mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
            if ((float.IsNaN(mouseXDist) && float.IsNaN(mouseYDist)) || (mouseXDist == 0f && mouseYDist == 0f)) {
                _ = (float)player.direction;
            }
            else {
                _ = rockSpeed / mouseDistance;
            }

            realPlayerPos = new Vector2(player.position.X + (player.width * 0.5f) + (float)(Main.rand.Next(201) * -(float)player.direction) + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
            realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-200, 201);
            realPlayerPos.Y -= 100;
            mouseXDist = Main.mouseX + Main.screenPosition.X - realPlayerPos.X;
            mouseYDist = Main.mouseY + Main.screenPosition.Y - realPlayerPos.Y;
            if (mouseYDist < 0f) {
                mouseYDist *= -1f;
            }
            if (mouseYDist < 20f) {
                mouseYDist = 20f;
            }
            mouseDistance = (float)Math.Sqrt((double)((mouseXDist * mouseXDist) + (mouseYDist * mouseYDist)));
            mouseDistance = rockSpeed / mouseDistance;
            mouseXDist *= mouseDistance;
            mouseYDist *= mouseDistance;
            float speedX4 = mouseXDist;
            float speedY5 = mouseYDist + (Main.rand.Next(-10, 11) * 0.02f);
            int rockDamage = player.CalcIntDamage<MeleeDamageClass>(Item.damage);
            _ = Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, speedX4, speedY5, ModContent.ProjectileType<AftershockRock>(), rockDamage, Knockback, player.whoAmI, 0f, Main.rand.Next(10));
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(5)) {
                _ = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Sand);
            }
        }
    }
}
