using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core
{
    /// <summary>
    /// 节肢爬行步态：本体没有任何自主推进——四只手轮流探向行进方向抓住
    /// 世界固定锚点，抓牢后钉死不动，身体被已抓的锚点拽过去。
    /// 步序沿对角环固定轮转（上左→下右→上右→下左，四足对角步态），
    /// 探爪走抬-刺两段（先抬向落点上方再猛刺锚点，甩鞭式末端加速）。
    /// 拉拽走"收缩曲线"：落爪先顿一拍（爪落身不动），随后该肢猛然收缩
    /// 拽动身体（拉力尖峰），再转入持续拖拽——每次窜动都归因于一只具体的手；
    /// 拽动间隙躯干缓缓下坠、发力时向出力侧压倾，一沉一起一拧即爬行的呼吸。
    /// 状态每帧经 <see cref="MLordContext.MovePolicy"/> 通道申报意图：
    /// Travel=爬行赶路，Tow=手阵携行（手全被征用的状态），Brace=四爪抓桩定身，
    /// Off=状态自管（演出/投技）。
    /// 按与目标位的差距分三档步态（走/小跑/奔袭）：步幅只小幅增长，
    /// 速度主要来自步频与本体封顶，奔袭还会对角成对同落带出短腾空。
    /// 相位/锚点写在手部 Override ai（随 netUpdate 同步），转移服务端裁定，
    /// 运动公式各端按同步数据镜像执行；三相期用存活手，
    /// 核心裸露期由四条眼窝已爆的手臂原样充当爬行肢（手臂完好，残口仍在蠕动）
    /// </summary>
    internal static class MLordLocomotion
    {
        /// <summary>步态载体模式</summary>
        internal enum LocoMode
        {
            /// <summary>停摆（状态自管本体）</summary>
            Off,
            /// <summary>三相期：存活手爬行</summary>
            RealHands,
            /// <summary>裸露期：残口实体原样爬行</summary>
            StumpArms,
        }

        //―――― 步态节拍常数 ――――
        /// <summary>探爪判定到达半径</summary>
        private const float ArriveDist = 30f;
        /// <summary>探爪超时帧（超时原地抓牢，绝不悬空卡死）</summary>
        private const int ReachTimeout = 30;
        /// <summary>抓牢过久强制换点（死区休息时放宽）</summary>
        private const int PlantTimeout = 300;
        /// <summary>死区内抓牢的手过此帧数逐只松爪回巢</summary>
        private const int IdleReleaseTick = 240;
        /// <summary>跛行肢探爪超时宽限（拖得久，常在半途就地抓牢——瘸腿够不到落点）</summary>
        private const int LimpTimeoutGrace = 12;
        /// <summary>探爪抬程帧长：前段爪走高弧（腿在迈），此后转入猛刺</summary>
        private const int ReachLiftFrames = 9;
        /// <summary>抬爪高度（落点上方的过路点，抬-刺两段的节肢腿语言）</summary>
        private const float ReachLiftHeight = 96f;
        /// <summary>刺爪段速度倍率：挥程慢、落点快，甩鞭式的末端加速</summary>
        private const float StabSpeedFactor = 1.35f;

        //―――― 三档步态 ――――
        /// <summary>步态档：贴身走、中距小跑、远距奔袭</summary>
        internal enum LocoGait
        {
            /// <summary>四拍单爪轮转（战斗默认，本 Boss 的性格步）</summary>
            Walk = 0,
            /// <summary>对角错拍，节拍收紧</summary>
            Trot = 1,
            /// <summary>对角成对同落，带短腾空</summary>
            Gallop = 2,
        }

        /// <summary>升档门槛（差距 px）</summary>
        private const float TrotEnterGap = 400f;
        private const float GallopEnterGap = 1100f;
        /// <summary>降档门槛：约七五折回差，边界上不来回抖</summary>
        private const float TrotExitGap = 300f;
        private const float GallopExitGap = 850f;
        /// <summary>升档最短驻留帧：至少让上一档跑完一个完整步序再换</summary>
        private const int GaitMinDwell = 40;
        /// <summary>超出封顶时的主动刹车衰减：降档不硬切速度，看得见庞然大物在急停</summary>
        private const float BrakeDamping = 0.9f;

        /// <summary>
        /// 单档步态参数。速度主要来自步频（Stagger/Concurrent/Settle/Recover）与
        /// 本体封顶（CapMul），步幅只小幅增长——但步幅直接买锚点寿命，
        /// 不给加成的话高速下锚会比手臂补得还快地被用尽，抓牢数塌了拉力跟着塌
        /// </summary>
        private readonly struct GaitTuning(int stagger, int concurrent, int settle, int recover,
            float reachSpeedMul, float strideBonus, float capMul, float damping, bool pairStep)
        {
            /// <summary>新探爪起步的错拍间隔</summary>
            public readonly int Stagger = stagger;
            /// <summary>同时在途探爪上限</summary>
            public readonly int Concurrent = concurrent;
            /// <summary>落爪顿帧：爪先落身不动，随后才收缩（因果可读）</summary>
            public readonly int Settle = settle;
            /// <summary>松爪收势帧长</summary>
            public readonly int Recover = recover;
            /// <summary>探爪飞行速度倍率</summary>
            public readonly float ReachSpeedMul = reachSpeedMul;
            /// <summary>步幅前引加成 px</summary>
            public readonly float StrideBonus = strideBonus;
            /// <summary>本体速度封顶倍率</summary>
            public readonly float CapMul = capMul;
            /// <summary>本体速度阻尼：越快越要保动量，否则腾空相把攒的速度漏光</summary>
            public readonly float Damping = damping;
            /// <summary>对角成对同帧出爪（双拍步态，落地双爪同时供力）</summary>
            public readonly bool PairStep = pairStep;
        }

        private static readonly GaitTuning[] GaitTable = [
            //走：整场默认步态，一个数都不动
            new GaitTuning(8, 2, 7, 14, 1f, 0f, 1f, 0.945f, false),
            //小跑：步序环相邻两位本就是对角，收紧节拍即自然成对错拍
            new GaitTuning(4, 3, 5, 9, 1.25f, 60f, 1.6f, 0.955f, false),
            //奔袭：对角成对同落，腾空靠"另一对刚松爪"自然形成
            new GaitTuning(2, 4, 3, 6, 1.55f, 120f, 2.5f, 0.965f, true),
        ];

        /// <summary>
        /// 当前步态。各端按同步位置同式推导；迟滞状态本地保有，
        /// 短暂分歧只影响本体封顶，手臂相位与锚点始终走服务端裁定的同步槽
        /// </summary>
        internal static LocoGait Gait { get; private set; } = LocoGait.Walk;
        private static int gaitDwell;

        private static GaitTuning Tuning => GaitTable[(int)Gait];

        /// <summary>节肢步序环：上左→下右→上右→下左，对角交替的固定循环（读作步态而非乱抓）</summary>
        private static readonly int[] StepRing = [0, 3, 1, 2];
        /// <summary>上一次出爪的槽位（服务端步序记忆）</summary>
        private static int lastSteppedSlot = -1;
        /// <summary>本体侧倾（向正在猛拽的肢体一侧压），姿态层经 <see cref="BodyRoll"/> 消费</summary>
        private static float bodyRoll;

        private static int ownerWhoAmI = -1;
        private static uint lastUpdateTick;
        private static LocoMode mode = LocoMode.Off;
        private static LocoMode lastMode = LocoMode.Off;
        /// <summary>上帧相位镜像（本地 FX 触发沿），按手 whoAmI 索引</summary>
        private static readonly int[] prevPhase = new int[Main.maxNPCs];
        /// <summary>抓地闷响节流（持续追击时抓点很密，声音限频不刷屏）</summary>
        private static uint lastGripSoundTick;
        /// <summary>远距拼命系数 0~1（差距 1100~2000px 线性升满），跛行惩罚随之淡出</summary>
        private static float lameDesperation;

        /// <summary>当前抓牢肢数（激光发射架就位判据等）</summary>
        internal static int PlantedCount { get; private set; }

        /// <summary>本体侧倾角：被哪只手猛拽就向哪侧压（<see cref="MLordStateBase.UpdateLean"/> 叠加消费）</summary>
        internal static float BodyRoll => bodyRoll;

        /// <summary>卸载/离开世界清空</summary>
        public static void Reset() {
            ownerWhoAmI = -1;
            mode = lastMode = LocoMode.Off;
            Array.Clear(prevPhase);
            PlantedCount = 0;
            lastSteppedSlot = -1;
            bodyRoll = 0f;
            Gait = LocoGait.Walk;
            gaitDwell = 0;
        }

        #region 对外查询

        /// <summary>该手当前被爬行系统征用（相位非空闲）</summary>
        public static bool IsClaimed(NPC hand) {
            return TryGetClaim(hand, out _, out _);
        }

        /// <summary>
        /// 跛行肢判定：每个个体固定一条残弱肢（whoAmI 派生——各端一致零同步，
        /// 场次之间还会换腿）。只影响爬行步态，不碰攻击编舞
        /// </summary>
        public static bool IsLameLimb(NPC core, NPC hand) {
            if (core == null || hand == null) {
                return false;
            }
            int slot = ((int)hand.ai[MLordAiSlots.HandRow] == 1 ? 2 : 0)
                + ((int)hand.ai[MLordAiSlots.HandSide] == 0 ? 0 : 1);
            return slot == core.whoAmI % MLordPartsStatus.HandSlots;
        }

        /// <summary>读取该手的爬行征用相位与锚点；系统停摆或空闲返回 false</summary>
        public static bool TryGetClaim(NPC hand, out int phase, out Vector2 anchor) {
            phase = MLordCrawlPhase.Free;
            anchor = Vector2.Zero;
            if (mode == LocoMode.Off || Main.GameUpdateCount + 1u - lastUpdateTick > 3u) {
                return false;
            }
            if (hand == null || (int)hand.ai[MLordAiSlots.PartCoreIndex] != ownerWhoAmI) {
                return false;
            }
            MoonLordHandAI ov = MLordFacts.GetHandOverride(hand);
            if (ov == null) {
                return false;
            }
            phase = (int)ov.ai[MLordAiSlots.HandOvCrawlPhase];
            anchor = new Vector2(ov.ai[MLordAiSlots.HandOvAnchorX], ov.ai[MLordAiSlots.HandOvAnchorY]);
            return phase != MLordCrawlPhase.Free;
        }

        /// <summary>
        /// 编队目标钳进该手的合法区：本侧外围（离中线有横向下限）+ 肩部可达环带
        /// [FormationReachMin, FormationReachMax]。根治手臂离身太近（拥挤压瘪、
        /// 折进躯干剪影）或太远（脱链星桥）的不合理位形
        /// </summary>
        public static Vector2 ClampFormationGoal(NPC core, NPC hand, Vector2 goal) {
            return ClampHandZone(core, hand, goal,
                MLordDirector.FormationReachMin, MLordDirector.FormationReachMax);
        }

        /// <summary>
        /// 手部合法区三段钳制：本侧外推→肩部环带→本侧复核。
        /// 环带沿肩向径向推拉，可能把点带回中线附近，故外侧约束最后重申
        /// （复核造成的环带小超差被过伸阀 800 兜住）
        /// </summary>
        private static Vector2 ClampHandZone(NPC core, NPC hand, Vector2 point, float minReach, float maxReach) {
            float dir = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            Vector2 shoulder = ShoulderOf(core, hand);
            point = ClampOutside(core, dir, point);
            point = ClampToAnnulus(shoulder, point, minReach, maxReach);
            return ClampOutside(core, dir, point);
        }

        /// <summary>横向外侧钳制：点相对核心中线的本侧距离不得小于 HandOutsideMin</summary>
        private static Vector2 ClampOutside(NPC core, float dir, Vector2 point) {
            float lateral = (point.X - core.Center.X) * dir;
            if (lateral < MLordDirector.HandOutsideMin) {
                point.X = core.Center.X + dir * MLordDirector.HandOutsideMin;
            }
            return point;
        }

        /// <summary>
        /// 攻击站位钳制：状态直控动作（掌击翼位/抓捕捕位）的手部落位
        /// 同样钳进合法区——技能不许把手甩出限定范围
        /// </summary>
        public static Vector2 ClampAttackPost(NPC core, NPC hand, Vector2 point) {
            return ClampHandZone(core, hand, point,
                MLordDirector.CrawlReachMin, MLordDirector.CrawlReachMax);
        }

        /// <summary>
        /// 直控冲线的过伸判据：手离本肩超过解剖上限（CrawlOverstretch）。
        /// 冲线段读到 true 应立即失速——抓不到就是抓不到，手不出格
        /// </summary>
        public static bool BeyondReach(NPC core, NPC hand) {
            if (core == null || hand == null) {
                return false;
            }
            return Vector2.Distance(hand.Center, ShoulderOf(core, hand)) > MLordDirector.CrawlOverstretch;
        }

        #endregion

        #region 主更新（核心 AI 每帧调用，各端执行）

        public static void Update(MLordContext ctx) {
            NPC core = ctx.Npc;
            ownerWhoAmI = core.whoAmI;
            lastUpdateTick = Main.GameUpdateCount + 1u;
            mode = ResolveMode(ctx);

            //模式切换：服务端立即释放全部征用（手交还编队/状态）
            if (mode != lastMode && !VaultUtils.isClient) {
                ReleaseAll(ctx);
            }
            lastMode = mode;

            if (mode == LocoMode.Off) {
                PlantedCount = 0;
                bodyRoll *= 0.9f;
                return;
            }

            MLordMovePolicy policy = ctx.MovePolicy;
            Vector2 gap = ctx.MoveGoal - core.Center;
            Vector2 travelDir = gap.SafeNormalize(Vector2.UnitX);
            lameDesperation = policy == MLordMovePolicy.Travel
                ? MathHelper.Clamp((gap.Length() - 1100f) / 900f, 0f, 1f) : 0f;

            //可用臂数先点清：成对出爪的档位要靠它兜底，臂不够会让四肢同时离地
            int eligibleArms = 0;
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                NPC probe = HandOf(ctx, slot);
                if (probe != null && MLordFacts.GetHandOverride(probe) != null && HandEligible(ctx, slot, probe)) {
                    eligibleArms++;
                }
            }
            ResolveGait(policy, gap.Length(), eligibleArms);

            if (policy == MLordMovePolicy.Tow) {
                //拖曳：手全被状态征用，不跑步态，仅编队携行
                if (!VaultUtils.isClient) {
                    ReleaseAll(ctx);
                }
                UpdateTowBody(core, gap);
                PlantedCount = 0;
                return;
            }

            //―――― 步态推进 ――――
            int plantedCount = 0;
            int reachingCount = 0;
            int youngestReachTick = int.MaxValue;
            Span<int> freeSlots = stackalloc int[MLordPartsStatus.HandSlots];
            Span<int> freeTicks = stackalloc int[MLordPartsStatus.HandSlots];
            int freeCount = 0;
            bool inDeadZone = gap.Length() < MLordDirector.CrawlDeadZone;

            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                NPC hand = HandOf(ctx, slot);
                if (hand == null) {
                    continue;
                }
                MoonLordHandAI ov = MLordFacts.GetHandOverride(hand);
                if (ov == null) {
                    continue;
                }
                bool eligible = HandEligible(ctx, slot, hand);
                int phase = (int)ov.ai[MLordAiSlots.HandOvCrawlPhase];

                //被状态征走：立即释放（服务端），本帧不入账
                if (!eligible) {
                    if (phase != MLordCrawlPhase.Free && !VaultUtils.isClient) {
                        SetPhase(hand, ov, MLordCrawlPhase.Free);
                    }
                    TickFx(hand, ov);
                    continue;
                }

                ov.ai[MLordAiSlots.HandOvPhaseTick]++;
                int tick = (int)ov.ai[MLordAiSlots.HandOvPhaseTick];
                Vector2 anchor = new(ov.ai[MLordAiSlots.HandOvAnchorX], ov.ai[MLordAiSlots.HandOvAnchorY]);
                Vector2 shoulder = ShoulderOf(core, hand);
                bool lame = IsLameLimb(core, hand);
                int reachLimit = ReachTimeout + (lame ? LimpTimeoutGrace : 0);

                //相位转移（服务端裁定）
                if (!VaultUtils.isClient) {
                    switch (phase) {
                        case MLordCrawlPhase.Reach:
                            if (hand.Distance(anchor) < ArriveDist || tick > reachLimit) {
                                //超时原地抓牢：锚改写为当前位置，绝不悬空硬拽
                                //（跛行肢常在这里半途落爪——瘸腿够不到原定落点）
                                if (tick > reachLimit) {
                                    ov.ai[MLordAiSlots.HandOvAnchorX] = hand.Center.X;
                                    ov.ai[MLordAiSlots.HandOvAnchorY] = hand.Center.Y;
                                }
                                SetPhase(hand, ov, MLordCrawlPhase.Planted);
                            }
                            break;
                        case MLordCrawlPhase.Planted: {
                            float dist = Vector2.Distance(shoulder, anchor);
                            bool overstretch = dist > MLordDirector.CrawlOverstretch;
                            bool spent = false;
                            if (policy == MLordMovePolicy.Travel) {
                                //用尽：拽到怀里 / 落到行进方向身后 / 抓太久
                                Vector2 toAnchor = anchor - core.Center;
                                bool behind = Vector2.Dot(toAnchor.SafeNormalize(Vector2.Zero), travelDir) < -0.15f;
                                spent = dist < MLordDirector.CrawlHoldMin
                                    || (!inDeadZone && behind)
                                    || tick > PlantTimeout
                                    || (inDeadZone && tick > IdleReleaseTick);
                            }
                            if (overstretch || spent) {
                                SetPhase(hand, ov, MLordCrawlPhase.Recover);
                            }
                            break;
                        }
                        case MLordCrawlPhase.Recover:
                            if (tick > Tuning.Recover) {
                                SetPhase(hand, ov, MLordCrawlPhase.Free);
                            }
                            break;
                    }
                    phase = (int)ov.ai[MLordAiSlots.HandOvCrawlPhase];
                }

                //入账（转移后的相位）
                if (phase == MLordCrawlPhase.Planted) {
                    plantedCount++;
                }
                else if (phase == MLordCrawlPhase.Reach) {
                    //跛行肢的慢探不占并发名额也不卡起步错拍：
                    //它是在拖行不是在出击，健全手照常轮转（否则整体步频被瘸腿拖死）
                    if (!lame) {
                        reachingCount++;
                        youngestReachTick = Math.Min(youngestReachTick, tick);
                    }
                }
                else if (phase == MLordCrawlPhase.Free) {
                    freeSlots[freeCount] = slot;
                    //记原始空闲帧：步序环用它做跛行肢的出爪门槛
                    freeTicks[freeCount] = tick;
                    freeCount++;
                }

                TickFx(hand, ov);
            }

            //―――― 起步调度（服务端）――――
            if (!VaultUtils.isClient && reachingCount < Tuning.Concurrent
                && youngestReachTick >= Tuning.Stagger && freeCount > 0) {
                bool wantStep = policy == MLordMovePolicy.Brace
                    || (policy == MLordMovePolicy.Travel && !inDeadZone);
                if (wantStep) {
                    //奔袭的成对同落：步序环相邻两位即对角伙伴，连出两爪就是一记双拍。
                    //只在还有爪抓着地时才成对——否则这一帧四肢全空，拉力断档
                    int launches = Tuning.PairStep && plantedCount > 0 ? 2 : 1;
                    for (int n = 0; n < launches; n++) {
                        if (!TryLaunchStep(ctx, core, policy, travelDir, freeSlots, freeTicks, freeCount)) {
                            break;
                        }
                    }
                }
            }

            PlantedCount = plantedCount;
            UpdateBody(ctx, core, gap, travelDir, plantedCount);
        }

        /// <summary>
        /// 沿步序环起一只爪，返回是否真的出了爪。
        /// 节肢步序：自上次出爪槽位起沿对角环轮转（上左→下右→上右→下左），
        /// 固定循环节拍读作虫的步态而非四手乱抓；
        /// 本侧锚点落在行进后方的候选跳过（横移时对侧手不做无效抓握）；
        /// 跛行肢在环里但门槛更高——空闲不足 LimpStepBias 帧就让过本轮
        /// （永远慢半拍的那条腿），远距拼命时豁免。
        /// 出爪后把该槽从空闲表划掉，成对出爪的第二爪才会落到对角伙伴上
        /// </summary>
        private static bool TryLaunchStep(MLordContext ctx, NPC core, MLordMovePolicy policy,
            Vector2 travelDir, Span<int> freeSlots, Span<int> freeTicks, int freeCount) {
            int ringStart = 0;
            for (int i = 0; i < StepRing.Length; i++) {
                if (StepRing[i] == lastSteppedSlot) {
                    ringStart = i + 1;
                    break;
                }
            }
            for (int step = 0; step < StepRing.Length; step++) {
                int slot = StepRing[(ringStart + step) % StepRing.Length];
                int freeAt = -1;
                for (int i = 0; i < freeCount; i++) {
                    if (freeSlots[i] == slot) {
                        freeAt = i;
                        break;
                    }
                }
                if (freeAt < 0) {
                    continue;
                }
                NPC hand = HandOf(ctx, slot);
                MoonLordHandAI ov = hand != null ? MLordFacts.GetHandOverride(hand) : null;
                if (ov == null) {
                    continue;
                }
                if (IsLameLimb(core, hand) && lameDesperation < 0.6f
                    && freeTicks[freeAt] < MLordDirector.LimpStepBias) {
                    continue;
                }
                Vector2 anchor = policy == MLordMovePolicy.Brace
                    ? BraceAnchor(core, hand, slot)
                    : TravelAnchor(ctx, core, hand, slot, travelDir);
                if (policy == MLordMovePolicy.Travel) {
                    Vector2 toAnchor = (anchor - core.Center).SafeNormalize(Vector2.Zero);
                    if (Vector2.Dot(toAnchor, travelDir) < -0.05f) {
                        continue;
                    }
                }
                ov.ai[MLordAiSlots.HandOvAnchorX] = anchor.X;
                ov.ai[MLordAiSlots.HandOvAnchorY] = anchor.Y;
                SetPhase(hand, ov, MLordCrawlPhase.Reach);
                lastSteppedSlot = slot;
                freeSlots[freeAt] = -1;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 步态换档：按差距分三档，升降门槛带回差、升档要过最短驻留（边界不抖）；
        /// 降档立刻生效——追不动了就该慢下来。
        /// 臂数门槛：奔袭要三条以上可用臂、小跑要两条，否则成对出爪会让四肢同时离地，
        /// 拉力归零身体就只剩下坠。非赶路策略（抓桩/携行/停摆）一律回到走
        /// </summary>
        private static void ResolveGait(MLordMovePolicy policy, float gapLen, int eligibleArms) {
            gaitDwell++;
            LocoGait want = Gait;
            if (policy != MLordMovePolicy.Travel) {
                want = LocoGait.Walk;
            }
            else {
                want = Gait switch {
                    LocoGait.Walk => gapLen >= GallopEnterGap ? LocoGait.Gallop
                        : gapLen >= TrotEnterGap ? LocoGait.Trot : LocoGait.Walk,
                    LocoGait.Trot => gapLen >= GallopEnterGap ? LocoGait.Gallop
                        : gapLen < TrotExitGap ? LocoGait.Walk : LocoGait.Trot,
                    _ => gapLen < GallopExitGap ? LocoGait.Trot : LocoGait.Gallop,
                };
            }

            if (want == LocoGait.Gallop && eligibleArms < 3) {
                want = LocoGait.Trot;
            }
            if (want == LocoGait.Trot && eligibleArms < 2) {
                want = LocoGait.Walk;
            }

            if (want == Gait) {
                return;
            }
            if (want < Gait || gaitDwell >= GaitMinDwell) {
                Gait = want;
                gaitDwell = 0;
            }
        }

        /// <summary>模式解析：策略 Off 即停摆；裸露期走残口黑臂，三相期走存活手</summary>
        private static LocoMode ResolveMode(MLordContext ctx) {
            if (ctx.MovePolicy == MLordMovePolicy.Off) {
                return LocoMode.Off;
            }
            int phase = (int)ctx.Npc.ai[MLordAiSlots.CorePhase];
            if (phase == MLordPhase.CoreExposed) {
                return LocoMode.StumpArms;
            }
            if (phase == MLordPhase.Trinity) {
                return LocoMode.RealHands;
            }
            return LocoMode.Off;
        }

        #endregion

        #region 本体物理（各端镜像）

        /// <summary>
        /// 爬行/抓桩的本体速度整合，无自主推进。
        /// 每只抓牢的手按各自的收缩曲线供力：顿帧内零拉力（爪落身不动），
        /// 随后拉力冲到尖峰把身体拽向该锚（身体朝"刚抓牢的那只手"窜动），
        /// 再回落为持续拖拽——手带着身体走的因果全部写在时序里
        /// </summary>
        private static void UpdateBody(MLordContext ctx, NPC core, Vector2 gap, Vector2 travelDir, int plantedCount) {
            MLordMovePolicy policy = ctx.MovePolicy;
            float urgency = ctx.MoveUrgency;

            if (policy == MLordMovePolicy.Brace) {
                //抓桩定身：爪一落，身体像被四根锁链吊死
                core.velocity *= plantedCount > 0 ? 0.82f : 0.9f;
                bodyRoll *= 0.9f;
                return;
            }

            float gapLen = gap.Length();
            float gapFactor = MathHelper.Clamp(gapLen / 300f, 0f, 1f);

            //各抓牢锚按收缩曲线牵引：只取行进向前方的分量（身后残锚不拖后腿），
            //拉力带方向与大小——身体明确偏向正在发力的那只手
            Vector2 pull = Vector2.Zero;
            float rollDrive = 0f;
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                NPC hand = HandOf(ctx, slot);
                MoonLordHandAI ov = hand != null ? MLordFacts.GetHandOverride(hand) : null;
                if (ov == null || (int)ov.ai[MLordAiSlots.HandOvCrawlPhase] != MLordCrawlPhase.Planted) {
                    continue;
                }
                float contraction = ContractionCurve((int)ov.ai[MLordAiSlots.HandOvPhaseTick], Tuning.Settle);
                if (contraction <= 0f) {
                    continue;
                }
                if (IsLameLimb(core, hand)) {
                    //跛行肢没有爆发力：砍掉收缩尖峰、持续拖拽略弱。
                    //远距拼命：差距拉大时惩罚淡出（被落下时顾不上坏腿），
                    //跛行只在玩家近处看得见步态的距离上呈现，马拉松追击不因瘸腿触发回归瞬移
                    contraction = Math.Min(contraction, MathHelper.Lerp(1f, 1.75f, lameDesperation))
                        * MathHelper.Lerp(0.85f, 1f, lameDesperation);
                }
                Vector2 anchor = new(ov.ai[MLordAiSlots.HandOvAnchorX], ov.ai[MLordAiSlots.HandOvAnchorY]);
                Vector2 dir = (anchor - core.Center).SafeNormalize(Vector2.Zero);
                float align = Math.Max(0f, Vector2.Dot(dir, travelDir));
                pull += dir * (align * contraction);
                //发力侧入账：尖峰段的手把躯干往自己那侧拧
                float side = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
                rollDrive += side * MathHelper.Clamp((contraction - 1f) / 0.75f, 0f, 1f);
            }

            if (pull.LengthSquared() > 0.0004f) {
                core.velocity += pull * (0.34f + 0.46f * urgency) * gapFactor;
            }

            //步间坠身：没有肢体在发力的间隙，躯干像挂在锚上的死重缓缓下沉，
            //下一记拉拽尖峰再把它拽起——一沉一起即爬行的呼吸
            float slack = 1f - MathHelper.Clamp(pull.Length() * 2.2f, 0f, 1f);
            core.velocity.Y += 0.055f * slack * (0.35f + 0.65f * gapFactor);

            //躯干侧倾：向正在猛拽的那侧压（节肢拖行的扭动）
            bodyRoll = MathHelper.Lerp(bodyRoll, MathHelper.Clamp(rollDrive, -1f, 1f) * 0.055f, 0.12f);

            //阻尼按档：奔袭要保住腾空相的动量，否则攒的速度全漏在飞行段
            core.velocity *= Tuning.Damping;
            float cap = (3.5f + 8.5f * urgency) * (0.35f + 0.65f * gapFactor) * Tuning.CapMul;
            float speed = core.velocity.Length();
            if (speed > cap) {
                //超顶不硬切：按刹车系数衰减过去，降档时读得出"庞然大物在急停"而不是瞬间变慢
                core.velocity = core.velocity.SafeNormalize(Vector2.Zero) * Math.Max(cap, speed * BrakeDamping);
            }
        }

        /// <summary>
        /// 收缩曲线（自落爪起的帧数）：顿帧零力→猛拽尖峰（easeOut 冲到 1.75）→
        /// 回落 1.0 持续拖拽。尖峰段就是身体窜动的那一下。
        /// 顿帧长度随步态收紧（奔袭的双爪几乎落地即发力）
        /// </summary>
        private static float ContractionCurve(int tick, int settle) {
            if (tick <= settle) {
                return 0f;
            }
            float t = tick - settle;
            if (t < 13f) {
                float k = t / 13f;
                return 1.75f * k * (2f - k);
            }
            if (t < 34f) {
                return MathHelper.Lerp(1.75f, 1f, (t - 13f) / 21f);
            }
            return 1f;
        }

        /// <summary>该手当前的收缩发力强度 0~1（尖峰段），姿态层用来把"正在发力"点亮</summary>
        public static float GripSurge(NPC hand) {
            MoonLordHandAI ov = MLordFacts.GetHandOverride(hand);
            if (ov == null || (int)ov.ai[MLordAiSlots.HandOvCrawlPhase] != MLordCrawlPhase.Planted) {
                return 0f;
            }
            float c = ContractionCurve((int)ov.ai[MLordAiSlots.HandOvPhaseTick], Tuning.Settle);
            return MathHelper.Clamp((c - 1f) / 0.75f, 0f, 1f);
        }

        /// <summary>拖曳携行：手阵抬着身体缓移（无步态）</summary>
        private static void UpdateTowBody(NPC core, Vector2 gap) {
            bodyRoll *= 0.9f;
            Vector2 want = gap * 0.028f;
            if (want.Length() > 4.6f) {
                want = want.SafeNormalize(Vector2.Zero) * 4.6f;
            }
            core.velocity = Vector2.Lerp(core.velocity, want, 0.12f);
        }

        #endregion

        #region 手部运动执行（手 AI 调用，各端镜像）

        /// <summary>
        /// 被征用手的运动：探爪（抬-刺两段）/ 抓牢钉死 / 松爪收势。
        /// 跛行肢的探爪只有六成速，且带周期性卡顿（抬不起来又硬拽的节奏）。
        /// 单写者约定：手的速度只在手自己的 AI 里写，本方法即该写入点
        /// </summary>
        public static void ApplyHandMotion(NPC hand, int phase, Vector2 anchor) {
            switch (phase) {
                case MLordCrawlPhase.Reach: {
                    MoonLordHandAI ov = MLordFacts.GetHandOverride(hand);
                    float tick = ov?.ai[MLordAiSlots.HandOvPhaseTick] ?? 0f;
                    bool lame = IsLameLimb(MLordFacts.GetCore(hand), hand);
                    //探爪飞行速度随步态提升：步频提上去了，爪也得真能在更少的帧里赶到
                    float cap = MLordDirector.CrawlReachSpeed * Tuning.ReachSpeedMul;
                    float gain = 0.5f;
                    if (lame) {
                        //卡顿包络：|sin| 在拖滞与发力间摆动，PhaseTick 各端镜像自增；
                        //远距拼命时慢探惩罚同步淡出
                        float seize = 0.45f + 0.55f * Math.Abs((float)Math.Sin(tick * 0.42f));
                        float speedMul = MLordDirector.LimpSpeedFactor * seize;
                        cap *= MathHelper.Lerp(speedMul, 1f, lameDesperation);
                        gain = MathHelper.Lerp(0.34f, 0.5f, lameDesperation);
                    }
                    //抬-刺两段：前段爪尖先奔落点上方的过路点（腿抬起来在迈），
                    //抬程结束一口气刺向锚点（末端加速）——节肢腿的甩鞭时序；
                    //瘸腿抬不高，贴着拖过去
                    float liftK = MathHelper.Clamp(1f - tick / ReachLiftFrames, 0f, 1f);
                    if (lame) {
                        liftK *= 0.35f;
                    }
                    Vector2 aimPoint = anchor - new Vector2(0f, ReachLiftHeight * liftK);
                    if (liftK <= 0f) {
                        cap *= StabSpeedFactor;
                        gain = Math.Min(1f, gain + 0.12f);
                    }
                    Vector2 want = (aimPoint - hand.Center) * 0.24f;
                    float len = want.Length();
                    if (len > cap) {
                        want = want / len * cap;
                    }
                    else if (len < 7f && len > 0.01f) {
                        want = want / len * 7f;
                    }
                    hand.velocity = Vector2.Lerp(hand.velocity, want, gain);
                    //探向锚点的星流预示（无伤位移，不用攻击级预警）
                    if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                        MLordScreenFX.ConvergeStreak(anchor, 90f, 0.4f);
                    }
                    break;
                }
                case MLordCrawlPhase.Planted:
                    //钉死：世界固定点，绝无漂移（各端锚点同步一致）
                    hand.Center = anchor;
                    hand.velocity = Vector2.Zero;
                    break;
                default: {
                    //收势：臂链仍超伸时主动向肩部环带回收——过伸松爪后身体
                    //还在被其他爪拽走，手滞留旧锚点会把臂链拉出脱链星桥
                    NPC core = MLordFacts.GetCore(hand);
                    if (core != null) {
                        Vector2 shoulder = ShoulderOf(core, hand);
                        Vector2 toHand = hand.Center - shoulder;
                        if (toHand.Length() > MLordDirector.FormationReachMax) {
                            Vector2 target = shoulder + toHand.SafeNormalize(Vector2.UnitY)
                                * (MLordDirector.FormationReachMax * 0.88f);
                            Vector2 want = (target - hand.Center) * 0.24f;
                            if (want.Length() > 24f) {
                                want = want.SafeNormalize(Vector2.Zero) * 24f;
                            }
                            hand.velocity = Vector2.Lerp(hand.velocity, want, 0.3f);
                            break;
                        }
                    }
                    hand.velocity *= 0.84f;
                    break;
                }
            }
        }

        #endregion

        #region 内部工具

        /// <summary>该槽位手实体（在场即可，破坏与否由模式区分），缺位 null</summary>
        private static NPC HandOf(MLordContext ctx, int slot) {
            int index = ctx.Parts.HandIndex(slot);
            if (index < 0 || !Main.npc[index].active) {
                return null;
            }
            return Main.npc[index];
        }

        /// <summary>
        /// 爬行资格：裸露期用残口、三相期用存活手；
        /// 再排除被状态征用的手（掌击执行者/协奏预备声部）
        /// </summary>
        private static bool HandEligible(MLordContext ctx, int slot, NPC hand) {
            bool broken = hand.ai[MLordAiSlots.PartBroken] == MLordAiSlots.BrokenMark;
            if (mode == LocoMode.StumpArms) {
                return broken;
            }
            if (broken) {
                return false;
            }

            MLordStateIndex state = MLordFacts.GetCoreState(ctx.Npc);
            int stateTimer = ctx.Owner.StateTimer;
            switch (state) {
                case MLordStateIndex.TidalPalms: {
                    //本拍执行者不征用
                    if (MLordTidalPalmsState.TryGetBeat(ctx, stateTimer, out int slamIndex, out _)) {
                        Span<int> performers = stackalloc int[MLordTidalPalmsState.MaxPerformers];
                        int count = MLordTidalPalmsState.ResolvePerformers(ctx, slamIndex, performers);
                        for (int i = 0; i < count; i++) {
                            if (performers[i] == hand.whoAmI) {
                                return false;
                            }
                        }
                    }
                    return true;
                }
                case MLordStateIndex.Concerto:
                    //预备声部要抬手亮眼做弹幕预告，不许拽去爬行
                    return MLordConcertoState.BeatWindup(ctx, stateTimer, slot) <= 0f;
                default:
                    return true;
            }
        }

        /// <summary>服务端写相位：清计时、挂同步、记录心跳冲量与本地 FX 沿</summary>
        private static void SetPhase(NPC hand, MoonLordHandAI ov, int phase) {
            ov.ai[MLordAiSlots.HandOvCrawlPhase] = phase;
            ov.ai[MLordAiSlots.HandOvPhaseTick] = 0f;
            hand.netUpdate = true;
        }

        /// <summary>释放全部爬行征用（模式切换/拖曳态，服务端）</summary>
        private static void ReleaseAll(MLordContext ctx) {
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                NPC hand = HandOf(ctx, slot);
                MoonLordHandAI ov = hand != null ? MLordFacts.GetHandOverride(hand) : null;
                if (ov == null || ov.ai[MLordAiSlots.HandOvCrawlPhase] == MLordCrawlPhase.Free) {
                    continue;
                }
                SetPhase(hand, ov, MLordCrawlPhase.Free);
            }
        }

        /// <summary>
        /// 整体阵形平移时同步平移全部抓取锚点（远距日蚀回归调用，服务端）。
        /// 不平移会让钉死的手下一帧被拽回旧世界坐标
        /// </summary>
        public static void ShiftAnchors(NPC core, Vector2 shift) {
            if (core == null || core.whoAmI != ownerWhoAmI || mode == LocoMode.Off) {
                return;
            }
            foreach (NPC other in Main.ActiveNPCs) {
                if (other.type != NPCID.MoonLordHand
                    || (int)other.ai[MLordAiSlots.PartCoreIndex] != core.whoAmI) {
                    continue;
                }
                MoonLordHandAI ov = MLordFacts.GetHandOverride(other);
                if (ov == null || ov.ai[MLordAiSlots.HandOvCrawlPhase] == MLordCrawlPhase.Free) {
                    continue;
                }
                ov.ai[MLordAiSlots.HandOvAnchorX] += shift.X;
                ov.ai[MLordAiSlots.HandOvAnchorY] += shift.Y;
                other.netUpdate = true;
            }
        }

        /// <summary>
        /// 本地相位沿检测：抓牢瞬间触发抓地 FX（拉拽本身由收缩曲线在顿帧后接手，
        /// 爪响在前、身动在后——因果可读）
        /// </summary>
        private static void TickFx(NPC hand, MoonLordHandAI ov) {
            int phase = (int)ov.ai[MLordAiSlots.HandOvCrawlPhase];
            int prev = prevPhase[hand.whoAmI];
            prevPhase[hand.whoAmI] = phase;
            if (phase != MLordCrawlPhase.Planted || prev == MLordCrawlPhase.Planted) {
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 anchor = new(ov.ai[MLordAiSlots.HandOvAnchorX], ov.ai[MLordAiSlots.HandOvAnchorY]);
            bool lame = IsLameLimb(MLordFacts.GetCore(hand), hand);
            //抓地一拍：空间裂纹 + 星尘 + 轻震屏 + 闷响
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_SpaceFracture>(anchor,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    MLordDirector.DeepViolet, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(14, 22), Main.rand.NextFloat(-0.05f, 0.05f));
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(anchor,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.8f, 5f),
                    MLordDirector.Phantasmal, Main.rand.NextFloat(0.45f, 0.8f))?.Configure(false, Main.rand.Next(12, 18));
            }
            MLordScreenFX.Punch(anchor, lame ? 1.1f : 1.7f, 6);
            uint now = Main.GameUpdateCount;
            if (now - lastGripSoundTick >= 16u) {
                lastGripSoundTick = now;
                //跛行肢的落爪更哑更沉——那条腿听起来就不对
                SoundEngine.PlaySound(SoundID.Item103 with {
                    Volume = lame ? 0.34f : 0.42f,
                    Pitch = lame ? -0.95f : -0.72f,
                    MaxInstances = 4
                }, anchor);
            }
        }

        /// <summary>
        /// 行进锚点：本侧外张基座（上对近、下对远的 X 形）+ 行进方向前引，
        /// 钳进合法区——锚永远落在躯干外侧，绝不折到中线上挤成一团
        /// </summary>
        private static Vector2 TravelAnchor(MLordContext ctx, NPC core, NPC hand, int slot, Vector2 travelDir) {
            float dir = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            int row = (int)hand.ai[MLordAiSlots.HandRow] == 1 ? 1 : 0;
            //步幅随档小幅前伸：多出来的距离是锚点寿命，高速下才不会锚比爪消耗得快
            float lead = MathHelper.Lerp(300f, 420f, ctx.MoveUrgency) + Tuning.StrideBonus;
            Vector2 outBase = new(dir * (row == 0 ? 350f : 410f), row == 0 ? -120f : 110f);
            Vector2 raw = core.Center + outBase + travelDir * lead;
            return ClampHandZone(core, hand, raw,
                MLordDirector.CrawlReachMin, MLordDirector.CrawlReachMax);
        }

        /// <summary>抓桩锚点：X 形四点张开（发射架姿态），钳进合法区</summary>
        private static Vector2 BraceAnchor(NPC core, NPC hand, int slot) {
            float dir = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            bool lowerRow = (int)hand.ai[MLordAiSlots.HandRow] == 1;
            Vector2 spread = lowerRow ? new Vector2(450f * dir, 150f) : new Vector2(480f * dir, -260f);
            return ClampHandZone(core, hand, core.Center + spread,
                MLordDirector.CrawlReachMin, MLordDirector.CrawlReachMax);
        }

        /// <summary>该手对应肩锚（与臂链 IK 同式）</summary>
        internal static Vector2 ShoulderOf(NPC core, NPC hand) {
            float dir = (int)hand.ai[MLordAiSlots.HandSide] == 0 ? -1f : 1f;
            Vector2 offset = (int)hand.ai[MLordAiSlots.HandRow] == 1
                ? MLordDirector.LowerShoulderOffset : MLordDirector.ShoulderOffset;
            return core.Center + new Vector2(offset.X * dir, offset.Y).RotatedBy(core.rotation);
        }

        /// <summary>点钳到以 center 为心的环带 [min, max]</summary>
        private static Vector2 ClampToAnnulus(Vector2 center, Vector2 point, float min, float max) {
            Vector2 delta = point - center;
            float len = delta.Length();
            if (len < 0.01f) {
                return center + new Vector2(0f, min);
            }
            if (len < min) {
                return center + delta / len * min;
            }
            if (len > max) {
                return center + delta / len * max;
            }
            return point;
        }

        #endregion
    }
}
