using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class REntropicClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<EntropicClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<EntropicClaymoreEcType>();
        public override string TargetToolTipItemName => "EntropicClaymoreEcType";
        public override void SetDefaults(Item item) => EntropicClaymoreEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
