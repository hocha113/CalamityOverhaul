using CalamityOverhaul.Content.Scenarios.Kiame.Overlay;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.Kiame.Gate
{
    /// <summary>
    /// 鬼域门伞的游走放置：主世界地表随机选一片平地立起 <see cref="KiameGateUmbrella"/>，
    /// 每个黎明换一处。世界态（是否已生成+锚点+上次黎明戳）随世界存档，
    /// 权威端逐帧维护恰好一个门伞 Actor；管线形制镜像 <see cref="OniUmbrellaWorldSpawn"/>。<br/>
    /// 存在性是世界级的（服务器读不到各玩家的获伞进度），
    /// 未获伞玩家看不见它也点不动它——可见性在 Actor 侧按玩家各自裁
    /// </summary>
    internal class KiameGateSpawn : ModSystem
    {
        public static bool IsGenerated { get; internal set; }
        /// <summary>地面锚点，伞柄尾钩落点（地表中心），像素</summary>
        public static Vector2 GatePosition { get; private set; }

        //运行期低频自检；首个安全更新帧不经过该节流
        private const int EnsureCheckInterval = 60;
        private const int PlacementFailureLogCooldown = 300;
        private static int ensureCheckTimer;
        private static int placementFailureLogTimer;
        //黎明沿检测：世界载入帧的白昼不算新黎明
        private static bool prevDayTime;
        private static bool dayTimePrimed;

        #region 选址参数
        private const int WorldEdgeMargin = 60;
        //避让半径（格）：世界出生点（故事伞与鸟居都在那一带）
        private const int SpawnAvoidTiles = 140;
        //上方净空格数：伞贴图 128px 高 ≈ 8 格，再留冗余
        private const int RequiredClearance = 10;
        //伞体+水洼约 8 格宽，按半宽采样
        private const int FlatSampleRadius = 4;
        private const int MaxGroundDeviation = 2;
        private const int PickAttempts = 80;
        #endregion

        /// <summary>世界尺寸是否足以安全解析伞位</summary>
        public static bool WorldGeometryReady
            => Main.maxTilesX > WorldEdgeMargin * 2 && Main.maxTilesY > WorldEdgeMargin * 2;

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(IsGenerated)] = IsGenerated;
            tag[nameof(GatePosition)] = GatePosition;
        }

        public override void LoadWorldData(TagCompound tag) {
            IsGenerated = false;
            GatePosition = Vector2.Zero;
            try {
                if (tag != null && tag.TryGet(nameof(IsGenerated), out bool generated)) {
                    IsGenerated = generated;
                }
                if (tag != null && tag.TryGet(nameof(GatePosition), out Vector2 pos)) {
                    GatePosition = pos;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[KiameGateSpawn:LoadWorldData] Failed to load gate data: {ex.Message}");
                IsGenerated = false;
                GatePosition = Vector2.Zero;
            }

            if (IsGenerated && WorldGeometryReady && !IsValidWorldPosition(GatePosition)) {
                CWRMod.Instance.Logger.Warn($"[KiameGateSpawn:LoadWorldData] Discarding invalid gate position {GatePosition}, will regenerate");
                IsGenerated = false;
                GatePosition = Vector2.Zero;
            }
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(IsGenerated);
            if (IsGenerated) {
                writer.WriteVector2(GatePosition);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            IsGenerated = reader.ReadBoolean();
            GatePosition = IsGenerated ? reader.ReadVector2() : Vector2.Zero;
            if (IsGenerated && !IsValidWorldPosition(GatePosition)) {
                CWRMod.Instance.Logger.Warn($"[KiameGateSpawn:NetReceive] Ignoring invalid gate position {GatePosition}");
                IsGenerated = false;
                GatePosition = Vector2.Zero;
            }
        }

        public override void OnWorldLoad() => ResetLocalState();

        public override void OnWorldUnload() => ClearGate();

        public override void Unload() => ClearGate();

        private static void ResetLocalState() {
            ensureCheckTimer = 0;
            placementFailureLogTimer = 0;
            dayTimePrimed = false;
        }

        public override void PostUpdateEverything() {
            MaintainAuthoritativeGate();
        }

        /// <summary>
        /// 世界态由服务端/单人统一维护：黎明换位 + 恰好一个 Actor。
        /// 子世界内不维护（门伞是主世界地标）
        /// </summary>
        private static void MaintainAuthoritativeGate() {
            if (VaultUtils.isClient || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            if (placementFailureLogTimer > 0) {
                placementFailureLogTimer--;
            }

            //黎明沿：换一处再立（载入帧的白昼不算）
            if (!dayTimePrimed) {
                prevDayTime = Main.dayTime;
                dayTimePrimed = true;
            }
            else if (Main.dayTime && !prevDayTime) {
                Relocate();
            }
            prevDayTime = Main.dayTime;

            if (IsGenerated && !IsValidWorldPosition(GatePosition)) {
                CWRMod.Instance.Logger.Warn($"[KiameGateSpawn] Invalid runtime position {GatePosition}, regenerating");
                IsGenerated = false;
                GatePosition = Vector2.Zero;
            }

            if (!IsGenerated) {
                TryGenerateGate();
                return;
            }

            if (ensureCheckTimer > 0) {
                ensureCheckTimer--;
                return;
            }

            bool actorReady = EnsureSingleGateActor();
            ensureCheckTimer = actorReady ? EnsureCheckInterval : 0;
        }

        /// <summary>黎明换位：清态清 Actor，下一帧维护自会重选新址</summary>
        internal static void Relocate() {
            if (VaultUtils.isClient) {
                return;
            }
            foreach (KiameGateUmbrella actor in ActorLoader.GetActiveActors<KiameGateUmbrella>()) {
                ActorLoader.KillActor(actor.WhoAmI);
            }
            IsGenerated = false;
            GatePosition = Vector2.Zero;
            if (VaultUtils.isServer) {
                SyncGateToClients();
            }
        }

        /// <summary>服务端/单人解析游走位并生成；世界几何未就绪时由下一帧重试</summary>
        public static void TryGenerateGate() {
            if (VaultUtils.isClient || IsGenerated || SubWorldRef.AnyActiveSubWorld()
                || !WorldGeometryReady) {
                return;
            }

            if (!TryPickWanderSite(out Vector2 position)) {
                if (placementFailureLogTimer <= 0) {
                    CWRMod.Instance.Logger.Warn("[KiameGateSpawn] No suitable wander site this pass; retrying");
                    placementFailureLogTimer = PlacementFailureLogCooldown;
                }
                return;
            }

            GatePosition = position;
            IsGenerated = true;
            bool actorReady = EnsureSingleGateActor();
            ensureCheckTimer = actorReady ? EnsureCheckInterval : 0;
            CWRMod.Instance.Logger.Info($"[KiameGateSpawn] Gate placed at {GatePosition}");
            if (VaultUtils.isServer) {
                SyncGateToClients();
            }
        }

        private static void SyncGateToClients() {
            ModPacket packet = CWRNetWork.GetPacket<KiameGateSyncNet>();
            packet.Write(IsGenerated);
            if (IsGenerated) {
                packet.WriteVector2(GatePosition);
            }
            packet.Send();
        }

        /// <summary>客户端接收权威世界态；Actor实体仍由InnoVault生成广播同步</summary>
        internal static void ReceiveGateSync(BinaryReader reader) {
            bool generated = reader.ReadBoolean();
            Vector2 position = generated ? reader.ReadVector2() : Vector2.Zero;
            if (!VaultUtils.isClient) {
                return;
            }

            if (generated && !IsValidWorldPosition(position)) {
                CWRMod.Instance.Logger.Warn($"[KiameGateSpawn:ReceiveGateSync] Ignoring invalid gate position {position}");
                generated = false;
                position = Vector2.Zero;
            }

            IsGenerated = generated;
            GatePosition = position;
        }

        /// <summary>权威端维持恰好一个门伞 Actor，并把位置纠正到锚点</summary>
        private static bool EnsureSingleGateActor() {
            if (VaultUtils.isClient || !IsGenerated || !IsValidWorldPosition(GatePosition)) {
                return false;
            }

            List<KiameGateUmbrella> actors = ActorLoader.GetActiveActors<KiameGateUmbrella>();
            KiameGateUmbrella keeper = null;
            foreach (KiameGateUmbrella actor in actors) {
                if (keeper == null) {
                    keeper = actor;
                    continue;
                }
                ActorLoader.KillActor(actor.WhoAmI);
            }

            if (keeper != null) {
                if ((keeper.Position - GatePosition).LengthSquared() > 0.25f) {
                    keeper.Position = GatePosition;
                    if (VaultUtils.isServer) {
                        keeper.NetUpdate = true;
                    }
                }
                return true;
            }

            int actorIndex = ActorLoader.NewActor<KiameGateUmbrella>(GatePosition);
            if (actorIndex >= 0) {
                return true;
            }

            if (placementFailureLogTimer <= 0) {
                CWRMod.Instance.Logger.Error($"[KiameGateSpawn] Actor placement failed at {GatePosition}; retrying automatically");
                placementFailureLogTimer = PlacementFailureLogCooldown;
            }
            return false;
        }

        /// <summary>清本地/存档态(不含Actor)，世界卸载等收尾用</summary>
        public static void ClearGate() {
            IsGenerated = false;
            GatePosition = Vector2.Zero;
            ResetLocalState();
        }

        #region 选址
        /// <summary>有限且位于世界内部安全区域的像素坐标</summary>
        public static bool IsValidWorldPosition(Vector2 position) {
            if (!WorldGeometryReady || !float.IsFinite(position.X) || !float.IsFinite(position.Y)) {
                return false;
            }
            const float TileSize = 16f;
            float margin = WorldEdgeMargin * TileSize;
            return position.X >= margin && position.X <= Main.maxTilesX * TileSize - margin
                && position.Y >= margin && position.Y <= Main.maxTilesY * TileSize - margin;
        }

        /// <summary>
        /// 游走选址：全图地表随机抽列，避开出生点一带（故事伞与鸟居的地盘），
        /// 要求平整净空。抽不中就这拍作罢，下拍再来——门伞晚到一步无妨
        /// </summary>
        private static bool TryPickWanderSite(out Vector2 position) {
            position = default;
            int spawnX = Main.spawnTileX;
            for (int attempt = 0; attempt < PickAttempts; attempt++) {
                int x = Main.rand.Next(WorldEdgeMargin + FlatSampleRadius + 2,
                    Main.maxTilesX - WorldEdgeMargin - FlatSampleRadius - 2);
                if (Math.Abs(x - spawnX) < SpawnAvoidTiles) {
                    continue;
                }
                if (TryEvaluateColumn(x, out position)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>评估伞占地区域的净空、液体、危险块与平整度（地表向下扫）</summary>
        private static bool TryEvaluateColumn(int tileX, out Vector2 position) {
            position = default;

            if (!TryFindSurfaceGround(tileX, out int groundY)) {
                return false;
            }
            //须在地表附近，不接受洞穴
            if (groundY > Main.worldSurface + 30) {
                return false;
            }

            for (int offsetX = -FlatSampleRadius; offsetX <= FlatSampleRadius; offsetX++) {
                int sampleX = tileX + offsetX;
                if (!TryFindSurfaceGround(sampleX, out int sideGroundY)) {
                    return false;
                }
                if (Math.Abs(sideGroundY - groundY) > MaxGroundDeviation) {
                    return false;
                }

                Tile ground = Main.tile[sampleX, sideGroundY];
                if (ground == null || Main.tileDungeon[ground.TileType] || Main.tileLavaDeath[ground.TileType]) {
                    return false;
                }

                for (int y = sideGroundY - 1; y >= sideGroundY - RequiredClearance; y--) {
                    if (y < WorldEdgeMargin) {
                        return false;
                    }
                    Tile tile = Main.tile[sampleX, y];
                    if (tile == null || tile.HasSolidTile() || tile.LiquidAmount > 0) {
                        return false;
                    }
                }
            }

            position = new Vector2(tileX * 16f + 8f, groundY * 16f);
            return IsValidWorldPosition(position);
        }

        /// <summary>自天顶向下扫到地表第一块实心地面</summary>
        private static bool TryFindSurfaceGround(int tileX, out int groundY) {
            groundY = 0;
            if (tileX < WorldEdgeMargin || tileX >= Main.maxTilesX - WorldEdgeMargin) {
                return false;
            }

            int startY = Math.Max(WorldEdgeMargin, (int)(Main.worldSurface * 0.3));
            int endY = Math.Min(Main.maxTilesY - WorldEdgeMargin, (int)Main.worldSurface + 60);
            for (int y = startY; y < endY; y++) {
                Tile tile = Main.tile[tileX, y];
                if (tile != null && tile.HasSolidTile()) {
                    groundY = y;
                    return true;
                }
            }

            return false;
        }
        #endregion
    }

    /// <summary>鬼域门伞权威世界态下发信道</summary>
    internal sealed class KiameGateSyncNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => KiameGateSpawn.ReceiveGateSync(reader);
    }
}
