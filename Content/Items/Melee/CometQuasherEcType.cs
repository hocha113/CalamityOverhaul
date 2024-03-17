using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 彗星陨刃
    /// </summary>
    internal class CometQuasherEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CometQuasher";

        public override void SetDefaults() {
            Item.width = 46;
            Item.height = 62;
            Item.scale = 1.5f;
            Item.damage = 160;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 28;
            Item.useStyle = 1;
            Item.useTime = 28;
            Item.useTurn = true;
            Item.knockBack = 7.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = 8;
            Item.shoot = ModContent.ProjectileType<CometQuasherMeteor>();
            Item.shootSpeed = 9f;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            float num116 = base.Item.shootSpeed;
            Vector2 vector2 = player.RotatedRelativePoint(player.MountedCenter, reverseRotation: true);
            float num117 = Main.mouseX + Main.screenPosition.X - vector2.X;
            float num118 = Main.mouseY + Main.screenPosition.Y - vector2.Y;
            if (player.gravDir == -1f) {
                num118 = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - vector2.Y;
            }
            float num119 = (float)Math.Sqrt(num117 * num117 + num118 * num118);
            if ((float.IsNaN(num117) && float.IsNaN(num118)) || (num117 == 0f && num118 == 0f)) {
                num117 = player.direction;
                num118 = 0f;
                num119 = num116;
            }
            else {
                num119 = num116 / num119;
            }
            for (int num113 = 0; num113 < 2; num113++) {
                vector2 = new Vector2(player.position.X + player.width * 0.5f + Main.rand.Next(201) * (0f - player.direction) + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
                vector2.X = (vector2.X + player.Center.X) / 2f + Main.rand.Next(-200, 201);
                vector2.Y -= 100 * num113;
                num117 = Main.mouseX + Main.screenPosition.X - vector2.X + Main.rand.Next(-40, 41) * 0.03f;
                num118 = Main.mouseY + Main.screenPosition.Y - vector2.Y;
                if (num118 < 0f) {
                    num118 *= -1f;
                }
                if (num118 < 20f) {
                    num118 = 20f;
                }
                num119 = (float)Math.Sqrt(num117 * num117 + num118 * num118);
                num119 = num116 / num119;
                num117 *= num119;
                num118 *= num119;
                float num114 = num117;
                float num115 = num118 + Main.rand.Next(-40, 41) * 0.02f;
                int proj = Projectile.NewProjectile(source, vector2.X, vector2.Y, num114 * 0.75f, num115 * 0.75f, ModContent.ProjectileType<CometQuasherMeteor>(), (int)(Item.damage * 0.25f), Item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
                Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            }
            damage = (int)(Item.damage * 0.4f);
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
                Vector2 spanPos = target.Center + offsetVr;
                Vector2 vr = offsetVr.UnitVector() * -17;
                int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos, vr, ModContent.ProjectileType<CometQuasherMeteor>(), (int)((float)base.Item.damage), base.Item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
                Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            IEntitySource source = player.GetSource_ItemUse(base.Item);
            float num116 = base.Item.shootSpeed;
            Vector2 vector2 = player.RotatedRelativePoint(player.MountedCenter, reverseRotation: true);
            float num117 = Main.mouseX + Main.screenPosition.X - vector2.X;
            float num118 = Main.mouseY + Main.screenPosition.Y - vector2.Y;
            if (player.gravDir == -1f) {
                num118 = Main.screenPosition.Y + Main.screenHeight - Main.mouseY - vector2.Y;
            }
            float num119 = (float)Math.Sqrt(num117 * num117 + num118 * num118);
            if ((float.IsNaN(num117) && float.IsNaN(num118)) || (num117 == 0f && num118 == 0f)) {
                num117 = player.direction;
                num118 = 0f;
                num119 = num116;
            }
            else {
                num119 = num116 / num119;
            }
            for (int num113 = 0; num113 < 2; num113++) {
                vector2 = new Vector2(player.position.X + player.width * 0.5f + Main.rand.Next(201) * (0f - player.direction) + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
                vector2.X = (vector2.X + player.Center.X) / 2f + Main.rand.Next(-200, 201);
                vector2.Y -= 100 * num113;
                num117 = Main.mouseX + Main.screenPosition.X - vector2.X + Main.rand.Next(-40, 41) * 0.03f;
                num118 = Main.mouseY + Main.screenPosition.Y - vector2.Y;
                if (num118 < 0f) {
                    num118 *= -1f;
                }
                if (num118 < 20f) {
                    num118 = 20f;
                }
                num119 = (float)Math.Sqrt(num117 * num117 + num118 * num118);
                num119 = num116 / num119;
                num117 *= num119;
                num118 *= num119;
                float num114 = num117;
                float num115 = num118 + Main.rand.Next(-40, 41) * 0.02f;
                int proj = Projectile.NewProjectile(source, vector2.X, vector2.Y, num114 * 0.75f, num115 * 0.75f, ModContent.ProjectileType<CometQuasherMeteor>(), (int)(Item.damage * 0.3f), base.Item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
                Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            }
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, 6);
            }
        }
    }
}
