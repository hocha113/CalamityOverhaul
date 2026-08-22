using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 矿脉共振：命中矿格后同种矿脉透墙点亮，期间挖掉的同种矿双倍掉落，
    /// 但每次翻倍都会在矿格处生成一只看守，到期看守全部消散。<br/>
    /// 区域账本以锚点格为键外挂在静态字典上（协议实例是单例，对齐 Cryostasis）；
    /// 账本存活性每帧与追踪器对齐，锚点矿被挖掉时追踪器直接丢效果、不走 OnRemove，
    /// 靠 <see cref="SweepZones"/> 兜底清账并消散看守，不存在无主账
    /// </summary>
    internal class VeinResonance : QuickHackDef
    {
        //共振持续（帧，12 秒）
        internal const int ResonanceDuration = 60 * 12;
        //同种矿扫描半径（格）
        internal const int VeinRadius = 60;
        //矿点缓存上限，防超大矿区把表撑爆
        private const int MaxCachedVeins = 400;
        //每区同时存活的看守上限：设计未封顶，这里作为反挂机护栏
        private const int MaxWardensPerZone = 10;

        internal sealed class ResonanceZone
        {
            public int OreType;
            //缓存的同种矿点，权威端与各客户端各自扫各自的（纯本地用途，允许少量分歧）
            public readonly List<Point> Veins = [];
            //看守账只在权威端有内容；身份带世代校验，NPC 槽位复用不背锅
            public readonly List<NetworkNPCIdentity> Wardens = [];
        }

        //锚点格 → 共振区。OnRemove、SweepZones、世界卸载、Unload 四处清账
        private static readonly Dictionary<(int X, int Y), ResonanceZone> zones = [];

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Tile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => ResonanceDuration;

        public override void Unload() {
            base.Unload();
            zones.Clear();
        }

        /// <summary>切世界时清账；看守是普通 NPC，不随世界持久，直接丢表即可</summary>
        internal static void ClearAllZones() => zones.Clear();

        internal static bool IsOreTile(int type) {
            return type >= 0 && type < TileLoader.TileCount
                && (TileID.Sets.Ore[type] || Main.tileOreFinderPriority[type] > 0);
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not TileScannable s) return false;
            if (!IsOreTile(Main.tile[s.TileCoordX, s.TileCoordY].TileType)) return false;
            //同一锚点已在共振时不重复登记，两笔账互相盖掉
            return !zones.ContainsKey((s.TileCoordX, s.TileCoordY));
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not TileScannable s) return false;
            int type = Main.tile[s.TileCoordX, s.TileCoordY].TileType;
            if (!IsOreTile(type)) return false;
            RegisterZone(s.TileCoordX, s.TileCoordY, type);
            if (Main.netMode != NetmodeID.Server) {
                EmitResonanceCue(s.TileCoordX, s.TileCoordY);
            }
            return true;
        }

        //客户端也登记一份区（只用 Veins 做透墙点亮），机制账留在权威端
        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (target is not TileScannable s) return;
            if (!HackTargets.InWorld(s.TileCoordX, s.TileCoordY)) return;
            Tile tile = Main.tile[s.TileCoordX, s.TileCoordY];
            if (tile.HasTile && IsOreTile(tile.TileType)) {
                RegisterZone(s.TileCoordX, s.TileCoordY, tile.TileType);
            }
            EmitResonanceCue(s.TileCoordX, s.TileCoordY);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server && target is TileScannable s) {
                TickZoneVisual(s.TileCoordX, s.TileCoordY, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (target is TileScannable s) {
                TickZoneVisual(s.TileCoordX, s.TileCoordY, elapsed);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (target is not TileScannable s) return;
            ClearZone((s.TileCoordX, s.TileCoordY));
            if (Main.netMode != NetmodeID.Server) {
                EmitEndCue(s.TileCoordX, s.TileCoordY);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (target is not TileScannable s) return;
            zones.Remove((s.TileCoordX, s.TileCoordY));
            EmitEndCue(s.TileCoordX, s.TileCoordY);
        }

        #region 区域账本

        private static void RegisterZone(int anchorX, int anchorY, int oreType) {
            var zone = new ResonanceZone { OreType = oreType };
            ScanVeins(anchorX, anchorY, oreType, zone.Veins);
            zones[(anchorX, anchorY)] = zone;
        }

        private static void ScanVeins(int anchorX, int anchorY, int oreType,
            List<Point> result) {
            result.Clear();
            for (int dx = -VeinRadius; dx <= VeinRadius; dx++) {
                for (int dy = -VeinRadius; dy <= VeinRadius; dy++) {
                    if (dx * dx + dy * dy > VeinRadius * VeinRadius) continue;
                    int tx = anchorX + dx;
                    int ty = anchorY + dy;
                    if (!HackTargets.InWorld(tx, ty)) continue;
                    Tile tile = Main.tile[tx, ty];
                    if (tile.HasTile && tile.TileType == oreType) {
                        result.Add(new Point(tx, ty));
                        if (result.Count >= MaxCachedVeins) return;
                    }
                }
            }
        }

        private static readonly List<(int X, int Y)> staleKeyBuffer = [];

        /// <summary>
        /// 每帧与追踪器对账：效果没了（到期外的任何路径，最常见是锚点矿被挖掉）
        /// 区就地拆掉。权威端顺带消散看守；客户端只丢本地表
        /// </summary>
        internal static void SweepZones() {
            if (zones.Count == 0) return;
            staleKeyBuffer.Clear();
            foreach ((int X, int Y) key in zones.Keys) {
                if (!HackEffectTracker.HasTileEffect<VeinResonance>(key.X, key.Y)) {
                    staleKeyBuffer.Add(key);
                }
            }
            for (int i = 0; i < staleKeyBuffer.Count; i++) {
                ClearZone(staleKeyBuffer[i]);
            }
            staleKeyBuffer.Clear();
        }

        private static void ClearZone((int X, int Y) key) {
            if (!zones.Remove(key, out ResonanceZone zone)) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            //到期全部消散：不是击杀，不走 loot、不出伤害数字
            for (int i = 0; i < zone.Wardens.Count; i++) {
                if (!zone.Wardens[i].TryResolve(out NPC npc)) continue;
                npc.active = false;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
                }
                else {
                    EmitWardenVanish(npc.Center);
                }
            }
            zone.Wardens.Clear();
        }

        #endregion

        #region 掉落翻倍与看守（权威端）

        /// <summary>
        /// 由 <see cref="VeinResonanceTileHook.Drop"/> 转入。选择「直接补掉落」而不是
        /// 改写原版掉落路径：Drop 钩子跑在 KillTile 清格之前、只在权威端触发，
        /// 在这里按同一格再铸一份掉落最省事也最不容易踩别的模组的钩
        /// </summary>
        internal static void HandleTileDropped(int i, int j, int type) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || zones.Count == 0 || !IsOreTile(type)) {
                return;
            }
            foreach (KeyValuePair<(int X, int Y), ResonanceZone> kvp in zones) {
                if (kvp.Value.OreType != type) continue;
                int dx = i - kvp.Key.X;
                int dy = j - kvp.Key.Y;
                if (dx * dx + dy * dy > VeinRadius * VeinRadius) continue;

                //Drop 钩子此刻物块数据还在，能读到应掉何物
                int dropType = Main.tile[i, j].GetTileDrop(i, j);
                if (dropType > 0) {
                    Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j),
                        i * 16, j * 16, 16, 16, dropType);
                }
                TrySpawnWarden(kvp.Value, i, j);
                //多区重叠只翻一次倍
                break;
            }
        }

        private static void TrySpawnWarden(ResonanceZone zone, int i, int j) {
            //先剔掉已死的看守账，再看上限
            for (int k = zone.Wardens.Count - 1; k >= 0; k--) {
                if (!zone.Wardens[k].TryResolve(out _)) {
                    zone.Wardens.RemoveAt(k);
                }
            }
            if (zone.Wardens.Count >= MaxWardensPerZone) return;

            int type = PickWardenType(j);
            int idx = NPC.NewNPC(new EntitySource_SpawnNPC("CWR_VeinWarden"),
                i * 16 + 8, j * 16 + 8, type);
            if (idx < 0 || idx >= Main.maxNPCs) return;
            NPC npc = Main.npc[idx];
            npc.TargetClosest();
            if (NetworkNPCIdentity.TryCapture(npc, out NetworkNPCIdentity identity)) {
                zone.Wardens.Add(identity);
            }
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, number: idx);
            }
            else {
                EmitWardenSpawn(npc.Center);
            }
        }

        //按深度与进度选现成敌怪当看守，不做新 NPC
        private static int PickWardenType(int tileY) {
            bool underworld = tileY > Main.UnderworldLayer;
            if (underworld) {
                return Main.hardMode ? NPCID.RedDevil : NPCID.FireImp;
            }
            if (!Main.hardMode) return NPCID.GraniteGolem;
            return NPC.downedPlantBoss ? NPCID.Paladin : NPCID.PossessedArmor;
        }

        #endregion

        #region 表现（各端各自演）

        private static void EmitResonanceCue(int anchorX, int anchorY) {
            Vector2 center = HackTargets.TileWorldCenter(anchorX, anchorY);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, HackTheme.AccentAlt, 0.9f)
                    ?.Configure(false, 26);
            }
            PRTLoader.NewParticle<PRT_TileHightlight>(center, Vector2.Zero,
                HackTheme.AccentAlt, 1.2f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.5f, Pitch = 0.5f },
                    center);
            }
        }

        //透墙点亮：从缓存里抽矿点打高亮帧动画，顺手剔掉已被挖走的
        private static void TickZoneVisual(int anchorX, int anchorY, int elapsed) {
            if (!zones.TryGetValue((anchorX, anchorY), out ResonanceZone zone)) return;

            if (elapsed % 5 == 0 && zone.Veins.Count > 0) {
                for (int n = 0; n < 2 && zone.Veins.Count > 0; n++) {
                    int pick = Main.rand.Next(zone.Veins.Count);
                    Point p = zone.Veins[pick];
                    Tile tile = Main.tile[p.X, p.Y];
                    if (!tile.HasTile || tile.TileType != zone.OreType) {
                        zone.Veins[pick] = zone.Veins[^1];
                        zone.Veins.RemoveAt(zone.Veins.Count - 1);
                        continue;
                    }
                    //0.8Hz 脉冲亮度
                    float pulse = 0.65f + 0.35f * MathF.Sin(elapsed * 0.084f);
                    PRTLoader.NewParticle<PRT_TileHightlight>(
                        HackTargets.TileWorldCenter(p.X, p.Y), Vector2.Zero,
                        HackTheme.AccentAlt * pulse, 0.55f);
                }
            }
            if (elapsed % 30 == 0) {
                Vector2 center = HackTargets.TileWorldCenter(anchorX, anchorY);
                PRTLoader.NewParticle<PRT_Spark>(center,
                    new Vector2(0f, Main.rand.NextFloat(-1f, -0.2f)),
                    HackTheme.AccentAlt, 0.5f)?.Configure(false, 18);
            }
        }

        private static void EmitEndCue(int anchorX, int anchorY) {
            Vector2 center = HackTargets.TileWorldCenter(anchorX, anchorY);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.5f, 2.5f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel,
                    new Color(120, 180, 220), 0.7f)?.Configure(false, 18);
            }
        }

        private static void EmitWardenSpawn(Vector2 center) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, HackTheme.Danger, 1.1f)
                    ?.Configure(false, 22);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.6f, Pitch = -0.4f },
                    center);
            }
        }

        private static void EmitWardenVanish(Vector2 center) {
            for (int i = 0; i < 8; i++) {
                var square = PRTLoader.NewParticle<PRT_CyberSquare>(center
                    + Main.rand.NextVector2Circular(16f, 16f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f), HackTheme.AccentAlt, 0.9f);
                square?.Configure(HackTheme.Accent, 20);
            }
        }

        #endregion
    }

    /// <summary>矿脉共振的区域账本每帧与追踪器对账；世界卸载清账</summary>
    internal class VeinResonanceSystem : ModSystem
    {
        public override void PostUpdateEverything() => VeinResonance.SweepZones();

        public override void OnWorldUnload() => VeinResonance.ClearAllZones();
    }

    /// <summary>
    /// 接掉落路径：KillTile 的 Drop 钩子只在权威端、清格前触发，
    /// 翻倍补掉落与看守生成都挂在这里
    /// </summary>
    internal class VeinResonanceTileHook : GlobalTile
    {
        public override void Drop(int i, int j, int type)
            => VeinResonance.HandleTileDropped(i, j, type);
    }
}
