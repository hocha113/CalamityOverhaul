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
    /// 拉拽走"收缩曲线"：落爪先顿一拍（爪落身不动），随后该肢猛然收缩
    /// 拽动身体（拉力尖峰），再转入持续拖拽——每次窜动都归因于一只具体的手。
    /// 状态每帧经 <see cref="MLordContext.MovePolicy"/> 通道申报意图：
    /// Travel=爬行赶路，Tow=手阵携行（手全被征用的状态），Brace=四爪抓桩定身，
    /// Off=状态自管（演出/投技）。
    /// 相位/锚点写在手部 Override ai（随 netUpdate 同步），转移服务端裁定，
    /// 运动公式各端按同步数据镜像执行；三相期用存活手，
    /// 核心裸露期由四个残口实体原样充当爬行肢（保留断腕蠕动形态）
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
        /// <summary>松爪收势帧长</summary>
        private const int RecoverLen = 14;
        /// <summary>同时在途探爪上限（节肢交错步态）</summary>
        private const int MaxConcurrentReach = 2;
        /// <summary>新探爪起步的错拍间隔（有更年轻的在途探爪时不再起步）</summary>
        private const int StepStagger = 8;
        /// <summary>抓牢过久强制换点（死区休息时放宽）</summary>
        private const int PlantTimeout = 300;
        /// <summary>死区内抓牢的手过此帧数逐只松爪回巢</summary>
        private const int IdleReleaseTick = 240;
        /// <summary>跛行肢探爪超时宽限（拖得久，常在半途就地抓牢——瘸腿够不到落点）</summary>
        private const int LimpTimeoutGrace = 12;
        /// <summary>落爪后的顿帧：爪先落，身不动，随后才开始收缩（因果可读）</summary>
        private const int GripSettleFrames = 7;

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

        /// <summary>卸载/离开世界清空</summary>
        public static void Reset() {
            ownerWhoAmI = -1;
            mode = lastMode = LocoMode.Off;
            Array.Clear(prevPhase);
            PlantedCount = 0;
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
                return;
            }

            MLordMovePolicy policy = ctx.MovePolicy;
            Vector2 gap = ctx.MoveGoal - core.Center;
            Vector2 travelDir = gap.SafeNormalize(Vector2.UnitX);
            lameDesperation = policy == MLordMovePolicy.Travel
                ? MathHelper.Clamp((gap.Length() - 1100f) / 900f, 0f, 1f) : 0f;

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
                            if (tick > RecoverLen) {
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
                    //跛行肢排序惩罚：轮换里永远最后一个出爪
                    freeTicks[freeCount] = tick - (lame ? MLordDirector.LimpStepBias : 0);
                    freeCount++;
                }

                TickFx(hand, ov);
            }

            //―――― 起步调度（服务端）――――
            if (!VaultUtils.isClient && reachingCount < MaxConcurrentReach
                && youngestReachTick >= StepStagger && freeCount > 0) {
                bool wantStep = policy == MLordMovePolicy.Brace
                    || (policy == MLordMovePolicy.Travel && !inDeadZone);
                if (wantStep) {
                    //空闲最久者先出爪（免存储的左右交错来源）；
                    //本侧锚点落在行进后方的手不起步（横移时对侧手不做无效抓握），
                    //候选逐个验锚直至找到能供力的那只
                    for (int guard = 0; guard < freeCount; guard++) {
                        int pick = 0;
                        for (int i = 1; i < freeCount; i++) {
                            if (freeTicks[i] > freeTicks[pick]) {
                                pick = i;
                            }
                        }
                        int slot = freeSlots[pick];
                        //淘汰本候选（无论成败不再复选）
                        freeTicks[pick] = int.MinValue;
                        NPC hand = HandOf(ctx, slot);
                        MoonLordHandAI ov = MLordFacts.GetHandOverride(hand);
                        if (hand == null || ov == null) {
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
                        break;
                    }
                }
            }

            PlantedCount = plantedCount;
            UpdateBody(ctx, core, gap, travelDir, plantedCount);
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
                return;
            }

            float gapLen = gap.Length();
            float gapFactor = MathHelper.Clamp(gapLen / 300f, 0f, 1f);

            //各抓牢锚按收缩曲线牵引：只取行进向前方的分量（身后残锚不拖后腿），
            //拉力带方向与大小——身体明确偏向正在发力的那只手
            Vector2 pull = Vector2.Zero;
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                NPC hand = HandOf(ctx, slot);
                MoonLordHandAI ov = hand != null ? MLordFacts.GetHandOverride(hand) : null;
                if (ov == null || (int)ov.ai[MLordAiSlots.HandOvCrawlPhase] != MLordCrawlPhase.Planted) {
                    continue;
                }
                float contraction = ContractionCurve((int)ov.ai[MLordAiSlots.HandOvPhaseTick]);
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
            }

            if (pull.LengthSquared() > 0.0004f) {
                core.velocity += pull * (0.34f + 0.46f * urgency) * gapFactor;
            }

            core.velocity *= 0.945f;
            float cap = (3.5f + 8.5f * urgency) * (0.35f + 0.65f * gapFactor);
            if (core.velocity.Length() > cap) {
                core.velocity = core.velocity.SafeNormalize(Vector2.Zero) * cap;
            }
        }

        /// <summary>
        /// 收缩曲线（自落爪起的帧数）：顿帧零力→猛拽尖峰（easeOut 冲到 1.75）→
        /// 回落 1.0 持续拖拽。尖峰段就是身体窜动的那一下
        /// </summary>
        private static float ContractionCurve(int tick) {
            if (tick <= GripSettleFrames) {
                return 0f;
            }
            float t = tick - GripSettleFrames;
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
            float c = ContractionCurve((int)ov.ai[MLordAiSlots.HandOvPhaseTick]);
            return MathHelper.Clamp((c - 1f) / 0.75f, 0f, 1f);
        }

        /// <summary>拖曳携行：手阵抬着身体缓移（无步态）</summary>
        private static void UpdateTowBody(NPC core, Vector2 gap) {
            Vector2 want = gap * 0.028f;
            if (want.Length() > 4.6f) {
                want = want.SafeNormalize(Vector2.Zero) * 4.6f;
            }
            core.velocity = Vector2.Lerp(core.velocity, want, 0.12f);
        }

        #endregion

        #region 手部运动执行（手 AI 调用，各端镜像）

        /// <summary>
        /// 被征用手的运动：探爪冲刺 / 抓牢钉死 / 松爪收势。
        /// 跛行肢的探爪只有六成速，且带周期性卡顿（抬不起来又硬拽的节奏）。
        /// 单写者约定：手的速度只在手自己的 AI 里写，本方法即该写入点
        /// </summary>
        public static void ApplyHandMotion(NPC hand, int phase, Vector2 anchor) {
            switch (phase) {
                case MLordCrawlPhase.Reach: {
                    float cap = MLordDirector.CrawlReachSpeed;
                    float gain = 0.5f;
                    if (IsLameLimb(MLordFacts.GetCore(hand), hand)) {
                        //卡顿包络：|sin| 在拖滞与发力间摆动，PhaseTick 各端镜像自增；
                        //远距拼命时慢探惩罚同步淡出
                        MoonLordHandAI ov = MLordFacts.GetHandOverride(hand);
                        float tick = ov?.ai[MLordAiSlots.HandOvPhaseTick] ?? 0f;
                        float seize = 0.45f + 0.55f * Math.Abs((float)Math.Sin(tick * 0.42f));
                        float speedMul = MLordDirector.LimpSpeedFactor * seize;
                        cap *= MathHelper.Lerp(speedMul, 1f, lameDesperation);
                        gain = MathHelper.Lerp(0.34f, 0.5f, lameDesperation);
                    }
                    Vector2 want = (anchor - hand.Center) * 0.24f;
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
                default:
                    hand.velocity *= 0.84f;
                    break;
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
            float lead = MathHelper.Lerp(300f, 420f, ctx.MoveUrgency);
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
        private static Vector2 ShoulderOf(NPC core, NPC hand) {
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
