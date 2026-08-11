using CalamityOverhaul.Content.HackTimes.Protocols;
using InnoVault.Actors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.CircuitNodes
{
    /// <summary>
    /// 电路节点布设器：Actor 不随世界持久化，每次进世界后由本系统在
    /// <see cref="PostUpdateWorld"/> 里渐进扫描落点并重新布设（刻意不动现有 worldgen 文件）。<br/>
    /// 布点偏好：实验室板材（Calamity 实验室）舱内地板放炮台；
    /// 地表远离出生点与住区的平地立信号塔，塔侧再配一两台炮台读作机械废墟。<br/>
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

        private static int scanCursorX;
        private static int scanStartX;
        private static int sweepsDone;
        private static bool placementDone;
        private static int placedTowers;
        private static int placedLabTurrets;
        private static int lastLabTurretX = int.MinValue;
        private static readonly List<Point> towerSpots = [];
        private static readonly List<int> labTileTypes = [];

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
        }

        public override void PostUpdateWorld() {
            //PostUpdateWorld 只在单机与服务器跑，天然权威侧；客户端等 Actor 同步
            if (placementDone || Main.gameMenu) {
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
            if (ActorLoader.NewActor<SignalTowerActor>(towerPos) < 0) {
                return;
            }
            placedTowers++;
            towerSpots.Add(new Point(x, groundY));

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
                if (!TryFindSurfaceGround(x, out int groundY)) {
                    continue;
                }
                if (!IsFlatSolid(x, groundY, 1) || !IsClearAbove(x - 1, groundY, 3, 5)) {
                    continue;
                }
                Vector2 pos = new(x * 16f + 8f - 18f, groundY * 16f - 46f);
                ActorLoader.NewActor<SentryTurretActor>(pos);
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
                    if (ActorLoader.NewActor<SentryTurretActor>(pos) >= 0) {
                        placedLabTurrets++;
                        lastLabTurretX = x;
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
