using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 罗马短剑
    /// </summary>
    internal class RGladius : BaseRItem
    {
        public override int TargetID => ItemID.Gladius;
        public override bool IsVanilla => true;
        public override string TargetToolTipItemName => "Wap_Gladius_Text";
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<GladiusRapier>();
            item.damage = 10;
            item.useTime = 45;
            item.useAnimation = 45;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 1.5f;
            item.shootSpeed = 5f;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.channel = true;
        }

        public override bool? On_CanUseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] <= 0;
        }
    }
}
