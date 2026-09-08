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
    /// 本类只声明 debuff、时长口径与承印体口径（<see cref="Bearer"/>：一个敌人只有一个印）
    /// </summary>
    internal class KikasaInkTag : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "KikasaInkTag";

        /// <summary>标记时长（帧），与原版鞭标签同款</summary>
        public const int TagFrames = 240;

        public override void SetStaticDefaults() => Main.debuff[Type] = true;

        /// <summary>
        /// 承印体：多段敌人（蠕虫）的印一律记在本体（realLife）上。
        /// 原版 AddBuff 不往 realLife 传递，一波墨雨咬中几节体节就落几个各自独立的印，
        /// 墨环便沿着蠕虫排成一串（反馈：墨圈重复出现且挡视野）。
        /// 盖印与带印判定共用这一个口径，打哪一节都算本体带印
        /// </summary>
        internal static NPC Bearer(NPC target) {
            int body = target.realLife;
            return body >= 0 && body < Main.maxNPCs && body != target.whoAmI && Main.npc[body].active
                ? Main.npc[body] : target;
        }

        /// <summary>盖印：落在承印体上，一个敌人至多一个印</summary>
        internal static void Apply(NPC target)
            => Bearer(target).AddBuff(ModContent.BuffType<KikasaInkTag>(), TagFrames);

        /// <summary>带印判定：命中任意体节都读本体那一个印</summary>
        internal static bool Marked(NPC target)
            => Bearer(target).HasBuff(ModContent.BuffType<KikasaInkTag>());
    }
}
