using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 恶犬导演（P2 计划书 S4）：潮位犬势（涨潮犬进村、退潮犬归湖）+ 梦压行为加压 +
    /// 视野外雾中选点生成 + 补员与哀鸣抚恤 + 双犬合围仲裁。<br/>
    /// 生成权威纪律（镜像 OldNetICEDirector）：isClient 早退、20t 巡检、
    /// NewNPC 后 isServer 补发 SyncNPC、OnWorldLoad/Unload 会话复位
    /// （ShouldSave=false 每次进梦全新，静态残留=幽灵威胁）。
    /// 全部裁决在权威端，实体经 SyncNPC 过线，无自定义包
    /// </summary>
    internal class KiyumeHoundDirector : ModSystem
    {
        /// <summary>
        /// 梦压 0..100（行为加压）：任何玩家在名义浓度 &gt;0.5 区奔跑/开火累积、自然衰减；
        /// ≥PackGate 时目标犬数 +1 并解锁双犬合围。一场梦一份压，非 per-player，
        /// 真·世界级状态且只在权威端写，static 合法；会话复位见 ResetSession
        /// </summary>
        internal static float DreamHeat;

        private int checkTimer;
        /// <summary>入梦以来的权威 tick（首入宽限用）</summary>
        private long sessionTicks;
        /// <summary>选点全败的巡检计数（fail quiet：整 50 次才记一行日志）</summary>
        private int spawnFailCount;
        /// <summary>上次巡检潮位（哀鸣抚恤的 0.5 上穿沿检测）</summary>
        private float prevTide = 1f;
        /// <summary>双犬合围：当前绕后犬槽位（-1=无，每次巡检重新仲裁，权威端本地字段）</summary>
        private int flankWho = -1;
        /// <summary>开火沿检测：各玩家上帧 itemAnimation（按槽位索引的会话数组，非 static 标量）</summary>
        private readonly int[] prevItemAnim = new int[Main.maxPlayers];

        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        private void ResetSession() {
            DreamHeat = 0f;
            checkTimer = 0;
            sessionTicks = 0;
            spawnFailCount = 0;
            //进梦潮汐从涨满起步（KiyumeFogTide.Reset 同源），上穿沿基准随之取 1
            prevTide = 1f;
            flankWho = -1;
            Array.Clear(prevItemAnim);
        }

        public override void PostUpdateNPCs() {
            //生成权威：客户端不做任何裁决（实体乘 SyncNPC 过线）
            if (VaultUtils.isClient || !KiyumeWorld.Active) {
                return;
            }
            sessionTicks++;
            TickDreamHeat();
            SteerFlanker();
            if (++checkTimer < KiyumeHoundMetrics.DirectorCheckTicks) {
                return;
            }
            checkTimer = 0;
            Inspect();
        }

        //==================== 梦压（每 tick，权威端） ====================

        //吵闹的梦更凶：浓雾区奔跑/开火累积，静下来自然消散
        private void TickDreamHeat() {
            float gain = 0f;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                //名义浓度口径（确定性、绕开调试倍率），浓雾区外的响动不入账
                if (KiyumeStealthSense.FogConcealmentAt(player.Center)
                    <= KiyumeHoundMetrics.DreamHeatFogGate) {
                    prevItemAnim[player.whoAmI] = player.itemAnimation;
                    continue;
                }
                if (player.velocity.Length() >= KiyumeHoundMetrics.RunSpeedGate) {
                    gain += KiyumeHoundMetrics.DreamHeatRunGain;
                }
                //开火沿：与 KiyumeStealthPlayer.FirePulse 同判据，导演独立记沿不借缓存
                if (player.itemAnimation > prevItemAnim[player.whoAmI]
                    && player.HeldItem.damage > 0) {
                    gain += KiyumeHoundMetrics.DreamHeatFireGain;
                }
                prevItemAnim[player.whoAmI] = player.itemAnimation;
            }
            DreamHeat = MathHelper.Clamp(
                DreamHeat + gain - KiyumeHoundMetrics.DreamHeatDecay, 0f, 100f);
        }

        //==================== 巡检（20t，权威端） ====================

        private void Inspect() {
            float tide = KiyumeFogTide.Tide;
            //哀鸣抚恤解除：潮位过 0.5 上穿沿恢复补员（旗标由 KiyumeHound.OnKill 置位）
            if (prevTide < KiyumeHoundMetrics.RecruitHoldReleaseTide
                && tide >= KiyumeHoundMetrics.RecruitHoldReleaseTide) {
                KiyumeHound.RecruitHoldUntilTideRise = false;
                //望乡庇护到期：山神的狗只护一潮（旗标由 KiyumeWhiteHound 化雾时登记）
                KiyumeWhiteHound.HomewardGraceUntilTideRise = false;
            }
            prevTide = tide;

            //──泵挂点：白毛望乡犬──（27000t 冷却 + 1/3 抽签，全场至多 1，泵体全住 KiyumeWhiteHound）
            KiyumeWhiteHound.DirectorPump();

            ArbitrateFlank();

            //盘点：committed 不含正在化雾离场的（它们已承诺退场，不占编制）
            int total = 0;
            int committed = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is not KiyumeHound) {
                    continue;
                }
                total++;
                if ((int)npc.ai[0] != KiyumeHound.StateFade) {
                    committed++;
                }
            }

            bool lowTide = tide < KiyumeHoundMetrics.TideLowGate;
            int target = tide >= KiyumeHoundMetrics.TideHighGate ? KiyumeHoundMetrics.TargetCountHigh
                : lowTide ? KiyumeHoundMetrics.TargetCountLow : KiyumeHoundMetrics.TargetCountMid;
            if (DreamHeat >= KiyumeHoundMetrics.PackGate) {
                target++;
            }
            target = Math.Min(target, KiyumeHoundMetrics.MaxAlive);
            //望乡庇护消费（点子 11）：见过白犬的当夜犬群不出——目标压 0 即止补员，
            //场上恶犬走下方裁员渐次化雾（追咬中的按裁员纪律打完这口才走）
            if (KiyumeWhiteHound.HomewardGraceUntilTideRise) {
                target = 0;
            }

            //裁员优先：潮位回落超编，或低潮滞留残留带外，渐次化雾（每巡检至多一条）
            if (DismissOne(committed, target, lowTide)) {
                return;
            }
            //补员：首入宽限期与哀鸣抚恤期不补，硬上限含化雾中的
            if (committed >= target || total >= KiyumeHoundMetrics.MaxAlive
                || sessionTicks < KiyumeHoundMetrics.EntryGraceTicks
                || KiyumeHound.RecruitHoldUntilTideRise) {
                return;
            }
            TrySpawnOne(lowTide);
        }

        //==================== 裁员（外写 ai 的 DismissHunters 同款手法） ====================

        //只裁隐蔽态（追咬中途消失既出戏又亏玩家）；越界者优先，其次离玩家最远的无声退场
        private bool DismissOne(int committed, int target, bool lowTide) {
            NPC pick = null;
            float pickScore = -1f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.ModNPC is not KiyumeHound) {
                    continue;
                }
                int state = (int)npc.ai[0];
                if (state is not (KiyumeHound.StateEmerge or KiyumeHound.StatePatrol
                    or KiyumeHound.StateAlert or KiyumeHound.StateSearch)) {
                    continue;
                }
                bool outOfBand = lowTide
                    && (npc.Center.X < KiyumeHoundMetrics.LowTideBandLeftCol * 16f
                        || npc.Center.X >= KiyumeHoundMetrics.LowTideBandRightCol * 16f);
                if (committed <= target && !outOfBand) {
                    continue;
                }
                float score = (outOfBand ? 100000f : 0f) + NearestPlayerDist(npc.Center);
                if (score > pickScore) {
                    pickScore = score;
                    pick = npc;
                }
            }
            if (pick == null) {
                return false;
            }
            //转 Fade：镜像 KiyumeHound.EnterFade 的字段写法（状态+计时清零+过线）
            pick.ai[0] = KiyumeHound.StateFade;
            pick.ai[1] = 0f;
            pick.netUpdate = true;
            return true;
        }

        //==================== 生成（视野外、雾中、可站，西偏） ====================

        private void TrySpawnOne(bool lowTide) {
            for (int attempt = 0; attempt < KiyumeHoundMetrics.SpawnAttempts; attempt++) {
                if (TrySpawnAt(RollSpawnCol(lowTide))) {
                    return;
                }
            }
            //fail quiet：浓度门+距离门叠加在部分潮相下会反复失败，静默跳过下轮再试
            if (++spawnFailCount % 50 == 0) {
                CWRMod.Instance.Logger.Info(
                    $"[Kiyume] hound spawn starved x{spawnFailCount}"
                    + $" tide={KiyumeFogTide.Tide:F2} heat={(int)DreamHeat}");
            }
        }

        //候选列：低潮只在残留带（滩涂+村西）；平时锚定随机玩家按带距摇点、西偏 65%
        private int RollSpawnCol(bool lowTide) {
            if (lowTide) {
                return Main.rand.Next(
                    KiyumeHoundMetrics.LowTideBandLeftCol, KiyumeHoundMetrics.LowTideBandRightCol);
            }
            Player anchor = null;
            int alive = 0;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                //水塘抽样：等概率取一名活人做锚
                if (Main.rand.Next(++alive) == 0) {
                    anchor = player;
                }
            }
            if (anchor == null) {
                return -1;
            }
            int side = Main.rand.NextFloat() < KiyumeHoundMetrics.WestBias ? -1 : 1;
            float dist = Main.rand.NextFloat(
                KiyumeHoundMetrics.SpawnBandMinPx, KiyumeHoundMetrics.SpawnBandMaxPx);
            return (int)((anchor.Center.X + side * dist) / 16f);
        }

        private bool TrySpawnAt(int col) {
            //西不入湖（滩涂西缘）、东不出图
            if (col < KiyumeHoundMetrics.LowTideBandLeftCol + 4 || col > KiyumeMetrics.Width - 40) {
                return false;
            }
            int floorRow = KiyumePlans.FloorTopAt(col);
            if (floorRow < 60 || floorRow > Main.maxTilesY - 60) {
                return false;
            }
            //探地可站：体格 64×34（4 列×3 行）净空，脚下不踩水
            for (int dx = -2; dx <= 1; dx++) {
                for (int dy = 1; dy <= 3; dy++) {
                    Tile tile = Framing.GetTileSafely(col + dx, floorRow - dy);
                    if (tile.HasTile && Main.tileSolid[tile.TileType]
                        && !Main.tileSolidTop[tile.TileType]) {
                        return false;
                    }
                }
            }
            //支撑核验：FloorTop 是生成期规划，井筒/地窖等后置开凿会把该行挖空。
            //犬掉进竖井出不来=永久占编制（CheckActive=false 不自灭），脚下两列必须仍是实心
            for (int dx = -1; dx <= 0; dx++) {
                Tile support = Framing.GetTileSafely(col + dx, floorRow);
                if (!support.HasTile || !Main.tileSolid[support.TileType]
                    || Main.tileSolidTop[support.TileType]) {
                    return false;
                }
            }
            if (Framing.GetTileSafely(col, floorRow - 1).LiquidAmount > 0) {
                return false;
            }
            var bottom = new Vector2(col * 16f + 8f, floorRow * 16f);
            Vector2 body = bottom - new Vector2(0f, 17f);
            //视野外：距所有活人 >1200px
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && !player.ghost
                    && Vector2.Distance(player.Center, body) < KiyumeHoundMetrics.SpawnMinDistPx) {
                    return false;
                }
            }
            //犬从雾里来：名义浓度门（含贴地残雾项，退潮滩涂靠残雾也够门）
            if (KiyumeStealthSense.FogConcealmentAt(body) < KiyumeHoundMetrics.SpawnFogGate) {
                return false;
            }
            //ai3=巡逻锚 X；NewNPC 的 Y 是脚底（上游源已核：position.Y = Y - height）
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)bottom.X, (int)bottom.Y,
                ModContent.NPCType<KiyumeHound>(), ai3: bottom.X);
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
            spawnFailCount = 0;
            CWRMod.Instance.Logger.Info(
                $"[Kiyume] hound spawned col={col} tide={KiyumeFogTide.Tide:F2} heat={(int)DreamHeat}");
            return true;
        }

        //==================== 双犬合围（梦压解锁，权威端） ====================

        //仲裁：两犬同追一人时，后进入 Chase 者（状态计时最小）领绕后位；
        //只指派一条，正面永远有一条可读主威胁
        private void ArbitrateFlank() {
            flankWho = -1;
            if (DreamHeat < KiyumeHoundMetrics.PackGate) {
                return;
            }
            NPC late = null;
            foreach (NPC a in Main.ActiveNPCs) {
                if (a.ModNPC is not KiyumeHound || (int)a.ai[0] != KiyumeHound.StateChase) {
                    continue;
                }
                foreach (NPC b in Main.ActiveNPCs) {
                    if (b.whoAmI == a.whoAmI || b.ModNPC is not KiyumeHound
                        || (int)b.ai[0] != KiyumeHound.StateChase || b.target != a.target) {
                        continue;
                    }
                    //同追对成立：计时小的是后进入者（平手取槽位大的，两端同判无碍——本字段仅权威端用）
                    NPC candidate = a.ai[1] < b.ai[1] || (a.ai[1] == b.ai[1] && a.whoAmI > b.whoAmI)
                        ? a : b;
                    if (late == null || candidate.ai[1] < late.ai[1]) {
                        late = candidate;
                    }
                }
            }
            if (late != null) {
                flankWho = late.whoAmI;
            }
        }

        //绕后转向（每帧，权威端）：目的地=玩家背侧 240px，钳到玩家可见雾外；
        //近点松油门吊成堵截位，扑咬仍由犬自身的贴身触发接管，导演不碰其状态机。
        //联机客户端在同步间隙按本体 AI 直追模拟，偏差由犬的 24t 同步起搏器校正；
        //绕后点被钳在玩家视雾之外，回弹不进玩家视野，可接受
        private void SteerFlanker() {
            if (flankWho < 0) {
                return;
            }
            NPC npc = Main.npc[flankWho];
            if (!npc.active || npc.ModNPC is not KiyumeHound
                || (int)npc.ai[0] != KiyumeHound.StateChase
                || npc.ai[1] <= KiyumeHoundMetrics.ChaseWindupTicks) {
                //蓄力前摇不夺权（读帧不能破）；状态破格即弃权等下轮仲裁
                return;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                return;
            }
            Player prey = Main.player[npc.target];
            if (prey == null || !prey.active || prey.dead || prey.ghost) {
                return;
            }
            int back = -prey.direction;
            float flankX = prey.Center.X + back * KiyumeHoundMetrics.FlankBehindPx;
            //钳制：绕后点必须藏在玩家看不穿的雾里，不够浓就再退
            for (int i = 0; i < KiyumeHoundMetrics.FlankFogScanSteps; i++) {
                if (KiyumeStealthSense.FogConcealmentAt(new Vector2(flankX, prey.Center.Y))
                    >= KiyumeHoundMetrics.SpawnFogGate) {
                    break;
                }
                flankX += back * KiyumeHoundMetrics.FlankFogStepPx;
            }
            flankX = MathHelper.Clamp(flankX,
                KiyumeHoundMetrics.LowTideBandLeftCol * 16f, (KiyumeMetrics.Width - 20) * 16f);
            float dx = flankX - npc.Center.X;
            if (MathF.Abs(dx) > KiyumeHoundMetrics.FlankHoldSlackPx) {
                //本体 WalkTowards 同式（0.18 lerp），只换目的地
                npc.velocity.X = MathHelper.Lerp(npc.velocity.X,
                    MathF.Sign(dx) * KiyumeHoundMetrics.ChaseSpeed, 0.18f);
            }
            else {
                //驻停堵口：与本体追击拉力叠成绕后环上的小幅踱步，读成雾里来回逡巡
                npc.velocity.X *= 0.8f;
            }
        }

        private static float NearestPlayerDist(Vector2 from) {
            float best = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                best = Math.Min(best, Vector2.Distance(player.Center, from));
            }
            return best;
        }
    }
}
