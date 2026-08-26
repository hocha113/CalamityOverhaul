using CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds
{
    /// <summary>
    /// 世界鬼伞的出生点放置：出生点附近选一片平地立起 <see cref="OniRainWorldUmbrella"/>，
    /// 作为鬼雨世界的入口地标。世界态（是否已生成+锚点）随世界存档，
    /// 权威端逐帧维护恰好一个 Actor；管线形制镜像 <see cref="ToriiShrine"/>。<br/>
    /// 选址等鸟居先落位（两者都在出生点附近，避让按已知锚点算），伞窄净空要求低。
    /// 已获伞玩家的隐藏是纯本地表现，在 Actor 侧处理，这里不掺和。
    /// </summary>
    internal class OniUmbrellaWorldSpawn : ModSystem
    {
        public static bool IsGenerated { get; internal set; }
        /// <summary>地面锚点，伞柄尾钩落点（地表中心），像素</summary>
        public static Vector2 UmbrellaPosition { get; private set; }

        //运行期低频自检；首个安全更新帧不经过该节流
        private const int EnsureCheckInterval = 60;
        private const int PlacementFailureLogCooldown = 300;
        private static int ensureCheckTimer;
        private static int placementFailureLogTimer;

        #region 选址参数
        private const int WorldEdgeMargin = 40;
        //横向搜索格数：避开复活落点，也给鸟居留出近位
        private const int MinOffsetX = 24;
        private const int MaxOffsetX = 220;
        //上方净空格数：伞贴图 128px 高 ≈ 8 格，再留冗余
        private const int RequiredClearance = 10;
        //伞体+水洼约 8 格宽，按半宽采样
        private const int FlatSampleRadius = 4;
        private const int MaxGroundDeviation = 2;
        //与鸟居锚点的最小横向距离（格），两处地标不许贴脸
        private const int MinToriiDistanceTiles = 30;
        #endregion

        /// <summary>世界尺寸是否足以安全解析伞位</summary>
        public static bool WorldGeometryReady
            => Main.maxTilesX > WorldEdgeMargin * 2 && Main.maxTilesY > WorldEdgeMargin * 2;

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(IsGenerated)] = IsGenerated;
            tag[nameof(UmbrellaPosition)] = UmbrellaPosition;
        }

        public override void LoadWorldData(TagCompound tag) {
            IsGenerated = false;
            UmbrellaPosition = Vector2.Zero;
            try {
                if (tag != null && tag.TryGet(nameof(IsGenerated), out bool generated)) {
                    IsGenerated = generated;
                }
                if (tag != null && tag.TryGet(nameof(UmbrellaPosition), out Vector2 pos)) {
                    UmbrellaPosition = pos;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[OniUmbrellaWorldSpawn:LoadWorldData] Failed to load umbrella data: {ex.Message}");
                IsGenerated = false;
                UmbrellaPosition = Vector2.Zero;
            }

            //世界尺寸已就绪时立即废弃损坏位置；未就绪则由首帧权威维护统一修复
            if (IsGenerated && WorldGeometryReady && !IsValidWorldPosition(UmbrellaPosition)) {
                CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn:LoadWorldData] Discarding invalid umbrella position {UmbrellaPosition}, will regenerate");
                IsGenerated = false;
                UmbrellaPosition = Vector2.Zero;
            }
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(IsGenerated);
            if (IsGenerated) {
                writer.WriteVector2(UmbrellaPosition);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            IsGenerated = reader.ReadBoolean();
            UmbrellaPosition = IsGenerated ? reader.ReadVector2() : Vector2.Zero;
            if (IsGenerated && !IsValidWorldPosition(UmbrellaPosition)) {
                CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn:NetReceive] Ignoring invalid umbrella position {UmbrellaPosition}");
                IsGenerated = false;
                UmbrellaPosition = Vector2.Zero;
            }
        }

        public override void OnWorldLoad() => ResetLocalState();

        public override void OnWorldUnload() => ClearUmbrella();

        public override void Unload() => ClearUmbrella();

        private static void ResetLocalState() {
            ensureCheckTimer = 0;
            placementFailureLogTimer = 0;
            entryLogged = false;
        }

        private static bool entryLogged;

        public override void PostUpdateEverything() {
            LogEntryStateOnce();
            MaintainAuthoritativeUmbrella();
        }

        /// <summary>进世界一条状态账:排查"老存档不显示鬼伞"时直接区分
        /// 未生成/已生成但因已获伞隐藏,不用再猜(反馈一·#44)</summary>
        private static void LogEntryStateOnce() {
            if (entryLogged || Main.dedServ || Main.gameMenu) {
                return;
            }
            if (Main.LocalPlayer?.active != true) {
                return;
            }
            entryLogged = true;
            CWRMod.Instance.Logger.Info(
                $"[OniUmbrellaWorldSpawn] World entry: generated={IsGenerated}, pos={UmbrellaPosition}, "
                + $"visibleToLocal={OniRainWorldUmbrella.ShouldShowForLocalPlayer()}");
        }

        /// <summary>
        /// 世界态由服务端/单人统一维护。首个安全更新帧立即恢复Actor，之后进入低频自检。
        /// </summary>
        private static void MaintainAuthoritativeUmbrella() {
            if (VaultUtils.isClient || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            if (placementFailureLogTimer > 0) {
                placementFailureLogTimer--;
            }

            if (IsGenerated && !IsValidWorldPosition(UmbrellaPosition)) {
                CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn] Invalid runtime position {UmbrellaPosition}, regenerating");
                IsGenerated = false;
                UmbrellaPosition = Vector2.Zero;
            }

            if (!IsGenerated) {
                //等鸟居先落位：两者同在出生点附近，按已知锚点避让；
                //鸟居有全兜底选址，至多晚一帧就绪
                if (!ToriiShrine.IsGenerated) {
                    return;
                }
                TryGenerateUmbrella();
                return;
            }

            if (ensureCheckTimer > 0) {
                ensureCheckTimer--;
                return;
            }

            bool actorReady = EnsureSingleUmbrellaActor();
            ensureCheckTimer = actorReady ? EnsureCheckInterval : 0;
        }

        /// <summary>服务端/单人解析可靠位置并生成；世界几何未就绪时由下一帧重试</summary>
        public static void TryGenerateUmbrella() {
            if (VaultUtils.isClient || IsGenerated || SubWorldRef.AnyActiveSubWorld()
                || !WorldGeometryReady) {
                return;
            }

            if (!TryResolveGuaranteedLocation(out Vector2 position, out string tier)) {
                return;
            }
            if (tier != "StrictTerrain") {
                CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn] Placement used {tier} fallback at {position}");
            }

            GenerateUmbrella(position);
            if (VaultUtils.isServer) {
                SyncUmbrellaToClients();
            }
        }

        private static void SyncUmbrellaToClients() {
            ModPacket packet = CWRNetWork.GetPacket<OniUmbrellaSyncNet>();
            packet.Write(IsGenerated);
            if (IsGenerated) {
                packet.WriteVector2(UmbrellaPosition);
            }
            packet.Send();
        }

        /// <summary>客户端接收权威世界态；Actor实体仍由InnoVault生成广播同步</summary>
        internal static void ReceiveUmbrellaSync(BinaryReader reader) {
            bool generated = reader.ReadBoolean();
            Vector2 position = generated ? reader.ReadVector2() : Vector2.Zero;
            if (!VaultUtils.isClient) {
                return;
            }

            if (generated && !IsValidWorldPosition(position)) {
                CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn:ReceiveUmbrellaSync] Ignoring invalid umbrella position {position}");
                generated = false;
                position = Vector2.Zero;
            }

            IsGenerated = generated;
            UmbrellaPosition = position;
        }

        /// <summary>提交有效世界态并立即放置Actor</summary>
        public static void GenerateUmbrella(Vector2 groundAnchor) {
            if (VaultUtils.isClient || IsGenerated) {
                return;
            }
            if (!IsValidWorldPosition(groundAnchor)) {
                return;
            }

            UmbrellaPosition = groundAnchor;
            IsGenerated = true;
            bool actorReady = EnsureSingleUmbrellaActor();
            ensureCheckTimer = actorReady ? EnsureCheckInterval : 0;
        }

        /// <summary>权威端维持恰好一个Actor，并把位置纠正到存档锚点</summary>
        private static bool EnsureSingleUmbrellaActor() {
            if (VaultUtils.isClient || !IsGenerated || !IsValidWorldPosition(UmbrellaPosition)) {
                return false;
            }

            List<OniRainWorldUmbrella> actors = ActorLoader.GetActiveActors<OniRainWorldUmbrella>();
            if (actors.Count > 1) {
                CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn] Found {actors.Count} umbrella actors; removing duplicates");
            }

            OniRainWorldUmbrella keeper = null;
            foreach (OniRainWorldUmbrella actor in actors) {
                if (keeper == null) {
                    keeper = actor;
                    continue;
                }
                ActorLoader.KillActor(actor.WhoAmI);
            }

            if (keeper != null) {
                if ((keeper.Position - UmbrellaPosition).LengthSquared() > 0.25f) {
                    CWRMod.Instance.Logger.Warn($"[OniUmbrellaWorldSpawn] Correcting actor position {keeper.Position} to {UmbrellaPosition}");
                    keeper.Position = UmbrellaPosition;
                    if (VaultUtils.isServer) {
                        keeper.NetUpdate = true;
                    }
                }
                return true;
            }

            int actorIndex = ActorLoader.NewActor<OniRainWorldUmbrella>(UmbrellaPosition);
            if (actorIndex >= 0) {
                return true;
            }

            if (placementFailureLogTimer <= 0) {
                CWRMod.Instance.Logger.Error($"[OniUmbrellaWorldSpawn] Actor placement failed at {UmbrellaPosition}; retrying automatically");
                placementFailureLogTimer = PlacementFailureLogCooldown;
            }
            return false;
        }

        /// <summary>清本地/存档态(不含Actor)，世界卸载等收尾用</summary>
        public static void ClearUmbrella() {
            IsGenerated = false;
            UmbrellaPosition = Vector2.Zero;
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
        /// 解析保证有效的锚点：严格地形 → 出生点地面 → 出生点原位 → 世界地表中心。
        /// </summary>
        private static bool TryResolveGuaranteedLocation(out Vector2 position, out string tier) {
            position = default;
            tier = "StrictTerrain";
            if (!WorldGeometryReady) {
                return false;
            }

            Vector2? best = FindBestLocation();
            if (best.HasValue && IsValidWorldPosition(best.Value)) {
                position = best.Value;
                return true;
            }

            //出生点向下吸附地面兜底（复用鸟居的通用吸附）
            Vector2 spawn = new(Main.spawnTileX * 16f + 8f, Main.spawnTileY * 16f);
            if (IsValidWorldPosition(spawn)) {
                if (ToriiShrineLocationFinder.TrySnapToGround(spawn, out Vector2 snapped)) {
                    position = snapped;
                    tier = "SpawnGround";
                    return true;
                }
                position = spawn;
                tier = "SpawnPoint";
                return true;
            }

            int tileX = Math.Clamp(Main.maxTilesX / 2, WorldEdgeMargin, Main.maxTilesX - WorldEdgeMargin);
            int preferredY = double.IsFinite(Main.worldSurface)
                ? (int)Math.Round(Main.worldSurface)
                : Main.maxTilesY / 3;
            int tileY = Math.Clamp(preferredY, WorldEdgeMargin, Main.maxTilesY - WorldEdgeMargin);
            position = new Vector2(tileX * 16f + 8f, tileY * 16f);
            tier = "WorldSurface";
            return IsValidWorldPosition(position);
        }

        /// <summary>最佳地面锚点像素坐标，找不到返回null；避让鸟居锚点</summary>
        private static Vector2? FindBestLocation() {
            int spawnX = Main.spawnTileX;
            int toriiTileX = ToriiShrine.IsGenerated
                ? (int)(ToriiShrine.ShrinePosition.X / 16f) : int.MinValue;

            Vector2? bestPosition = null;
            int bestScore = int.MinValue;

            for (int offset = MinOffsetX; offset <= MaxOffsetX; offset += 4) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    int x = spawnX + offset * dir;
                    if (toriiTileX != int.MinValue
                        && Math.Abs(x - toriiTileX) < MinToriiDistanceTiles) {
                        continue;
                    }
                    if (!TryEvaluateColumn(x, out Vector2 position, out int score)) {
                        continue;
                    }

                    //越近出生点越好
                    score -= offset;
                    if (score > bestScore) {
                        bestScore = score;
                        bestPosition = position;
                    }
                }
            }

            return bestPosition;
        }

        /// <summary>评估伞占地区域的净空、液体、危险块与平整度</summary>
        private static bool TryEvaluateColumn(int tileX, out Vector2 position, out int score) {
            position = default;
            score = 0;

            if (!TryFindSurfaceGround(tileX, out int groundY)) {
                return false;
            }

            //须在地表附近，不接受洞穴；出生点本身在地下的世界按出生高度放行
            if (groundY > Main.worldSurface + 40 && Math.Abs(groundY - Main.spawnTileY) > 60) {
                return false;
            }

            int totalDeviation = 0;
            for (int offsetX = -FlatSampleRadius; offsetX <= FlatSampleRadius; offsetX++) {
                int sampleX = tileX + offsetX;
                if (!TryFindSurfaceGround(sampleX, out int sideGroundY)) {
                    return false;
                }

                int deviation = Math.Abs(sideGroundY - groundY);
                if (deviation > MaxGroundDeviation) {
                    return false;
                }
                totalDeviation += deviation;

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
            if (!IsValidWorldPosition(position)) {
                return false;
            }

            score = 100 - totalDeviation * 6;
            return true;
        }

        /// <summary>出生高度附近向下扫到第一块实心地面</summary>
        private static bool TryFindSurfaceGround(int tileX, out int groundY) {
            groundY = 0;
            if (tileX < WorldEdgeMargin || tileX >= Main.maxTilesX - WorldEdgeMargin) {
                return false;
            }

            int startY = Math.Max(WorldEdgeMargin, Main.spawnTileY - 120);
            int endY = Math.Min(Main.maxTilesY - WorldEdgeMargin, Main.spawnTileY + 100);
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

        /// <summary>调试重建(单人)，附近吸附地面并清旧态</summary>
        public static void DebugRebuildAt(Vector2 worldPos) {
            if (!VaultUtils.isSinglePlayer) {
                return;
            }
            foreach (OniRainWorldUmbrella actor in ActorLoader.GetActiveActors<OniRainWorldUmbrella>()) {
                ActorLoader.KillActor(actor.WhoAmI);
            }
            ClearUmbrella();
            if (ToriiShrineLocationFinder.TrySnapToGround(worldPos, out Vector2 groundPos)) {
                GenerateUmbrella(groundPos);
            }
        }
    }

    /// <summary>世界鬼伞权威世界态下发信道</summary>
    internal sealed class OniUmbrellaSyncNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => OniUmbrellaWorldSpawn.ReceiveUmbrellaSync(reader);
    }
}
