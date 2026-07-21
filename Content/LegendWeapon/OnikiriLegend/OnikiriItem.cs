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
    /// 鬼切太刀,按住左键绯红裂空连段;
    /// 里世界点中真身/媒介走肢解居合(<see cref="OnikiriPlayer.TryClickDismember"/>),落空回退连段
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
            Item.channel = true;   //控制器按住循环,物品只触发首拍
            Item.UseSound = null;
            Item.shoot = ModContent.ProjectileType<CrimsonRendSlash>();
            Item.shootSpeed = 1f;
            Item.rare = CWRID.Rarity_BurnishedAuric > 0 ? CWRID.Rarity_BurnishedAuric : ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 25, 0, 0);
            OnikiriOverride.SetDefaultsFunc(Item);
        }

        /// <summary>连段/肢解在场时封锁再用</summary>
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<CrimsonRendSlash>()] == 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] == 0;

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.Onikiri_Execute, noneTip: CWRKeySystem.Notbound.Value);
            OnikiriOverride.SetTooltip(Item, ref tooltips);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            OnikiriPlayer okp = player.GetModPlayer<OnikiriPlayer>();
            //里世界按下沿→肢解居合
            if (okp.TryClickDismember(Item)) {
                return false;
            }
            //追斩窗按下沿→残心斩
            if (okp.TryZanshinStrike(Item, edgeVerified: false)) {
                return false;
            }
            float bladeScale = OnikiriOverride.GetBladeScale(Item);
            CrimsonRendSlash.Fire(player, player.Center, velocity, damage, knockback, scale: bladeScale, source);
            return false;
        }
    }
}
