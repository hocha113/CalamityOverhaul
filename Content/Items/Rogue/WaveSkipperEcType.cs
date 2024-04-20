using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Rogue
{
    [LegacyName("DuneHopper")]
    public class WaveSkipperEcType : EctypeItem
    {
        public override string Texture => "CalamityMod/Items/Weapons/Rogue/WaveSkipper";
        public static int SpreadAngle = 8;
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 70;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = Item.useTime = 22;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 44;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<RWaveSkipperProjectile>();
            Item.shootSpeed = 12f;
            Item.DamageType = ModContent.GetInstance<RogueDamageClass>();
            
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                if (player.Calamity().StealthStrikeAvailable()) {
                    for (int i = 0; i < 7; i++) {
                        Vector2 spanPos = position + new Vector2(Main.rand.Next(-160, 160), Main.rand.Next(-560, -500));
                        Vector2 vr = spanPos.To(Main.MouseWorld).UnitVector().RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * Main.rand.Next(13, 17);
                        int stealth = Projectile.NewProjectile(source, spanPos, vr, ModContent.ProjectileType<WaveSkipperProjectile>(), damage, knockback, player.whoAmI);
                        if (stealth.WithinBounds(Main.maxProjectiles)) {
                            Main.projectile[stealth].Calamity().stealthStrike = true;
                            Main.projectile[stealth].tileCollide = false;
                            Main.projectile[stealth].MaxUpdates = 3;
                        }

                    }
                    return false;
                }
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<WaveSkipperProjectile>(), damage, knockback, player.whoAmI);
            }
            else {
                if (player.Calamity().StealthStrikeAvailable()) {
                    for (int i = -SpreadAngle; i < SpreadAngle * 2; i += SpreadAngle) {
                        Vector2 spreadVelocity = player.SafeDirectionTo(Main.MouseWorld).RotatedBy(MathHelper.ToRadians(i)) * Item.shootSpeed;
                        int stealth = Projectile.NewProjectile(source, position, spreadVelocity, ModContent.ProjectileType<RWaveSkipperProjectile>(), damage, knockback, player.whoAmI);
                        if (stealth.WithinBounds(Main.maxProjectiles))
                            Main.projectile[stealth].Calamity().stealthStrike = true;
                    }
                    return false;
                }
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RWaveSkipperProjectile>(), damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
