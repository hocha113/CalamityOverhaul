using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>祝福系统共用文案（播报与往生轮界面）</summary>
    internal sealed class BlessingSystemText : ILocalizedModType, ICWRLoader
    {
        public Mod Mod => CWRMod.Instance;
        public string Name => "System";
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Blessings";

        /// <summary>解锁播报，{0}=祝福名</summary>
        public static LocalizedText UnlockBroadcast { get; private set; }
        /// <summary>点燃按钮</summary>
        public static LocalizedText KindleLabel { get; private set; }
        /// <summary>熄灭按钮</summary>
        public static LocalizedText SnuffLabel { get; private set; }
        /// <summary>燃焰计数，{0}=当前 {1}=上限</summary>
        public static LocalizedText BurningCounter { get; private set; }
        /// <summary>槽满拒绝提示</summary>
        public static LocalizedText SlotFullNotice { get; private set; }
        /// <summary>未解锁珠位提示</summary>
        public static LocalizedText LockedHint { get; private set; }
        /// <summary>HUD 引魂灯悬停名</summary>
        public static LocalizedText HudName { get; private set; }
        /// <summary>HUD 点击提示</summary>
        public static LocalizedText HudOpenHint { get; private set; }
        /// <summary>往生轮标题</summary>
        public static LocalizedText WheelTitle { get; private set; }
        /// <summary>未选中珠位时的中心提示</summary>
        public static LocalizedText CenterHint { get; private set; }
        /// <summary>轮底关闭提示，{0}=当前键位</summary>
        public static LocalizedText CloseHint { get; private set; }

        void ICWRLoader.LoadData() {
            UnlockBroadcast = this.GetLocalization(nameof(UnlockBroadcast),
                () => "死神颔首，「{0}」已入往生轮");
            KindleLabel = this.GetLocalization(nameof(KindleLabel), () => "点燃");
            SnuffLabel = this.GetLocalization(nameof(SnuffLabel), () => "熄灭");
            BurningCounter = this.GetLocalization(nameof(BurningCounter), () => "燃焰 {0}/{1}");
            SlotFullNotice = this.GetLocalization(nameof(SlotFullNotice),
                () => "灯座已满，先熄灭一盏，才能点燃新的祝福");
            LockedHint = this.GetLocalization(nameof(LockedHint),
                () => "在修罗降世时讨伐它，魂焰才会归位");
            HudName = this.GetLocalization(nameof(HudName), () => "引魂灯");
            HudOpenHint = this.GetLocalization(nameof(HudOpenHint), () => "点击打开往生轮");
            WheelTitle = this.GetLocalization(nameof(WheelTitle), () => "往生轮");
            CenterHint = this.GetLocalization(nameof(CenterHint), () => "点选一枚魂珠，查看它的祝福");
            CloseHint = this.GetLocalization(nameof(CloseHint), () => "Esc、右键或 {0} 键合拢往生轮");
        }

        void ICWRLoader.UnLoadData() {
            UnlockBroadcast = null;
            KindleLabel = null;
            SnuffLabel = null;
            BurningCounter = null;
            SlotFullNotice = null;
            LockedHint = null;
            HudName = null;
            HudOpenHint = null;
            WheelTitle = null;
            CenterHint = null;
            CloseHint = null;
        }
    }
}
