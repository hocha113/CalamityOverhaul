using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 鬼伞 UI 跨屏共享文本（亲和名/通用状态词）。
    /// 快捷转盘、湖心景、风铃 HUD 共用，别再往各屏散写同一份词
    /// </summary>
    internal class KikasaUIText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        /// <summary>焰系亲和名</summary>
        internal static LocalizedText AffinityFlame;
        /// <summary>魇系亲和名</summary>
        internal static LocalizedText AffinityNightmare;
        /// <summary>潦系亲和名</summary>
        internal static LocalizedText AffinityRain;
        /// <summary>百搭亲和名</summary>
        internal static LocalizedText AffinityWild;
        /// <summary>械奴记忆的类别名（无灵异亲和）</summary>
        internal static LocalizedText AffinityArms;

        /// <summary>鬼奴在场</summary>
        internal static LocalizedText StateOut;
        /// <summary>鬼奴被玩家收起</summary>
        internal static LocalizedText StateHeld;
        /// <summary>候湖：受理了召回但湖未就绪</summary>
        internal static LocalizedText StateAwait;
        /// <summary>湖未就绪的通用说明</summary>
        internal static LocalizedText LakeNotReadyLine;

        public override void SetStaticDefaults() {
            AffinityFlame = this.GetLocalization(nameof(AffinityFlame), () => "Flame");
            AffinityNightmare = this.GetLocalization(nameof(AffinityNightmare), () => "Nightmare");
            AffinityRain = this.GetLocalization(nameof(AffinityRain), () => "Downpour");
            AffinityWild = this.GetLocalization(nameof(AffinityWild), () => "Wild");
            AffinityArms = this.GetLocalization(nameof(AffinityArms), () => "Armament");
            StateOut = this.GetLocalization(nameof(StateOut), () => "On the field");
            StateHeld = this.GetLocalization(nameof(StateHeld), () => "Held back");
            StateAwait = this.GetLocalization(nameof(StateAwait), () => "Awaiting the lake");
            LakeNotReadyLine = this.GetLocalization(nameof(LakeNotReadyLine),
                () => "The lake is not ready — it will surface once the lake rises");
        }

        /// <summary>亲和展示名：械奴记忆（负键）报「械」，无亲和空串</summary>
        internal static string AffinityName(KikasaServants.KikasaAffinity affinity, int key) {
            if (key < 0) {
                return AffinityArms.Value;
            }
            return affinity switch {
                KikasaServants.KikasaAffinity.Flame => AffinityFlame.Value,
                KikasaServants.KikasaAffinity.Nightmare => AffinityNightmare.Value,
                KikasaServants.KikasaAffinity.Rain => AffinityRain.Value,
                KikasaServants.KikasaAffinity.Wild => AffinityWild.Value,
                _ => string.Empty,
            };
        }
    }
}
