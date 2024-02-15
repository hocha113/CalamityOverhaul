using CalamityMod.Items;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 天神之剑
    /// </summary>
    internal class CelestialClaymore : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CelestialClaymore";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 82;
            Item.damage = 85;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 19;
            Item.useTime = 19;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 5.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.Rarity4BuyPrice;
            Item.rare = ItemRarityID.LightRed;
            Item.shoot = ModContent.ProjectileType<CosmicSpiritBombs>();
            Item.shootSpeed = 0.1f;
            
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
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

        public override void MeleeEffects(Player player, Rectangle hitbox) {
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
