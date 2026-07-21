using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>框架级文案，键 Wraiths.System.*，Mod.Load 装载</summary>
    internal sealed class WraithSystemText : ILocalizedModType, ICWRLoader
    {
        public Mod Mod => CWRMod.Instance;
        public string Name => "System";
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Wraiths";

        /// <summary>死机入场浮字</summary>
        public static LocalizedText HaltPopup { get; private set; }
        /// <summary>死机窗头顶提示，{0}=借力键</summary>
        public static LocalizedText RitePrompt { get; private set; }
        /// <summary>首次铭刻回执，{0}=鬼名</summary>
        public static LocalizedText RiteFirstBind { get; private set; }
        /// <summary>认主回执，{0}=鬼名</summary>
        public static LocalizedText RiteRenewPact { get; private set; }
        /// <summary>收伏回执，{0}=鬼名</summary>
        public static LocalizedText RiteResubdue { get; private set; }
        /// <summary>封印之鬼仪式被拒</summary>
        public static LocalizedText RiteDeniedSealed { get; private set; }
        /// <summary>他人挣脱体仪式被拒</summary>
        public static LocalizedText RiteDeniedEscaped { get; private set; }
        /// <summary>借力拒，未持载体</summary>
        public static LocalizedText PowerDeniedNoVessel { get; private set; }
        /// <summary>借力拒，鬼正挣脱在外</summary>
        public static LocalizedText PowerDeniedEscaped { get; private set; }
        /// <summary>借力拒，簿上无可借</summary>
        public static LocalizedText PowerDeniedNoBound { get; private set; }
        /// <summary>借力拒，冷却中</summary>
        public static LocalizedText PowerDeniedCooldown { get; private set; }
        /// <summary>犯戒回执，{0}=鬼名</summary>
        public static LocalizedText PowerTaboo { get; private set; }
        /// <summary>反噬挣脱播报，{0}=鬼名</summary>
        public static LocalizedText BacklashEscape { get; private set; }
        /// <summary>挣脱体自行散去，{0}=鬼名</summary>
        public static LocalizedText BacklashFade { get; private set; }
        /// <summary>侵蚀一阶</summary>
        public static LocalizedText ErosionCrawl { get; private set; }
        /// <summary>侵蚀二阶</summary>
        public static LocalizedText ErosionStain { get; private set; }
        /// <summary>侵蚀三阶</summary>
        public static LocalizedText ErosionMirror { get; private set; }

        void ICWRLoader.LoadData() {
            HaltPopup = this.GetLocalization(nameof(HaltPopup), () => "死机");
            RitePrompt = this.GetLocalization(nameof(RitePrompt), () => "[{0}] 落名于簿");
            RiteFirstBind = this.GetLocalization(nameof(RiteFirstBind), () => "「{0}」落名于簿");
            RiteRenewPact = this.GetLocalization(nameof(RiteRenewPact), () => "「{0}」重续了契约——它认得这只手了");
            RiteResubdue = this.GetLocalization(nameof(RiteResubdue), () => "「{0}」被按回了簿上");
            RiteDeniedSealed = this.GetLocalization(nameof(RiteDeniedSealed), () => "封印未解，名讳不可示人");
            RiteDeniedEscaped = this.GetLocalization(nameof(RiteDeniedEscaped), () => "它不认这只手——去找放走它的人");
            PowerDeniedNoVessel = this.GetLocalization(nameof(PowerDeniedNoVessel), () => "手中无刀，簿上无名");
            PowerDeniedEscaped = this.GetLocalization(nameof(PowerDeniedEscaped), () => "簿上的名讳空着——它还在外面");
            PowerDeniedNoBound = this.GetLocalization(nameof(PowerDeniedNoBound), () => "簿上无可借之力");
            PowerDeniedCooldown = this.GetLocalization(nameof(PowerDeniedCooldown), () => "它还不肯再次应声");
            PowerTaboo = this.GetLocalization(nameof(PowerTaboo), () => "犯戒——「{0}」的名讳在簿上洇开");
            BacklashEscape = this.GetLocalization(nameof(BacklashEscape), () => "「{0}」从簿上挣脱了");
            BacklashFade = this.GetLocalization(nameof(BacklashFade), () => "「{0}」的气息散了——它还会回来");
            ErosionCrawl = this.GetLocalization(nameof(ErosionCrawl), () => "皮肤下有什么在爬");
            ErosionStain = this.GetLocalization(nameof(ErosionStain), () => "指尖泛起尸斑的青");
            ErosionMirror = this.GetLocalization(nameof(ErosionMirror), () => "镜子里的东西不太像你");
        }

        void ICWRLoader.UnLoadData() {
            HaltPopup = null;
            RitePrompt = null;
            RiteFirstBind = null;
            RiteRenewPact = null;
            RiteResubdue = null;
            RiteDeniedSealed = null;
            RiteDeniedEscaped = null;
            PowerDeniedNoVessel = null;
            PowerDeniedEscaped = null;
            PowerDeniedNoBound = null;
            PowerDeniedCooldown = null;
            PowerTaboo = null;
            BacklashEscape = null;
            BacklashFade = null;
            ErosionCrawl = null;
            ErosionStain = null;
            ErosionMirror = null;
        }
    }
}
