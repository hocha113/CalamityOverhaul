using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAftershock : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Aftershock>();
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback)
            => ShootFunc(player, source, position, velocity, type, damage, knockback);
        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) => false;
        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) => false;

        internal static void SetDefaultsFunc(Item Item) {
            Item.damage = 65;
            Item.DamageType = DamageClass.Melee;
            Item.width = 54;
            Item.height = 58;
            Item.useTime = 28;
            Item.useAnimation = 25;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 7.5f;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MeleeFossilShard>();
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<AftershockHeld>();
        }

        public static bool ShootFunc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.GetItem().GiveMeleeType();
            if (player.altFunctionUse == 2) {
                player.GetItem().GiveMeleeType(true);
                Projectile.NewProjectile(source, position, velocity, type, (int)(damage * 1.25f), knockback, player.whoAmI, 1);
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
