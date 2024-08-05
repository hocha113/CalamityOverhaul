using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 庇护之刃
    /// </summary>
    internal class AegisBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AegisBlade";

        private int Level;
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 72;
            Item.height = 72;
            Item.scale = 0.9f;
            Item.damage = 72;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.SetKnifeHeld<AegisBladeHeld>();
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<AegisBladeProj>()] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return ShootFunc(ref Level, player, source, position, velocity, type, damage, knockback);
        }

        public static bool ShootFunc(ref int Level, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(SoundID.Item69 with { Pitch = 0.76f }, player.Center);
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<AegisBladeProj>(), damage, knockback, player.whoAmI);
                return false;
            }
            int num = 0;
            if (++Level > 6) {
                num = Level - 6;
                if (Level > 9) {
                    Level = 0;
                }
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, num);
            return false;
        }
    }
}
