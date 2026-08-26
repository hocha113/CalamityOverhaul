using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Rooms;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.NPCs
{
    /// <summary>
    /// 固定威胁公共地基（04 主题域）：绊网/哨雷的注册表驱动、哨眼与绊网的运行时布防、
    /// 噪音联动封锁闸状态机。镜像 <see cref="OldNetICEDirector"/> 形态：
    /// 权威端裁决 + OnWorldLoad 复位 + OldNetWorld.Active 门控 + 消费 OldNetPlans 的权威端豁免先例。
    /// 注册表型装置不吃 NPC 冻结，统一自查 WorldFreezeSystem.IsActive（时停=固定威胁通行券）
    /// </summary>
    internal class OldNetThreatField : ModSystem
    {
        private static readonly Color WarnRed = new(235, 64, 44);
        private static readonly Color Amber = new(255, 170, 60);
        private static readonly Color Mint = new(120, 255, 170);

        //──── 哨雷注册表（懒扫描维护；出窗弃态，回来重新武装）────

        internal enum MinePhase { Dormant, Arming }

        internal sealed class MineState
        {
            internal MinePhase Phase;
            internal int ArmTimer;
        }

        /// <summary>键=雷 tile 坐标</summary>
        internal static readonly Dictionary<Point, MineState> Mines = [];

        //──── 绊网注册表（按对存；剪任意一桩双删，孤儿桩巡检清理）────

        internal sealed class TripwirePair
        {
            /// <summary>锚桩（横梁=左桩，竖梁=上桩），光束由它登记批绘</summary>
            internal Point A;
            internal Point B;
            /// <summary>true=门洞竖梁</summary>
            internal bool Vertical;
            /// <summary>周期相位偏移（坐标哈希，同屏错相成通行序列）</summary>
            internal int Phase;
            /// <summary>触发后冷却（防连报）</summary>
            internal int RearmTimer;
        }

        internal static readonly List<TripwirePair> TripwirePairs = [];
        //两端坐标都指向同一实例（右键任一桩可剪）
        private static readonly Dictionary<Point, TripwirePair> tripwireLookup = [];

        //过线闪报的红环脉冲（一次性演出，TileFXRender CPU 批消费）
        internal sealed class TripPulse
        {
            internal Vector2 Pos;
            internal int Timer;
        }

        internal const int TripPulseLife = 24;
        internal static readonly List<TripPulse> TripPulses = [];

        //──── 封锁闸组（每口竖井一组）────

        internal enum BulkheadState { Open, Warn, Shut }

        internal sealed class BulkheadGroup
        {
            /// <summary>闸槽（tile 坐标，ShaftWidth×2，井口平台正下方）</summary>
            internal Rectangle Slot;
            /// <summary>应急泄压杆坐标；(-1,-1)=落位失败</summary>
            internal Point Breaker = new(-1, -1);
            /// <summary>泄压临时开启剩余 tick</summary>
            internal int BreakerTimer;
            /// <summary>与玩家碰撞盒重叠的延迟落格（每 BulkheadRetryTicks 重试，不夹人）</summary>
            internal readonly List<Point> Pending = [];
        }

        internal static readonly List<BulkheadGroup> Bulkheads = [];
        internal static BulkheadState GateState { get; private set; }
        //重开保持计时（档位 ≤1 持续 BulkheadReopenHoldTicks）
        private static int reopenHold;

        /// <summary>重开前的薄荷绿脉冲 0..1（TileFXRender 预告层消费）</summary>
        internal static float ReopenPulse01 {
            get {
                if (GateState != BulkheadState.Shut) {
                    return 0f;
                }
                int lead = OldNetMetrics.BulkheadReopenHoldTicks - OldNetMetrics.BulkheadReopenPulseTicks;
                return reopenHold <= lead ? 0f
                    : (reopenHold - lead) / (float)OldNetMetrics.BulkheadReopenPulseTicks;
            }
        }

        /// <summary>闸门预告层是否需要绘制（OPEN/WARN 的括号与闸影不依赖实体格存在）</summary>
        internal static bool BulkheadOverlayVisible => OldNetWorld.Active && Bulkheads.Count > 0;

        //──── 会话时钟与旗标 ────

        /// <summary>装置共用时钟：时停中不推进（绊网节律/落格重试与判定同源冻结）。
        /// TODO MP: 时钟为本机推进，联机化需以世界 tick 为相位基</summary>
        internal static int FieldTicks { get; private set; }

        private static bool seeded;
        private static int scanTimer;

        private static void ResetSession() {
            seeded = false;
            scanTimer = 0;
            FieldTicks = 0;
            reopenHold = 0;
            GateState = BulkheadState.Open;
            Mines.Clear();
            TripwirePairs.Clear();
            tripwireLookup.Clear();
            TripPulses.Clear();
            Bulkheads.Clear();
        }

        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        public override void PostUpdateNPCs() {
            //权威端裁决（Director 同款）。TODO MP: 判定移服务器后客户端走表现同步
            if (VaultUtils.isClient || !OldNetWorld.Active) {
                return;
            }

            if (!seeded) {
                seeded = true;
                SeedTripwires();
                SeedSweepEyes();
                SeedBulkheads();
            }

            Player player = ResolveLocalPlayer();
            if (player == null) {
                return;
            }

            //时停通则：注册表型装置整体暂停（时钟不走、判定不跑、状态机不迁移）
            if (WorldFreezeSystem.IsActive) {
                return;
            }
            FieldTicks++;

            if (++scanTimer >= OldNetMetrics.ThreatScanInterval) {
                scanTimer = 0;
                ScanWindow(player);
            }

            //红环脉冲衰减（纯演出计时）
            for (int i = TripPulses.Count - 1; i >= 0; i--) {
                if (--TripPulses[i].Timer <= 0) {
                    TripPulses.RemoveAt(i);
                }
            }

            //弹幕引爆逐 tick 查（二审修复：20t 采样对 16px/t 快弹漏检九成；雷数恒 ≤10 成本可忽略）
            ScanProjectiles(player);
            TickMines(player);
            TickTripwires(player);
            TickBulkheads(player);
        }

        //M1 单人语义：本机玩家即威胁源；服务器兜底首个活人（Director 同款）
        private static Player ResolveLocalPlayer() {
            if (!Main.dedServ && Main.LocalPlayer?.active == true && !Main.LocalPlayer.dead) {
                return Main.LocalPlayer;
            }
            foreach (Player p in Main.ActivePlayers) {
                if (!p.dead) {
                    return p;
                }
            }
            return null;
        }

        //════════════════ 懒扫描（雷入册/出窗弃态 + 绊网孤儿巡检）════════════════

        private static void ScanWindow(Player player) {
            int mineType = ModContent.TileType<Tiles.OldNetSentryMineTile>();
            int cx = (int)(player.Center.X / 16f);
            int cy = (int)(player.Center.Y / 16f);
            int x0 = cx - OldNetMetrics.ThreatScanCols, x1 = cx + OldNetMetrics.ThreatScanCols;
            int y0 = cy - OldNetMetrics.ThreatScanRows, y1 = cy + OldNetMetrics.ThreatScanRows;

            for (int x = x0; x <= x1; x++) {
                for (int y = y0; y <= y1; y++) {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasTile || tile.TileType != mineType) {
                        continue;
                    }
                    Point p = new(x, y);
                    if (!Mines.ContainsKey(p)) {
                        Mines[p] = new MineState();
                    }
                }
            }

            //出窗/失格清册
            List<Point> drop = null;
            foreach (Point p in Mines.Keys) {
                Tile tile = Framing.GetTileSafely(p.X, p.Y);
                bool gone = !tile.HasTile || tile.TileType != mineType;
                bool outside = p.X < x0 || p.X > x1 || p.Y < y0 || p.Y > y1;
                if (gone || outside) {
                    (drop ??= []).Add(p);
                }
            }
            if (drop != null) {
                foreach (Point p in drop) {
                    Mines.Remove(p);
                }
            }

            //绊网孤儿巡检：任一桩失格 → 整对拆除（按对存的完整性约定）
            int pylonType = ModContent.TileType<Tiles.OldNetTripwireTile>();
            List<TripwirePair> broken = null;
            foreach (TripwirePair pair in TripwirePairs) {
                Tile a = Framing.GetTileSafely(pair.A.X, pair.A.Y);
                Tile b = Framing.GetTileSafely(pair.B.X, pair.B.Y);
                bool aOk = a.HasTile && a.TileType == pylonType;
                bool bOk = b.HasTile && b.TileType == pylonType;
                if (!aOk || !bOk) {
                    (broken ??= []).Add(pair);
                }
            }
            if (broken != null) {
                foreach (TripwirePair pair in broken) {
                    RemovePair(pair);
                }
            }
        }

        //玩家弹幕近点引爆：远程排雷省血省 RAM，不省暴露（尖叫在雷不在人）
        private static void ScanProjectiles(Player player) {
            if (Mines.Count == 0) {
                return;
            }
            List<Point> boom = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (!proj.friendly || proj.damage <= 0 || proj.owner != player.whoAmI) {
                    continue;
                }
                //二审修复三道闸：手持投影（BaseHeldProj 常驻手部 10-30px，持械慢走拆雷会被自己的枪误爆）、
                //召唤物/哨兵（路过连环爆）、低速门（真实飞行弹才配引爆）
                if (proj.ModProjectile is BaseHeldProj || proj.minion || proj.sentry
                    || proj.velocity.LengthSquared() <= 9f) {
                    continue;
                }
                foreach (KeyValuePair<Point, MineState> kv in Mines) {
                    Vector2 c = new(kv.Key.X * 16 + 8, kv.Key.Y * 16 + 8);
                    if (Vector2.Distance(proj.Center, c) <= OldNetMetrics.MineRemoteDetonateRadius) {
                        (boom ??= []).Add(kv.Key);
                    }
                }
            }
            if (boom != null) {
                foreach (Point p in boom) {
                    if (Mines.ContainsKey(p)) {
                        Detonate(p, player);
                    }
                }
            }
        }

        //════════════════ 静默哨雷 ════════════════

        private static void TickMines(Player player) {
            if (Mines.Count == 0) {
                return;
            }
            List<Point> boom = null;
            foreach (KeyValuePair<Point, MineState> kv in Mines) {
                MineState mine = kv.Value;
                Vector2 c = new(kv.Key.X * 16 + 8, kv.Key.Y * 16 + 8);
                float dist = Vector2.Distance(player.Center, c);
                //慢速接近（≤2f）不触发武装：潜行语言全域一致
                bool fast = player.velocity.Length() > OldNetMetrics.MineArmSpeedGate;

                switch (mine.Phase) {
                    case MinePhase.Dormant:
                        if (dist < OldNetMetrics.MineWakeRadius && fast && !player.dead) {
                            mine.Phase = MinePhase.Arming;
                            mine.ArmTimer = 0;
                            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.65f, Pitch = 0.9f }, c);
                        }
                        break;
                    case MinePhase.Arming:
                        if (dist > OldNetMetrics.MineWakeRadius || !fast || player.dead) {
                            //离场或降速即回落
                            mine.Phase = MinePhase.Dormant;
                            mine.ArmTimer = 0;
                            break;
                        }
                        mine.ArmTimer++;
                        //加速蜂鸣：越接近引爆节拍越密、音高越尖
                        int beat = mine.ArmTimer < OldNetMetrics.MineArmTicks / 2 ? 6 : 3;
                        if (mine.ArmTimer % beat == 0) {
                            SoundEngine.PlaySound(SoundID.MenuTick with {
                                Volume = 0.6f,
                                Pitch = 0.4f + mine.ArmTimer / (float)OldNetMetrics.MineArmTicks * 0.6f,
                            }, c);
                        }
                        if (mine.ArmTimer >= OldNetMetrics.MineArmTicks) {
                            (boom ??= []).Add(kv.Key);
                        }
                        break;
                }
            }
            if (boom != null) {
                foreach (Point p in boom) {
                    if (Mines.ContainsKey(p)) {
                        Detonate(p, player);
                    }
                }
            }
        }

        //引爆：不炸匿名性才是本体（HP/RAM 只对贴身者结算，尖叫必然广播）
        private static void Detonate(Point p, Player player) {
            Vector2 c = new(p.X * 16 + 8, p.Y * 16 + 8);
            OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseMineScream);
            if (Vector2.Distance(player.Center, c) <= OldNetMetrics.MineWakeRadius && !player.dead) {
                int dir = player.Center.X < c.X ? -1 : 1;
                player.Hurt(PlayerDeathReason.ByCustomReason(
                    OldNetTexts.OldNetMineDeath.Format(player.name)),
                    OldNetMetrics.MineDamage, dir, knockback: 8f);
                //TODO MP: RAM 扣减 MP 客户端直调必失败，联机化走请求包（TurretBolt 同款）
                RamSystem.TryConsume(player, OldNetMetrics.MineRam);
            }
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), WarnRed, OldNetTexts.OldNetMineScream.Value, dramatic: true);
            }
            SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.9f, Pitch = 0.6f }, c);

            Mines.Remove(p);
            KillDeviceTile(p, ModContent.TileType<Tiles.OldNetSentryMineTile>());
            EmitBurst(c, WarnRed, 12);
        }

        /// <summary>读雷态（tile 绘制消费）；未入册给 null（休眠画法兜底）</summary>
        internal static MineState GetMineState(int i, int j)
            => Mines.TryGetValue(new Point(i, j), out MineState state) ? state : null;

        /// <summary>右键自愈入册（懒扫描间隙内点雷也要有态可读）</summary>
        internal static MineState EnsureMineTracked(int i, int j) {
            Point p = new(i, j);
            if (!Mines.TryGetValue(p, out MineState state)) {
                state = new MineState();
                Mines[p] = state;
            }
            return state;
        }

        /// <summary>拆除完成：静默移除，零噪音、无掉落（安静路线收益不被污染）</summary>
        internal static void CompleteDefuse(Point p, Player player) {
            Mines.Remove(p);
            KillDeviceTile(p, ModContent.TileType<Tiles.OldNetSentryMineTile>());
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), Mint, OldNetTexts.OldNetMineDefused.Value);
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.3f },
                new Vector2(p.X, p.Y) * 16f);
        }

        //════════════════ 光栅绊网 ════════════════

        private static void TickTripwires(Player player) {
            if (TripwirePairs.Count == 0) {
                return;
            }
            Rectangle hit = player.Hitbox;
            foreach (TripwirePair pair in TripwirePairs) {
                if (pair.RearmTimer > 0) {
                    pair.RearmTimer--;
                    continue;
                }
                BeamCycleState(pair.Phase, out bool lit, out _, out _);
                if (!lit || !BeamRect(pair).Intersects(hit)) {
                    continue;
                }
                //过线：只烧噪音预算不叫猎队（绊网是计量器不是警铃）
                pair.RearmTimer = OldNetMetrics.TripwireRearmTicks;
                OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseTripwire);
                //位置闪报：红环脉冲（规格项）+ 方块碎粒
                TripPulses.Add(new TripPulse { Pos = player.Center, Timer = TripPulseLife });
                EmitBurst(player.Center, WarnRed, 6);
                SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.35f, Pitch = 0.3f },
                    player.Center);
            }
        }

        /// <summary>光束节律：lit=亮相判定期，litT=亮相进度，preBlink=亮相前的起搏预告</summary>
        internal static void BeamCycleState(int phase, out bool lit, out float litT, out bool preBlink) {
            int cycle = OldNetMetrics.TripwireOnTicks + OldNetMetrics.TripwireOffTicks;
            int pos = (FieldTicks + phase) % cycle;
            if (pos < 0) {
                pos += cycle;
            }
            lit = pos < OldNetMetrics.TripwireOnTicks;
            litT = lit ? pos / (float)OldNetMetrics.TripwireOnTicks : 0f;
            preBlink = !lit && cycle - pos <= OldNetMetrics.TripwireBlinkTicks;
        }

        /// <summary>光束判定盒（世界 px，6px 厚）</summary>
        internal static Rectangle BeamRect(TripwirePair pair) {
            int ax = pair.A.X * 16 + 8, ay = pair.A.Y * 16 + 8;
            int bx = pair.B.X * 16 + 8, by = pair.B.Y * 16 + 8;
            if (pair.Vertical) {
                return new Rectangle(ax - 3, Math.Min(ay, by), 6, Math.Abs(by - ay));
            }
            return new Rectangle(Math.Min(ax, bx), ay - 3, Math.Abs(bx - ax), 6);
        }

        internal static bool TryGetTripwire(int i, int j, out TripwirePair pair)
            => tripwireLookup.TryGetValue(new Point(i, j), out pair);

        /// <summary>剪断完成：双删 + 小额噪音（时停中经 AddNoise 自动打折）</summary>
        internal static void CompleteCut(Point pylon, Player player) {
            if (!tripwireLookup.TryGetValue(pylon, out TripwirePair pair)) {
                CWRMod.Instance.Logger.Warn($"[OldNet] 剪断目标不在绊网注册表 ({pylon.X},{pylon.Y})");
                return;
            }
            RemovePair(pair);
            OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseTripwireCut);
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), Mint, OldNetTexts.OldNetTripwireCut.Value);
            }
            SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.5f, Pitch = 0.2f },
                new Vector2(pylon.X, pylon.Y) * 16f);
        }

        private static void RemovePair(TripwirePair pair) {
            int pylonType = ModContent.TileType<Tiles.OldNetTripwireTile>();
            KillDeviceTile(pair.A, pylonType);
            KillDeviceTile(pair.B, pylonType);
            tripwireLookup.Remove(pair.A);
            tripwireLookup.Remove(pair.B);
            TripwirePairs.Remove(pair);
        }

        //════════════════ 噪音联动封锁闸 ════════════════

        private static void TickBulkheads(Player player) {
            if (Bulkheads.Count == 0) {
                return;
            }
            int tier = OldNetPlayer.Get(player).NoiseTier;
            //TODO MP: per-player 档位对闸门的语义重定义（取区域最高档？）留 MP 批次整体裁决
            bool wantShut = tier >= OldNetMetrics.BulkheadShutTier || OldNetICEDirector.CleanupWaveActive;

            switch (GateState) {
                case BulkheadState.Open:
                    if (wantShut) {
                        EnterShut(player);
                    }
                    else if (tier >= OldNetMetrics.BulkheadWarnTier) {
                        //预紧演出：最后一班车窗口，不阻挡
                        GateState = BulkheadState.Warn;
                        Announce(player, OldNetTexts.OldNetBulkheadWarn.Value, Amber);
                        SoundEngine.PlaySound(CWRSound.FaultTransition with { Volume = 0.55f, Pitch = -0.7f },
                            NearestSlotCenter(player));
                    }
                    break;
                case BulkheadState.Warn:
                    if (wantShut) {
                        EnterShut(player);
                    }
                    else if (tier < OldNetMetrics.BulkheadWarnTier) {
                        GateState = BulkheadState.Open;
                    }
                    break;
                case BulkheadState.Shut:
                    if (!wantShut && tier <= 1) {
                        if (++reopenHold >= OldNetMetrics.BulkheadReopenHoldTicks) {
                            EnterOpen(player);
                        }
                    }
                    else {
                        reopenHold = 0;
                    }
                    break;
            }

            //每组：泄压窗口倒数与延迟落格重试
            foreach (BulkheadGroup g in Bulkheads) {
                if (g.BreakerTimer > 0) {
                    if (--g.BreakerTimer == 0 && GateState == BulkheadState.Shut) {
                        ShutGroup(g);
                    }
                    continue;
                }
                if (GateState == BulkheadState.Shut && g.Pending.Count > 0
                    && FieldTicks % OldNetMetrics.BulkheadRetryTicks == 0) {
                    RetryPending(g);
                }
            }
        }

        private static void EnterShut(Player player) {
            GateState = BulkheadState.Shut;
            reopenHold = 0;
            foreach (BulkheadGroup g in Bulkheads) {
                if (g.BreakerTimer > 0) {
                    continue;
                }
                ShutGroup(g);
            }
            Announce(player, OldNetTexts.OldNetBulkheadShut.Value, WarnRed);
            //音画堆叠约束：五口井同触发只在距玩家最近一口出声
            SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.85f, Pitch = -0.6f },
                NearestSlotCenter(player));
            CWRMod.Instance.Logger.Info("[OldNet] bulkheads SHUT");
        }

        private static void EnterOpen(Player player) {
            GateState = BulkheadState.Open;
            reopenHold = 0;
            foreach (BulkheadGroup g in Bulkheads) {
                OpenGroup(g);
                g.BreakerTimer = 0;
            }
            Announce(player, OldNetTexts.OldNetBulkheadReopen.Value, Mint);
            SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.6f, Pitch = -0.1f },
                NearestSlotCenter(player));
            CWRMod.Instance.Logger.Info("[OldNet] bulkheads REOPEN");
        }

        //逐格落闸：与玩家碰撞盒重叠的格延迟落（不夹人，但挡不住整扇门）
        private static void ShutGroup(BulkheadGroup g) {
            g.Pending.Clear();
            for (int x = g.Slot.Left; x < g.Slot.Right; x++) {
                for (int y = g.Slot.Top; y < g.Slot.Bottom; y++) {
                    if (Framing.GetTileSafely(x, y).HasTile) {
                        continue;
                    }
                    if (AnyPlayerOverlap(x, y)) {
                        g.Pending.Add(new Point(x, y));
                    }
                    else {
                        WriteGateCell(x, y);
                    }
                }
            }
        }

        private static void OpenGroup(BulkheadGroup g) {
            int gateType = ModContent.TileType<Tiles.OldNetBulkheadTile>();
            for (int x = g.Slot.Left; x < g.Slot.Right; x++) {
                for (int y = g.Slot.Top; y < g.Slot.Bottom; y++) {
                    KillDeviceTile(new Point(x, y), gateType);
                }
            }
            g.Pending.Clear();
        }

        private static void RetryPending(BulkheadGroup g) {
            for (int i = g.Pending.Count - 1; i >= 0; i--) {
                Point p = g.Pending[i];
                if (Framing.GetTileSafely(p.X, p.Y).HasTile) {
                    g.Pending.RemoveAt(i);
                    continue;
                }
                if (!AnyPlayerOverlap(p.X, p.Y)) {
                    WriteGateCell(p.X, p.Y);
                    g.Pending.RemoveAt(i);
                }
            }
        }

        private static void WriteGateCell(int x, int y) {
            Tile tile = Main.tile[x, y];
            tile.HasTile = true;
            tile.TileType = (ushort)ModContent.TileType<Tiles.OldNetBulkheadTile>();
            tile.TileFrameX = 0;
            tile.TileFrameY = 0;
            tile.Slope = SlopeType.Solid;
            tile.IsHalfBlock = false;
            WorldGen.SquareTileFrame(x, y);
            //TODO MP: 服务器落格需 SendTileSquare 批量账
        }

        //格子外扩 2px 再查：贴边像素也算重叠，宁可晚落不夹人
        private static bool AnyPlayerOverlap(int x, int y) {
            Rectangle cell = new(x * 16 - 2, y * 16 - 2, 20, 20);
            foreach (Player p in Main.ActivePlayers) {
                if (!p.dead && p.getRect().Intersects(cell)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>泄压杆右键：落闸期间买 8s 通行（+10 噪，付款方式本身延长刑期）</summary>
        internal static bool TryPullBreaker(int i, int j, Player player) {
            BulkheadGroup group = null;
            foreach (BulkheadGroup g in Bulkheads) {
                if (g.Breaker.X == i && g.Breaker.Y == j) {
                    group = g;
                    break;
                }
            }
            if (group == null) {
                return false;
            }
            //时停中状态机冻结，杆也拉不动；非落闸期是死杆
            if (WorldFreezeSystem.IsActive || GateState != BulkheadState.Shut || group.BreakerTimer > 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.6f },
                    new Vector2(i, j) * 16f);
                return true;
            }
            OpenGroup(group);
            group.BreakerTimer = OldNetMetrics.BreakerOpenTicks;
            OldNetPlayer.Get(player).AddNoise(OldNetMetrics.NoiseBreaker);
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), Mint, OldNetTexts.OldNetBreakerPulled.Value);
            }
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = -0.2f },
                new Vector2(i, j) * 16f);
            return true;
        }

        /// <summary>泄压窗口剩余 tick（泄压杆绘制读取）；非本组杆给 -1</summary>
        internal static int BreakerWindowTicks(int i, int j) {
            foreach (BulkheadGroup g in Bulkheads) {
                if (g.Breaker.X == i && g.Breaker.Y == j) {
                    return g.BreakerTimer;
                }
            }
            return -1;
        }

        private static void Announce(Player player, string text, Color color) {
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), color, text);
            }
        }

        private static Vector2 NearestSlotCenter(Player player) {
            Vector2 best = player.Center;
            float bestDist = float.MaxValue;
            foreach (BulkheadGroup g in Bulkheads) {
                Vector2 c = new(g.Slot.Center.X * 16, g.Slot.Center.Y * 16);
                float d = Vector2.DistanceSquared(player.Center, c);
                if (d < bestDist) {
                    bestDist = d;
                    best = c;
                }
            }
            return best;
        }

        //════════════════ 运行时布防（权威端一次性，消费 OldNetPlans 豁免先例）════════════════

        //绊网：竖井歇脚平台间隔段 2~3 道 + 门洞 35%（井壁落位质量：逐对落点入日志供目检）
        private static void SeedTripwires() {
            int pylonType = ModContent.TileType<Tiles.OldNetTripwireTile>();
            int shaftPairs = 0, socketPairs = 0;

            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                int want = Main.rand.Next(OldNetMetrics.TripwirePerShaftMin,
                    OldNetMetrics.TripwirePerShaftMax + 1);
                List<int> usedRows = [];
                for (int attempt = 0; attempt < 24 && want > 0; attempt++) {
                    //起始行 +3：让开井口平台正下方 2 行的封锁闸槽（落闸后红线不嵌闸体）
                    int y = Main.rand.Next(shaft.SurfaceRow + 3, shaft.Landing.Top - 2);
                    //歇脚平台行让位 + 同井道间距 ≥3 行
                    if ((y - shaft.SurfaceRow) % OldNetMetrics.ShaftLedgeStep == 0) {
                        continue;
                    }
                    bool near = false;
                    foreach (int used in usedRows) {
                        if (Math.Abs(used - y) < 3) {
                            near = true;
                            break;
                        }
                    }
                    if (near) {
                        continue;
                    }
                    int ax = shaft.Col;
                    int bx = shaft.Col + OldNetMetrics.ShaftWidth - 1;
                    //两端内腔空气格 + 外侧井衬实心（桩要有墙可挂）
                    if (Framing.GetTileSafely(ax, y).HasTile || Framing.GetTileSafely(bx, y).HasTile
                        || !IsSolid(ax - 1, y) || !IsSolid(bx + 1, y)) {
                        continue;
                    }
                    if (!TryWritePylonPair(new Point(ax, y), new Point(bx, y), vertical: false, pylonType)) {
                        continue;
                    }
                    usedRows.Add(y);
                    want--;
                    shaftPairs++;
                    CWRMod.Instance.Logger.Info($"[OldNet] tripwire shaft col={shaft.Col} row={y}");
                }
            }

            //门洞：房间开口上下沿（竖梁）；开口矩形来自建造方登记的 DoorSocket
            foreach (OldNetBuildContext ctx in new[] { OldNetPlans.Z1, OldNetPlans.Z2, OldNetPlans.Z3 }) {
                if (ctx == null) {
                    continue;
                }
                foreach (OldNetRoomNode room in ctx.Graph.Rooms) {
                    foreach (OldNetDoorSocket socket in room.Sockets) {
                        if (Main.rand.NextFloat() >= OldNetMetrics.TripwireSocketChance) {
                            continue;
                        }
                        Rectangle opening = socket.Opening;
                        if (opening.Height < 3) {
                            continue;
                        }
                        int col = opening.Center.X;
                        Point a = new(col, opening.Top);
                        Point b = new(col, opening.Bottom - 1);
                        if (Framing.GetTileSafely(a.X, a.Y).HasTile
                            || Framing.GetTileSafely(b.X, b.Y).HasTile
                            || tripwireLookup.ContainsKey(a) || tripwireLookup.ContainsKey(b)) {
                            continue;
                        }
                        if (!TryWritePylonPair(a, b, vertical: true, pylonType)) {
                            continue;
                        }
                        socketPairs++;
                        CWRMod.Instance.Logger.Info($"[OldNet] tripwire socket ({a.X},{a.Y})-({b.X},{b.Y})");
                    }
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] tripwires seeded shaft={shaftPairs} socket={socketPairs}");
        }

        private static bool TryWritePylonPair(Point a, Point b, bool vertical, int pylonType) {
            if (!OldNetNodeBudget.WriteNodeTile(a.X, a.Y, pylonType)) {
                return false;
            }
            if (!OldNetNodeBudget.WriteNodeTile(b.X, b.Y, pylonType)) {
                //半置回滚：不留孤儿桩
                WorldGen.KillTile(a.X, a.Y, noItem: true);
                return false;
            }
            WorldGen.SquareTileFrame(a.X, a.Y);
            WorldGen.SquareTileFrame(b.X, b.Y);
            int cycle = OldNetMetrics.TripwireOnTicks + OldNetMetrics.TripwireOffTicks;
            TripwirePair pair = new() {
                A = a,
                B = b,
                Vertical = vertical,
                Phase = (a.X * 53 + a.Y * 131) % cycle,
            };
            TripwirePairs.Add(pair);
            tripwireLookup[a] = pair;
            tripwireLookup[b] = pair;
            //TODO MP: 运行时写格需 SendTileSquare 同步到客户端
            return true;
        }

        //哨眼：封锁盒顶 + 中继邻近（守结算窗口）+ 浅井井口 50%
        private static void SeedSweepEyes() {
            int type = ModContent.NPCType<OldNetSweepEye>();
            int placed = 0;

            foreach (Rectangle box in OldNetPlans.SealBoxes) {
                placed += SpawnEye(type, box.Center.X, box.Y - 3, -MathHelper.PiOver2);
            }
            foreach (Point spot in OldNetPlans.RelaySpots) {
                //二审修复：中继眼悬于结算点上方、锥轴向下俯照地面（原朝天轴可达范围
                //永不低于水平线，站地面结算的玩家永远在锥外，落点意图落空）
                int x = spot.X + 1;
                int row = ProbeSurfaceRow(x);
                if (row < 0) {
                    continue;
                }
                placed += SpawnEye(type, x, row - 6, MathHelper.PiOver2);
            }
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                if (shaft.Deep || Main.rand.NextFloat() >= OldNetMetrics.SweepEyeShaftChance) {
                    continue;
                }
                //井口上方俯视井腔：锥轴向下，覆盖下潜动线
                placed += SpawnEye(type, shaft.Col + OldNetMetrics.ShaftWidth / 2,
                    shaft.SurfaceRow - 5, MathHelper.PiOver2);
            }
            CWRMod.Instance.Logger.Info($"[OldNet] sweep eyes seeded={placed}");
        }

        private static int SpawnEye(int type, int tileX, int tileY, float baseAxis) {
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(),
                tileX * 16 + 8, tileY * 16 + 8, type, ai1: baseAxis);
            if (idx < 0 || idx >= Main.maxNPCs) {
                return 0;
            }
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
            return 1;
        }

        //封锁闸：每口竖井井口平台正下方 2 行闸槽 + 落点厅内壁一枚泄压杆
        private static void SeedBulkheads() {
            int breakerType = ModContent.TileType<Tiles.OldNetBreakerTile>();
            foreach (OldNetShaft shaft in OldNetPlans.Shafts) {
                BulkheadGroup group = new() {
                    Slot = new Rectangle(shaft.Col, shaft.SurfaceRow + 1, OldNetMetrics.ShaftWidth, 2),
                };
                Rectangle landing = shaft.Landing;
                Point[] candidates = [
                    new(landing.Left, landing.Bottom - 2),
                    new(landing.Right - 1, landing.Bottom - 2),
                    new(landing.Left + 1, landing.Bottom - 2),
                    new(landing.Right - 2, landing.Bottom - 2),
                ];
                foreach (Point cand in candidates) {
                    if (OldNetNodeBudget.WriteNodeTile(cand.X, cand.Y, breakerType)) {
                        WorldGen.SquareTileFrame(cand.X, cand.Y);
                        group.Breaker = cand;
                        break;
                    }
                }
                if (group.Breaker.X < 0) {
                    CWRMod.Instance.Logger.Warn($"[OldNet] 泄压杆无处落位 shaft col={shaft.Col}");
                }
                Bulkheads.Add(group);
            }
            CWRMod.Instance.Logger.Info($"[OldNet] bulkheads seeded={Bulkheads.Count}");
        }

        //════════════════ 杂项 ════════════════

        //从天空向下找该列首块实心，返回行号；找不到给 -1（Director 同款探针，其版本为私有故本地复刻）
        private static int ProbeSurfaceRow(int x) {
            for (int y = OldNetMetrics.BorderThick + 4; y < OldNetMetrics.FloorRow + 12; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }
            return -1;
        }

        private static bool IsSolid(int x, int y) {
            Tile tile = Framing.GetTileSafely(x, y);
            return tile.HasTile && Main.tileSolid[tile.TileType];
        }

        private static void KillDeviceTile(Point p, int expectType) {
            Tile tile = Framing.GetTileSafely(p.X, p.Y);
            if (!tile.HasTile || tile.TileType != expectType) {
                return;
            }
            WorldGen.KillTile(p.X, p.Y, noItem: true);
            //TODO MP: 权威端删格的对客户端广播并入既有 SendTileSquare 台账（与运行时写格同批结）
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, p.X, p.Y, 1);
            }
        }

        //像素方块喷发 + 白闪（哨雷引爆/绊网闪报共用，客户端限定）
        private static void EmitBurst(Vector2 pos, Color color, int count) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(
                    pos + Main.rand.NextVector2Circular(5f, 5f), vel,
                    color, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Color.Lerp(color, Color.White, 0.4f), Main.rand.Next(16, 28));
            }
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, Color.White,
                Main.rand.NextFloat(0.45f, 0.6f))?.Configure(10, opacity: 0.75f);
        }
    }

    /// <summary>
    /// 04 固定威胁的每玩家会话态：剪断绊网/拆除哨雷的站桩信道。
    /// 松开右键/移动/受击/离开即中断（受击以 immuneTime 上跳近似检测）。
    /// TODO MP: 信道裁决移服务器（当前本机语义与旧网单人门禁同口径）
    /// </summary>
    internal class OldNetThreatPlayer : ModPlayer
    {
        internal const int KindNone = 0;
        internal const int KindCutTripwire = 1;
        internal const int KindDefuseMine = 2;

        internal int ChannelKind;
        internal Point ChannelTarget = new(-1, -1);
        internal int ChannelTimer;
        private int lastImmuneTime;

        /// <summary>信道进度 0..1（装置绘制读取）</summary>
        internal float ChannelProgress => ChannelKind switch {
            KindCutTripwire => MathHelper.Clamp(
                ChannelTimer / (float)OldNetMetrics.TripwireCutTicks, 0f, 1f),
            KindDefuseMine => MathHelper.Clamp(
                ChannelTimer / (float)OldNetMetrics.MineDefuseTicks, 0f, 1f),
            _ => 0f,
        };

        internal bool IsChanneling(int kind, int i, int j)
            => ChannelKind == kind && ChannelTarget.X == i && ChannelTarget.Y == j;

        internal void BeginChannel(int kind, int i, int j) {
            //按住期间右键重复触发不重置进度
            if (IsChanneling(kind, i, j)) {
                return;
            }
            ChannelKind = kind;
            ChannelTarget = new Point(i, j);
            ChannelTimer = 0;
            lastImmuneTime = Player.immuneTime;
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = 0.3f },
                new Vector2(i, j) * 16f);
        }

        internal void CancelChannel() {
            ChannelKind = KindNone;
            ChannelTarget = new Point(-1, -1);
            ChannelTimer = 0;
        }

        public override void OnEnterWorld() => CancelChannel();

        public override void PostUpdate() {
            if (ChannelKind == KindNone) {
                lastImmuneTime = Player.immuneTime;
                return;
            }
            if (!OldNetWorld.Active || Player.whoAmI != Main.myPlayer || Player.dead) {
                CancelChannel();
                return;
            }
            bool hurt = Player.immuneTime > lastImmuneTime;
            lastImmuneTime = Player.immuneTime;
            Vector2 c = new(ChannelTarget.X * 16 + 8, ChannelTarget.Y * 16 + 8);
            if (!Main.mouseRight || hurt
                || Player.velocity.Length() > OldNetMetrics.MineArmSpeedGate
                || Vector2.Distance(Player.Center, c) > OldNetMetrics.EncryptChannelRadius) {
                CancelChannel();
                return;
            }
            //目标装置还在才推进
            Tile tile = Framing.GetTileSafely(ChannelTarget.X, ChannelTarget.Y);
            int wantType = ChannelKind == KindCutTripwire
                ? ModContent.TileType<Tiles.OldNetTripwireTile>()
                : ModContent.TileType<Tiles.OldNetSentryMineTile>();
            if (!tile.HasTile || tile.TileType != wantType) {
                CancelChannel();
                return;
            }

            ChannelTimer++;
            if (ChannelKind == KindCutTripwire && ChannelTimer >= OldNetMetrics.TripwireCutTicks) {
                Point target = ChannelTarget;
                CancelChannel();
                OldNetThreatField.CompleteCut(target, Player);
            }
            else if (ChannelKind == KindDefuseMine && ChannelTimer >= OldNetMetrics.MineDefuseTicks) {
                Point target = ChannelTarget;
                CancelChannel();
                OldNetThreatField.CompleteDefuse(target, Player);
            }
        }
    }
}
