using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切：传奇太刀，按住左键驱动绯红裂空斩滚动五段连段（轻点出快斩，按住循环，随时转向）。<br/>
    /// 里世界中左键是另一套语言：点中真身/媒介的那一击化为肢解居合
    /// （<see cref="OnikiriPlayer.TryClickDismember"/>，领域翻转即模式切换），落空回退连段
    /// </summary>
    internal class OnikiriItem : ModItem
    {
        public override void SetStaticDefaults() {
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 90;
            Item.height = 96;
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
            OnikiriOverride.SetDefaultsFunc(Item);
        }

        /// <summary>连段控制器在场时由它自驱排拍；肢解居合演出期同样封锁再使用，防按住把残心踩掉</summary>
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CrimsonRendSlash>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] == 0;

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            //把说明里的 [KEY] 占位符替换为玩家实际绑定的处决键
            tooltips.InsertHotkeyBinding(CWRKeySystem.Onikiri_Execute, noneTip: CWRKeySystem.Notbound.Value);
            OnikiriOverride.SetTooltip(Item, ref tooltips);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            OnikiriPlayer okp = player.GetModPlayer<OnikiriPlayer>();
            //里世界按下沿：点中媒介/真身的这一击化为肢解居合，落空回退连段
            if (okp.TryClickDismember(Item)) {
                return false;
            }
            //追斩窗内的按下沿：普攻化为残心斩(边沿由 OnikiriPlayer 自持鉴别,按住穿过不转换)
            if (okp.TryZanshinStrike(Item, edgeVerified: false)) {
                return false;
            }
            float bladeScale = OnikiriOverride.GetBladeScale(Item);
            CrimsonRendSlash.Fire(player, player.Center, velocity, damage, knockback, scale: bladeScale, source);
            return false;
        }
    }
}
