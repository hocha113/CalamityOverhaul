using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAegisBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AegisBlade>();
        public override int ProtogenesisID => ModContent.ItemType<AegisBladeEcType>();
        public override string TargetToolTipItemName => "AegisBladeEcType";
        private int Level;
        public override void SetDefaults(Item item) => AegisBladeEcType.SetDefaultsFunc(item);
        public override bool? On_CanUseItem(Item item, Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<AegisBladeGuardian>()] == 0;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return AegisBladeEcType.ShootFunc(ref Level, player, source, position, velocity, type, damage, knockback);
        }
    }
}
