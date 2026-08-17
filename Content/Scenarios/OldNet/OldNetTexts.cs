using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet
{
    //旧网玩家可见文本集中登记（键 Mods.CalamityOverhaul.UI.OldNet*）
    internal class OldNetTexts : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText OldNetHarvest { get; private set; }
        public static LocalizedText OldNetNodeHint { get; private set; }
        public static LocalizedText OldNetTerminalHint { get; private set; }
        public static LocalizedText OldNetSettleDone { get; private set; }
        public static LocalizedText OldNetSettleEmpty { get; private set; }
        public static LocalizedText OldNetEjectRam { get; private set; }
        public static LocalizedText OldNetEjectDeath { get; private set; }
        public static LocalizedText OldNetLedgerFull { get; private set; }
        public static LocalizedText OldNetEncryptHint { get; private set; }
        public static LocalizedText OldNetEventHint { get; private set; }
        public static LocalizedText OldNetEventPulled { get; private set; }
        public static LocalizedText OldNetRelayHint { get; private set; }
        public static LocalizedText OldNetRelayDone { get; private set; }
        public static LocalizedText OldNetRelayEmpty { get; private set; }
        //M2c 入口
        public static LocalizedText OldNetEnterHint { get; private set; }
        public static LocalizedText OldNetEnterConfirm { get; private set; }
        public static LocalizedText OldNetEnterSPOnly { get; private set; }
        public static LocalizedText OldNetDiveCharge { get; private set; }
        //M2c 分带横幅
        public static LocalizedText OldNetBandFoot { get; private set; }
        public static LocalizedText OldNetBandRuin { get; private set; }
        public static LocalizedText OldNetBandFade { get; private set; }
        //M2c 引导（五步）
        public static LocalizedText GuideNoiseTitle { get; private set; }
        public static LocalizedText GuideNoiseBody { get; private set; }
        public static LocalizedText GuideLedgerTitle { get; private set; }
        public static LocalizedText GuideLedgerBody { get; private set; }
        public static LocalizedText GuideNodesTitle { get; private set; }
        public static LocalizedText GuideNodesBody { get; private set; }
        public static LocalizedText GuideRelayTitle { get; private set; }
        public static LocalizedText GuideRelayBody { get; private set; }
        public static LocalizedText GuideDrainTitle { get; private set; }
        public static LocalizedText GuideDrainBody { get; private set; }
        public static LocalizedText GuideSkip { get; private set; }
        //M2c 委托
        public static LocalizedText EntrustTitle { get; private set; }
        public static LocalizedText EntrustSummary { get; private set; }
        public static LocalizedText EntrustCategory { get; private set; }
        public static LocalizedText TrackerOverworld { get; private set; }
        public static LocalizedText TrackerDive { get; private set; }
        //M2c 带界立牌
        public static LocalizedText OldNetSignRuin { get; private set; }
        public static LocalizedText OldNetSignFade { get; private set; }
        //M3 回声考古 / 深潜缓存
        public static LocalizedText OldNetEchoHint { get; private set; }
        public static LocalizedText OldNetEchoFizzle { get; private set; }
        public static LocalizedText OldNetCacheHint { get; private set; }

        public override void SetStaticDefaults() {
            OldNetHarvest = this.GetLocalization(nameof(OldNetHarvest), () => "+{0} 模具碎片（未铭刻）");
            OldNetNodeHint = this.GetLocalization(nameof(OldNetNodeHint), () => "回收数据");
            OldNetTerminalHint = this.GetLocalization(nameof(OldNetTerminalHint), () => "登出并铭刻收获");
            OldNetSettleDone = this.GetLocalization(nameof(OldNetSettleDone), () => "已铭刻 {0} 枚模具碎片，链路安全断开");
            OldNetSettleEmpty = this.GetLocalization(nameof(OldNetSettleEmpty), () => "链路安全断开，本次没有收获");
            OldNetEjectRam = this.GetLocalization(nameof(OldNetEjectRam), () => "RAM耗尽——链路烧断，未铭刻的收获已丢失");
            OldNetEjectDeath = this.GetLocalization(nameof(OldNetEjectDeath), () => "构念崩解——链路烧断，未铭刻的收获已丢失");
            OldNetLedgerFull = this.GetLocalization(nameof(OldNetLedgerFull), () => "账本已满——先去中继站或登出");
            OldNetEncryptHint = this.GetLocalization(nameof(OldNetEncryptHint), () => "引导破解（站桩约3秒，动静很大）");
            OldNetEventHint = this.GetLocalization(nameof(OldNetEventHint), () => "拉闸：解除全图封锁，惊动整张网");
            OldNetEventPulled = this.GetLocalization(nameof(OldNetEventPulled), () => "封锁已解除——清剿波正在路上");
            OldNetRelayHint = this.GetLocalization(nameof(OldNetRelayHint), () => "中继上行：铭刻当前账本（上行有噪音）");
            OldNetRelayDone = this.GetLocalization(nameof(OldNetRelayDone), () => "已铭刻 {0} 枚模具碎片，链路保持");
            OldNetRelayEmpty = this.GetLocalization(nameof(OldNetRelayEmpty), () => "账本为空，无可上行");

            OldNetEnterHint = this.GetLocalization(nameof(OldNetEnterHint), () => "接入旧网（越墙深潜）");
            OldNetEnterConfirm = this.GetLocalization(nameof(OldNetEnterConfirm), () => "链路已预热——再次交互，越墙深潜");
            OldNetEnterSPOnly = this.GetLocalization(nameof(OldNetEnterSPOnly), () => "深潜仅单人模式可用");
            OldNetDiveCharge = this.GetLocalization(nameof(OldNetDiveCharge), () => "保持下潜——正在穿墙");

            OldNetBandFoot = this.GetLocalization(nameof(OldNetBandFoot), () => "墙脚带 // FOOTHOLD");
            OldNetBandRuin = this.GetLocalization(nameof(OldNetBandRuin), () => "废墟带 // RUINFIELD");
            OldNetBandFade = this.GetLocalization(nameof(OldNetBandFade), () => "信号衰减区 // SIGNAL DECAY");

            GuideNoiseTitle = this.GetLocalization(nameof(GuideNoiseTitle), () => "噪音计");
            GuideNoiseBody = this.GetLocalization(nameof(GuideNoiseBody),
                () => "左下角是噪音计：移动、开火、破解都会点亮你。四道刻度对应四档威胁——过 T2 就会有东西从墙那边过来。静止不动，噪音会自己冷却。");
            GuideLedgerTitle = this.GetLocalization(nameof(GuideLedgerTitle), () => "未铭刻账本");
            GuideLedgerBody = this.GetLocalization(nameof(GuideLedgerBody),
                () => "采到的碎片先进账本（LEDGER 读数），死亡或 RAM 耗尽会全部作废——只有铭刻过的才真正属于你。账本有容量，满载会拒收。");
            GuideNodesTitle = this.GetLocalization(nameof(GuideNodesTitle), () => "节点分级");
            GuideNodesBody = this.GetLocalization(nameof(GuideNodesBody),
                () => "青色节点右键即采；琥珀色加密节点要站桩引导约 3 秒，动静很大但值三倍；红色事件闸一拉，全图封锁解除，清剿波也会立刻到场。");
            GuideRelayTitle = this.GetLocalization(nameof(GuideRelayTitle), () => "中继与登出");
            GuideRelayBody = this.GetLocalization(nameof(GuideRelayBody),
                () => "琥珀光柱是中继站：就地铭刻账本、人不撤，但上行广播很响。薄荷绿光柱是登出终端：铭刻并安全断链。贪还是撤，是这里唯一的问题。");
            GuideDrainTitle = this.GetLocalization(nameof(GuideDrainTitle), () => "距离底噪");
            GuideDrainBody = this.GetLocalization(nameof(GuideDrainBody),
                () => "离墙越远，RAM 底噪越贵——HUD 的 DEPTH 读数旁标着当前每秒消耗。RAM 烧干等于弹出，回程的路费要提前算进去。");
            GuideSkip = this.GetLocalization(nameof(GuideSkip), () => "知道了");

            EntrustTitle = this.GetLocalization(nameof(EntrustTitle), () => "越墙深潜");
            EntrustSummary = this.GetLocalization(nameof(EntrustSummary),
                () => "坠舱中舱的接入终端还在通电。接入旧网，采回模具碎片，并从登出终端安全断链一次。RAM 耗尽或死亡会弄丢一切未铭刻的收获。");
            EntrustCategory = this.GetLocalization(nameof(EntrustCategory), () => "深潜考古");
            TrackerOverworld = this.GetLocalization(nameof(TrackerOverworld), () => "接入坠舱中舱的终端");
            TrackerDive = this.GetLocalization(nameof(TrackerDive), () => "从登出终端安全断链");

            OldNetSignRuin = this.GetLocalization(nameof(OldNetSignRuin),
                () => "废墟带。主产区，加密节点自此出没——引导破解前，先看好退路。");
            OldNetSignFade = this.GetLocalization(nameof(OldNetSignFade),
                () => "信号衰减区。底噪自此陡增，链路撑不了多久——深入自负。");

            OldNetEchoHint = this.GetLocalization(nameof(OldNetEchoHint),
                () => "回声节点：只在时停中可触及（零噪音，产出翻倍）");
            OldNetEchoFizzle = this.GetLocalization(nameof(OldNetEchoFizzle),
                () => "回声太淡——时停中才能触及");
            OldNetCacheHint = this.GetLocalization(nameof(OldNetCacheHint),
                () => "深潜缓存：撬开取一件封存模块（动静不小）");
        }
    }
}
