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
        /// <summary>操作需要厉鬼载体</summary>
        public static LocalizedText VesselRequired { get; private set; }
        /// <summary>替死成功，{0}=替死对象</summary>
        public static LocalizedText ScapeGhostActivated { get; private set; }
        /// <summary>没有可承受替死的对象</summary>
        public static LocalizedText ScapeGhostNoTarget { get; private set; }
        /// <summary>替死公告，{0}=NPC名，{1}=玩家名，{2}=原始致死文本</summary>
        public static LocalizedText ScapeGhostDeathBroadcast { get; private set; }
        /// <summary>联机包缺失目标名时的占位</summary>
        public static LocalizedText ScapeGhostUnknownTarget { get; private set; }
        /// <summary>复苏进度满格击杀玩家的死亡文案，{0}=玩家名</summary>
        public static LocalizedText RevivalKillReason { get; private set; }
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
            RiteFirstBind = this.GetLocalization(nameof(RiteFirstBind), () => "「{0}」落名于簿");
            RiteRenewPact = this.GetLocalization(nameof(RiteRenewPact), () => "「{0}」重续了契约——它认得这只手了");
            RiteResubdue = this.GetLocalization(nameof(RiteResubdue), () => "「{0}」被按回了簿上");
            RiteDeniedSealed = this.GetLocalization(nameof(RiteDeniedSealed), () => "封印未解，名讳不可示人");
            RiteDeniedEscaped = this.GetLocalization(nameof(RiteDeniedEscaped), () => "它不认这只手——去找放走它的人");
            VesselRequired = this.GetLocalization(nameof(VesselRequired), () => "身上没有可承载厉鬼的器物");
            ScapeGhostActivated = this.GetLocalization(nameof(ScapeGhostActivated), () => "「替死」——{0}替你承下了这次死亡");
            ScapeGhostNoTarget = this.GetLocalization(nameof(ScapeGhostNoTarget), () => "四野无活物可替，印记随你的命一同散去");
            ScapeGhostDeathBroadcast = this.GetLocalization(nameof(ScapeGhostDeathBroadcast), () => "{0}替{1}承下了这劫——{2}");
            ScapeGhostUnknownTarget = this.GetLocalization(nameof(ScapeGhostUnknownTarget), () => "某个活物");
            RevivalKillReason = this.GetLocalization(nameof(RevivalKillReason), () => "{0}被鬼魂彻底夺走了身躯");
            BacklashEscape = this.GetLocalization(nameof(BacklashEscape), () => "「{0}」从簿上挣脱了");
            BacklashFade = this.GetLocalization(nameof(BacklashFade), () => "「{0}」的气息散了——它还会回来");
            ErosionCrawl = this.GetLocalization(nameof(ErosionCrawl), () => "皮肤下有什么在爬");
            ErosionStain = this.GetLocalization(nameof(ErosionStain), () => "指尖泛起尸斑的青");
            ErosionMirror = this.GetLocalization(nameof(ErosionMirror), () => "镜子里的东西不太像你");
        }

        void ICWRLoader.UnLoadData() {
            HaltPopup = null;
            RiteFirstBind = null;
            RiteRenewPact = null;
            RiteResubdue = null;
            RiteDeniedSealed = null;
            RiteDeniedEscaped = null;
            VesselRequired = null;
            ScapeGhostActivated = null;
            ScapeGhostNoTarget = null;
            ScapeGhostDeathBroadcast = null;
            ScapeGhostUnknownTarget = null;
            RevivalKillReason = null;
            BacklashEscape = null;
            BacklashFade = null;
            ErosionCrawl = null;
            ErosionStain = null;
            ErosionMirror = null;
        }
    }
}
