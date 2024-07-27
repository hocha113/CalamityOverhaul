using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RHolyCollider : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<HolyCollider>();
        public override int ProtogenesisID => ModContent.ItemType<HolyColliderEcType>();
        public override string TargetToolTipItemName => "HolyColliderEcType";
        private int Level;
        public override void SetDefaults(Item item) => HolyColliderEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return HolyColliderEcType.ShootFunc(ref Level, item, player, source, position, velocity, type, damage, knockback);
        }
    }
}
