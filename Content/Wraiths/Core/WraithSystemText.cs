using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>役鬼能力仍在使用的系统文案。</summary>
    internal sealed class WraithSystemText : ILocalizedModType, ICWRLoader
    {
        public Mod Mod => CWRMod.Instance;
        public string Name => "System";
        public string FullName => Mod.Name + "/" + Name;
        public string LocalizationCategory => "Wraiths";

        public static LocalizedText ScapeGhostActivated { get; private set; }
        public static LocalizedText ScapeGhostDeathBroadcast { get; private set; }
        public static LocalizedText ScapeGhostUnknownTarget { get; private set; }
        public static LocalizedText RevivalKillReason { get; private set; }
        public static LocalizedText RevivalStir { get; private set; }
        public static LocalizedText RevivalRise { get; private set; }
        public static LocalizedText RevivalBrink { get; private set; }
        public static LocalizedText RevivalHudBrink { get; private set; }
        public static LocalizedText ErosionCrawl { get; private set; }
        public static LocalizedText ErosionStain { get; private set; }
        public static LocalizedText ErosionMirror { get; private set; }

        void ICWRLoader.LoadData() {
            ScapeGhostActivated = this.GetLocalization(nameof(ScapeGhostActivated),
                () => "{0}成为了替死鬼");
            ScapeGhostDeathBroadcast = this.GetLocalization(nameof(ScapeGhostDeathBroadcast),
                () => "{0}成为了{1}的替死鬼，{2}");
            ScapeGhostUnknownTarget = this.GetLocalization(nameof(ScapeGhostUnknownTarget),
                () => "某个活物");
            RevivalKillReason = this.GetLocalization(nameof(RevivalKillReason),
                () => "{0}死于厉鬼复苏");
            RevivalStir = this.GetLocalization(nameof(RevivalStir),
                () => "札上的名字动了一下");
            RevivalRise = this.GetLocalization(nameof(RevivalRise),
                () => "它隔着纸在看你");
            RevivalBrink = this.GetLocalization(nameof(RevivalBrink),
                () => "封印札烫得握不住了");
            RevivalHudBrink = this.GetLocalization(nameof(RevivalHudBrink),
                () => "再役使一次，它就会醒");
            ErosionCrawl = this.GetLocalization(nameof(ErosionCrawl),
                () => "皮肤下有什么在爬");
            ErosionStain = this.GetLocalization(nameof(ErosionStain),
                () => "指尖泛起尸斑的青");
            ErosionMirror = this.GetLocalization(nameof(ErosionMirror),
                () => "镜子里的东西不太像你");
        }

        void ICWRLoader.UnLoadData() {
            ScapeGhostActivated = null;
            ScapeGhostDeathBroadcast = null;
            ScapeGhostUnknownTarget = null;
            RevivalKillReason = null;
            RevivalStir = null;
            RevivalRise = null;
            RevivalBrink = null;
            RevivalHudBrink = null;
            ErosionCrawl = null;
            ErosionStain = null;
            ErosionMirror = null;
        }
    }
}
