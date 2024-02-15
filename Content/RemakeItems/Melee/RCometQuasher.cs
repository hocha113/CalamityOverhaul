using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCometQuasher : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.CometQuasher>();
        public override int ProtogenesisID => ModContent.ItemType<CometQuasher>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.width = 46;
            item.height = 62;
            item.scale = 1.5f;
            item.damage = 160;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 28;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 28;
            item.useTurn = true;
            item.knockBack = 7.75f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.Rarity8BuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.shoot = ModContent.ProjectileType<CometQuasherMeteor>();
            item.shootSpeed = 9f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            float num116 = item.shootSpeed;
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
                vector2 = new Vector2(player.position.X + player.width * 0.5f + Main.rand.Next(201) * (0f - player.direction)
                    + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y - 600f);
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
                int proj = Projectile.NewProjectile(source, vector2.X, vector2.Y, num114 * 0.75f, num115 * 0.75f
                    , ModContent.ProjectileType<CometQuasherMeteor>(), (int)(item.damage * 0.25f), item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
                Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            }
            damage = (int)(item.damage * 0.4f);
            return null;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (CWRUtils.RemakeByItem<CalamityMod.Items.Weapons.Melee.CometQuasher>(item)) {
                CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "CometQuasher");
            }
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
                Vector2 spanPos = target.Center + offsetVr;
                Vector2 vr = offsetVr.UnitVector() * -17;
                int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos, vr
                    , ModContent.ProjectileType<CometQuasherMeteor>(), (int)((float)item.damage), item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
                Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            }
        }
    }
}
