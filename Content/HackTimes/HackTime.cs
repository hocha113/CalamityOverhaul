using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇客时间核心状态管理器
    /// <br/>控制骇客模式的激活、目标选择、运镜和时间冻结
    /// <br/>按键切换进入或退出，进入后世界冻结，屏幕叠加赛博科技感滤镜
    /// </summary>
    internal class HackTime : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public override void Unload() {
            Reset();
            //模组卸载时也要专门卸载条件注册，写在这里提醒自己
            HackTimeAccess.Reset();
        }

        public override void OnWorldUnload() => Reset();

        #region 本地化字段

        public static LocalizedText Locked { get; private set; }
        public static LocalizedText Done { get; private set; }
        public static LocalizedText Queued { get; private set; }
        public static LocalizedText UploadingText { get; private set; }
        public static LocalizedText BreachReady { get; private set; }
        public static LocalizedText UploadComplete { get; private set; }
        public static LocalizedText UploadQueue { get; private set; }
        public static LocalizedText TargetLocked { get; private set; }
        public static LocalizedText HpFormat { get; private set; }
        public static LocalizedText Protocols { get; private set; }
        public static LocalizedText RamDepleted { get; private set; }
        public static LocalizedText LowRam { get; private set; }
        public static LocalizedText Scanning { get; private set; }
        public static LocalizedText AnalysisComplete { get; private set; }
        public static LocalizedText TypeLabel { get; private set; }
        public static LocalizedText BossClass { get; private set; }
        public static LocalizedText EliteUnit { get; private set; }
        public static LocalizedText HostileEntity { get; private set; }
        public static LocalizedText TownNpc { get; private set; }
        public static LocalizedText FriendlyUnit { get; private set; }
        public static LocalizedText PassiveCritter { get; private set; }
        public static LocalizedText NeutralEntity { get; private set; }
        public static LocalizedText ThreatLabel { get; private set; }
        public static LocalizedText ThreatExtreme { get; private set; }
        public static LocalizedText ThreatHigh { get; private set; }
        public static LocalizedText ThreatModerate { get; private set; }
        public static LocalizedText ThreatLow { get; private set; }
        public static LocalizedText DefLabel { get; private set; }
        public static LocalizedText DmgLabel { get; private set; }
        public static LocalizedText KbResLabel { get; private set; }
        public static LocalizedText Breach { get; private set; }
        public static LocalizedText InitBreach { get; private set; }
        public static LocalizedText SystemBreach { get; private set; }
        public static LocalizedText Rebooting { get; private set; }
        public static LocalizedText SystemOnline { get; private set; }
        public static LocalizedText MemoryWiped { get; private set; }
        public static LocalizedText Cyberpsychosis { get; private set; }
        public static LocalizedText RamRefund { get; private set; }
        public static LocalizedText ActiveText { get; private set; }
        public static LocalizedText ActivePct { get; private set; }
        public static LocalizedText Complete { get; private set; }
        public static LocalizedText UploadingPct { get; private set; }
        public static LocalizedText CatLethal { get; private set; }
        public static LocalizedText CatControl { get; private set; }
        public static LocalizedText CatCovert { get; private set; }
        public static LocalizedText CatContagion { get; private set; }
        public static LocalizedText CatUnknown { get; private set; }
        public static LocalizedText CatTileManip { get; private set; }
        public static LocalizedText CatParanormal { get; private set; }

        //乱码鬼扫描本地化字段
        public static LocalizedText WraithScanName { get; private set; }
        public static LocalizedText WraithScanNameValue { get; private set; }
        public static LocalizedText WraithScanType { get; private set; }
        public static LocalizedText WraithScanThreat { get; private set; }
        public static LocalizedText WraithScanStatus { get; private set; }
        public static LocalizedText WraithScanStatusStalking { get; private set; }
        public static LocalizedText WraithScanStatusWatched { get; private set; }
        public static LocalizedText WraithScanStatusHalt { get; private set; }
        public static LocalizedText WraithScanStatusMemory { get; private set; }
        public static LocalizedText WraithScanStatusDismember { get; private set; }
        public static LocalizedText WraithScanIntegrity { get; private set; }
        public static LocalizedText WraithScanIntegrityValue { get; private set; }
        public static LocalizedText WraithScanOrigin { get; private set; }
        public static LocalizedText WraithScanOriginValue { get; private set; }

        //物块扫描本地化字段
        public static LocalizedText TileScanName { get; private set; }
        public static LocalizedText TileScanClass { get; private set; }
        public static LocalizedText TileScanSize { get; private set; }
        public static LocalizedText TileScanHardness { get; private set; }
        public static LocalizedText TileScanStatus { get; private set; }
        public static LocalizedText TileScanCrafting { get; private set; }
        public static LocalizedText TileScanContainer { get; private set; }
        public static LocalizedText TileScanLight { get; private set; }
        public static LocalizedText TileScanFurniture { get; private set; }
        public static LocalizedText TileScanBlock { get; private set; }
        public static LocalizedText TileScanDungeon { get; private set; }
        public static LocalizedText TileScanLihzahrd { get; private set; }
        public static LocalizedText TileScanHardnessExtreme { get; private set; }
        public static LocalizedText TileScanHardnessHigh { get; private set; }
        public static LocalizedText TileScanHardnessNormal { get; private set; }
        public static LocalizedText TileScanHardnessLow { get; private set; }
        public static LocalizedText TileScanActive { get; private set; }
        public static LocalizedText TileScanInactive { get; private set; }
        public static LocalizedText TileScanSealed { get; private set; }
        public static LocalizedText TileScanOnline { get; private set; }
        public static LocalizedText TileScanIntact { get; private set; }
        public static LocalizedText TileScanMisc { get; private set; }
        public static LocalizedText TileScanMiscPile { get; private set; }

        //弹幕扫描本地化字段
        public static LocalizedText ProjectileScanName { get; private set; }
        public static LocalizedText ProjectileScanClass { get; private set; }
        public static LocalizedText ProjectileScanSpeed { get; private set; }
        public static LocalizedText ProjectileScanKnockback { get; private set; }
        public static LocalizedText ProjectileScanPenetrate { get; private set; }
        public static LocalizedText ProjectileScanTimeLeft { get; private set; }
        public static LocalizedText ProjectileScanOwner { get; private set; }
        public static LocalizedText ProjectileScanAI { get; private set; }
        public static LocalizedText ProjectileScanPosition { get; private set; }
        public static LocalizedText ProjectileScanHostile { get; private set; }
        public static LocalizedText ProjectileScanFriendly { get; private set; }
        public static LocalizedText ProjectileScanNeutral { get; private set; }
        public static LocalizedText ProjectileScanMinion { get; private set; }
        public static LocalizedText ProjectileScanSentry { get; private set; }
        public static LocalizedText ProjectileScanTrap { get; private set; }
        public static LocalizedText ProjectileScanInfinite { get; private set; }
        public static LocalizedText ProjectileScanOwnerWorld { get; private set; }

        //掉落物扫描本地化字段
        public static LocalizedText ItemScanName { get; private set; }
        public static LocalizedText ItemScanClass { get; private set; }
        public static LocalizedText ItemScanStack { get; private set; }
        public static LocalizedText ItemScanValue { get; private set; }
        public static LocalizedText ItemScanRarity { get; private set; }
        public static LocalizedText ItemScanCombat { get; private set; }
        public static LocalizedText ItemScanUtility { get; private set; }
        public static LocalizedText ItemScanPrefix { get; private set; }
        public static LocalizedText ItemScanTypeId { get; private set; }
        public static LocalizedText ItemScanPosition { get; private set; }
        public static LocalizedText ItemScanWeapon { get; private set; }
        public static LocalizedText ItemScanTool { get; private set; }
        public static LocalizedText ItemScanArmor { get; private set; }
        public static LocalizedText ItemScanAccessory { get; private set; }
        public static LocalizedText ItemScanAmmo { get; private set; }
        public static LocalizedText ItemScanConsumable { get; private set; }
        public static LocalizedText ItemScanPlaceable { get; private set; }
        public static LocalizedText ItemScanMaterial { get; private set; }
        public static LocalizedText ItemScanQuest { get; private set; }
        public static LocalizedText ItemScanMisc { get; private set; }
        public static LocalizedText ItemScanNone { get; private set; }
        public static LocalizedText ItemScanNoValue { get; private set; }

        //液体扫描本地化字段
        public static LocalizedText WaterScanLiquid { get; private set; }
        public static LocalizedText WaterScanEnvironment { get; private set; }
        public static LocalizedText WaterScanDepth { get; private set; }
        public static LocalizedText WaterScanWorldLayer { get; private set; }
        public static LocalizedText WaterScanTileCoord { get; private set; }
        public static LocalizedText WaterScanContainment { get; private set; }
        public static LocalizedText WaterScanWater { get; private set; }
        public static LocalizedText WaterScanLava { get; private set; }
        public static LocalizedText WaterScanHoney { get; private set; }
        public static LocalizedText WaterScanShimmer { get; private set; }
        public static LocalizedText WaterScanLayerSky { get; private set; }
        public static LocalizedText WaterScanLayerSurface { get; private set; }
        public static LocalizedText WaterScanLayerUnderground { get; private set; }
        public static LocalizedText WaterScanLayerCavern { get; private set; }
        public static LocalizedText WaterScanLayerUnderworld { get; private set; }
        public static LocalizedText WaterScanEnvOcean { get; private set; }
        public static LocalizedText WaterScanEnvDesert { get; private set; }
        public static LocalizedText WaterScanEnvSnow { get; private set; }
        public static LocalizedText WaterScanEnvJungle { get; private set; }
        public static LocalizedText WaterScanEnvCorruption { get; private set; }
        public static LocalizedText WaterScanEnvCrimson { get; private set; }
        public static LocalizedText WaterScanEnvHallow { get; private set; }
        public static LocalizedText WaterScanEnvDungeon { get; private set; }
        public static LocalizedText WaterScanEnvMushroom { get; private set; }
        public static LocalizedText WaterScanEnvUnderworld { get; private set; }
        public static LocalizedText WaterScanContainmentPocket { get; private set; }
        public static LocalizedText WaterScanContainmentChannel { get; private set; }
        public static LocalizedText WaterScanContainmentOpen { get; private set; }
        public static LocalizedText WaterScanStatusStill { get; private set; }
        public static LocalizedText WaterScanStatusFlowing { get; private set; }

        //炮台扫描本地化字段
        public static LocalizedText TurretScanName { get; private set; }
        public static LocalizedText TurretScanLaserName { get; private set; }
        public static LocalizedText TurretScanGatlinName { get; private set; }
        public static LocalizedText TurretScanType { get; private set; }
        public static LocalizedText TurretScanPhase { get; private set; }
        public static LocalizedText TurretScanPhaseIdle { get; private set; }
        public static LocalizedText TurretScanPhaseCharging { get; private set; }
        public static LocalizedText TurretScanPhaseFiring { get; private set; }
        public static LocalizedText TurretScanPhaseCooldown { get; private set; }
        public static LocalizedText TurretScanPhaseLocking { get; private set; }
        public static LocalizedText TurretScanCircuit { get; private set; }
        public static LocalizedText TurretScanCircuitOnline { get; private set; }
        public static LocalizedText TurretScanCircuitShorted { get; private set; }
        public static LocalizedText TurretScanCircuitOverload { get; private set; }

        //信号塔扫描本地化字段
        public static LocalizedText SignalTowerScanName { get; private set; }
        public static LocalizedText SignalTowerScanType { get; private set; }
        public static LocalizedText SignalTowerScanThreat { get; private set; }
        public static LocalizedText SignalTowerScanStatus { get; private set; }
        public static LocalizedText SignalTowerScanStatusOnline { get; private set; }
        public static LocalizedText SignalTowerScanStatusBroadcasting { get; private set; }
        public static LocalizedText SignalTowerScanStatusElectrified { get; private set; }
        public static LocalizedText SignalTowerScanProtocol { get; private set; }

        public static LocalizedText RightClickHint { get; private set; }

        //权限校验失败弹窗本地化字段
        public static LocalizedText AccessDeniedTitle { get; private set; }
        public static LocalizedText AccessDeniedDesc { get; private set; }

        public override void SetStaticDefaults() {
            Locked = this.GetLocalization(nameof(Locked));
            Done = this.GetLocalization(nameof(Done));
            Queued = this.GetLocalization(nameof(Queued));
            UploadingText = this.GetLocalization(nameof(UploadingText));
            BreachReady = this.GetLocalization(nameof(BreachReady));
            UploadComplete = this.GetLocalization(nameof(UploadComplete));
            UploadQueue = this.GetLocalization(nameof(UploadQueue));
            TargetLocked = this.GetLocalization(nameof(TargetLocked));
            HpFormat = this.GetLocalization(nameof(HpFormat));
            Protocols = this.GetLocalization(nameof(Protocols));
            RamDepleted = this.GetLocalization(nameof(RamDepleted));
            LowRam = this.GetLocalization(nameof(LowRam));
            Scanning = this.GetLocalization(nameof(Scanning));
            AnalysisComplete = this.GetLocalization(nameof(AnalysisComplete));
            TypeLabel = this.GetLocalization(nameof(TypeLabel));
            BossClass = this.GetLocalization(nameof(BossClass));
            EliteUnit = this.GetLocalization(nameof(EliteUnit));
            HostileEntity = this.GetLocalization(nameof(HostileEntity));
            TownNpc = this.GetLocalization(nameof(TownNpc));
            FriendlyUnit = this.GetLocalization(nameof(FriendlyUnit));
            PassiveCritter = this.GetLocalization(nameof(PassiveCritter));
            NeutralEntity = this.GetLocalization(nameof(NeutralEntity));
            ThreatLabel = this.GetLocalization(nameof(ThreatLabel));
            ThreatExtreme = this.GetLocalization(nameof(ThreatExtreme));
            ThreatHigh = this.GetLocalization(nameof(ThreatHigh));
            ThreatModerate = this.GetLocalization(nameof(ThreatModerate));
            ThreatLow = this.GetLocalization(nameof(ThreatLow));
            DefLabel = this.GetLocalization(nameof(DefLabel));
            DmgLabel = this.GetLocalization(nameof(DmgLabel));
            KbResLabel = this.GetLocalization(nameof(KbResLabel));
            Breach = this.GetLocalization(nameof(Breach));
            InitBreach = this.GetLocalization(nameof(InitBreach));
            SystemBreach = this.GetLocalization(nameof(SystemBreach));
            Rebooting = this.GetLocalization(nameof(Rebooting));
            SystemOnline = this.GetLocalization(nameof(SystemOnline));
            MemoryWiped = this.GetLocalization(nameof(MemoryWiped));
            Cyberpsychosis = this.GetLocalization(nameof(Cyberpsychosis));
            RamRefund = this.GetLocalization(nameof(RamRefund));
            ActiveText = this.GetLocalization(nameof(ActiveText));
            ActivePct = this.GetLocalization(nameof(ActivePct));
            Complete = this.GetLocalization(nameof(Complete));
            UploadingPct = this.GetLocalization(nameof(UploadingPct));
            CatLethal = this.GetLocalization(nameof(CatLethal));
            CatControl = this.GetLocalization(nameof(CatControl));
            CatCovert = this.GetLocalization(nameof(CatCovert));
            CatContagion = this.GetLocalization(nameof(CatContagion));
            CatUnknown = this.GetLocalization(nameof(CatUnknown));
            CatTileManip = this.GetLocalization(nameof(CatTileManip));
            CatParanormal = this.GetLocalization(nameof(CatParanormal));

            WraithScanName = this.GetLocalization(nameof(WraithScanName));
            WraithScanNameValue = this.GetLocalization(nameof(WraithScanNameValue));
            WraithScanType = this.GetLocalization(nameof(WraithScanType));
            WraithScanThreat = this.GetLocalization(nameof(WraithScanThreat));
            WraithScanStatus = this.GetLocalization(nameof(WraithScanStatus));
            WraithScanStatusStalking = this.GetLocalization(nameof(WraithScanStatusStalking));
            WraithScanStatusWatched = this.GetLocalization(nameof(WraithScanStatusWatched));
            WraithScanStatusHalt = this.GetLocalization(nameof(WraithScanStatusHalt));
            WraithScanStatusMemory = this.GetLocalization(nameof(WraithScanStatusMemory));
            WraithScanStatusDismember = this.GetLocalization(nameof(WraithScanStatusDismember));
            WraithScanIntegrity = this.GetLocalization(nameof(WraithScanIntegrity));
            WraithScanIntegrityValue = this.GetLocalization(nameof(WraithScanIntegrityValue));
            WraithScanOrigin = this.GetLocalization(nameof(WraithScanOrigin));
            WraithScanOriginValue = this.GetLocalization(nameof(WraithScanOriginValue));

            TileScanName = this.GetLocalization(nameof(TileScanName));
            TileScanClass = this.GetLocalization(nameof(TileScanClass));
            TileScanSize = this.GetLocalization(nameof(TileScanSize));
            TileScanHardness = this.GetLocalization(nameof(TileScanHardness));
            TileScanStatus = this.GetLocalization(nameof(TileScanStatus));
            TileScanCrafting = this.GetLocalization(nameof(TileScanCrafting));
            TileScanContainer = this.GetLocalization(nameof(TileScanContainer));
            TileScanLight = this.GetLocalization(nameof(TileScanLight));
            TileScanFurniture = this.GetLocalization(nameof(TileScanFurniture));
            TileScanBlock = this.GetLocalization(nameof(TileScanBlock));
            TileScanDungeon = this.GetLocalization(nameof(TileScanDungeon));
            TileScanLihzahrd = this.GetLocalization(nameof(TileScanLihzahrd));
            TileScanHardnessExtreme = this.GetLocalization(nameof(TileScanHardnessExtreme));
            TileScanHardnessHigh = this.GetLocalization(nameof(TileScanHardnessHigh));
            TileScanHardnessNormal = this.GetLocalization(nameof(TileScanHardnessNormal));
            TileScanHardnessLow = this.GetLocalization(nameof(TileScanHardnessLow));
            TileScanActive = this.GetLocalization(nameof(TileScanActive));
            TileScanInactive = this.GetLocalization(nameof(TileScanInactive));
            TileScanSealed = this.GetLocalization(nameof(TileScanSealed));
            TileScanOnline = this.GetLocalization(nameof(TileScanOnline));
            TileScanIntact = this.GetLocalization(nameof(TileScanIntact));
            TileScanMisc = this.GetLocalization(nameof(TileScanMisc));
            TileScanMiscPile = this.GetLocalization(nameof(TileScanMiscPile));

            ProjectileScanName = this.GetLocalization(nameof(ProjectileScanName));
            ProjectileScanClass = this.GetLocalization(nameof(ProjectileScanClass));
            ProjectileScanSpeed = this.GetLocalization(nameof(ProjectileScanSpeed));
            ProjectileScanKnockback = this.GetLocalization(nameof(ProjectileScanKnockback));
            ProjectileScanPenetrate = this.GetLocalization(nameof(ProjectileScanPenetrate));
            ProjectileScanTimeLeft = this.GetLocalization(nameof(ProjectileScanTimeLeft));
            ProjectileScanOwner = this.GetLocalization(nameof(ProjectileScanOwner));
            ProjectileScanAI = this.GetLocalization(nameof(ProjectileScanAI));
            ProjectileScanPosition = this.GetLocalization(nameof(ProjectileScanPosition));
            ProjectileScanHostile = this.GetLocalization(nameof(ProjectileScanHostile));
            ProjectileScanFriendly = this.GetLocalization(nameof(ProjectileScanFriendly));
            ProjectileScanNeutral = this.GetLocalization(nameof(ProjectileScanNeutral));
            ProjectileScanMinion = this.GetLocalization(nameof(ProjectileScanMinion));
            ProjectileScanSentry = this.GetLocalization(nameof(ProjectileScanSentry));
            ProjectileScanTrap = this.GetLocalization(nameof(ProjectileScanTrap));
            ProjectileScanInfinite = this.GetLocalization(nameof(ProjectileScanInfinite));
            ProjectileScanOwnerWorld = this.GetLocalization(nameof(ProjectileScanOwnerWorld));

            ItemScanName = this.GetLocalization(nameof(ItemScanName));
            ItemScanClass = this.GetLocalization(nameof(ItemScanClass));
            ItemScanStack = this.GetLocalization(nameof(ItemScanStack));
            ItemScanValue = this.GetLocalization(nameof(ItemScanValue));
            ItemScanRarity = this.GetLocalization(nameof(ItemScanRarity));
            ItemScanCombat = this.GetLocalization(nameof(ItemScanCombat));
            ItemScanUtility = this.GetLocalization(nameof(ItemScanUtility));
            ItemScanPrefix = this.GetLocalization(nameof(ItemScanPrefix));
            ItemScanTypeId = this.GetLocalization(nameof(ItemScanTypeId));
            ItemScanPosition = this.GetLocalization(nameof(ItemScanPosition));
            ItemScanWeapon = this.GetLocalization(nameof(ItemScanWeapon));
            ItemScanTool = this.GetLocalization(nameof(ItemScanTool));
            ItemScanArmor = this.GetLocalization(nameof(ItemScanArmor));
            ItemScanAccessory = this.GetLocalization(nameof(ItemScanAccessory));
            ItemScanAmmo = this.GetLocalization(nameof(ItemScanAmmo));
            ItemScanConsumable = this.GetLocalization(nameof(ItemScanConsumable));
            ItemScanPlaceable = this.GetLocalization(nameof(ItemScanPlaceable));
            ItemScanMaterial = this.GetLocalization(nameof(ItemScanMaterial));
            ItemScanQuest = this.GetLocalization(nameof(ItemScanQuest));
            ItemScanMisc = this.GetLocalization(nameof(ItemScanMisc));
            ItemScanNone = this.GetLocalization(nameof(ItemScanNone));
            ItemScanNoValue = this.GetLocalization(nameof(ItemScanNoValue));

            WaterScanLiquid = this.GetLocalization(nameof(WaterScanLiquid));
            WaterScanEnvironment = this.GetLocalization(nameof(WaterScanEnvironment));
            WaterScanDepth = this.GetLocalization(nameof(WaterScanDepth));
            WaterScanWorldLayer = this.GetLocalization(nameof(WaterScanWorldLayer));
            WaterScanTileCoord = this.GetLocalization(nameof(WaterScanTileCoord));
            WaterScanContainment = this.GetLocalization(nameof(WaterScanContainment));
            WaterScanWater = this.GetLocalization(nameof(WaterScanWater));
            WaterScanLava = this.GetLocalization(nameof(WaterScanLava));
            WaterScanHoney = this.GetLocalization(nameof(WaterScanHoney));
            WaterScanShimmer = this.GetLocalization(nameof(WaterScanShimmer));
            WaterScanLayerSky = this.GetLocalization(nameof(WaterScanLayerSky));
            WaterScanLayerSurface = this.GetLocalization(nameof(WaterScanLayerSurface));
            WaterScanLayerUnderground = this.GetLocalization(nameof(WaterScanLayerUnderground));
            WaterScanLayerCavern = this.GetLocalization(nameof(WaterScanLayerCavern));
            WaterScanLayerUnderworld = this.GetLocalization(nameof(WaterScanLayerUnderworld));
            WaterScanEnvOcean = this.GetLocalization(nameof(WaterScanEnvOcean));
            WaterScanEnvDesert = this.GetLocalization(nameof(WaterScanEnvDesert));
            WaterScanEnvSnow = this.GetLocalization(nameof(WaterScanEnvSnow));
            WaterScanEnvJungle = this.GetLocalization(nameof(WaterScanEnvJungle));
            WaterScanEnvCorruption = this.GetLocalization(nameof(WaterScanEnvCorruption));
            WaterScanEnvCrimson = this.GetLocalization(nameof(WaterScanEnvCrimson));
            WaterScanEnvHallow = this.GetLocalization(nameof(WaterScanEnvHallow));
            WaterScanEnvDungeon = this.GetLocalization(nameof(WaterScanEnvDungeon));
            WaterScanEnvMushroom = this.GetLocalization(nameof(WaterScanEnvMushroom));
            WaterScanEnvUnderworld = this.GetLocalization(nameof(WaterScanEnvUnderworld));
            WaterScanContainmentPocket = this.GetLocalization(nameof(WaterScanContainmentPocket));
            WaterScanContainmentChannel = this.GetLocalization(nameof(WaterScanContainmentChannel));
            WaterScanContainmentOpen = this.GetLocalization(nameof(WaterScanContainmentOpen));
            WaterScanStatusStill = this.GetLocalization(nameof(WaterScanStatusStill));
            WaterScanStatusFlowing = this.GetLocalization(nameof(WaterScanStatusFlowing));

            TurretScanName = this.GetLocalization(nameof(TurretScanName));
            TurretScanLaserName = this.GetLocalization(nameof(TurretScanLaserName));
            TurretScanGatlinName = this.GetLocalization(nameof(TurretScanGatlinName));
            TurretScanType = this.GetLocalization(nameof(TurretScanType));
            TurretScanPhase = this.GetLocalization(nameof(TurretScanPhase));
            TurretScanPhaseIdle = this.GetLocalization(nameof(TurretScanPhaseIdle));
            TurretScanPhaseCharging = this.GetLocalization(nameof(TurretScanPhaseCharging));
            TurretScanPhaseFiring = this.GetLocalization(nameof(TurretScanPhaseFiring));
            TurretScanPhaseCooldown = this.GetLocalization(nameof(TurretScanPhaseCooldown));
            TurretScanPhaseLocking = this.GetLocalization(nameof(TurretScanPhaseLocking));
            TurretScanCircuit = this.GetLocalization(nameof(TurretScanCircuit));
            TurretScanCircuitOnline = this.GetLocalization(nameof(TurretScanCircuitOnline));
            TurretScanCircuitShorted = this.GetLocalization(nameof(TurretScanCircuitShorted));
            TurretScanCircuitOverload = this.GetLocalization(nameof(TurretScanCircuitOverload));

            SignalTowerScanName = this.GetLocalization(nameof(SignalTowerScanName));
            SignalTowerScanType = this.GetLocalization(nameof(SignalTowerScanType));
            SignalTowerScanThreat = this.GetLocalization(nameof(SignalTowerScanThreat));
            SignalTowerScanStatus = this.GetLocalization(nameof(SignalTowerScanStatus));
            SignalTowerScanStatusOnline = this.GetLocalization(nameof(SignalTowerScanStatusOnline));
            SignalTowerScanStatusBroadcasting = this.GetLocalization(nameof(SignalTowerScanStatusBroadcasting));
            SignalTowerScanStatusElectrified = this.GetLocalization(nameof(SignalTowerScanStatusElectrified));
            SignalTowerScanProtocol = this.GetLocalization(nameof(SignalTowerScanProtocol));

            RightClickHint = this.GetLocalization(nameof(RightClickHint));

            AccessDeniedTitle = this.GetLocalization(nameof(AccessDeniedTitle));
            AccessDeniedDesc = this.GetLocalization(nameof(AccessDeniedDesc));
        }

        #endregion

        /// <summary>
        /// 骇客时间是否处于激活状态
        /// </summary>
        public static bool Active { get; private set; }
        /// <summary>
        /// 屏幕效果强度(0到1)，用于着色器参数插值
        /// </summary>
        public static float Intensity { get; set; }
        /// <summary>
        /// 运镜进度(0到1)，选中目标后平滑过渡到目标中心
        /// </summary>
        public static float CameraProgress { get; set; }
        /// <summary>
        /// 运镜缩放进度(0到1)，选中目标后画面放大
        /// </summary>
        public static float ZoomProgress { get; set; }
        /// <summary>
        /// 目标选中时的光圈动画计时器
        /// </summary>
        public static float ReticleTimer { get; set; }
        /// <summary>
        /// 运镜偏移量，每帧在ModifyScreenPosition中应用
        /// </summary>
        public static Vector2 CameraOffset { get; set; }

        /// <summary>
        /// 当前扫描目标，null 表示无扫描目标
        /// <br/>所有目标种类（NPC、物块、灵异、炮台、信号塔等）统一通过<see cref="IHackTarget"/>暴露
        /// </summary>
        public static IHackTarget CurrentScanTarget { get; private set; }

        /// <summary>
        /// 当前选中的 NPC 索引（兼容旧 API；从<see cref="CurrentScanTarget"/>派生），负数表示未选中或选中的不是 NPC
        /// </summary>
        public static int SelectedTargetIndex
            => CurrentScanTarget is NpcScannable n ? n.NpcIndex : -1;

        /// <summary>
        /// 当前悬停的 NPC 索引（兼容旧 API；从<see cref="HackTimeTargeting.HoveredTarget"/>派生），负数表示无悬停或悬停的不是 NPC
        /// </summary>
        public static int HoveredTargetIndex
            => HackTimeTargeting.HoveredTarget is NpcScannable n ? n.NpcIndex : -1;

        //无限骇入模式（无限袭击终态演出用）
        public static bool InfiniteHack { get; set; }

        private static float targetIntensity;
        //运镜目标位置（选中NPC的中心世界坐标）
        private static Vector2 cameraTo;

        //基础缩放增量，选中目标后画面放大倍率
        private const float TargetZoomBoost = 0.35f;
        //运镜插值速度
        private const float CameraLerpSpeed = 0.06f;
        //效果淡入速度
        private const float FadeInSpeed = 0.055f;
        //效果淡出速度
        private const float FadeOutSpeed = 0.07f;

        //WorldFreezeSystem 的 reason 标签，统一通过它叠加/释放冻结
        private const string FreezeReason = "HackTime";

        /// <summary>
        /// 切换骇客时间的开关状态
        /// </summary>
        public static void Toggle() {
            if (Active) {
                Deactivate();
            }
            else if (Intensity > 0.001f) {
                //正在淡出中，直接反转回来，无需重置状态
                Active = true;
                targetIntensity = 1f;
                if (VaultUtils.isSinglePlayer)//只在单人模式生效时停
                    WorldFreezeSystem.Activate(FreezeReason);
            }
            else {
                Activate();
            }
        }

        /// <summary>
        /// 激活骇客时间
        /// </summary>
        public static void Activate() {
            if (Main.gameMenu) return;
            Active = true;
            targetIntensity = 1f;
            CurrentScanTarget = null;
            CameraProgress = 0f;
            ZoomProgress = 0f;
            ReticleTimer = 0f;
            CameraOffset = Vector2.Zero;
            cameraTo = Vector2.Zero;
            if (VaultUtils.isSinglePlayer)//只在单人模式生效时停
                WorldFreezeSystem.Activate(FreezeReason);
            if (WorldFreezeSystem.IsActive && Main.LocalPlayer.Alives()) {
                //预填飞行时间，避免首次进入 PreUpdate 快照被零值覆盖
                WorldFreezePlayer freezePlayer = Main.LocalPlayer.GetModPlayer<WorldFreezePlayer>();
                freezePlayer.frozenWingTime = Main.LocalPlayer.wingTime;
                freezePlayer.frozenRocketTime = Main.LocalPlayer.rocketTime;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Scanning);
            }
        }

        /// <summary>
        /// 退出骇客时间
        /// </summary>
        public static void Deactivate() {
            Active = false;
            targetIntensity = 0f;
            CurrentScanTarget = null;
            WorldFreezeSystem.Deactivate(FreezeReason);
            HackTimeUI.Instance?.Panel.Hide();
        }

        /// <summary>
        /// 选中一个骇入目标
        /// <br/>所有目标种类（NPC、物块、灵异、炮台、信号塔等）通过<see cref="IHackTarget"/>统一进入
        /// <br/>触发运镜并取消正在进行的上传
        /// </summary>
        public static void Select(IHackTarget target) {
            if (!Active || target == null || !target.IsValid) return;

            //目标无变化时不做处理（点击同一目标不要重复重置上传）
            if (CurrentScanTarget != null && target.TargetEquals(CurrentScanTarget)) return;

            //切换目标时保留各目标自己的上传进度，不再清空队列也不退还 RAM
            //每个目标的入队状态由队列按 (slot, target) 维度独立维护

            bool freshSelect = CurrentScanTarget == null;
            CurrentScanTarget = target;
            cameraTo = target.WorldCenter;

            //首次选中时从零开始推进；切换目标时保持当前进度，让偏移量平滑重定向
            if (freshSelect) {
                CameraProgress = 0f;
                ZoomProgress = 0f;
            }

            //目标种类工厂可定制选中反馈（默认播放扫描音效）
            target.TargetType?.OnSelectFeedback(target);
        }

        /// <summary>
        /// 兼容旧 API：选中指定 NPC
        /// </summary>
        public static void SelectTarget(int npcIndex) {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
            if (!Main.npc[npcIndex].active) return;
            Select(new NpcScannable(npcIndex));
        }

        /// <summary>
        /// 取消选中目标，运镜平滑回归
        /// </summary>
        public static void DeselectTarget() {
            CurrentScanTarget = null;
            //不立即归零CameraProgress/CameraOffset，由UpdateCamera平滑回归
            HackTimeUI.Instance?.Panel.Hide();
        }

        /// <summary>
        /// 世界更新：RAM恢复、骇入效果驱动、队列上传推进
        /// </summary>
        public override void PostUpdateEverything() {
            //RAM自动恢复
            RamSystem.Update();
            //骇入效果全局驱动，退出骇客时间后仍持续生效
            HackEffectTracker.Update();
            //物块骇入效果驱动
            HackEffectTracker.UpdateTileEffects();
            //队列上传+消费：退出骇客时间后实时推进上传并施加完成的效果
            var queue = HackTimeUI.Instance?.Queue;
            queue?.Update();
            queue?.ConsumeAndApplyAll();
        }

        /// <summary>
        /// 每帧逻辑更新
        /// </summary>
        public static void Update() {
            if (Main.gameMenu) {
                Reset();
                return;
            }

            //玩家死亡 / 幽灵态 → 自动关闭，避免本系统的 UI / 镜头残留
            //通用的冻结安全网由 WorldFreezePlayer.UpdateDead 兜底，这里只负责 HackTime 自身的状态
            if (Active && Main.LocalPlayer != null
                && (Main.LocalPlayer.dead || Main.LocalPlayer.ghost)) {
                Deactivate();
            }

            //效果强度插值
            float fadeSpeed = Active ? FadeInSpeed : FadeOutSpeed;
            Intensity = MathHelper.Lerp(Intensity, targetIntensity, fadeSpeed);

            //淡出完毕后彻底清理残余状态
            if (!Active && targetIntensity <= 0f && Intensity < 0.005f) {
                Intensity = 0f;
                CameraOffset = Vector2.Zero;
                CameraProgress = 0f;
                ZoomProgress = 0f;
                return;
            }

            //光圈动画计时
            ReticleTimer += 0.016f;

            //处理运镜逻辑
            UpdateCamera();
        }

        private static void UpdateCamera() {
            //有目标且仍然有效：平滑推进运镜与缩放
            if (CurrentScanTarget != null && CurrentScanTarget.IsValid) {
                cameraTo = CurrentScanTarget.WorldCenter;

                CameraProgress = MathHelper.Lerp(CameraProgress, 1f, CameraLerpSpeed);
                ZoomProgress = MathHelper.Lerp(ZoomProgress, 1f, CameraLerpSpeed * 0.8f);

                //偏移量独立平滑插值到目标位置（切换目标时从当前偏移重定向，而非瞬跳）
                Vector2 desiredOffset = cameraTo - Main.LocalPlayer.Center;
                CameraOffset = Vector2.Lerp(CameraOffset, desiredOffset, CameraLerpSpeed);
                return;
            }

            //目标失效时自动取消
            if (CurrentScanTarget != null && !CurrentScanTarget.IsValid) {
                DeselectTarget();
                return;
            }

            //无目标时平滑回归
            float returnSpeed = CameraLerpSpeed * 1.5f;
            CameraProgress = MathHelper.Lerp(CameraProgress, 0f, returnSpeed);
            ZoomProgress = MathHelper.Lerp(ZoomProgress, 0f, returnSpeed);
            CameraOffset = Vector2.Lerp(CameraOffset, Vector2.Zero, returnSpeed);

            if (CameraProgress < 0.005f && CameraOffset.LengthSquared() < 0.5f) {
                CameraProgress = 0f;
                ZoomProgress = 0f;
                CameraOffset = Vector2.Zero;
            }
        }

        /// <summary>
        /// 获取当前运镜产生的额外缩放值
        /// </summary>
        public static float GetZoomBoost() {
            return TargetZoomBoost * ZoomProgress * Intensity;
        }

        /// <summary>
        /// 判断指定NPC是否为可骇入目标
        /// </summary>
        public static bool IsHackableTarget(NPC npc) {
            if (npc == null || !npc.active) return false;
            //在赛博空间范围内的敌对生物为可骇入目标
            if (!Cyberspace.Active) return true;
            float dx = npc.Center.X - Main.LocalPlayer.Center.X;
            float dy = npc.Center.Y - Main.LocalPlayer.Center.Y;
            float effectiveRadius = Cyberspace.Radius * Cyberspace.ExpandProgress;
            return dx * dx + dy * dy <= effectiveRadius * effectiveRadius;
        }

        /// <summary>
        /// 立即重置所有状态
        /// </summary>
        public static void Reset() {
            Active = false;
            Intensity = 0f;
            targetIntensity = 0f;
            CurrentScanTarget = null;
            CameraProgress = 0f;
            ZoomProgress = 0f;
            ReticleTimer = 0f;
            CameraOffset = Vector2.Zero;
            cameraTo = Vector2.Zero;
            InfiniteHack = false;
            //Reset 是异常路径（卸载、世界切换），只释放本系统持有的 reason；
            //其他 reason 若仍持有冻结应由各自系统在自己的 Reset/Unload 中处理
            WorldFreezeSystem.Deactivate(FreezeReason);
            HackTimeUI.Instance?.Queue?.Clear();
            HackEffectTracker.Reset();
            RamSystem.Reset();
        }
    }
}
