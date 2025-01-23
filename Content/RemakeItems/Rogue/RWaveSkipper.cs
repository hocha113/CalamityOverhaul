using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Rogue;
using CalamityMod.Projectiles.Rogue;
using CalamityOverhaul.Content.Items.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Rogue
{
    internal class RWaveSkipper : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<WaveSkipper>();
        public override int ProtogenesisID => ModContent.ItemType<WaveSkipperEcType>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<WaveSkipper>()] = true;
        }
        public override string TargetToolTipItemName => "WaveSkipperEcType";

        public override void SetDefaults(Item item) {
            item.width = 44;
            item.damage = 70;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.useAnimation = item.useTime = 22;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 4f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 44;
            item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            item.rare = ItemRarityID.Pink;
            item.shoot = ModContent.ProjectileType<RWaveSkipperProjectile>();
            item.shootSpeed = 12f;
            item.DamageType = CWRLoad.RogueDamageClass;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (CWRMod.Instance.betterWaveSkipper != null) {//如果添加了"BetterWaveSkipper"，那么不会执行这个Shoot覆盖行为
                return null;
            }
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
                    int stealth = Projectile.NewProjectile(source, position, velocity.RotatedBy(-0.05f), ModContent.ProjectileType<RWaveSkipperProjectile>(), damage, knockback, player.whoAmI);
                    if (stealth.WithinBounds(Main.maxProjectiles))
                        Main.projectile[stealth].Calamity().stealthStrike = true;
                    int stealth2 = Projectile.NewProjectile(source, position, velocity.RotatedBy(0.05f), ModContent.ProjectileType<RWaveSkipperProjectile>(), damage, knockback, player.whoAmI);
                    if (stealth2.WithinBounds(Main.maxProjectiles))
                        Main.projectile[stealth2].Calamity().stealthStrike = true;
                    return false;
                }
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<RWaveSkipperProjectile>(), damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
