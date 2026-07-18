using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>鬼切：传奇太刀，按住左键驱动绯红裂空斩滚动五段连段（轻点出快斩，按住循环，随时转向）</summary>
    internal class OnikiriItem : ModItem
    {
        public override void SetStaticDefaults() {
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 90;
            Item.height = 96;
            Item.damage = 420;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.knockBack = 6.5f;
            Item.crit = 8;
            Item.useAnimation = Item.useTime = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.channel = true;   //连段由控制器按住循环驱动，物品只负责首次触发
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<CrimsonRendSlash>();
            Item.shootSpeed = 1f;
            Item.rare = CWRID.Rarity_BurnishedAuric > 0 ? CWRID.Rarity_BurnishedAuric : ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 25, 0, 0);
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[ModContent.ProjectileType<CrimsonRendSlash>()] == 0;

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //把说明里的 [KEY] 占位符替换为玩家实际绑定的处决键
            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_R, noneTip: CWRKeySystem.Notbound.Value);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            CrimsonRendSlash.Fire(player, player.Center, velocity, damage, knockback, scale: 1f, source);
            return false;
        }
    }
}
