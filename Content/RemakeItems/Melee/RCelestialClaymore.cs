using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCelestialClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.CelestialClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<CelestialClaymoreEcType>();
        public override string TargetToolTipItemName => "CelestialClaymoreEcType";

        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.width = 80;
            item.height = 82;
            item.damage = 85;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 19;
            item.useTime = 19;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 5.25f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.Rarity4BuyPrice;
            item.rare = ItemRarityID.LightRed;
            item.shoot = ModContent.ProjectileType<CosmicSpiritBombs>();
            item.shootSpeed = 0.1f;
            CWRUtils.EasySetLocalTextNameOverride(item, "CelestialClaymore");
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            _ = player.RotatedRelativePoint(player.MountedCenter, true);
            for (int i = 0; i < 3; i++) {
                Vector2 realPlayerPos = new Vector2(player.position.X + (player.width * 0.5f) + (float)(Main.rand.Next(201) * -(float)player.direction)
                    + (Main.mouseX + Main.screenPosition.X - player.position.X), player.MountedCenter.Y);
                realPlayerPos.X = ((realPlayerPos.X + player.Center.X) / 2f) + Main.rand.Next(-200, 201);
                realPlayerPos.Y -= 100 * i;
                int proj = Projectile.NewProjectile(source, realPlayerPos.X, realPlayerPos.Y, 0f, 0f, type, damage / 2, knockback, player.whoAmI, 0f, Main.rand.Next(3));
                CosmicSpiritBombs cosmicSpiritBombs = Main.projectile[proj].ModProjectile as CosmicSpiritBombs;
                cosmicSpiritBombs.overTextIndex = Main.rand.Next(1, 4);
            }
            return false;
        }

        public override void MeleeEffects(Item item, Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(4)) {
                int dustType = Main.rand.Next(2);
                if (dustType == 0) {
                    dustType = 15;
                }
                else {
                    dustType = dustType == 1 ? 73 : 244;
                }
                int swingDust = Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, dustType, player.direction * 2, 0f, 150, default, 1.3f);
                Main.dust[swingDust].velocity *= 0.2f;
                Vector2 toMou = player.Center.To(Main.MouseWorld).UnitVector();
                foreach (Projectile proj in Main.projectile) {
                    if (proj.type == ModContent.ProjectileType<CosmicSpiritBombs>()) {
                        if (proj.Hitbox.Intersects(hitbox)) {
                            proj.ai[0] += 1;
                            proj.velocity += toMou * (6);
                            proj.timeLeft = 150;
                            proj.damage = proj.originalDamage / 2;
                            proj.netUpdate = true;
                        }
                    }
                }
            }
        }
    }
}
