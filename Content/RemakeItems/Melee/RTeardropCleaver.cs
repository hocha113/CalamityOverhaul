using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTeardropCleaver : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.TeardropCleaver>();
        public override int ProtogenesisID => ModContent.ItemType<TeardropCleaverEcType>();
        public override string TargetToolTipItemName => "TeardropCleaverEcType";

        public override void SetDefaults(Item item) {
            item.width = 56;
            item.damage = 25;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 24;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 24;
            item.useTurn = true;
            item.knockBack = 5.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 66;
            item.value = CalamityGlobalItem.Rarity2BuyPrice;
            item.rare = ItemRarityID.Green;
            item.shoot = ModContent.ProjectileType<TeardropCleaverProj>();
            item.shootSpeed = 1;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position + velocity * 100, velocity, type, damage / 2, knockback, player.whoAmI);
            return false;
        }
    }
}
