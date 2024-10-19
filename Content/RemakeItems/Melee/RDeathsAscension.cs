using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RDeathsAscension : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<DeathsAscension>();
        public override int ProtogenesisID => ModContent.ItemType<DeathsAscensionEcType>();
        public override string TargetToolTipItemName => "DeathsAscensionEcType";
        private int swingIndex = 0;
        public override void SetDefaults(Item item) {
            swingIndex = 0;
            item.UseSound = null;
            item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            item.SetKnifeHeld<DeathsAscensionHeld>();
        }

        public override bool? On_UseItem(Item item, Player player) => true;

        public override bool? On_CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DeathsAscensionThrowable>()] <= 0;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position
            , ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => DeathsAscensionEcType.ModifyShootStatsFunc(ref swingIndex, item, player
                , ref position, ref velocity, ref type, ref damage, ref knockback);

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => DeathsAscensionEcType.ShootFunc(ref swingIndex, item, player, source, position
                , velocity, type, damage, knockback);
    }
}
