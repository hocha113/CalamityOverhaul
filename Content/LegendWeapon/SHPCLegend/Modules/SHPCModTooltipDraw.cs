using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>
    /// 改件展示的文本宿主。旧的 PostDrawTooltip 右侧小窗已退役(2026-08),
    /// 展示融入自绘面板 <see cref="UI.SHPCItemTooltipPanel"/> 的改件区,
    /// 本类只保留三个本地化键(键路径不变,老翻译继续生效)
    /// </summary>
    internal class SHPCModTooltipDraw : GlobalItem, ILocalizedModType
    {
        public string LocalizationCategory => "Items";

        public static LocalizedText InstalledHeader { get; private set; }
        public static LocalizedText NoModules { get; private set; }
        public static LocalizedText BonusHeader { get; private set; }

        public override void SetStaticDefaults() {
            InstalledHeader = this.GetLocalization(nameof(InstalledHeader));
            NoModules = this.GetLocalization(nameof(NoModules));
            BonusHeader = this.GetLocalization(nameof(BonusHeader));
        }
    }
}
