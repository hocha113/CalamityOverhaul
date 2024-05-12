using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTerratomere : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Terratomere>();
        public override int ProtogenesisID => ModContent.ItemType<TerratomereEcType>();
        public override string TargetToolTipItemName => "TerratomereEcType";
        public override bool CanLoad() => false;//TODO:在当前版本暂时移除
        public override void SetDefaults(Item item) {
            item.width = 60;
            item.height = 66;
            item.damage = 185;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 21;
            item.useTime = 21;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTurn = true;
            item.knockBack = 7f;
            item.autoReuse = true;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.shoot = ModContent.ProjectileType<RTerratomereHoldoutProj>();
            item.shootSpeed = 60f;
            CWRUtils.EasySetLocalTextNameOverride(item, "TerratomereEcType");
        }

        public override bool? UseItem(Item item, Player player) {
            return player.ownedProjectileCounts[item.shoot] == 0;
        }
    }
}
