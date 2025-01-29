using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBrinyBaron : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<BrinyBaron>();
        private int Level;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(ref Level, player, source, position, velocity, type, damage, knockback);
        public override bool? On_AltFunctionUse(Item item, Player player) => false;
        public override bool On_ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) => false;
        public override bool? On_CanUseItem(Item item, Player player) => true;
        public override bool? On_UseAnimation(Item item, Player player) => false;

        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 110;
            Item.knockBack = 2f;
            Item.useAnimation = Item.useTime = 20;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.shootSpeed = 4f;
            Item.shoot = ModContent.ProjectileType<Razorwind>();
            Item.width = 100;
            Item.height = 102;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.SetKnifeHeld<BrinyBaronHeld>();
        }

        public static bool ShootFunc(ref int Level, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int newLevel = 0;
            if (++Level > 6) {
                newLevel = Level - 6;
                if (Level > 8) {
                    Level = 0;
                }
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, newLevel);
            return false;
        }
    }
}
