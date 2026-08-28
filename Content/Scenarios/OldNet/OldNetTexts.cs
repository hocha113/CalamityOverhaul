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
        public static LocalizedText OldNetEnterArmedHint { get; private set; }
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
        //04 固定威胁（绊网/哨雷/封锁闸；哨眼无交互文本）
        public static LocalizedText OldNetTripwireHint { get; private set; }
        public static LocalizedText OldNetTripwireCut { get; private set; }
        public static LocalizedText OldNetMineHint { get; private set; }
        public static LocalizedText OldNetMineDefused { get; private set; }
        public static LocalizedText OldNetMineScream { get; private set; }
        public static LocalizedText OldNetMineDeath { get; private set; }
        public static LocalizedText OldNetBulkheadWarn { get; private set; }
        public static LocalizedText OldNetBulkheadShut { get; private set; }
        public static LocalizedText OldNetBulkheadReopen { get; private set; }
        public static LocalizedText OldNetBreakerHint { get; private set; }
        public static LocalizedText OldNetBreakerPulled { get; private set; }
        //P1 结构扩容：检疫关卡告示
        public static LocalizedText OldNetSignCheckpoint { get; private set; }
        //03 猎杀敌人包（灯蛾/循迹猎犬/回收官）
        public static LocalizedText TaggerAttached { get; private set; }
        public static LocalizedText TracerHowlWarn { get; private set; }
        public static LocalizedText TracerConfused { get; private set; }
        public static LocalizedText WardenDispatch { get; private set; }
        public static LocalizedText WardenSlain { get; private set; }
        public static LocalizedText WardenGraceHud { get; private set; }
        //02 交互经济（P2）：账本扩容坞
        public static LocalizedText OldNetDockHint { get; private set; }
        public static LocalizedText OldNetDockDone { get; private set; }
        //02 交互经济（P2）：冷存储节点
        public static LocalizedText OldNetColdHint { get; private set; }
        public static LocalizedText OldNetColdNoRam { get; private set; }
        //02 交互经济（P2）：保险契约终端
        public static LocalizedText OldNetEscrowHint { get; private set; }
        public static LocalizedText OldNetEscrowEmpty { get; private set; }
        public static LocalizedText OldNetEscrowTooThin { get; private set; }
        public static LocalizedText OldNetEscrowSigned { get; private set; }
        public static LocalizedText OldNetEscrowPayout { get; private set; }
        //02 交互经济（P2）：主控破译矩阵（台体 + 面板）
        public static LocalizedText OldNetVaultHint { get; private set; }
        public static LocalizedText OldNetVaultNoRam { get; private set; }
        public static LocalizedText OldNetVaultLocked { get; private set; }
        public static LocalizedText OldNetVaultPayout { get; private set; }
        public static LocalizedText VaultTitle { get; private set; }
        public static LocalizedText VaultGuide { get; private set; }
        public static LocalizedText VaultStage { get; private set; }
        public static LocalizedText VaultPot { get; private set; }
        public static LocalizedText VaultPotModule { get; private set; }
        public static LocalizedText VaultPotChip { get; private set; }
        public static LocalizedText VaultLedgerRoom { get; private set; }
        public static LocalizedText VaultCashOut { get; private set; }
        public static LocalizedText VaultContinue { get; private set; }
        public static LocalizedText VaultBust { get; private set; }
        public static LocalizedText VaultCounterHack { get; private set; }
        public static LocalizedText VaultDiscard { get; private set; }
        //06 导演与评分（P6）：衰减区余震
        public static LocalizedText OldNetEncryptFadeHint { get; private set; }
        public static LocalizedText OldNetAftershockWarn { get; private set; }
        public static LocalizedText OldNetAftershockHit { get; private set; }
        //06 导演与评分（P6）：收网协议
        public static LocalizedText OldNetDragnetWarn { get; private set; }
        public static LocalizedText OldNetDragnetOn { get; private set; }
        //06 导演与评分（P6）：热断链
        public static LocalizedText OldNetHotExtractHint { get; private set; }
        public static LocalizedText OldNetHotExtractStart { get; private set; }
        public static LocalizedText OldNetHotExtractAbort { get; private set; }
        public static LocalizedText OldNetHotExtractTooFar { get; private set; }

        public override void SetStaticDefaults() {
            OldNetHarvest = this.GetLocalization(nameof(OldNetHarvest), () => "+{0} 模具碎片（未铭刻）");
            OldNetNodeHint = this.GetLocalization(nameof(OldNetNodeHint), () => "回收数据");
            OldNetTerminalHint = this.GetLocalization(nameof(OldNetTerminalHint), () => "登出并铭刻收获");
            OldNetSettleDone = this.GetLocalization(nameof(OldNetSettleDone), () => "已铭刻 {0} 枚模具碎片，链路安全断开");
            OldNetSettleEmpty = this.GetLocalization(nameof(OldNetSettleEmpty), () => "链路安全断开，本次没有收获");
            OldNetEjectRam = this.GetLocalization(nameof(OldNetEjectRam), () => "RAM耗尽，链路烧断，未铭刻的收获已丢失");
            OldNetEjectDeath = this.GetLocalization(nameof(OldNetEjectDeath), () => "构念崩解，链路烧断，未铭刻的收获已丢失");
            OldNetLedgerFull = this.GetLocalization(nameof(OldNetLedgerFull), () => "账本已满，先去中继站或登出");
            OldNetEncryptHint = this.GetLocalization(nameof(OldNetEncryptHint), () => "引导破解（站桩约3秒，动静很大）");
            OldNetEventHint = this.GetLocalization(nameof(OldNetEventHint), () => "拉闸：解除全图封锁，惊动整张网");
            OldNetEventPulled = this.GetLocalization(nameof(OldNetEventPulled), () => "封锁已解除，清剿波正在路上");
            OldNetRelayHint = this.GetLocalization(nameof(OldNetRelayHint), () => "中继上行：铭刻当前账本（上行有噪音）");
            OldNetRelayDone = this.GetLocalization(nameof(OldNetRelayDone), () => "已铭刻 {0} 枚模具碎片，链路保持");
            OldNetRelayEmpty = this.GetLocalization(nameof(OldNetRelayEmpty), () => "账本为空，无可上行");

            OldNetEnterHint = this.GetLocalization(nameof(OldNetEnterHint), () => "接入旧网（越墙深潜）");
            OldNetEnterConfirm = this.GetLocalization(nameof(OldNetEnterConfirm), () => "链路已预热，再次交互，越墙深潜");
            OldNetEnterArmedHint = this.GetLocalization(nameof(OldNetEnterArmedHint), () => "链路已预热：再次交互，越墙深潜");
            OldNetEnterSPOnly = this.GetLocalization(nameof(OldNetEnterSPOnly), () => "深潜仅单人模式可用");
            OldNetDiveCharge = this.GetLocalization(nameof(OldNetDiveCharge), () => "保持下潜，正在穿墙");

            OldNetBandFoot = this.GetLocalization(nameof(OldNetBandFoot), () => "墙脚带 // FOOTHOLD");
            OldNetBandRuin = this.GetLocalization(nameof(OldNetBandRuin), () => "废墟带 // RUINFIELD");
            OldNetBandFade = this.GetLocalization(nameof(OldNetBandFade), () => "信号衰减区 // SIGNAL DECAY");

            GuideNoiseTitle = this.GetLocalization(nameof(GuideNoiseTitle), () => "噪音计");
            GuideNoiseBody = this.GetLocalization(nameof(GuideNoiseBody),
                () => "左下角是噪音计：移动、开火、破解都会点亮你。四道刻度对应四档威胁，过 T2 就会有东西从墙那边过来。静止不动，噪音会自己冷却。");
            GuideLedgerTitle = this.GetLocalization(nameof(GuideLedgerTitle), () => "未铭刻账本");
            GuideLedgerBody = this.GetLocalization(nameof(GuideLedgerBody),
                () => "采到的碎片先进账本（LEDGER 读数），死亡或 RAM 耗尽会全部作废，只有铭刻过的才真正属于你。账本有容量，满载会拒收。");
            GuideNodesTitle = this.GetLocalization(nameof(GuideNodesTitle), () => "节点分级");
            GuideNodesBody = this.GetLocalization(nameof(GuideNodesBody),
                () => "青色节点右键即采；琥珀色加密节点要站桩引导约 3 秒，动静很大但值三倍；红色事件闸一拉，全图封锁解除，清剿波也会立刻到场。");
            GuideRelayTitle = this.GetLocalization(nameof(GuideRelayTitle), () => "中继与登出");
            GuideRelayBody = this.GetLocalization(nameof(GuideRelayBody),
                () => "琥珀光柱是中继站：就地铭刻账本、人不撤，但上行广播很响。薄荷绿光柱是登出终端：铭刻并安全断链。贪还是撤，是这里唯一的问题。");
            GuideDrainTitle = this.GetLocalization(nameof(GuideDrainTitle), () => "距离底噪");
            GuideDrainBody = this.GetLocalization(nameof(GuideDrainBody),
                () => "离墙越远，RAM 底噪越贵，HUD 的 DEPTH 读数旁标着当前每秒消耗。RAM 烧干等于弹出，回程的路费要提前算进去。");
            GuideSkip = this.GetLocalization(nameof(GuideSkip), () => "知道了");

            EntrustTitle = this.GetLocalization(nameof(EntrustTitle), () => "越墙深潜");
            EntrustSummary = this.GetLocalization(nameof(EntrustSummary),
                () => "旧网的入口在出生点上空的坠毁空岛：顺着岛底垂到地面的锚绳爬上去，中舱里发青光的柱子就是接入终端。右键终端预热链路，5 秒内再右键一次即可入网（仅单人模式）。网内右键青色节点回收模具碎片，收获先记在账本上；想带走就回到黑墙脚下，右键薄荷绿光柱登出。安全登出一次，这单委托就算完成。半路 RAM 耗尽或死亡，未铭刻的收获全部作废。");
            EntrustCategory = this.GetLocalization(nameof(EntrustCategory), () => "深潜考古");
            TrackerOverworld = this.GetLocalization(nameof(TrackerOverworld), () => "到出生点上空的坠舱中舱，右键青光终端两次");
            TrackerDive = this.GetLocalization(nameof(TrackerDive), () => "回黑墙脚下，右键薄荷绿光柱安全登出");

            OldNetSignRuin = this.GetLocalization(nameof(OldNetSignRuin),
                () => "废墟带。主产区，加密节点自此出没，引导破解前，先看好退路。");
            OldNetSignFade = this.GetLocalization(nameof(OldNetSignFade),
                () => "信号衰减区。底噪自此陡增，链路撑不了多久，深入自负。");

            OldNetEchoHint = this.GetLocalization(nameof(OldNetEchoHint),
                () => "回声节点：只在时停中可触及（零噪音，产出翻倍）");
            OldNetEchoFizzle = this.GetLocalization(nameof(OldNetEchoFizzle),
                () => "回声太淡，时停中才能触及");
            OldNetCacheHint = this.GetLocalization(nameof(OldNetCacheHint),
                () => "深潜缓存：撬开取一件封存模块（动静不小）");

            OldNetTripwireHint = this.GetLocalization(nameof(OldNetTripwireHint),
                () => "光栅绊网：红线亮起时穿过会上报你的位置，按住右键可剪断");
            OldNetTripwireCut = this.GetLocalization(nameof(OldNetTripwireCut),
                () => "已剪断，这条路安静了");
            OldNetMineHint = this.GetLocalization(nameof(OldNetMineHint),
                () => "哨戒雷：快速靠近会引爆，慢速贴近按住右键可拆除");
            OldNetMineDefused = this.GetLocalization(nameof(OldNetMineDefused),
                () => "已拆除，没有惊动任何东西");
            OldNetMineScream = this.GetLocalization(nameof(OldNetMineScream),
                () => "哨戒雷尖叫，你的位置被广播了");
            OldNetMineDeath = this.GetLocalization(nameof(OldNetMineDeath),
                () => "{0} 踩响了哨戒雷");
            OldNetBulkheadWarn = this.GetLocalization(nameof(OldNetBulkheadWarn),
                () => "检测到高噪信号，竖井闸门预紧");
            OldNetBulkheadShut = this.GetLocalization(nameof(OldNetBulkheadShut),
                () => "竖井已封锁，压低噪音等待重开，或找泄压杆强开");
            OldNetBulkheadReopen = this.GetLocalization(nameof(OldNetBulkheadReopen),
                () => "噪音回落，竖井闸门重开");
            OldNetBreakerHint = this.GetLocalization(nameof(OldNetBreakerHint),
                () => "应急泄压杆：拉下开闸 8 秒，代价是更多噪音");
            OldNetBreakerPulled = this.GetLocalization(nameof(OldNetBreakerPulled),
                () => "泄压完成，闸门临时开启 8 秒");

            OldNetSignCheckpoint = this.GetLocalization(nameof(OldNetSignCheckpoint),
                () => "检疫关卡 K-3。东侧信号未消毒，禁止携带活协议出入。本关卡已于第 771 夜失守。");

            TaggerAttached = this.GetLocalization(nameof(TaggerAttached),
                () => "信号被标记。它在场时，噪音无法自然消散。");
            TracerHowlWarn = this.GetLocalization(nameof(TracerHowlWarn),
                () => "猎犬正在广播你的坐标，打断它。");
            TracerConfused = this.GetLocalization(nameof(TracerConfused),
                () => "路径交叉，它丢失了线索。");
            WardenDispatch = this.GetLocalization(nameof(WardenDispatch),
                () => "回收协议已授权。执行体正在剥离黑墙。");
            WardenSlain = this.GetLocalization(nameof(WardenSlain),
                () => "执行体已归档。全网为你静默 60 秒。");
            WardenGraceHud = this.GetLocalization(nameof(WardenGraceHud),
                () => "静默余量");

            OldNetDockHint = this.GetLocalization(nameof(OldNetDockHint),
                () => "右键扩容：本次深潜账本 +8 格，噪音 +15");
            OldNetDockDone = this.GetLocalization(nameof(OldNetDockDone), () => "账本扩容 +8 格");

            OldNetColdHint = this.GetLocalization(nameof(OldNetColdHint),
                () => "右键提取：耗 2 RAM，无声");
            OldNetColdNoRam = this.GetLocalization(nameof(OldNetColdNoRam), () => "RAM 不足，提取需要 2 点");

            OldNetEscrowHint = this.GetLocalization(nameof(OldNetEscrowHint),
                () => "右键投保：收账本三成作保费，上行噪音 +10。烧断或死亡时其余照赔，安全登出不退保费");
            OldNetEscrowEmpty = this.GetLocalization(nameof(OldNetEscrowEmpty), () => "账本是空的，无可投保");
            OldNetEscrowTooThin = this.GetLocalization(nameof(OldNetEscrowTooThin),
                () => "账本太薄，保费会吃掉一切，无可投保");
            OldNetEscrowSigned = this.GetLocalization(nameof(OldNetEscrowSigned),
                () => "已投保 {0} 枚，保费 {1} 枚");
            OldNetEscrowPayout = this.GetLocalization(nameof(OldNetEscrowPayout),
                () => "保险兑付 {0} 枚模具碎片");

            OldNetVaultHint = this.GetLocalization(nameof(OldNetVaultHint),
                () => "右键开台破译：耗 3 RAM，开台后每秒 +2 噪音。每关赌注升级，脱靶彩池清空");
            OldNetVaultNoRam = this.GetLocalization(nameof(OldNetVaultNoRam), () => "RAM 不足，开台需要 3 点");
            OldNetVaultLocked = this.GetLocalization(nameof(OldNetVaultLocked), () => "主控台正在运行");
            OldNetVaultPayout = this.GetLocalization(nameof(OldNetVaultPayout),
                () => "彩池入账 {0} 枚碎片（未铭刻）");
            VaultTitle = this.GetLocalization(nameof(VaultTitle), () => "主控破译矩阵");
            VaultGuide = this.GetLocalization(nameof(VaultGuide),
                () => "指针扫进亮弧时，点左键或按跳跃键确认。转动中受击或离台，视同脱靶");
            VaultStage = this.GetLocalization(nameof(VaultStage), () => "第 {0} 关 / 共 5 关");
            VaultPot = this.GetLocalization(nameof(VaultPot), () => "彩池：碎片 ×{0}");
            VaultPotModule = this.GetLocalization(nameof(VaultPotModule), () => "封存模块 ×{0}");
            VaultPotChip = this.GetLocalization(nameof(VaultPotChip), () => "RAM 扩容芯片 ×1");
            VaultLedgerRoom = this.GetLocalization(nameof(VaultLedgerRoom), () => "账本余位 {0}");
            VaultCashOut = this.GetLocalization(nameof(VaultCashOut), () => "收手结算");
            VaultContinue = this.GetLocalization(nameof(VaultContinue), () => "下一关");
            VaultBust = this.GetLocalization(nameof(VaultBust), () => "爆仓，彩池清空");
            VaultCounterHack = this.GetLocalization(nameof(VaultCounterHack), () => "主控台反制：骇入注入");
            VaultDiscard = this.GetLocalization(nameof(VaultDiscard), () => "账本装不下，{0} 枚弃置");

            OldNetEncryptFadeHint = this.GetLocalization(nameof(OldNetEncryptFadeHint),
                () => "引导破解：疯域的锁会反咬，完成后必引来追猎");
            OldNetAftershockWarn = this.GetLocalization(nameof(OldNetAftershockWarn),
                () => "残响暴露，回声正在回溯");
            OldNetAftershockHit = this.GetLocalization(nameof(OldNetAftershockHit),
                () => "回溯完成，它们来了");
            OldNetDragnetWarn = this.GetLocalization(nameof(OldNetDragnetWarn),
                () => "网络正在收束，威胁即将不可逆 // DRAGNET 75%");
            OldNetDragnetOn = this.GetLocalization(nameof(OldNetDragnetOn),
                () => "收网开始，封锁不再解除，加密锁已全网离线 // DRAGNET");
            OldNetHotExtractHint = this.GetLocalization(nameof(OldNetHotExtractHint),
                () => "链路过热：断链需要站桩 10 秒，网络会扑过来");
            OldNetHotExtractStart = this.GetLocalization(nameof(OldNetHotExtractStart),
                () => "强制断链开始，扛住 // SEVERING");
            OldNetHotExtractAbort = this.GetLocalization(nameof(OldNetHotExtractAbort),
                () => "断链中止，链路保持");
            OldNetHotExtractTooFar = this.GetLocalization(nameof(OldNetHotExtractTooFar),
                () => "离终端太远，贴近后再断链");
        }
    }
}
