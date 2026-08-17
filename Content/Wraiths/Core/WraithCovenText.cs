using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 合鬼名录：结印三角每条边写的那个名字，以及它对应的说明。<br/>
    /// 有专属反应的配对各有其名；其余配对仍然互相催醒，落到「相唤」
    /// </summary>
    internal sealed class WraithCovenText : ILocalizedModType, ICWRLoader
    {
        public Mod Mod => CWRMod.Instance;
        public string Name => "Coven";
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Wraiths";

        public static LocalizedText RainHandName { get; private set; }
        public static LocalizedText RainHandNote { get; private set; }
        public static LocalizedText RainShadeName { get; private set; }
        public static LocalizedText RainShadeNote { get; private set; }
        public static LocalizedText HandShadeName { get; private set; }
        public static LocalizedText HandShadeNote { get; private set; }
        public static LocalizedText LanternHandName { get; private set; }
        public static LocalizedText LanternHandNote { get; private set; }
        public static LocalizedText BrideName { get; private set; }
        public static LocalizedText BrideNote { get; private set; }
        public static LocalizedText ScapeName { get; private set; }
        public static LocalizedText ScapeNote { get; private set; }
        public static LocalizedText CallName { get; private set; }
        public static LocalizedText CallNote { get; private set; }
        public static LocalizedText BurstName { get; private set; }
        public static LocalizedText BurstNote { get; private set; }

        void ICWRLoader.LoadData() {
            RainHandName = this.GetLocalization(nameof(RainHandName), () => "雨里伸手");
            RainHandNote = this.GetLocalization(nameof(RainHandNote),
                () => "淋着雨的猎物不再受臂展所限，枯手自它头顶的雨线垂下。雨中最多同时探出五只手，碾轧伤害随雨势加重");
            RainShadeName = this.GetLocalization(nameof(RainShadeName), () => "湿刃");
            RainShadeNote = this.GetLocalization(nameof(RainShadeNote),
                () => "鬼影穿体留下的刀口未合时，该目标每一跳雨蚀翻倍");
            HandShadeName = this.GetLocalization(nameof(HandShadeName), () => "按住了斩");
            HandShadeNote = this.GetLocalization(nameof(HandShadeNote),
                () => "鬼影优先扑向被枯手攥住的猎物——速度已被压到零，那一刀不会落空");
            LanternHandName = this.GetLocalization(nameof(LanternHandName), () => "照见");
            LanternHandNote = this.GetLocalization(nameof(LanternHandNote),
                () => "灯照见过的猎物，枯手隔着遮挡也索得到；三灯刀光落在被攥住的目标上时加重一成六");
            BrideName = this.GetLocalization(nameof(BrideName), () => "喜堂");
            BrideNote = this.GetLocalization(nameof(BrideNote),
                () => "合卺之刻圈进喜堂的猎物，身上其余印记停止消退，直到喜堂散去");
            ScapeName = this.GetLocalization(nameof(ScapeName), () => "顶劫");
            ScapeNote = this.GetLocalization(nameof(ScapeNote),
                () => "替身死一次，同盘其余役鬼的复苏各退一档");
            CallName = this.GetLocalization(nameof(CallName), () => "相唤");
            CallNote = this.GetLocalization(nameof(CallNote),
                () => "同盘役鬼彼此催醒：任一只被役使，其余各涨少量复苏。没有专属反应的两只之间也照样如此");
            BurstName = this.GetLocalization(nameof(BurstName), () => "三印崩");
            BurstNote = this.GetLocalization(nameof(BurstNote),
                () => "同一猎物身上凑齐三种印记时，印记一并引爆算一次伤害，参与的役鬼各付一次复苏");
        }

        void ICWRLoader.UnLoadData() {
            RainHandName = null;
            RainHandNote = null;
            RainShadeName = null;
            RainShadeNote = null;
            HandShadeName = null;
            HandShadeNote = null;
            LanternHandName = null;
            LanternHandNote = null;
            BrideName = null;
            BrideNote = null;
            ScapeName = null;
            ScapeNote = null;
            CallName = null;
            CallNote = null;
            BurstName = null;
            BurstNote = null;
        }

        /// <summary>
        /// 两只鬼之间那条边的名字与说明。专属反应优先，其次绯嫁与替死的通配，
        /// 都不沾则落到「相唤」——它们仍然在互相催醒，边不该是死的
        /// </summary>
        internal static (LocalizedText Name, LocalizedText Note) Pair(
            WraithAbilityKind a, WraithAbilityKind b) {
            if (a == WraithAbilityKind.None || b == WraithAbilityKind.None || a == b) {
                return (null, null);
            }
            if (Match(a, b, WraithAbilityKind.GhostRain, WraithAbilityKind.GhostHand)) {
                return (RainHandName, RainHandNote);
            }
            if (Match(a, b, WraithAbilityKind.GhostRain, WraithAbilityKind.HeadlessShade)) {
                return (RainShadeName, RainShadeNote);
            }
            if (Match(a, b, WraithAbilityKind.GhostHand, WraithAbilityKind.HeadlessShade)) {
                return (HandShadeName, HandShadeNote);
            }
            if (Match(a, b, WraithAbilityKind.LanternBoy, WraithAbilityKind.GhostHand)) {
                return (LanternHandName, LanternHandNote);
            }
            if (a == WraithAbilityKind.CrimsonBride || b == WraithAbilityKind.CrimsonBride) {
                return (BrideName, BrideNote);
            }
            if (a == WraithAbilityKind.ScapeGhost || b == WraithAbilityKind.ScapeGhost) {
                return (ScapeName, ScapeNote);
            }
            return (CallName, CallNote);
        }

        private static bool Match(WraithAbilityKind a, WraithAbilityKind b,
            WraithAbilityKind x, WraithAbilityKind y)
            => a == x && b == y || a == y && b == x;
    }
}
