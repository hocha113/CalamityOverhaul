using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RMirrorBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<MirrorBlade>();
        public override int ProtogenesisID => ModContent.ItemType<MirrorBladeEcType>();
        public override string TargetToolTipItemName => "MirrorBladeEcType";
        public override void SetDefaults(Item item) {
            item.shoot = ModContent.ProjectileType<MirrorBladeRapier>();
            item.useTime = 30;
            item.useAnimation = 30;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 3.5f;
            item.shootSpeed = 5f;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.channel = true;
        }
        public override bool? On_CanUseItem(Item item, Player player)
            => player.ownedProjectileCounts[item.shoot] <= 0;
    }
}
