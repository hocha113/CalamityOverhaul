namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    //百鬼全组调音常量（P4 计划书 §2.1-2.5 数值表全量落名 + 表外正文数值补录）
    //进度锚 = 专家困难模式中期（裁决12），生产门轮统一重标只动此文件
    //W2 各敌人包只读不添（五路并发不许改同一文件）；不触碰 Gen/KiyumeMetrics.cs
    internal static class KiyumeYokaiMetrics
    {
        //════════ 共用：统一现形语法（§2.0，数值同源 KiyumeHoundShade.Advance） ════════

        //雾浓度项归一下限：DensityAt 低于此值现形为零
        internal const float RevealFogFloor = 0.28f;
        //雾浓度项归一跨度：(DensityAt − Floor) / Span 钳 [0,1]
        internal const float RevealFogSpan = 0.26f;
        //浓度采样点离地抬升（px）：贴脚采样会吃到地面雾元的边
        internal const float RevealFogLiftPx = 24f;

        //潮相门控总开关（§3.3）：W4 已核潮汐权威化落地后翻开（移交项2）——
        //服务器在 KiyumeFogSystem.PostUpdateEverything 的 dedServ 分支推 KiyumeFogTide.Update()，
        //KiyumeTideNet 600t 下行对钟；两个消费口（井手 CurrentAlertThreshold、导演 PumpCortege）
        //均在服务器侧读 LineWorldY，潮位归一恰等于 Tide∈[0,1]，CortegeTideGate=0.75 每主周期可达
        internal const bool TideGateEnabled = true;

        //════════ 提灯翁 Lantern*（§2.1） ════════

        //进世界首发延迟（tick）
        internal const int LanternFirstDelay = 3600;
        //事件冷却（tick）
        internal const int LanternCooldown = 5400;
        //玩家前方路面生成带（px）
        internal const float LanternSpawnDistMin = 700f;
        internal const float LanternSpawnDistMax = 1100f;
        //生成位解析浓度下限（雾里才有它）
        internal const float LanternSpawnFogMin = 0.30f;
        //步速（px/f）
        internal const float LanternWalkSpeed = 1.05f;
        //跟随计量带（px）
        internal const float LanternFollowBandNear = 96f;
        internal const float LanternFollowBandFar = 460f;
        //判「被跟随」阈 / 计量上限
        internal const int LanternFollowGoal = 600;
        internal const int LanternFollowCap = 900;
        //惊扰半径（px）
        internal const float LanternScareRadius = 96f;
        //转身公平前摇（tick）
        internal const int LanternTurnTicks = 20;
        //冷握扣 maxHP 比例 + 黑暗时长（tick）
        internal const float LanternGripFrac = 0.12f;
        internal const int LanternGripDarkTicks = 300;
        //悬灯寿命（tick）/ 清雾圈（px）/ 压制强度
        internal const int LanternRewardLife = 5400;
        internal const float LanternRewardRadius = 260f;
        internal const float LanternRewardStrength = 0.55f;
        //名义血量：任何伤害即触发惊扰序列，不可击杀获利，无掉落
        internal const int LanternLife = 600;

        //──── 表外正文数值（§2.1 正文） ────

        //灯前静立（tick）
        internal const int LanternIdleTicks = 90;
        //跟随计量判定周期（tick）/ 带内增量 / 带外衰减
        internal const int LanternFollowJudgeTicks = 10;
        internal const int LanternFollowGain = 10;
        internal const int LanternFollowLoss = 6;
        //冷握结算距离（px）：Turn 结束帧对此距离内最近玩家结算
        internal const float LanternGripRange = 140f;
        //目的地取距生成位不小于此值的锚点（px）
        internal const float LanternDestMinDist = 800f;
        //走不动自动 Arrive（tick）
        internal const int LanternStuckArriveTicks = 400;
        //锚点全空回退：走向枯林方向这么远处（px）
        internal const float LanternFallbackDestPx = 1200f;
        //滩涂水线目的地：WaterRightPx 回退量（px）
        internal const float LanternWaterEdgeBackPx = 160f;
        //悬灯清雾续订：ttl（帧）/ 羽化（px）
        internal const int LanternRewardTtl = 4;
        internal const float LanternRewardFeatherPx = 200f;
        //悬灯暖光色与总乘（Lighting.AddLight）
        internal const float LanternLightR = 1.0f;
        internal const float LanternLightG = 0.62f;
        internal const float LanternLightB = 0.30f;
        internal const float LanternLightMul = 0.9f;
        //纸衣化下摆碎边 uDissolve 常值
        internal const float LanternPaperDissolve = 0.12f;
        //Turn 拍帽下余烬 uEyeGlow 峰值（无面者 Reveal 拍同源）
        internal const float LanternEyeGlowMax = 0.35f;

        //════════ 井手 Well*（§2.2） ════════

        //听觉声明：井口收听半径（px）/ 警觉阈（噪点）
        internal const float WellHearRadius = 340f;
        internal const float WellHearThreshold = 30f;
        //P2 通道未就绪的回退阈：半径内玩家速度门（px/f）/ 累计触发（tick）
        internal const float WellFallbackSpeedGate = 2.8f;
        internal const int WellFallbackChargeTicks = 20;
        //前摇（tick）：井口水声 + 怨雾上涌，公平阀
        internal const int WellArmTicks = 14;
        //强袭行程（px）与用时（tick）
        internal const float WellStrikeRise = 52f;
        internal const int WellStrikeTicks = 8;
        //接触伤（仅强袭窗口）+ 命中迟缓时长（tick）
        internal const int WellBiteDamage = 60;
        internal const int WellBiteSlowTicks = 180;
        //回收（可打，处决窗口）/ 再触发冷却（tick）
        internal const int WellReelTicks = 30;
        internal const int WellCooldown = 480;
        //血量 / 防御；击杀 = 本井会话内永久静默
        internal const int WellLife = 260;
        internal const int WellDefense = 8;
        //潮位过此行（tile 行）警觉阈值打折（TideGateEnabled 开启后生效）
        internal const int WellFloodGate = 440;
        internal const float WellFloodAlertMul = 0.5f;

        //──── 表外正文数值（§2.2 正文） ────

        //休眠态藏井口下几格
        internal const int WellHideRowsBelow = 2;

        //════════ 守田人 Scare*（§2.3） ════════

        //行动判定节拍（tick）
        internal const int ScareJudgeInterval = 30;
        //单次挪步（px）
        internal const float ScareStepMin = 32f;
        internal const float ScareStepMax = 80f;
        //行动权重：挪步 / 消隐 / 复现
        internal const int ScareWeightStep = 60;
        internal const int ScareWeightVanish = 15;
        internal const int ScareWeightReturn = 25;
        //保守视窗半径（px，观测判定）
        internal const float ScareViewHalfW = 1010f;
        internal const float ScareViewHalfH = 640f;
        //解析浓度过此值 = 看不见（雾深处等同没被看见）
        internal const float ScareFogBlind = 0.62f;
        //袭击贴身距（px）/ 贴身未观测时长（tick）
        internal const float ScareStrikeRange = 60f;
        internal const int ScareUnseenTicks = 120;
        //收割一击（打完自毁散作干草）
        internal const int ScareStrikeDamage = 70;
        //识破距离（px）/ 全员离开后解冻延迟（tick）
        internal const float ScareSpotRange = 300f;
        internal const int ScareRefreezeTicks = 300;
        //脆皮；击杀触发盲拆惩罚（未被观测者获得一次免费行动）
        internal const int ScareLife = 90;
        //初始布防数 / 会话补员池 / 在场上限
        internal const int ScareFieldInit = 5;
        internal const int ScarePool = 3;
        internal const int ScareCap = 7;

        //──── 表外正文数值（§2.3 正文） ────

        //复现落点距最近玩家的带（px）
        internal const float ScareReturnMin = 240f;
        internal const float ScareReturnMax = 480f;
        //识破后：此范围内有人期间保持死物（px）
        internal const float ScareFreezeHoldRange = 600f;

        //════════ 夜行列 Cortege*（§2.4） ════════

        //事件冷却（tick，全场唯一）
        internal const int CortegeCooldown = 14400;
        //潮位归一触发窗（TideGateEnabled 关闭时退化纯冷却）
        internal const float CortegeTideGate = 0.75f;
        //队列速（px/f）/ 纵列间距（px）
        internal const float CortegeWalkSpeed = 0.8f;
        internal const float CortegeSpacing = 52f;
        //挡路判定带（±px）/ 驻留时长（tick）
        internal const float CortegeBlockBand = 40f;
        internal const int CortegeBlockTicks = 90;
        //全列回头静止拍（tick）
        internal const int CortegeTurnBeat = 20;
        //化煞抬棺者：冲刹速（px/f）/ 接触伤 / 血量 / 防御
        internal const float CortegeWraithSpeed = 3.4f;
        internal const int CortegeWraithDamage = 55;
        internal const int CortegeWraithLife = 180;
        internal const int CortegeWraithDefense = 6;
        //执幡者：血量 / 防御 / 铃周期（tick）/ 铃迟缓（tick）/ 铃半径（px）
        internal const int CortegeLeadLife = 320;
        internal const int CortegeLeadDefense = 10;
        internal const int CortegeBellPeriod = 90;
        internal const int CortegeBellSlowTicks = 60;
        internal const float CortegeBellRadius = 500f;
        //化煞自散时限（tick，45s）
        internal const int CortegeRageTimeout = 2700;
        //未惊扰供品总开关（裁决13 解禁）与内容：心 ×N + 银 ×N
        internal const bool CortegeRewardOn = true;
        internal const int CortegeRewardHearts = 2;
        internal const int CortegeRewardSilver = 50;

        //──── 表外正文数值（§2.4 正文） ────

        //枯林东段生成列（tile 列，中心 ± 抖动）
        internal const int CortegeSpawnColCenter = 2300;
        internal const int CortegeSpawnColJitter = 100;
        //墓地锚点缺失回退目的地（tile 列平地点）
        internal const int CortegeFallbackDestCol = 1850;
        //成员触碰判定（px）
        internal const float CortegeTouchRange = 24f;
        //化煞冲-刹循环周期（tick）
        internal const int CortegeWraithDashPeriod = 40;
        //坟前低头（tick）
        internal const int CortegeBowTicks = 40;
        //白灯笼 tint 转红时长（tick）
        internal const int CortegeLanternRedTicks = 30;
        //队首探针失败累计此时长全队入土消散（tick）
        internal const int CortegeStuckDissolveTicks = 200;
        //棺 prop 会话上限（具）
        internal const int CortegeCoffinSessionCap = 2;

        //════════ 无面者 Faceless*（§2.5） ════════

        //帧停距 / 触发距（px）
        internal const float FacelessAwareRange = 240f;
        internal const float FacelessTriggerRange = 96f;
        //对视累计（tick）
        internal const int FacelessGazeTicks = 60;
        //尖啸拍 / 化雾（tick）
        internal const int FacelessShriekBeat = 30;
        internal const int FacelessDissolveTicks = 40;
        //触发者黑暗时长（tick）
        internal const int FacelessDarkTicks = 120;
        //一次性背向击退（px/f）
        internal const float FacelessKnockback = 8f;
        //重现冷却随机带（tick）
        internal const int FacelessCooldownMin = 9000;
        internal const int FacelessCooldownMax = 12600;
        //会话现身上限（per-player，ModPlayer 计数）
        internal const int FacelessSessionCap = 3;
        //名义血量，不可真死（CheckDead 拦截，life 归 1 转 Dissolve）
        internal const int FacelessLife = 300;

        //──── 表外正文数值（§2.5 正文） ────

        //回退生成：村落带距玩家不小于此值的平地点（px）
        internal const float FacelessSpawnMinDist = 600f;

        //════════ 雾脊行者 Ridge*（P4 §1 点子11，R2 内容波授权尾追） ════════

        //导演泵：首发延迟 / 冷却（tick，任务书 ~7200t）
        internal const int RidgeFirstDelay = 1800;
        internal const int RidgeCooldown = 7200;
        //出没潮窗（潮位归一 0=退 1=涨满）：0.7 ⟺ 雾线 ≤ 约 418.8 行，村落地板全没只剩屋顶；
        //退场滞回 0.6，两阈之间不进不退
        internal const float RidgeTideGateOn = 0.7f;
        internal const float RidgeTideGateOff = 0.6f;
        //生成带：锚玩家同侧屋脊线距离（px）
        internal const float RidgeSpawnDistMin = 400f;
        internal const float RidgeSpawnDistMax = 700f;
        //保持距离带（px）：带内同向同速；近于 Near 加速拉开；远于 Far 收拢补速
        internal const float RidgeBandNear = 260f;
        internal const float RidgeBandFar = 520f;
        //同速上限 / 被逼近拉开速 / 出带收拢补速（px/f）
        internal const float RidgeMaxWalkSpeed = 3.0f;
        internal const float RidgePullSpeed = 3.2f;
        internal const float RidgeCatchUp = 0.6f;
        //抢屋脊判定：同脊横距（px）/ 脚底高差（px，=屋顶路线 RoofStepMaxDh×16）/ 持续（tick）
        internal const float RidgeStealDistPx = 160f;
        internal const float RidgeStealDyPx = 64f;
        internal const int RidgeStealTicks = 30;
        //冷视一拍（tick）与眼光峰值
        internal const int RidgeStareTicks = 30;
        internal const float RidgeEyeGlowMax = 0.45f;
        //坠雾结算：象征伤 / 黑暗（tick）/ 横推离脊（px/f）/ 微抬（px/f，贴地摩擦会吞横速）
        internal const int RidgePunishDamage = 12;
        internal const int RidgePunishDarkTicks = 180;
        internal const float RidgeDropPushX = 7.5f;
        internal const float RidgeDropLiftY = 2.0f;
        //化雾（tick）
        internal const int RidgeDissolveTicks = 40;
        //退场：走离速（px/f）/ 距全员此远即散（px）/ 走不出去的超时兜底（tick）
        internal const float RidgeLeaveSpeed = 2.6f;
        internal const float RidgeLeaveDistPx = 1100f;
        internal const int RidgeLeaveTimeoutTicks = 900;
        //名义血量：任何伤害即触怒序列，不可击杀获利，无掉落
        internal const int RidgeLife = 600;
        //现形语法（本怪极性）：距离项近隐远显（px）；潮位项地板/跨度（潮落向退场线身形转薄）
        internal const float RidgeFadeNearPx = 120f;
        internal const float RidgeFadeFarPx = 300f;
        internal const float RidgeTideFadeFloor = 0.55f;
        internal const float RidgeTideFadeSpan = 0.15f;

        //════════ 水中手 Shallow*（R2-A，P4 点子6） ════════

        //潮相门控：潮位归一过此值涨潮布防 / 低于此值退潮回收（滞回带防抖；
        //TideGateEnabled 关闭时本内容整体缺席，潮相存在无退化路径）
        internal const float ShallowRiseTide = 0.6f;
        internal const float ShallowEbbTide = 0.5f;
        //布防列窗（tile 列，滩涂湿滩带水线附近）与每茬只数
        internal const int ShallowColMin = 330;
        internal const int ShallowColMax = 440;
        internal const int ShallowCountMin = 3;
        internal const int ShallowCountMax = 5;
        //抓距（px）：站进即触发咬合（唯一伤害窗，主动点名结算非接触伤）
        internal const float ShallowGrabRange = 48f;
        //咬合前摇（tick，手扬起的可读拍，冲刺可脱）与结算距（px，前摇内退出=脱手）
        internal const int ShallowSnapTicks = 6;
        internal const float ShallowSnapSettleRange = 64f;
        //一口伤害 + 迟缓（tick）
        internal const int ShallowBiteDamage = 45;
        internal const int ShallowBiteSlowTicks = 120;
        //立起 / 缩回冷却 / 沉泥退场（tick）
        internal const int ShallowRiseTicks = 24;
        internal const int ShallowRetractTicks = 240;
        internal const int ShallowSinkTicks = 30;
        //血防：可打；打死的这一茬不补，下个涨潮窗才有新手
        internal const int ShallowLife = 110;
        internal const int ShallowDefense = 6;

        //════════ 蓑翁 Mino*（R2-A，P4 点子7） ════════

        //触怒几何：正面半径（px）× 正面锥点积下限（朝向恒向西，纯几何方位角）
        internal const float MinoWakeRadius = 140f;
        internal const float MinoFrontDot = 0.25f;
        //转身拍（tick）与定向拍击结算距（px）/ 伤害
        internal const int MinoTurnTicks = 18;
        internal const float MinoSlapRange = 150f;
        internal const int MinoSlapDamage = 55;
        //化雾退场（tick）
        internal const int MinoDissolveTicks = 36;
        //落座列窗（tile 列，滩涂水线边）
        internal const int MinoSpawnColMin = 324;
        internal const int MinoSpawnColMax = 356;
        //退场后再现冷却（tick，在场不计冷却）与会话现身上限
        internal const int MinoRespawnCooldown = 5400;
        internal const int MinoSessionCap = 2;
        //名义血量：不可真死，受击即触怒序列
        internal const int MinoLife = 400;
        //落座与玩家最小距离（px，防落座上屏）
        internal const float MinoSpawnMinDist = 500f;
    }
}
