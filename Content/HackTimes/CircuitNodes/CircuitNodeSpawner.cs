using CalamityOverhaul.Content.HackTimes.Protocols;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.OldNet;
using InnoVault.Actors;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.HackTimes.CircuitNodes
{
    /// <summary>
    /// 电路节点布设器：Actor 不随世界持久化，每次进世界后由本系统在
    /// <see cref="PostUpdateWorld"/> 里渐进扫描落点并重新布设（刻意不动现有 worldgen 文件）。<br/>
    /// 布设范围仅旧网（2026-08-28 裁定，此前主世界地表+灾厄实验室的布点全部收编）：
    /// 地表远离出生点的平地立信号塔，塔侧再配一两台炮台读作机械废墟；
    /// 实验室炮位分支保留代码，但旧网无实验室板材，实际不落点。<br/>
    /// 可拆规则（#67/#118）：玩家填埋本体或挖空基座即视作拆除，锚列写入禁布名单，
    /// 重扫时跳过；名单只在权威端读写。旧网 ShouldSave=false 不落盘，
    /// 名单只活一次深潜，重潜随世界一并重生成，符合旧网回放制。<br/>
    /// 同时兼任电路节点族的本地化文本挂点（HackCircuit 类目）
    /// </summary>
    internal class CircuitNodeSpawner : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "HackCircuit";

        #region 本地化
        public static LocalizedText TurretName { get; private set; }
        public static LocalizedText TowerName { get; private set; }
        public static LocalizedText StatusLabel { get; private set; }
        public static LocalizedText IffLabel { get; private set; }
        public static LocalizedText RateLabel { get; private set; }
        public static LocalizedText MunitionLabel { get; private set; }
        public static LocalizedText RangeLabel { get; private set; }
        public static LocalizedText CoverageLabel { get; private set; }
        public static LocalizedText LinkedLabel { get; private set; }
        public static LocalizedText SourceLabel { get; private set; }
        public static LocalizedText StatusOnline { get; private set; }
        public static LocalizedText StatusDisabled { get; private set; }
        public static LocalizedText StatusHijacked { get; private set; }
        public static LocalizedText StatusMunition { get; private set; }
        public static LocalizedText StatusMeshed { get; private set; }
        public static LocalizedText StatusIdle { get; private set; }
        public static LocalizedText StatusVirus { get; private set; }
        public static LocalizedText StatusBlackout { get; private set; }
        public static LocalizedText StatusBeacon { get; private set; }
        public static LocalizedText StatusUplink { get; private set; }
        public static LocalizedText IffHostile { get; private set; }
        public static LocalizedText IffFriendly { get; private set; }
        public static LocalizedText MunitionKinetic { get; private set; }
        public static LocalizedText BeaconCount { get; private set; }

        public override void SetStaticDefaults() {
            //默认串是 en-US；zh-Hans 正典文案见交付报告，由整合者补进 hjson
            TurretName = this.GetLocalization(nameof(TurretName), () => "Sentry Turret");
            TowerName = this.GetLocalization(nameof(TowerName), () => "Signal Tower");
            StatusLabel = this.GetLocalization(nameof(StatusLabel), () => "Status");
            IffLabel = this.GetLocalization(nameof(IffLabel), () => "IFF");
            RateLabel = this.GetLocalization(nameof(RateLabel), () => "Rate");
            MunitionLabel = this.GetLocalization(nameof(MunitionLabel), () => "Munition");
            RangeLabel = this.GetLocalization(nameof(RangeLabel), () => "Range");
            CoverageLabel = this.GetLocalization(nameof(CoverageLabel), () => "Coverage");
            LinkedLabel = this.GetLocalization(nameof(LinkedLabel), () => "Linked Nodes");
            SourceLabel = this.GetLocalization(nameof(SourceLabel), () => "Broadcast");
            StatusOnline = this.GetLocalization(nameof(StatusOnline), () => "ONLINE");
            StatusDisabled = this.GetLocalization(nameof(StatusDisabled), () => "OFFLINE");
            StatusHijacked = this.GetLocalization(nameof(StatusHijacked), () => "HIJACKED");
            StatusMunition = this.GetLocalization(nameof(StatusMunition), () => "FED");
            StatusMeshed = this.GetLocalization(nameof(StatusMeshed), () => "MESHED");
            StatusIdle = this.GetLocalization(nameof(StatusIdle), () => "IDLE");
            StatusVirus = this.GetLocalization(nameof(StatusVirus), () => "VIRUS BROADCAST");
            StatusBlackout = this.GetLocalization(nameof(StatusBlackout), () => "BLACKOUT");
            StatusBeacon = this.GetLocalization(nameof(StatusBeacon), () => "FORGED BEACON");
            StatusUplink = this.GetLocalization(nameof(StatusUplink), () => "UPLINK");
            IffHostile = this.GetLocalization(nameof(IffHostile), () => "Hostile");
            IffFriendly = this.GetLocalization(nameof(IffFriendly), () => "Friendly");
            MunitionKinetic = this.GetLocalization(nameof(MunitionKinetic), () => "Kinetic bolt");
            BeaconCount = this.GetLocalization(nameof(BeaconCount), () => "Lured {0}");
        }
        #endregion

        #region 布设参数与状态
        //每帧检查的列数；列间隔 3 格，一次全图扫约两三秒
        private const int ColumnsPerFrame = 20;
        private const int ColumnStep = 3;
        //各类间距（格）
        private const int TowerSpacing = 110;
        private const int SpawnKeepOut = 120;
        private const int PlayerKeepOut = 90;
        private const int TownKeepOut = 60;
        private const int LabTurretSpacing = 40;
        //禁布名单命中半径（格）：重扫的候选列受玩家/城镇位置影响会漂移几列，
        //按邻近匹配而不是精确列号；塔取大半径覆盖整片废墟，炮台取小半径只封本机位
        private const int TowerBanRadius = 48;
        private const int TurretBanRadius = 24;
        //拆除监视的检查间隔（帧）
        private const int DemolishCheckInterval = 30;

        private static int scanCursorX;
        private static int scanStartX;
        private static int sweepsDone;
        private static bool placementDone;
        private static int placedTowers;
        private static int placedLabTurrets;
        private static int lastLabTurretX = int.MinValue;
        private static readonly List<Point> towerSpots = [];
        private static readonly List<int> labTileTypes = [];

        //已拆除布点的禁布名单：按类别记锚列 X，随世界存档持久化（权威端专属）
        private static readonly List<int> bannedTowerX = [];
        private static readonly List<int> bannedTurretX = [];
        private static readonly List<int> bannedLabX = [];

        //本次会话的布点登记：拆除监视据此巡查，命中后回写对应名单
        private readonly record struct PlacedSpot(List<int> BanList, int AnchorX, Actor ActorRef);
        private static readonly List<PlacedSpot> placedSpots = [];
        private static int demolishTimer;

        private static int TowerBudget => Math.Clamp(Main.maxTilesX / 1500, 2, 5);
        private static int LabTurretBudget => 8;
        #endregion

        public override void PostSetupContent() {
            labTileTypes.Clear();
            //实验室板材类 tile；名字对不上就静默跳过（Calamity 缺席或改名 → 只走地表布点）
            if (ModLoader.TryGetMod("CalamityMod", out Mod cal)) {
                TryAddLabTile(cal, "LaboratoryPlating");
                TryAddLabTile(cal, "LaboratoryPanels");
                TryAddLabTile(cal, "RustedPlating");
            }
        }

        private static void TryAddLabTile(Mod mod, string name) {
            if (mod.TryFind(name, out ModTile tile)) {
                labTileTypes.Add(tile.Type);
            }
        }

        public override void OnWorldLoad() => ResetScanState();

        public override void OnWorldUnload() {
            ResetScanState();
            //禁布名单属上一个世界，此处清空；下个世界由 LoadWorldData 重建（无档则保持空）
            bannedTowerX.Clear();
            bannedTurretX.Clear();
            bannedLabX.Clear();
            //协议的 per-effect 静态账属于上一个世界，清账集中在这里
            TurretMesh.ClearMeshes();
            BeaconForge.ClearBeacons();
            PrivilegeEscalateState.ClearAll();
            MunitionSwap.ClearPendingFeeds();
        }

        private static void ResetScanState() {
            scanStartX = 300;
            scanCursorX = scanStartX;
            sweepsDone = 0;
            placementDone = false;
            placedTowers = 0;
            placedLabTurrets = 0;
            lastLabTurretX = int.MinValue;
            towerSpots.Clear();
            //布点登记只活一个会话；注意禁布名单不能在这里清，
            //OnWorldLoad 晚于 LoadWorldData，清了会把刚读进来的档抹掉
            placedSpots.Clear();
            demolishTimer = 0;
        }

        #region 世界存档：禁布名单
        public override void SaveWorldData(TagCompound tag) {
            //写快照副本，避免自动存档线程与拆除登记撞车
            if (bannedTowerX.Count > 0) {
                tag["CircuitBanTowerX"] = new List<int>(bannedTowerX);
            }
            if (bannedTurretX.Count > 0) {
                tag["CircuitBanTurretX"] = new List<int>(bannedTurretX);
            }
            if (bannedLabX.Count > 0) {
                tag["CircuitBanLabX"] = new List<int>(bannedLabX);
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            bannedTowerX.Clear();
            bannedTurretX.Clear();
            bannedLabX.Clear();
            if (tag.TryGet("CircuitBanTowerX", out List<int> towers)) {
                bannedTowerX.AddRange(towers);
            }
            if (tag.TryGet("CircuitBanTurretX", out List<int> turrets)) {
                bannedTurretX.AddRange(turrets);
            }
            if (tag.TryGet("CircuitBanLabX", out List<int> labs)) {
                bannedLabX.AddRange(labs);
            }
        }
        #endregion

        public override void PostUpdateWorld() {
            //PostUpdateWorld 只在单机与服务器跑，天然权威侧；客户端等 Actor 同步
            if (Main.gameMenu) {
                return;
            }

            //电路节点旧网专属（2026-08-28 裁定）：主世界与其他子世界一律不布设。
            //SubLib 的 NormalUpdates=false 拦不住本钩子，必须显式判世界；
            //进出世界都会 ResetScanState，每次深潜进场后照常从头渐进重布
            if (!OldNetWorld.Active) {
                return;
            }

            WatchDemolition();

            if (placementDone) {
                return;
            }

            for (int i = 0; i < ColumnsPerFrame; i++) {
                ProcessColumn(scanCursorX);
                scanCursorX += ColumnStep;
                if (scanCursorX >= Main.maxTilesX - 200) {
                    scanCursorX = 200;
                }
                //绕回起点算完成一轮
                if (Math.Abs(scanCursorX - scanStartX) < ColumnStep) {
                    sweepsDone++;
                    if (sweepsDone >= 2 || BudgetSatisfied()) {
                        placementDone = true;
                        return;
                    }
                }
            }

            if (BudgetSatisfied()) {
                placementDone = true;
            }
        }

        private static bool BudgetSatisfied() {
            bool labsDone = labTileTypes.Count == 0 || placedLabTurrets >= LabTurretBudget;
            return placedTowers >= TowerBudget && labsDone;
        }

        private static void ProcessColumn(int x) {
            if (x < 200 || x >= Main.maxTilesX - 200) {
                return;
            }

            if (placedTowers < TowerBudget) {
                TrySurfaceTowerSite(x);
            }

            if (labTileTypes.Count > 0 && placedLabTurrets < LabTurretBudget
                && x - lastLabTurretX >= LabTurretSpacing) {
                TryLabTurretSite(x);
            }
        }

        #region 地表塔位
        private static void TrySurfaceTowerSite(int x) {
            if (IsBannedColumn(bannedTowerX, x, TowerBanRadius)) {
                return;
            }
            if (!TryFindSurfaceGround(x, out int groundY)) {
                return;
            }
            if (!IsFlatSolid(x, groundY, 1) || !IsClearAbove(x - 1, groundY, 3, 10)) {
                return;
            }
            if (!FarFromEverything(x, groundY, TowerSpacing)) {
                return;
            }

            //塔锚点：底边中心落地
            Vector2 towerPos = new(x * 16f + 8f - 15f, groundY * 16f - 96f);
            int towerIdx = ActorLoader.NewActor<SignalTowerActor>(towerPos);
            if (towerIdx < 0) {
                return;
            }
            placedTowers++;
            towerSpots.Add(new Point(x, groundY));
            RegisterSpot(bannedTowerX, x, towerIdx);

            //塔侧一两台哨戒炮塔，读作一片机械废墟
            TryPlaceGuardTurret(x, -1);
            TryPlaceGuardTurret(x, 1);
        }

        private static void TryPlaceGuardTurret(int towerX, int side) {
            for (int offset = 14; offset <= 30; offset += 4) {
                int x = towerX + side * offset;
                if (x < 200 || x >= Main.maxTilesX - 200) {
                    return;
                }
                if (IsBannedColumn(bannedTurretX, x, TurretBanRadius)) {
                    continue;
                }
                if (!TryFindSurfaceGround(x, out int groundY)) {
                    continue;
                }
                if (!IsFlatSolid(x, groundY, 1) || !IsClearAbove(x - 1, groundY, 3, 5)) {
                    continue;
                }
                Vector2 pos = new(x * 16f + 8f - 18f, groundY * 16f - 46f);
                int idx = ActorLoader.NewActor<SentryTurretActor>(pos);
                if (idx >= 0) {
                    RegisterSpot(bannedTurretX, x, idx);
                }
                return;
            }
        }

        /// <summary>从天顶向下找第一格实心地表，液面上方与漂浮岛内侧都拒绝</summary>
        private static bool TryFindSurfaceGround(int x, out int groundY) {
            groundY = -1;
            int yEnd = (int)Main.worldSurface + 40;
            for (int y = 80; y < yEnd; y++) {
                if (!WorldGen.SolidTile(x, y)) {
                    continue;
                }
                //液体里的"地面"不要
                if (Main.tile[x, y - 1].LiquidAmount > 0) {
                    return false;
                }
                groundY = y;
                return true;
            }
            return false;
        }
        #endregion

        #region 实验室炮位
        private static void TryLabTurretSite(int x) {
            //旧网无实验室板材，本分支在"仅旧网布设"裁定下处于休眠；保留以备布设范围再调整
            if (IsBannedColumn(bannedLabX, x, TurretBanRadius)) {
                return;
            }
            int yStart = (int)Main.worldSurface;
            int yEnd = (int)(Main.maxTilesY * 0.85f);
            for (int y = yStart; y < yEnd; y += 4) {
                Tile tile = Main.tile[x, y];
                if (!tile.HasTile || !IsLabTile(tile.TileType)) {
                    continue;
                }
                if (TryDropToLabFloor(x, y, out int floorY)
                    && ActorSpacingOk(x, floorY, 24)) {
                    Vector2 pos = new(x * 16f + 8f - 18f, floorY * 16f - 46f);
                    int idx = ActorLoader.NewActor<SentryTurretActor>(pos);
                    if (idx >= 0) {
                        placedLabTurrets++;
                        lastLabTurretX = x;
                        RegisterSpot(bannedLabX, x, idx);
                    }
                }
                return;
            }
        }

        private static bool IsLabTile(int tileType) {
            for (int i = 0; i < labTileTypes.Count; i++) {
                if (labTileTypes[i] == tileType) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>从命中的实验室板材向下穿进舱内空腔，落到舱内地板</summary>
        private static bool TryDropToLabFloor(int x, int y, out int floorY) {
            floorY = -1;
            int yy = y;
            int guard = 0;
            //先穿过板材本体
            while (yy < Main.maxTilesY - 40 && WorldGen.SolidTile(x, yy)) {
                if (++guard > 12) {
                    return false;
                }
                yy++;
            }
            //舱内空腔要够高，太浅是夹层、太深是竖井
            int air = 0;
            while (yy < Main.maxTilesY - 40 && !WorldGen.SolidTile(x, yy)) {
                yy++;
                if (++air > 24) {
                    return false;
                }
            }
            if (air < 4) {
                return false;
            }
            //地板也得是实验室板材，避免把炮塞进天然溶洞
            if (!Main.tile[x, yy].HasTile || !IsLabTile(Main.tile[x, yy].TileType)) {
                return false;
            }
            if (!IsFlatSolid(x, yy, 1)) {
                return false;
            }
            floorY = yy;
            return true;
        }
        #endregion

        #region 拆除监视与禁布名单
        //Actor 没有生命值也没有受击口（玩家反馈 #67/#118 的"无法拆除"），
        //拆除交互直接沿用玩家已有的方块动作：填埋本体或挖空基座，不发明新交互
        private static void WatchDemolition() {
            if (placedSpots.Count == 0 || ++demolishTimer < DemolishCheckInterval) {
                return;
            }
            demolishTimer = 0;

            for (int i = placedSpots.Count - 1; i >= 0; i--) {
                PlacedSpot spot = placedSpots[i];
                Actor actor = spot.ActorRef;
                if (actor is not { Active: true }) {
                    //被其他途径销毁（当前没有这种途径，防御性清账），不计入禁布
                    placedSpots.RemoveAt(i);
                    continue;
                }
                if (!SpotDemolished(actor)) {
                    continue;
                }
                //视作玩家拆除：锚列入禁布名单（随世界存档），销毁实体；
                //销毁对客户端的广播由 InnoVault Actor 网络层负责，这里不加包
                spot.BanList.Add(spot.AnchorX);
                DemolishFeedback(actor);
                ActorLoader.KillActor(actor.WhoAmI);
                placedSpots.RemoveAt(i);
            }
        }

        /// <summary>本体中下两处采样点都被实心方块占据（填埋），或基座三格支撑全空（挖空），即视作拆除</summary>
        private static bool SpotDemolished(Actor actor) {
            int cx = (int)(actor.Center.X / 16f);
            int midY = (int)(actor.Center.Y / 16f);
            int lowY = (int)((actor.Position.Y + actor.Height - 8f) / 16f);
            if (WorldGen.SolidTile(cx, midY) && WorldGen.SolidTile(cx, lowY)) {
                return true;
            }
            //布设时 IsFlatSolid 保证过基座下三格实心，三格全空只能是有意挖除（或陨石级的地形破坏）
            int underY = (int)((actor.Position.Y + actor.Height + 8f) / 16f);
            for (int dx = -1; dx <= 1; dx++) {
                if (WorldGen.SolidTile(cx + dx, underY)) {
                    return false;
                }
            }
            return true;
        }

        //拆除反馈：单机与本地主机可见的火花与闷响；远端客户端只看到实体消失，本次不为演出加包
        private static void DemolishFeedback(Actor actor) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath14 with { Volume = 0.5f, Pitch = -0.3f }, actor.Center);
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3.5f, -0.5f));
                PRTLoader.NewParticle<PRT_Spark>(actor.Center + Main.rand.NextVector2Circular(12f, 16f),
                    vel, new Color(120, 200, 255), 0.7f)?.Configure(true, 24);
            }
        }

        private static void RegisterSpot(List<int> banList, int anchorX, int actorIdx) {
            Actor actor = ActorLoader.Actors?[actorIdx];
            if (actor?.Active == true) {
                placedSpots.Add(new PlacedSpot(banList, anchorX, actor));
            }
        }

        private static bool IsBannedColumn(List<int> banList, int x, int radius) {
            for (int i = 0; i < banList.Count; i++) {
                if (Math.Abs(banList[i] - x) < radius) {
                    return true;
                }
            }
            return false;
        }

        //玩家建筑避让已随"仅旧网布设"裁定删除：旧网每次深潜重生成、没有常驻玩家建筑，
        //而 Z1/Z2 带的补墙（TinPlating/MartianConduit 计入 Main.wallHouse）会让住房判据全域误中，
        //留着只会把大半个旧网地表误判成禁区
        #endregion

        #region 通用校验
        private static bool IsFlatSolid(int x, int groundY, int halfWidth) {
            for (int dx = -halfWidth; dx <= halfWidth; dx++) {
                if (!WorldGen.SolidTile(x + dx, groundY)) {
                    return false;
                }
                if (WorldGen.SolidTile(x + dx, groundY - 1)) {
                    return false;
                }
            }
            return true;
        }

        private static bool IsClearAbove(int x, int groundY, int width, int height) {
            for (int dx = 0; dx < width; dx++) {
                for (int dy = 1; dy <= height; dy++) {
                    Tile tile = Main.tile[x + dx, groundY - dy];
                    if (WorldGen.SolidTile(x + dx, groundY - dy) || tile.LiquidAmount > 0) {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool FarFromEverything(int x, int y, int towerSpacing) {
            if (Math.Abs(x - Main.spawnTileX) < SpawnKeepOut) {
                return false;
            }
            for (int i = 0; i < towerSpots.Count; i++) {
                if (Math.Abs(towerSpots[i].X - x) < towerSpacing) {
                    return false;
                }
            }
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active == true
                    && Math.Abs(player.Center.X / 16f - x) < PlayerKeepOut
                    && Math.Abs(player.Center.Y / 16f - y) < PlayerKeepOut) {
                    return false;
                }
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active == true && npc.townNPC
                    && Math.Abs(npc.Center.X / 16f - x) < TownKeepOut
                    && Math.Abs(npc.Center.Y / 16f - y) < TownKeepOut) {
                    return false;
                }
            }
            return true;
        }

        private static bool ActorSpacingOk(int x, int y, int minTiles) {
            Vector2 world = new(x * 16f, y * 16f);
            float minDistSq = minTiles * 16f * (minTiles * 16f);
            foreach (Actor actor in ActorLoader.GetActiveActors<Actor>()) {
                if (actor is not (SentryTurretActor or SignalTowerActor)) {
                    continue;
                }
                if (Vector2.DistanceSquared(actor.Center, world) < minDistSq) {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }
}
