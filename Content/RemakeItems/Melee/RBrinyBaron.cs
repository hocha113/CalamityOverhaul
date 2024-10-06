using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBrinyBaron : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<BrinyBaron>();
        public override int ProtogenesisID => ModContent.ItemType<BrinyBaronEcType>();
        public override string TargetToolTipItemName => "BrinyBaronEcType";
        private int Level;
        public override void SetDefaults(Item item) => BrinyBaronEcType.SetDefaultsFunc(item);
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) 
            => BrinyBaronEcType.ShootFunc(ref Level, player, source, position, velocity, type, damage, knockback);
        public override bool? On_AltFunctionUse(Item item, Player player) => false;
        public override bool On_ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) => false;
        public override bool? On_CanUseItem(Item item, Player player) => true;
        public override bool? On_UseAnimation(Item item, Player player) => false;
    }
}
