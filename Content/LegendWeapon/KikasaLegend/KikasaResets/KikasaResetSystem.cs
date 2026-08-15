using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 大范围重启调度：历史记录与权威/演出推进必须在服务器也跑，
    /// 不能住在 dedServ 早退的钩子里。拒绝文案也挂在这里注册
    /// </summary>
    internal class KikasaResetSystem : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static LocalizedText NeedRainForm { get; private set; }
        public static LocalizedText ResetBusy { get; private set; }
        public static LocalizedText ResetCooling { get; private set; }

        public override void SetStaticDefaults() {
            NeedRainForm = this.GetLocalization(nameof(NeedRainForm), () => "得先落一场鬼雨");
            ResetBusy = this.GetLocalization(nameof(ResetBusy), () => "这场雨还没停");
            ResetCooling = this.GetLocalization(nameof(ResetCooling), () => "雨还没蓄够");
        }

        public override void PostUpdateEverything() {
            KikasaResetHistory.Update();
            KikasaReset.Update();
        }

        public override void ClearWorld() {
            KikasaReset.Reset();
            KikasaResetHistory.Clear();
        }
    }
}
