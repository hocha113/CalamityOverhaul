using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 鬼伞 UI 跨屏共享文本（亲和名/通用状态词/鬼火状态口径）。
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

        /// <summary>鬼火点不着：鬼雨压着（无沸雨边）</summary>
        internal static LocalizedText WispRainBlock;
        /// <summary>鬼火/鬼梦号令不受理：湖面正处过渡（翻转/入梦演出中）</summary>
        internal static LocalizedText NeedSettleLine;

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
            WispRainBlock = this.GetLocalization(nameof(WispRainBlock),
                () => "The ghost rain smothers any spark");
            NeedSettleLine = this.GetLocalization(nameof(NeedSettleLine),
                () => "The lake has not settled yet");
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

        /// <summary>鬼火此刻的状态行（燃烧/蒸沸/被浇灭/未点燃），转盘、湖心景、HUD 共用一份口径</summary>
        internal static string WispStateLine(KikasaDomainPlayer domain) {
            if (!domain.WispFireActive) {
                return Panorama.KikasaPanoramaUI.WispIdle.Value;
            }
            if (domain.WispQuench > 0.3f) {
                return Panorama.KikasaPanoramaUI.WispQuenchedLine.Value;
            }
            if (domain.WispRainProof && domain.IsRainForm) {
                return Panorama.KikasaPanoramaUI.WispBoil.Value;
            }
            return Panorama.KikasaPanoramaUI.WispBurning.Value;
        }

        /// <summary>鬼火点不着的原因（差哪一步说哪一步）；点得着或已燃（收火恒可）返回 null</summary>
        internal static string WispBlockReason(KikasaDomainPlayer domain) {
            if (domain.WispFireActive || KikasaWisps.KikasaWisp.IgniteReady(domain)) {
                return null;
            }
            if (!domain.AnyActive) {
                return string.Format(Panorama.KikasaPanoramaUI.NeedDomainFormat.Value,
                    CWRKeySystem.Legend_Domain.ToTooltipString(CWRKeySystem.Notbound.Value));
            }
            if (domain.IsRainForm && !domain.WispRainProof) {
                return WispRainBlock.Value;
            }
            if (domain.RiseT < 0.98f) {
                return Panorama.KikasaPanoramaUI.NeedFullWater.Value;
            }
            //剩下的都是过渡相位：翻转/入梦演出里镜面正忙
            return NeedSettleLine.Value;
        }
    }
}
