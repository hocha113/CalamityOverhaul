using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>
    /// 合鬼名录：结印三角边名与说明的文案容器（本地化键沿用 Wraiths.Coven.*）。<br/>
    /// 哪条边亮哪个名字不再写在这里，由各鬼声明的 <see cref="WraithSynergyRule"/>
    /// 经 <see cref="WraithSynergy.EdgePair"/> 推导；「相唤」与「三印崩」是盘的固有性质，仍由 UI 直接引用
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
                () => "鬼影优先扑向被枯手攥住的猎物，速度已被压到零，那一刀不会落空");
            LanternHandName = this.GetLocalization(nameof(LanternHandName), () => "照见");
            LanternHandNote = this.GetLocalization(nameof(LanternHandNote),
                () => "灯照见过的猎物，枯手隔着遮挡也索得到；三灯刀光落在被攥住的目标上时加重六成");
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

    }
}
