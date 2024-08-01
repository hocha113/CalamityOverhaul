using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAftershock : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Aftershock>();
        public override int ProtogenesisID => ModContent.ItemType<AftershockEcType>();
        public override string TargetToolTipItemName => "AftershockEcType";
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) => AftershockEcType.SetDefaultsFunc(item);
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback)
            => AftershockEcType.ShootFunc(player, source, position, velocity, type, damage, knockback);
        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) => false;
        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) => false;
    }
}
