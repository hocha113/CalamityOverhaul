using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨印 debuff：亲手指挥的墨雨/墨瀑命中盖上
    /// （归属端命中钩 AddBuff 骑原版 buff 同步，自动墨雨与墨洼/墨泉不盖）。
    /// 结算在 <see cref="KikasaServants.KikasaServantBalanceGlobal"/>：
    /// 一切召唤系命中对带印目标追加随等级表成长的平伤。
    /// 贴身演出（墨环/盖印拍/淌墨/干涸淡出）全在 <see cref="KikasaInkTagNPC"/>，
    /// 本类只声明 debuff 与时长口径
    /// </summary>
    internal class KikasaInkTag : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "KikasaInkTag";

        /// <summary>标记时长（帧），与原版鞭标签同款</summary>
        public const int TagFrames = 240;

        public override void SetStaticDefaults() => Main.debuff[Type] = true;
    }
}
