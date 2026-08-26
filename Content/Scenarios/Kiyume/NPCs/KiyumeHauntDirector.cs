using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 百鬼生成权威（镜像 OldNetICEDirector 的 watcher 形态）：20t 巡检驱动五个泵——
    /// 一次性布防（井手/守田人）与事件泵（提灯翁/夜行列/无面者）。
    /// 泵体由 W2 各敌人包在自己的泵挂点区内锚定填充，本骨架只铺心跳、会话状态与生成纪律。
    /// 生成一律走受控 NewNPC（SpawnYokai），绕开 KiyumeSpawnGate 双闸（那扇闸一字不动）。
    /// 锚点单一真相 = Gen.KiyumeStructures（裁决8，KiyumeHauntAnchors 取消不建）：
    /// 泵在服务器侧直读其列表合法（生成端=服务器），地板线走 Gen.KiyumePlans.FloorTopAt/ProbeGround。
    /// 会话状态随 OnWorldLoad/Unload 复位（ShouldSave=false 每次入梦全新，静态残留=幽灵威胁）
    /// </summary>
    internal class KiyumeHauntDirector : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => KiyumeYokaiGate.Enabled;

        /// <summary>巡检间隔（tick）</summary>
        private const int CheckInterval = 20;

        private int checkTimer;

        //──── 事件泵冷却（tick，巡检粒度递减；初值见 ResetSession，重臂由各泵自己写）────
        private int lanternCooldown;
        private int cortegeCooldown;
        private int facelessCooldown;
        private int ridgeCooldown;

        //──── 一次性布防旗标 ────
        private bool wellsSeeded;
        private bool scarecrowsSeeded;

        //──── W2 消费的会话量（internal：泵体与各怪 NPC 类经 Instance 读写）────
        /// <summary>守田人会话补员池余量（ScarePool 起，耗尽即终局）</summary>
        internal int scarecrowPoolLeft;
        /// <summary>夜行列棺 prop 会话计数（上限 CortegeCoffinSessionCap）</summary>
        internal int coffinsSpawned;

        //井手击杀静默表（服务器侧井表：客户端无需知道，表现上手不再出现即自洽）
        private readonly List<Point> silencedWells = [];

        //一次性教学提示旗标（会话内去重；提示 id 由各泵挂点区自定 const，0..7）
        private readonly bool[] hintShown = new bool[8];

        public static KiyumeHauntDirector Instance => ModContent.GetInstance<KiyumeHauntDirector>();

        private void ResetSession() {
            checkTimer = 0;
            lanternCooldown = KiyumeYokaiMetrics.LanternFirstDelay;
            cortegeCooldown = KiyumeYokaiMetrics.CortegeCooldown;
            //无面者首发取带下限，重臂时由泵在 [Min,Max] 带内随机
            facelessCooldown = KiyumeYokaiMetrics.FacelessCooldownMin;
            ridgeCooldown = KiyumeYokaiMetrics.RidgeFirstDelay;
            wellsSeeded = false;
            scarecrowsSeeded = false;
            scarecrowPoolLeft = KiyumeYokaiMetrics.ScarePool;
            coffinsSpawned = 0;
            silencedWells.Clear();
            Array.Clear(hintShown);
        }

        //锚点表不在这里清：KiyumeStructures 由生成 pass 开头自清
        //（生成 pass 先于 OnWorldLoad 运行，镜像 OldNet SealGates 教训）
        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        /// <summary>
        /// 一次性教学提示去重：首次调用返回 true，会话内同 id 后续调用 false。
        /// 入梦会话随 ResetSession 复位
        /// </summary>
        internal static bool TryMarkHintOnce(int hintId) {
            KiyumeHauntDirector inst = Instance;
            if (inst == null || hintId < 0 || hintId >= inst.hintShown.Length
                || inst.hintShown[hintId]) {
                return false;
            }
            inst.hintShown[hintId] = true;
            return true;
        }

        /// <summary>井手击杀后本井会话内永久静默（服务器侧调用，井口 tile 坐标为键）</summary>
        internal static void MarkWellSilenced(Point wellMouth) {
            KiyumeHauntDirector inst = Instance;
            if (inst != null && !inst.silencedWells.Contains(wellMouth)) {
                inst.silencedWells.Add(wellMouth);
            }
        }

        /// <summary>该井是否已静默（布防与再触发均跳过静默井）</summary>
        internal static bool IsWellSilenced(Point wellMouth)
            => Instance?.silencedWells.Contains(wellMouth) ?? false;

        /// <summary>
        /// 受控生成统一出口：NewNPC 后 isServer 补发 SyncNPC（镜像 OldNetICEDirector 逐处惯例）。
        /// 泵与怪类（悬灯/棺 prop）一律走这里，不得裸调 NewNPC
        /// </summary>
        internal static int SpawnYokai(int type, Vector2 worldPos,
            float ai0 = 0f, float ai1 = 0f, float ai2 = 0f, float ai3 = 0f) {
            int idx = NPC.NewNPC(new EntitySource_WorldEvent(), (int)worldPos.X, (int)worldPos.Y,
                type, ai0: ai0, ai1: ai1, ai2: ai2, ai3: ai3);
            if (idx >= 0 && idx < Main.maxNPCs && VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
            }
            return idx;
        }

        /// <summary>取一个存活玩家作事件锚（单人=本机；服务器兜底取首个活人；全灭 null，镜像 OldNet）</summary>
        internal static Player AnyLivePlayer() {
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

        public override void PostUpdateNPCs() {
            //生成权威：客户端不做任何裁决（实体乘 SyncNPC 过线）
            if (VaultUtils.isClient || !KiyumeWorld.Active) {
                return;
            }
            if (++checkTimer < CheckInterval) {
                return;
            }
            checkTimer = 0;

            PumpLanternGuide();
            PumpWellHands();
            PumpScarecrows();
            PumpCortege();
            PumpFaceless();
            PumpRidgeWalker();
            PumpShallowHands();
            PumpMinoFisher();
        }

        //──泵挂点：提灯翁──
        //事件泵（全场 ≤1）：数值 Lantern*；目的地锚消费 KiyumeStructures.WellMouths/GraveMain/水线
        private void PumpLanternGuide() {
            if (lanternCooldown > 0) {
                lanternCooldown -= CheckInterval;
                return;
            }
            //全场唯一：在场期间不重臂（冷却在成功生成时重写）
            if (NPC.AnyNPCs(ModContent.NPCType<LanternGuideYokai>())) {
                return;
            }
            Player anchor = AnyLivePlayer();
            if (anchor == null) {
                return;
            }
            //玩家前方路面生成带：贴地 + 解析浓度达标（服务器 DensityAt 即解析式）+ 不在人眼前现形
            for (int i = 0; i < 6; i++) {
                float dist = Main.rand.NextFloat(KiyumeYokaiMetrics.LanternSpawnDistMin,
                    KiyumeYokaiMetrics.LanternSpawnDistMax);
                int dir = anchor.direction != 0 ? anchor.direction : 1;
                int col = (int)((anchor.Center.X + dir * dist) / 16f);
                if (col < 40 || col > Main.maxTilesX - 40) {
                    continue;
                }
                int row = Gen.KiyumePlans.ProbeGround(col, Gen.KiyumePlans.FloorTopAt(col) - 6);
                Vector2 pos = new(col * 16f + 8f, row * 16f);
                if (Fog.KiyumeFogSim.DensityAt(pos - new Vector2(0f, 24f))
                    < KiyumeYokaiMetrics.LanternSpawnFogMin) {
                    continue;
                }
                bool seen = false;
                foreach (Player player in Main.ActivePlayers) {
                    seen |= !player.dead && player.Distance(pos) < 400f;
                }
                if (seen) {
                    continue;
                }
                SpawnYokai(ModContent.NPCType<LanternGuideYokai>(), pos);
                lanternCooldown = KiyumeYokaiMetrics.LanternCooldown;
                return;
            }
            //本轮探样全败（雾薄/无地/贴脸）：短臂重试，别每次巡检白探
            lanternCooldown = 300;
        }

        //──泵挂点：井手──
        //一次性布防（每井一只）：按 KiyumeStructures.WellMouths 落防，静默井（IsWellSilenced）跳过
        private void PumpWellHands() {
            if (wellsSeeded) {
                return;
            }
            wellsSeeded = true;
            //逐井落防：井口锚（世界 px）写 ai[0..1]，出生点=井口下藏身位；
            //注册表为空（P3-E 井位形未落）则本会话无井手，结构性内容不做探测回退
            foreach (Point mouth in Gen.KiyumeStructures.WellMouths) {
                if (IsWellSilenced(mouth)) {
                    continue;
                }
                var mouthWorld = new Vector2(mouth.X * 16f + 8f, mouth.Y * 16f + 8f);
                Vector2 hidePos = mouthWorld
                    + new Vector2(0f, KiyumeYokaiMetrics.WellHideRowsBelow * 16f);
                SpawnYokai(ModContent.NPCType<WellHandYokai>(), hidePos,
                    ai0: mouthWorld.X, ai1: mouthWorld.Y);
            }
        }

        //──泵挂点：守田人──
        //一次性布防（ScareFieldInit 只）+ 会话补员（scarecrowPoolLeft）：田块锚 KiyumeStructures.ScarecrowPlot
        private void PumpScarecrows() {
            if (!scarecrowsSeeded) {
                //田块非空用之，null 由本体静态面走平地探测回退（连续 6 列高差 ≤1）；两者皆无则本会话缺席
                scarecrowsSeeded = true;
                ScarecrowWatcherYokai.SeedField();
                return;
            }
            //消隐回池、复现出池都在本体行动拍里；这里只兜「全场归零无人掷骰」的死局：
            //池有余量时在无人观测的田位悄悄补一只，「数目对不上」的戏才续得下去
            ScarecrowWatcherYokai.TryReplenishField();
        }

        //──泵挂点：夜行列──
        //事件泵（全场 ≤1 队）：潮窗判定（TideGateEnabled × CortegeTideGate）+ 编队生成，墓锚 KiyumeStructures.GraveMain
        private void PumpCortege() {
            if (cortegeCooldown > 0) {
                cortegeCooldown -= CheckInterval;
                return;
            }
            //潮窗（§3.3）：开关翻开即读服务器权威潮位，归一不达标持续待窗（冷却已清零不重臂）；
            //开关关闭 = 纯冷却调度，两路都真实可跑
            if (KiyumeYokaiMetrics.TideGateEnabled) {
                float span = (Gen.KiyumeMetrics.FogLineLowRow - Gen.KiyumeMetrics.FogLineHighRow) * 16f;
                float tideNorm = (Gen.KiyumeMetrics.FogLineLowRow * 16f - Fog.KiyumeFogTide.LineWorldY) / span;
                if (tideNorm < KiyumeYokaiMetrics.CortegeTideGate) {
                    return;
                }
            }
            //全场唯一队：队首在场即不重臂（冷却在成功生成时重写）
            if (NPC.AnyNPCs(ModContent.NPCType<FuneralCortegeYokai>()) || AnyLivePlayer() == null) {
                return;
            }
            //枯林东段（列 2300±100）探平地成列：队首在西头，四抬棺依 52px 纵列缀东
            for (int i = 0; i < 4; i++) {
                int col = KiyumeYokaiMetrics.CortegeSpawnColCenter + Main.rand.Next(
                    -KiyumeYokaiMetrics.CortegeSpawnColJitter, KiyumeYokaiMetrics.CortegeSpawnColJitter + 1);
                int row = Gen.KiyumePlans.ProbeGround(col, Gen.KiyumePlans.FloorTopAt(col) - 6);
                var head = new Vector2(col * 16f + 8f, row * 16f);
                bool seen = false;
                foreach (Player player in Main.ActivePlayers) {
                    seen |= !player.dead && player.Distance(head) < 400f;
                }
                if (seen) {
                    continue;   //不在人眼前成列
                }
                int leadIdx = SpawnYokai(ModContent.NPCType<FuneralCortegeYokai>(), head);
                if (leadIdx < 0 || leadIdx >= Main.maxNPCs) {
                    break;
                }
                for (int slot = 1; slot <= 4; slot++) {
                    //编队位与队首索引随生成包过线（ai 在 NewNPC 参数里，无生成后补写窗口）
                    int pcol = col + (int)MathF.Round(slot * KiyumeYokaiMetrics.CortegeSpacing / 16f);
                    int prow = Gen.KiyumePlans.ProbeGround(pcol, Gen.KiyumePlans.FloorTopAt(pcol) - 6);
                    SpawnYokai(ModContent.NPCType<FuneralCortegePorter>(),
                        new Vector2(pcol * 16f + 8f, prow * 16f), ai0: slot, ai3: leadIdx);
                }
                cortegeCooldown = KiyumeYokaiMetrics.CortegeCooldown;
                return;
            }
            //本轮探样全败（贴脸）：短臂重试，别每次巡检白探
            cortegeCooldown = 300;
        }

        //──泵挂点：无面者──
        //事件泵（全场 ≤1）：门口/巷口锚 KiyumeStructures.DoorwayPoints；per-player 现身计数在 ModPlayer
        private void PumpFaceless() {
            if (facelessCooldown > 0) {
                facelessCooldown -= CheckInterval;
                return;
            }
            //全场唯一：在场期间不重臂；全员看满三次她就不再来（恐惧要留白）
            if (NPC.AnyNPCs(ModContent.NPCType<FacelessOneYokai>())
                || !FacelessOneYokai.AnyPlayerBelowSessionCap()) {
                return;
            }
            if (FacelessOneYokai.TryHauntSpawn()) {
                facelessCooldown = Main.rand.Next(KiyumeYokaiMetrics.FacelessCooldownMin,
                    KiyumeYokaiMetrics.FacelessCooldownMax + 1);
            }
            else {
                //探位全败（门洞未注册且平地探样不合）：短臂重试（镜像提灯翁泵）
                facelessCooldown = 300;
            }
        }

        //──泵挂点：雾脊行者──
        //事件泵（全场 ≤1，R2 追加）：涨潮门（潮位归一 ≥0.7，村落只剩屋顶）+ 屋顶层玩家锚；
        //潮窗未开持续待窗（冷却已清零不重臂，镜像夜行列）；落点探测细节在本体 TryRidgeSpawn
        private void PumpRidgeWalker() {
            if (ridgeCooldown > 0) {
                ridgeCooldown -= CheckInterval;
                return;
            }
            if (NPC.AnyNPCs(ModContent.NPCType<RidgeWalkerYokai>())
                || !RidgeWalkerYokai.TideWindowOpen()) {
                return;
            }
            if (RidgeWalkerYokai.TryRidgeSpawn()) {
                ridgeCooldown = KiyumeYokaiMetrics.RidgeCooldown;
            }
            else {
                //探样全败（无屋顶层玩家/落点全湿）：短臂重试（镜像姊妹泵）
                ridgeCooldown = 300;
            }
        }

        //──泵挂点：水中手──
        //潮相泵（R2-A）：涨潮（归一 ≥ShallowRiseTide）一次性布防一茬，退潮（<ShallowEbbTide）
        //全员沉泥回收；窗旗滞回防抖。会话态懒复位走 FacelessSessionSystem.Stamp
        //（ResetSession 归 P4-A 既有区不改，戳式复位等效且零冲突面）
        private int shallowStamp;
        private bool shallowFloodArmed;
        private void PumpShallowHands() {
            if (shallowStamp != FacelessSessionSystem.Stamp) {
                shallowStamp = FacelessSessionSystem.Stamp;
                shallowFloodArmed = false;
            }
            //潮相存在：开关关闭即整体缺席（§3.3 潮相门控消费，无退化路径）
            if (!KiyumeYokaiMetrics.TideGateEnabled) {
                return;
            }
            float span = (Gen.KiyumeMetrics.FogLineLowRow - Gen.KiyumeMetrics.FogLineHighRow) * 16f;
            float tideNorm = (Gen.KiyumeMetrics.FogLineLowRow * 16f - Fog.KiyumeFogTide.LineWorldY) / span;
            if (tideNorm < KiyumeYokaiMetrics.ShallowEbbTide) {
                //退潮：这一茬全部沉回泥里，窗旗复位（下个涨潮窗立新茬；被打死的不补）
                if (shallowFloodArmed) {
                    ShallowHandsYokai.RecallAll();
                    shallowFloodArmed = false;
                }
                return;
            }
            if (shallowFloodArmed || tideNorm < KiyumeYokaiMetrics.ShallowRiseTide) {
                return;
            }
            shallowFloodArmed = true;
            ShallowHandsYokai.SeedFlat();
        }

        //──泵挂点：蓑翁──
        //座席泵（R2-A，全场 ≤1，会话限 MinoSessionCap 现）：在场不计冷却，
        //退场起臂 MinoRespawnCooldown 后允许下一现；会话计数懒复位同上走戳
        private int minoStamp;
        private int minoSpawnCount;
        private int minoRespawnCooldown;
        private void PumpMinoFisher() {
            if (minoStamp != FacelessSessionSystem.Stamp) {
                minoStamp = FacelessSessionSystem.Stamp;
                minoSpawnCount = 0;
                minoRespawnCooldown = 0;
            }
            //在场即不重臂也不走冷却：冷却语义=从退场起算
            if (NPC.AnyNPCs(ModContent.NPCType<MinoFisherYokai>())) {
                return;
            }
            if (minoRespawnCooldown > 0) {
                minoRespawnCooldown -= CheckInterval;
                return;
            }
            if (minoSpawnCount >= KiyumeYokaiMetrics.MinoSessionCap || AnyLivePlayer() == null) {
                return;
            }
            if (MinoFisherYokai.TrySeatSpawn()) {
                minoSpawnCount++;
                minoRespawnCooldown = KiyumeYokaiMetrics.MinoRespawnCooldown;
            }
            else {
                //探样全败（全员挤在水线边）：短臂重试（镜像提灯翁泵）
                minoRespawnCooldown = 300;
            }
        }
    }
}
