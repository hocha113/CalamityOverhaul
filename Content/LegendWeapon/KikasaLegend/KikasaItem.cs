using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend
{
    /// <summary>
    /// 传奇武器·鬼伞。第一能力模块：血湖领域——持伞按
    /// <see cref="Common.CWRKeySystem.Legend_Domain"/> 开阖，
    /// 输入与状态机在 <see cref="KikasaDomains.KikasaDomainPlayer"/>；
    /// 领域含鬼雨异化表里形态——按 <see cref="Common.CWRKeySystem.Kikasa_DomainMutate"/>
    /// （默认中键）血湖沸腾倒转切换血/雨形态。
    /// 第二能力模块：湖藏——领域中持物按 <see cref="Common.CWRKeySystem.Kikasa_Sink"/>
    /// 沉物入湖存储，持伞按 <see cref="Common.CWRKeySystem.Legend_UIControl"/> 开湖窗提取；
    /// 数据与输入在 <see cref="KikasaVaults.KikasaVaultPlayer"/>，
    /// 沉浮演出在 <see cref="KikasaVaults.KikasaLakeFX"/>
    /// </summary>
    internal class KikasaItem : ModItem
    {
        public override void SetDefaults() {
            Item.width = 50;
            Item.height = 54;
            Item.damage = 100;
            Item.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Item.useTime = Item.useAnimation = 24;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.value = Terraria.Item.sellPrice(gold: 25);
            Item.rare = ItemRarityID.Purple;
        }

        //攻击形态是后续模块，当前左键不做任何事
        public override bool CanUseItem(Player player) => false;
    }
}
