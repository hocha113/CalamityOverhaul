using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
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
    /// 旧网 ICE 生成权威（镜像 GaolBossRoomWatcher 的 watcher 形态）：
    /// 深潜开始一次性布巡逻，噪音档位跃迁触发猎杀响应，T4 清剿波周期补员。
    /// 会话状态随 OnWorldLoad 复位（ShouldSave=false 每次深潜全新，静态残留=幽灵威胁）。
    /// M1 单人：本机玩家即威胁源；MP 化时按 per-player 档位重排 TODO
    /// </summary>
    internal class OldNetICEDirector : ModSystem
    {
        /// <summary>巡检间隔（tick）</summary>
        private const int CheckInterval = 20;

        private int checkTimer;
        private bool patrolsSeeded;
        private bool turretsSeeded;
        private bool lurkersSeeded;
        private int lastTier;
        private bool cleanupWave;
        private int reinforceTimer;
        //灯蛾标记体：T2+ 维持场上 ≥1 的补员冷却（03 猎杀敌人包）
        private int taggerRespawnTimer;
        //循迹猎犬：T1 跃迁后的延迟派遣倒数（灯蛾先到、猎犬后至）与 T2+ 阵亡补员
        private int tracerSpawnDelay;
        private int tracerRespawnTimer;
        private bool tracerDispatched;
        //回收官升格：清剿波持续计时 / 每潜一次的派遣旗标 / 广播到入场的延迟倒数
        private int t4SustainTicks;
        private bool wardenDispatched;
        private int wardenSpawnDelay;
        //收网协议（06 P6）：清剿波在场时长累计器（累计口径：跨波不清，区别于
        //t4SustainTicks 的单波口径）/ 收网旗标（触发后直到弹出不复位）/ 75% 预告去重
        private int t4AccumTicks;
        private bool dragnetActive;
        private bool dragnetWarned;
        //回收官静默余量的上帧状态（升沿=击杀奖励兑现点，全场猎杀者离场）
        private bool graceWasActive;
        //一次性教学提示旗标（会话内去重；TODO MP: per-player 化）
        private readonly bool[] hintShown = new bool[3];

        //提示 id（TryMarkHintOnce 的槽位）
        internal const int HintTaggerAttached = 0;
        internal const int HintTracerHowl = 1;
        internal const int HintTracerConfused = 2;

        /// <summary>封锁区闸门坐标登记（生成期写入，事件节点拉闸时解封；M1b）</summary>
        internal static readonly List<Point> SealGates = [];

        public static OldNetICEDirector Instance => ModContent.GetInstance<OldNetICEDirector>();

        /// <summary>清剿波进行中：全 ICE 全图感知、潜行失效</summary>
        public static bool CleanupWaveActive => Instance?.cleanupWave ?? false;

        /// <summary>
        /// 收网协议激活（06 P6 不可逆棘轮）：噪音衰减地板 70、清剿波永不解除、
        /// 补员目标 +2、加密引导双速。消费点：OldNetPlayer 衰减钳制与 ChannelStepNow、
        /// OldNetHud 徽记、OldNetRating 风格判定。TODO MP: 收网状态需广播
        /// </summary>
        public static bool DragnetActive => Instance?.dragnetActive ?? false;

        /// <summary>场上猎杀者数（不含正在离场的），HUD 被追指示用</summary>
        public static int ActiveHunterCount {
            get {
                int type = ModContent.NPCType<OldNetBlackICE>();
                int count = 0;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.type == type && (int)npc.ai[0] == 0) {
                        count++;
                    }
                }
                return count;
            }
        }

        private void ResetSession() {
            checkTimer = 0;
            patrolsSeeded = false;
            turretsSeeded = false;
            lurkersSeeded = false;
            lastTier = 0;
            cleanupWave = false;
            reinforceTimer = 0;
            taggerRespawnTimer = OldNetMetrics.TaggerRespawnTicks;
            tracerSpawnDelay = 0;
            tracerRespawnTimer = OldNetMetrics.TracerRespawnTicks;
            tracerDispatched = false;
            t4SustainTicks = 0;
            wardenDispatched = false;
            wardenSpawnDelay = 0;
            t4AccumTicks = 0;
            dragnetActive = false;
            dragnetWarned = false;
            graceWasActive = false;
            Array.Clear(hintShown);
        }

        //回收官在场或在途：清剿收束为单体处决，猎杀小队全面让位
        private bool WardenEngaged => wardenSpawnDelay > 0
            || CountActive(ModContent.NPCType<OldNetWardenICE>()) > 0;

        //清剿波补员目标：收网期加压 +2（SpawnHuntSquad 上限与周期补员共用）
        private int SustainTarget => OldNetMetrics.T4SustainCount
            + (dragnetActive ? OldNetMetrics.DragnetSustainBonus : 0);

        /// <summary>
        /// 一次性教学提示去重：首次调用返回 true，会话内同 id 后续调用 false。
        /// 深潜会话随 ResetSession 复位。TODO MP: per-player 化
        /// </summary>
        internal static bool TryMarkHintOnce(int hintId) {
            OldNetICEDirector inst = Instance;
            if (inst == null || hintId < 0 || hintId >= inst.hintShown.Length
                || inst.hintShown[hintId]) {
                return false;
            }
            inst.hintShown[hintId] = true;
            return true;
        }

        //统计场上某类 ICE 的活跃数
        private static int CountActive(int type) {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type) {
                    count++;
                }
            }
            return count;
        }

        //闸门表不能在这里清：生成 pass 先于 OnWorldLoad 运行，登记发生在生成期，
        //由 pass 开头自清（每次深潜重生成即重登记）
        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        public override void PostUpdateNPCs() {
            //生成权威：客户端不做任何裁决（实体乘 SyncNPC 过线）
            if (VaultUtils.isClient || !OldNetWorld.Active) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            if (!patrolsSeeded) {
                patrolsSeeded = true;
                SeedPatrols();
            }
            if (!turretsSeeded) {
                turretsSeeded = true;
                SeedTurrets();
            }
            if (!lurkersSeeded) {
                lurkersSeeded = true;
                SeedLurkers();
            }

            Player player = ResolveThreatTarget();
            if (player == null) {
                return;
            }
            OldNetPlayer session = OldNetPlayer.Get(player);
            int tier = session.NoiseTier;

            //回收官全网静默兑现（二审裁定）：静默余量起始拍全场猎杀者离场，
            //余量期内清剿波补员冻结（见下方补员门控）；余量结束后按既有逻辑自然回场。
            //收网态的地板 70 与清剿波永久化不受此影响
            bool graceActive = session.WardenGraceTicks > session.DiveTicks;
            if (graceActive && !graceWasActive) {
                DismissHunters();
            }
            graceWasActive = graceActive;

            //档位跃迁响应：T1 标记者 / T2 猎杀小队 / T3 精英 / T4 清剿波
            //T1 空档填充（03 猎杀敌人包）：灯蛾先到，猎犬延迟后至；
            //按存量补差防迟滞区反复跨档无限堆蛾
            if (tier >= 1 && lastTier < 1) {
                int taggerLack = 2 - CountActive(ModContent.NPCType<OldNetTaggerICE>());
                if (taggerLack > 0) {
                    SpawnTaggers(player, taggerLack);
                }
                if (!tracerDispatched && tracerSpawnDelay <= 0) {
                    tracerSpawnDelay = OldNetMetrics.TracerSpawnDelayTicks;
                }
            }
            if (tier >= 2 && lastTier < 2) {
                SpawnHuntSquad(player, 2, elite: false);
            }
            if (tier >= 3 && lastTier < 3) {
                SpawnHuntSquad(player, 1, elite: true);
            }
            if (tier >= 4 && lastTier < 4) {
                cleanupWave = true;
                //入档立即补一次员，之后按周期
                reinforceTimer = OldNetMetrics.T4ReinforceTicks;
            }
            lastTier = tier;

            //清剿波：补员至场上 N 只，直至噪音冷却到释放线以下（收网后永不解除）
            if (cleanupWave) {
                if (!dragnetActive && session.Noise < OldNetMetrics.T4ReleaseBelow) {
                    cleanupWave = false;
                    t4SustainTicks = 0;
                }
                else {
                    //回收官升格：清剿波持续 45s 且本潜未派遣 → 终极账单
                    t4SustainTicks += CheckInterval;
                    if (!wardenDispatched && t4SustainTicks >= OldNetMetrics.WardenEscalateTicks) {
                        DispatchWarden(player);
                    }
                    //收网协议：赖在清剿波里的时长换不可逆升级
                    TickDragnet(player);
                    //静默余量期补员冻结（计时一并冻住，余量结束后按剩余周期渐进回场，不瞬间灌满）
                    if (!graceActive) {
                        reinforceTimer += CheckInterval;
                        if (reinforceTimer >= OldNetMetrics.T4ReinforceTicks) {
                            reinforceTimer = 0;
                            int lack = SustainTarget - ActiveHunterCount;
                            if (lack > 0) {
                                SpawnHuntSquad(player, lack, elite: false);
                            }
                        }
                    }
                }
            }

            //回收官入场延迟泵：派遣广播 3s 后本体自黑墙剥离（派遣既成，波是否解除都会入场）
            if (wardenSpawnDelay > 0) {
                wardenSpawnDelay -= CheckInterval;
                if (wardenSpawnDelay <= 0) {
                    SpawnWarden(player);
                }
            }

            //──── 03 扩展敌人巡检（灯蛾/猎犬维持）────
            //T2+ 维持场上 ≥1 只灯蛾：阵亡后按冷却补 1（热度锁不缺岗）
            if (tier >= 2 && CountActive(ModContent.NPCType<OldNetTaggerICE>()) == 0) {
                taggerRespawnTimer -= CheckInterval;
                if (taggerRespawnTimer <= 0) {
                    taggerRespawnTimer = OldNetMetrics.TaggerRespawnTicks;
                    SpawnTaggers(player, 1);
                }
            }
            else {
                taggerRespawnTimer = OldNetMetrics.TaggerRespawnTicks;
            }

            //循迹猎犬：T1 延迟派遣泵（灯蛾先到、猎犬后至的分批入场）
            if (tracerSpawnDelay > 0) {
                tracerSpawnDelay -= CheckInterval;
                if (tracerSpawnDelay <= 0) {
                    tracerDispatched = true;
                    SpawnTracer(player);
                }
            }
            //循迹猎犬：T2+ 阵亡后按冷却补 1
            else if (tier >= 2 && tracerDispatched
                && CountActive(ModContent.NPCType<OldNetTracerICE>()) == 0) {
                tracerRespawnTimer -= CheckInterval;
                if (tracerRespawnTimer <= 0) {
                    tracerRespawnTimer = OldNetMetrics.TracerRespawnTicks;
                    SpawnTracer(player);
                }
            }
            else {
                tracerRespawnTimer = OldNetMetrics.TracerRespawnTicks;
            }
        }

        //M1 单人：本机玩家；服务器兜底取首个活人
        private static Player ResolveThreatTarget() {
            if (!Main.dedServ && Main.LocalPlayer?.active == true && !Main.LocalPlayer.dead) {
                return Main.LocalPlayer;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead) {
                    return player;
                }
            }
            return null;
        }

        /// <summary>
        /// 拉闸解封：移除全图登记的封锁闸门（事件节点右键调用，本机删格）。
        /// MP 化时与节点右键一起过 SendTileSquare 账 TODO
        /// </summary>
        internal static void UnsealAll() {
            int gateType = ModContent.TileType<Tiles.OldNetSealGateTile>();
            foreach (Point gate in SealGates) {
                Tile tile = Framing.GetTileSafely(gate.X, gate.Y);
                if (!tile.HasTile || tile.TileType != gateType) {
                    continue;
                }
                WorldGen.KillTile(gate.X, gate.Y, noItem: true);
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendTileSquare(-1, gate.X, gate.Y, 1);
                }
            }
            SealGates.Clear();
        }

        //──── 收网协议（06 P6）：清剿波累计在场 50s → 棘轮闭合，一局进入终章音色 ────
        //干净应对（立刻收声的单次 T4 约 43s）不触发；赖着不走或二进宫才触发。
        //与回收官共存：wardenDispatched 每潜一次，收网让 t4SustainTicks 无限累计也不会重复派遣

        private void TickDragnet(Player player) {
            if (dragnetActive) {
                return;
            }
            t4AccumTicks += CheckInterval;
            //75% 预告：网开始收束（横幅+低鸣双通道）
            if (!dragnetWarned && t4AccumTicks >= OldNetMetrics.DragnetAfterT4Ticks * 3 / 4) {
                dragnetWarned = true;
                if (!Main.dedServ) {
                    UI.OldNetHud.PushBanner(OldNetTexts.OldNetDragnetWarn.Value);
                    SoundEngine.PlaySound(CWRSound.FaultTransition with { Volume = 0.6f, Pitch = -0.6f },
                        player.Center);
                }
            }
            if (t4AccumTicks < OldNetMetrics.DragnetAfterT4Ticks) {
                return;
            }
            //触发：本潜不再复位。效果消费点=玩家侧衰减钳制/引导步进、本类补员目标与解除判定、HUD 徽记
            dragnetActive = true;
            if (!Main.dedServ) {
                UI.OldNetHud.PushBanner(OldNetTexts.OldNetDragnetOn.Value);
                SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.85f, Pitch = -0.5f },
                    player.Center);
            }
            CWRMod.Instance.Logger.Info($"[OldNet] dragnet engaged, t4Accum={t4AccumTicks}");
        }

        //全网静默兑现：场上猎杀态黑冰全体转离场（DispatchWarden 让位同款手法），
        //让"全网为你静默 60 秒"的承诺在行为层为真（收网态下尤其：波不解除但人先撤）
        private static void DismissHunters() {
            int hunterType = ModContent.NPCType<OldNetBlackICE>();
            int dismissed = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == hunterType && (int)npc.ai[0] == 0) {
                    npc.ai[0] = 1f;
                    npc.damage = 0;
                    npc.netUpdate = true;
                    dismissed++;
                }
            }
            if (dismissed > 0) {
                CWRMod.Instance.Logger.Info($"[OldNet] silence grace: {dismissed} hunters dismissed");
            }
        }

        /// <summary>
        /// 目击汇点：立即触发一次 T2 响应（已在 T2+ 则补员 1 只）。
        /// 巡逻/哨眼/猎犬在权威端调用；余震与热断链的自招响应传
        /// <paramref name="countAsSpotted"/>=false 豁免评级目击计数（幽灵潜行不该被系统回礼取消资格）
        /// </summary>
        internal static void NotifySpotted(Player player, bool countAsSpotted = true) {
            OldNetICEDirector inst = Instance;
            if (inst == null || VaultUtils.isClient || !OldNetWorld.Active || player == null) {
                return;
            }
            //评级埋点（2.1）：目击唯一汇点，计数先于补员上限判定（被看见就是被看见）
            if (countAsSpotted) {
                OldNetPlayer.Get(player).SpottedCount++;
            }
            int count = inst.lastTier >= 2 || inst.cleanupWave ? 1 : 2;
            inst.SpawnHuntSquad(player, count, elite: false);
        }

        //──── 猎杀小队生成：从墙的方向来 ────

        private void SpawnHuntSquad(Player player, int count, bool elite) {
            //回收官在场：群狼让位，处决是一场单挑（NotifySpotted 调用面签名不变）
            if (WardenEngaged) {
                return;
            }
            //上限对齐清剿波补员目标（收网期 +2），防目击连报堆一屏
            int room = SustainTarget - ActiveHunterCount;
            count = Math.Min(count, room);
            if (count <= 0) {
                return;
            }

            int type = ModContent.NPCType<OldNetBlackICE>();
            int spawnX = OldNetMetrics.HunterSpawnCol * 16;
            float minY = (OldNetMetrics.BorderThick + 5) * 16f;
            float maxY = (OldNetMetrics.FloorRow - 3) * 16f;

            for (int i = 0; i < count; i++) {
                int spawnY = (int)MathHelper.Clamp(
                    player.Center.Y + Main.rand.NextFloat(-90f, 90f), minY, maxY);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), spawnX, spawnY, type,
                    ai3: elite ? 1f : 0f);
                if (idx >= 0 && idx < Main.maxNPCs && VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }

            //被追次数：每次响应事件 +1（战报统计）
            OldNetPlayer.Get(player).HuntedCount++;

            //派遣提示音：玩家耳边低鸣，威胁上线的听觉阀
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultTransition with { Volume = 0.45f, Pitch = -0.4f },
                    player.Center);
            }
            CWRMod.Instance.Logger.Info(
                $"[OldNet] hunt squad spawned count={count} elite={elite} tier={lastTier} wave={cleanupWave}");
        }

        //──── 灯蛾标记体派遣：墙侧飞来的热度锁（03 猎杀敌人包）────

        private void SpawnTaggers(Player player, int count) {
            int type = ModContent.NPCType<OldNetTaggerICE>();
            int spawnX = OldNetMetrics.HunterSpawnCol * 16;
            float minY = (OldNetMetrics.BorderThick + 5) * 16f;
            float maxY = (OldNetMetrics.FloorRow - 3) * 16f;
            for (int i = 0; i < count; i++) {
                int spawnY = (int)MathHelper.Clamp(
                    player.Center.Y + Main.rand.NextFloat(-120f, 40f), minY, maxY);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), spawnX, spawnY, type);
                if (idx >= 0 && idx < Main.maxNPCs && VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] tagger ICE spawned count={count}");
        }

        //──── 循迹猎犬派遣：空投在玩家西侧地表（"从墙的方向来"惯例）────

        private void SpawnTracer(Player player) {
            int type = ModContent.NPCType<OldNetTracerICE>();
            int col = (int)((player.Center.X - OldNetMetrics.TracerSpawnWestPx) / 16f);
            col = Math.Max(col, OldNetMetrics.WallCols + 6);
            int surfaceRow = ProbeSurfaceRow(col);
            int spawnY = surfaceRow > 0
                ? surfaceRow * 16 - 40
                : (int)player.Center.Y;
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), col * 16 + 8, spawnY, type);
            if (idx >= 0 && idx < Main.maxNPCs && VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
            OldNetPlayer.Get(player).HuntedCount++;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultTransition with { Volume = 0.4f, Pitch = -0.2f },
                    player.Center);
            }
            CWRMod.Instance.Logger.Info($"[OldNet] tracer ICE spawned col={col}");
        }

        //──── 回收官升格派遣：T4 赖场的终极账单（03 猎杀敌人包，每潜一次）────

        private void DispatchWarden(Player player) {
            wardenDispatched = true;
            wardenSpawnDelay = OldNetMetrics.WardenSpawnDelayTicks;
            //群狼给王让位：全场猎杀态黑冰转离场（ai0=1 即 BlackICE 的 StateLeave）
            int hunterType = ModContent.NPCType<OldNetBlackICE>();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == hunterType && (int)npc.ai[0] == 0) {
                    npc.ai[0] = 1f;
                    npc.damage = 0;
                    npc.netUpdate = true;
                }
            }
            //被追次数：升格派遣也是一次响应事件（战报统计）
            OldNetPlayer.Get(player).HuntedCount++;
            //派遣广播：3s 读秒给玩家收枪的时间（入场 glitch 尖峰已改在回收官 NPC 入场态实现）
            //TODO MP: 广播文案 per-player 化
            if (player.whoAmI == Main.myPlayer) {
                CombatText.NewText(player.getRect(), new Color(235, 64, 44),
                    OldNetTexts.WardenDispatch.Value, dramatic: true);
            }
            if (!Main.dedServ) {
                SoundEngine.PlaySound(CWRSound.FaultTransition with { Volume = 0.8f, Pitch = -0.7f },
                    player.Center);
            }
            CWRMod.Instance.Logger.Info(
                $"[OldNet] warden dispatched, t4Sustain={t4SustainTicks}");
        }

        private void SpawnWarden(Player player) {
            int type = ModContent.NPCType<OldNetWardenICE>();
            int spawnX = (OldNetMetrics.HunterSpawnCol + 8) * 16;
            float minY = (OldNetMetrics.BorderThick + 8) * 16f;
            float maxY = (OldNetMetrics.FloorRow - 5) * 16f;
            int spawnY = (int)MathHelper.Clamp(player.Center.Y, minY, maxY);
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), spawnX, spawnY, type);
            if (idx >= 0 && idx < Main.maxNPCs && VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
            CWRMod.Instance.Logger.Info("[OldNet] warden ICE spawned");
        }

        //──── 巡逻布防：深潜开始一次性铺设 ────

        private void SeedPatrols() {
            int type = ModContent.NPCType<OldNetPatrolICE>();
            int fadeLeft = OldNetMetrics.WallCols + OldNetMetrics.FootCols + OldNetMetrics.RuinCols;
            int minX = OldNetMetrics.WallCols + OldNetMetrics.SpawnFlatCols + 150;
            int maxX = fadeLeft - 40;
            int placed = 0;

            for (int col = minX; col < maxX; col += OldNetMetrics.PatrolSpacingCols) {
                int x = col + Main.rand.Next(-25, 26);
                int surfaceRow = ProbeSurfaceRow(x);
                if (surfaceRow < 0) {
                    continue;
                }
                int spawnY = (int)(surfaceRow * 16f - OldNetMetrics.PatrolHoverHeight);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), x * 16 + 8, spawnY, type,
                    ai0: x * 16f + 8f, ai1: Main.rand.NextBool() ? 1f : -1f);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    placed++;
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                    }
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] patrol ICE seeded={placed}");
        }

        //──── 哨戒炮塔布防：地下机房吊装（M3 威胁扩容）────
        //消费 gen 期规划态：每次深潜重生成，Plans 与本会话同源（SP/服务器同端）

        private void SeedTurrets() {
            int type = ModContent.NPCType<OldNetTurretICE>();
            int placed = 0;
            foreach (Gen.OldNetBuildContext ctx in new[] { Gen.OldNetPlans.Z1, Gen.OldNetPlans.Z2, Gen.OldNetPlans.Z3 }) {
                if (ctx == null) {
                    continue;
                }
                foreach (Gen.Rooms.OldNetRoomNode room in ctx.Graph.Rooms) {
                    if (room.Role == Gen.Rooms.OldNetRoomRole.Landing) {
                        continue;
                    }
                    //深层机房必装；浅层按概率
                    bool deep = room.FloorTop >= OldNetMetrics.UnderShallowBottom;
                    if (!deep && Main.rand.NextFloat() >= OldNetMetrics.TurretRoomChance) {
                        continue;
                    }
                    int cx = (room.InteriorLeft + room.InteriorRight) / 2;
                    int cy = room.InteriorTop + 1;
                    int idx = NPC.NewNPC(new EntitySource_WorldEvent(),
                        cx * 16 + 8, cy * 16 + 8, type);
                    if (idx >= 0 && idx < Main.maxNPCs) {
                        placed++;
                        if (VaultUtils.isServer) {
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                        }
                    }
                }
            }
            CWRMod.Instance.Logger.Info($"[OldNet] turret ICE seeded={placed}");
        }

        //──── 缢影布防：房间顶三分位吊点 + 露天悬垂面（03 猎杀敌人包）────
        //主源消费 gen 期规划态（SeedTurrets 同款权威端豁免）；备选源读 tile 扫悬垂面

        private void SeedLurkers() {
            int type = ModContent.NPCType<OldNetLurkerICE>();
            int placed = 0;
            List<int> usedCols = [];

            bool TryHang(int x, int airRow) {
                //吊点间距去重（列）
                foreach (int col in usedCols) {
                    if (Math.Abs(col - x) < OldNetMetrics.LurkerSpacingCols) {
                        return false;
                    }
                }
                //丝根 = 吊面底缘（首块空气行的上边）
                float anchorX = x * 16f + 8f;
                float anchorY = airRow * 16f;
                int spawnY = (int)(anchorY + OldNetMetrics.LurkerHangOffset + 11f);
                int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)anchorX, spawnY, type,
                    ai0: anchorX, ai1: anchorY);
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return false;
                }
                usedCols.Add(x);
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                }
                return true;
            }

            //主源：废墟/衰减带地下房间顶（左/右三分位，避开炮塔中央吊装；跳过平台厅）
            foreach (Gen.OldNetBuildContext ctx in new[] { Gen.OldNetPlans.Z2, Gen.OldNetPlans.Z3 }) {
                if (ctx == null) {
                    continue;
                }
                foreach (Gen.Rooms.OldNetRoomNode room in ctx.Graph.Rooms) {
                    if (placed >= OldNetMetrics.LurkerCount) {
                        break;
                    }
                    if (room.Role == Gen.Rooms.OldNetRoomRole.Landing) {
                        continue;
                    }
                    int spanW = room.InteriorRight - room.InteriorLeft;
                    //内膛太窄：三分位会撞中央吊装位
                    if (spanW < 6 || Main.rand.NextFloat() >= OldNetMetrics.LurkerRoomChance) {
                        continue;
                    }
                    int cx = Main.rand.NextBool()
                        ? room.InteriorLeft + spanW / 4
                        : room.InteriorRight - spanW / 4;
                    if (TryHang(cx, room.InteriorTop)) {
                        placed++;
                    }
                }
            }

            //备选源：露天悬垂面（断桥/方舟底面/浮空板下），随机列自上而下扫
            //"实心上方 + ≥N 行空气下方"图形；平台 tileSolid=false 天然排除
            int minX = OldNetMetrics.WallCols + OldNetMetrics.FootCols;
            int maxX = OldNetMetrics.PlayRight - 40;
            for (int attempt = 0; attempt < 240 && placed < OldNetMetrics.LurkerCount; attempt++) {
                int x = Main.rand.Next(minX, maxX);
                int airRow = ProbeOverhangRow(x);
                if (airRow > 0 && TryHang(x, airRow)) {
                    placed++;
                }
            }
            //挂点不足即大声记录（fail loud，退化为少量布防仍可玩）
            CWRMod.Instance.Logger.Info(
                $"[OldNet] lurker ICE seeded={placed}/{OldNetMetrics.LurkerCount}");
        }

        //自高空带下缘向下找悬垂面：实心格且其下连续空气 ≥ LurkerOverhangAirRows，
        //返回首块空气行；找不到给 -1
        private static int ProbeOverhangRow(int x) {
            int airRun = 0;
            int lastSolidRow = -1;
            for (int y = OldNetMetrics.SkyBandBottom; y < OldNetMetrics.FloorRow - 2; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                bool solid = tile.HasTile && Main.tileSolid[tile.TileType];
                if (solid) {
                    lastSolidRow = y;
                    airRun = 0;
                    continue;
                }
                if (lastSolidRow < 0) {
                    continue;
                }
                airRun++;
                if (airRun >= OldNetMetrics.LurkerOverhangAirRows) {
                    return lastSolidRow + 1;
                }
            }
            return -1;
        }

        //从天空向下找该列首块实心，返回行号；找不到给 -1
        private static int ProbeSurfaceRow(int x) {
            for (int y = OldNetMetrics.BorderThick + 4; y < OldNetMetrics.FloorRow + 12; y++) {
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return y;
                }
            }
            return -1;
        }
    }
}
