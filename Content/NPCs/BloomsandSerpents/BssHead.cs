using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Magic.BloomTomes;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Melee.Budcrowns;
using CalamityOverhaul.Content.Items.Melee.BudPiercers;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Ranged.Thornstrings;
using CalamityOverhaul.Content.Items.Summon.BloomCallers;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using EffectPriority = Terraria.Graphics.Effects.EffectPriority;
using Filter = Terraria.Graphics.Effects.Filter;
using SceneFilters = Terraria.Graphics.Effects.Filters;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>沙暴滤镜与环境暗化：头死亡/消失后无 AI 帧驱动淡出，这里兜底收场</summary>
    internal class BssStormSystem : BssModSystem
    {
        /// <summary>沙暴环境强度（本地表现量，头部每帧喂值，无头自然衰减）</summary>
        internal static float AmbientStorm;

        public override void PostUpdateNPCs() {
            if (Main.dedServ) {
                return;
            }
            bool anyHead = false;
            int headType = ModContent.NPCType<BssHead>();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == headType) {
                    anyHead = true;
                    break;
                }
            }
            if (!anyHead) {
                AmbientStorm = Math.Max(AmbientStorm - 0.01f, 0f);
                Filter filter = SceneFilters.Scene[BssHead.StormFilterName];
                if (filter != null && filter.IsActive() && AmbientStorm <= 0.02f) {
                    SceneFilters.Scene.Deactivate(BssHead.StormFilterName);
                }
            }
        }

        /// <summary>沙暴压迫：日色勒向尘沙的浑浊暖暗（镜像沙丘风暴氛围包，禁真黑屏）</summary>
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (AmbientStorm <= 0.01f) {
                return;
            }
            Color duskTile = new(150, 124, 86);
            Color duskBg = new(112, 90, 58);
            tileColor = Color.Lerp(tileColor, duskTile, AmbientStorm * 0.34f);
            backgroundColor = Color.Lerp(backgroundColor, duskBg, AmbientStorm * 0.52f);
        }

        public override void ClearWorld() {
            AmbientStorm = 0f;
        }
    }

    /// <summary>
    /// 荒花沙蟒头部主控：状态机 + 统一血池 + 爬行/钻沙/腾空三套身体语言 + 四足步态宿主。
    /// 身份：沙漠游龙，入场破土即掀起压场沙暴，贴地快爬直线压迫、蓄力后撤的冲刺与蹲伏扑击，
    /// 破空天游、盘天环猎、漩涡冲刺、回环沙瀑、回马甩尾撑起空中身段；沙丘柱三招（突刺/腾跃/爆震）
    /// 把沙漠本身立成场地；鳌足掘沙扬雨与合击、沙浪、沙鳍、龙卷、花刃、流沙、盘身刺阵作区域变化
    /// （招表见 <see cref="BssHubState"/>）。运动法则：原生 1 倍贴图、全链约 1704px，头是火车头——
    /// 寻的按转弯半径算、朝向每帧限速，颈段永远跟得上（见 <see cref="BssDirector.MinTurnRadius"/>）。
    /// 联机契约：转场只在权威端裁决（状态走 ai[3]），各端本地跑同一状态机做表现，
    /// 弹幕只在权威端生成，粒子音效全走 !dedServ 门，腿与鳌足是纯本地表现。
    /// </summary>
    [AutoloadBossHead]
    internal class BssHead : BssModNPC, ICWRLoader
    {
        #region 数据
        public override string Texture => CWRConstant.NPC + "BSS/Head";
        public override string BossHeadTexture => CWRConstant.NPC + "BSS/Head_Boss";

        /// <summary>贴图前方朝下的旋转修正（整张素材表统一约定）</summary>
        internal const float FacingRot = -MathHelper.PiOver2;

        //同步槽：ai[0]=统一血池上限 ai[1]=体节总数 ai[2]=阶段 ai[3]=状态机
        internal const int SlotUnifiedLifeMax = 0;
        internal const int SlotSegmentCount = 1;

        internal const string StormFilterName = "CalamityOverhaul:BssSandstorm";

        /// <summary>腿贴图（正式稿；上=基节腿节，下=胫爪。骨节绘制按长度拉伸，各槽共用不失真）</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/LegUpper")]
        internal static Asset<Texture2D> LegUpperAsset = null;
        [VaultLoaden(CWRConstant.NPC + "BSS/LegLower")]
        internal static Asset<Texture2D> LegLowerAsset = null;
        /// <summary>爪尖微节贴图（共用胫爪稿；要专属稿时改回独立路径并补 png）</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/LegLower")]
        internal static Asset<Texture2D> LegClawAsset = null;
        /// <summary>鳌足三件（源图朝向入库，关节像素与骨轴角记在 <see cref="BssClawRig"/>）：上臂 70×34</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/ClawUpper")]
        internal static Asset<Texture2D> ClawUpperAsset = null;
        /// <summary>鳌足下臂 48×18</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/ClawLower")]
        internal static Asset<Texture2D> ClawLowerAsset = null;
        /// <summary>鳌足掌 58×66</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/ClawChela")]
        internal static Asset<Texture2D> ClawChelaAsset = null;
        /// <summary>左颚 52×76（铰链在根部顶边，见 <see cref="BssJawDraw"/>）</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/JawLeft")]
        internal static Asset<Texture2D> JawLeftAsset = null;
        /// <summary>右颚 52×76</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/JawRight")]
        internal static Asset<Texture2D> JawRightAsset = null;

        private NpcStateMachine<BssStateContext> stateMachine;
        internal BssStateContext Context { get; private set; }
        private Player targetPlayer;
        /// <summary>四足步态（本地表现）</summary>
        internal BssLegRig LegRig { get; } = new();
        /// <summary>鳌足螳臂（本地表现）</summary>
        internal BssClawRig ClawRig { get; } = new();
        /// <summary>滤镜平滑包络（本地）</summary>
        private float stormSmooth;
        /// <summary>颚开合平滑（本地跟手，从已同步指令推导）</summary>
        private float jawSmooth = 0.42f;
        /// <summary>远距滞留帧</summary>
        private int farTimer;
        /// <summary>联机运动：客户端位置纠偏 + 状态计时收养</summary>
        private readonly BossNetMotion netMotion = new();
#if DEBUG
        /// <summary>上一帧速度（航向突变探针用）</summary>
        private Vector2 probeVelocity;
#endif

        internal BssStateIndex CurrentStateIndex => (BssStateIndex)(int)NPC.ai[3];
        #endregion

        #region 定义
        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            SceneFilters.Scene[StormFilterName] = new Filter(
                new ScreenShaderData("FilterMiniTower")
                    .UseColor(0.66f, 0.5f, 0.24f)
                    .UseOpacity(0.2f),
                EffectPriority.High);
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MustAlwaysDraw[Type] = true;
            NPCID.Sets.DontDoHardmodeScaling[Type] = true;
            NPCID.Sets.TrailingMode[Type] = 1;
            NPCID.Sets.TrailCacheLength[Type] = 10;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
        }

        public override void SetDefaults() {
            //判定盒写贴图尺度的原值，缩放交给原版 SetDefaults 收尾的 width/height × scale（与脓蕾沙蟒同一写法）
            NPC.width = 56;
            NPC.height = 56;
            NPC.scale = BssDirector.BodyScale;
            NPC.damage = BssDirector.HeadContact;
            NPC.defense = BssDirector.HeadDefense;
            NPC.lifeMax = BssDirector.HeadLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.npcSlots = 12f;
            NPC.alpha = 255;
            NPC.value = Item.buyPrice(0, 3);
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath5;
            Music = MusicID.Sandstorm;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Desert,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.BssHead.Bestiary"),
            ]);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //专家：宝藏袋；普通：同池直掉（与袋共享一张掉落表，袋内材料量更足）
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<BssTreasureBag>()));
            LeadingConditionRule notExpert = new(new Conditions.NotExpert());
            RegisterSharedLoot(rule => notExpert.OnSuccess(rule), expert: false);
            npcLoot.Add(notExpert);
        }

        /// <summary>
        /// 共享掉落池（普通直掉与专家袋同表）：荒花兵装五取一保底 + 荒漠沙器四取一保底 +
        /// 沙中曲彩头 + 沙漠材料（蚁狮颚是沙器线与花蕾配方共同的瓶颈，一次击杀够换一把）
        /// </summary>
        internal static void RegisterSharedLoot(Action<IItemDropRule> add, bool expert) {
            //荒花兵装五件套：沙蟒的签名武器，每次必出一把
            add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<BudPiercer>(),
                ModContent.ItemType<Thornstring>(),
                ModContent.ItemType<BloomCaller>(),
                ModContent.ItemType<Budcrown>(),
                ModContent.ItemType<BloomTome>()));
            //荒漠沙器四件套：每次必出一把。这四把原本只在灾厄荒漠灾虫身上，
            //挂到这里之后无灾厄环境也有正经来源（原有的无灾厄合成配方仍留作保底）
            add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<SandDagger>(),
                ModContent.ItemType<WastelandFang>(),
                ModContent.ItemType<UnderTheSand>(),
                ModContent.ItemType<DuneStalker>()));
            //沙中曲：小沙龙卷与本体的旋沙冲同源，越级一档，压低概率当额外彩头
            add(ItemDropRule.Common(ModContent.ItemType<MelodyTheSand>(), 4));

            add(ItemDropRule.Common(ItemID.AntlionMandible, 1, expert ? 12 : 8, expert ? 20 : 14));
            add(ItemDropRule.Common(ItemID.FossilOre, 1, expert ? 18 : 12, expert ? 30 : 20));
            add(ItemDropRule.Common(ItemID.SandBlock, 1, expert ? 60 : 40, expert ? 100 : 70));
            add(ItemDropRule.Common(ItemID.Cactus, 1, expert ? 30 : 20, expert ? 60 : 40));
            add(ItemDropRule.Common(ItemID.Amber, expert ? 2 : 3, 1, expert ? 3 : 2));
        }

        public override void OnKill() {
            //击杀旗标：SetEventFlagCleared 自动处理联机 WorldData 广播（镜像脓蕾沙蟒）
            NPC.SetEventFlagCleared(ref BssWorldFlag.DownedBloomSerpent, -1);
        }
        #endregion

        #region 状态机装配
        private void InitializeStateMachine() {
            Context = new BssStateContext {
                Npc = NPC,
                Owner = this,
            };
            stateMachine = new NpcStateMachine<BssStateContext>(Context);

            //换态包带的是新态的计时：客户端在框架换态（新实例 OnEnter 刚清零）之后立刻收养
            stateMachine.OnStateChanged += (_, next, _) => {
                if (VaultUtils.isClient && next is BssStateBase entered
                    && netMotion.TryTakeTiming(entered.StateId, out int timer, out int counter)) {
                    entered.AdoptNetTiming(timer, counter);
                }
            };

            //中途加入的客户端从 ai[3] 恢复状态
            if (VaultUtils.isClient) {
                int syncedIndex = (int)NPC.ai[3];
                IVaultState<BssStateContext> synced = VaultStateRegistry<BssStateContext>.Create(syncedIndex);
                stateMachine.SetInitialState(synced ?? new BssIntroState());
            }
            else {
                stateMachine.SetInitialState(new BssIntroState());
            }
        }
        #endregion

        #region 主 AI
        public override void AI() {
            if (stateMachine == null || Context == null) {
                InitializeStateMachine();
            }

            bool client = VaultUtils.isClient;
            if (client) {
                //自接位置纠偏：冲刺 30~50 px/f，命中驱动的高频快照下原版 netOffset 平滑只会锯齿，
                //而且整链由头集中绘制、体节读的是原始坐标，头带着平滑偏移就会与颈段错开
                netMotion.BeginFrame(NPC);
                //蛇行相位是持久累加量，必须在消费它的 SteerMovement 之前收养
                if (netMotion.TakeSway(out float sway)) {
                    Context.SlitherPhase = sway;
                }
                //同态收包：让本帧的拍点从权威端的计时起算
                if (stateMachine?.CurrentState is BssStateBase adopting
                    && netMotion.TryTakeTiming(adopting.StateId, out int timer, out int counter)) {
                    adopting.AdoptNetTiming(timer, counter);
                }
            }

            NPC.dontTakeDamage = false;
            NPC.damage = 0;

            FindTarget();
            UpdateContextFacts();
            EvaluateGlobalTransitions();

            Context.BeginFrameDefaults();
            stateMachine.Update();

            //步态时钟：腿的划桨排程与爬行涌动/贴地呼吸共读一拍（全端同算；
            //任何状态都推进，速率取全速度模长——竖直攀升/俯冲时腿照样有划水节奏）
            Context.GaitPhase += BssStateContext.GaitIncrement(NPC.velocity.Length());
            ApplyDeclaredMovement();

            //肌肉波自动触发（各端从同步速度同算）：一帧提速 >18 = 出手释放波；
            //高速骤降 = 急刹追压波（波龄门防刹车段逐帧重触，也防覆盖状态同帧的显式波）
            float speedNow = NPC.velocity.Length();
            if (speedNow - Context.HeadSpeed > 18f && Context.GapWaveAge >= 1f) {
                Context.PulseGapWave(SerpentChainMath.WaveRelease, 0.12f);
            }
            else if (Context.HeadSpeed > 22f && speedNow < Context.HeadSpeed * 0.72f
                && Context.GapWaveAge > 24f) {
                Context.PulseGapWave(SerpentChainMath.WavePress, 0.15f);
            }
            Context.HeadSpeed = speedNow;

#if DEBUG
            //航向突变探针：高速中单帧航向跳变 >0.5 rad = 漏网折角（调参期从日志追查来源状态）
            if (probeVelocity.LengthSquared() > 400f && NPC.velocity.LengthSquared() > 400f) {
                float jump = Math.Abs(MathHelper.WrapAngle(
                    NPC.velocity.ToRotation() - probeVelocity.ToRotation()));
                if (jump > 0.5f) {
                    Mod.Logger.Debug($"BssHead 航向突变 {jump:F2} rad，状态 {CurrentStateIndex}，速度 {NPC.velocity.Length():F1}");
                }
            }
            probeVelocity = NPC.velocity;
#endif

            if (Main.GameUpdateCount % 45 == 0 || Context.Segments.Count == 0) {
                Context.RefreshSegments();
            }
            if (!Main.dedServ) {
                LegRig.Update(Context);
                ClawRig.Update(Context);
                float jawTarget = BssJawDraw.ResolveOpen(Context.JawCommand, Context.JawPhase, Context.JawBurst,
                    Context.ClawCommand, Context.ClawPhase, Context.ClawBurst, Context.BreathPhase);
                jawSmooth = MathHelper.Lerp(jawSmooth, jawTarget,
                    BssJawDraw.SnapRate(Context.JawCommand, Context.ClawCommand));
            }

            //沙暴底线：入场破土即掀起、全程压场（各端从同步的 Phase 推导，确定性）；
            //入场自管爬升，死亡/撤离让位给演出退场
            if (CurrentStateIndex is not BssStateIndex.Intro and not BssStateIndex.Death and not BssStateIndex.Despawn) {
                float stormFloor = BssDirector.StormFloor(Context.Phase);
                if (Context.StormLevel < stormFloor) {
                    Context.StormLevel = MathHelper.Clamp(Context.StormLevel + 0.012f, 0f, stormFloor);
                }
            }

            UpdateStormPresentation();
            FarReturnValve();
            SyncSlots();

            if (Context.AttackCooldown > 0) {
                Context.AttackCooldown--;
            }

            //入场演出自管 alpha，其余状态兜底淡入（中途加入的客户端以 255 重建）
            if (stateMachine?.CurrentState is not BssIntroState && NPC.alpha > 0) {
                NPC.alpha = Math.Max(NPC.alpha - 42, 0);
            }

            if (client) {
                netMotion.EndFrame(NPC);
            }
            else if (Main.GameUpdateCount % BossNetMotion.HeartbeatFrames == 0) {
                //决策点（换态/回归瞬移/出手锁向/命中）各自 netUpdate，这里只留慢频兜底心跳
                NPC.netUpdate = true;
            }
        }

        /// <summary>权威端：把当前状态计时写进快照，与位置/速度原子过线</summary>
        public override void SendExtraAI(BinaryWriter writer) {
            int stateId = -1;
            int timer = 0;
            int counter = 0;
            if (stateMachine?.CurrentState is BssStateBase state) {
                stateId = state.StateId;
                timer = state.Timer;
                counter = state.Counter;
            }
            BossNetMotion.WriteTiming(writer, stateId, timer, counter, Context?.SlitherPhase ?? 0f);
        }

        /// <summary>客户端收包：position/velocity/ai 已是服务端值，据计时差纠偏，计时留给收养</summary>
        public override void ReceiveExtraAI(BinaryReader reader) {
            int localStateId = -1;
            int localTimer = 0;
            if (stateMachine?.CurrentState is BssStateBase state) {
                localStateId = state.StateId;
                localTimer = state.Timer;
            }
            netMotion.ReceiveTiming(reader, NPC, localStateId, localTimer);
        }

        private void FindTarget() {
            if (NPC.target < 0 || NPC.target >= 255 || !Main.player[NPC.target].Alives()) {
                NPC.TargetClosest();
            }
            targetPlayer = Main.player[NPC.target];
            if (TargetInvalid()) {
                NPC.TargetClosest();
                targetPlayer = Main.player[NPC.target];
            }
        }

        internal bool TargetInvalid() {
            return targetPlayer == null || targetPlayer.dead || !targetPlayer.active
                || Math.Abs(NPC.position.X - targetPlayer.position.X) > BssDirector.MaxFindDistance
                || Math.Abs(NPC.position.Y - targetPlayer.position.Y) > BssDirector.MaxFindDistance;
        }

        private void UpdateContextFacts() {
            Context.Npc = NPC;
            Context.Target = targetPlayer;
            Context.Owner = this;
            Context.MasterMode = Main.masterMode;
            Context.TotalSegments = (int)NPC.ai[SlotSegmentCount] > 0
                ? (int)NPC.ai[SlotSegmentCount] : Context.Segments.Count;
        }

        /// <summary>全局转移，仅权威端驱动；入场/转阶段/死亡/撤离中不打断</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }
            IVaultState<BssStateContext> current = stateMachine.CurrentState;
            if (current is BssIntroState or BssStormTransitionState or BssApexBloomState
                or BssDespawnState or BssDeathState) {
                return;
            }

            //血线见底：死亡演出（清弹、锁血、逐腿失力）
            if (NPC.life <= BssDirector.DeathTriggerLife && NPC.ai[SlotUnifiedLifeMax] > 0
                && !Context.DeathPerformanceFinished) {
                stateMachine.ChangeState(new BssDeathState());
                return;
            }

            //目标失效：钻沙遁走
            if (TargetInvalid()) {
                stateMachine.ChangeState(new BssDespawnState());
                return;
            }

            //转阶段演出以立起为主体，头埋在沙里时起演会整场看不见：等出面再转
            //（钻地类招式都有超时兜底，最迟几十帧内必出面）
            if (!BssVfx.IsAboveGround(NPC.Center)) {
                return;
            }

            //60%：沙暴转阶段
            if (Context.Phase == 1 && NPC.life <= NPC.lifeMax * BssDirector.StormThreshold) {
                stateMachine.ChangeState(new BssStormTransitionState());
                return;
            }

            //25%：繁花怒放
            if (Context.Phase == 2 && NPC.life <= NPC.lifeMax * BssDirector.ApexThreshold) {
                stateMachine.ChangeState(new BssApexBloomState());
            }
        }

        /// <summary>锁血：死亡演出没放完不许真死，一击超杀也拦回演出</summary>
        public override bool CheckDead() {
            if (Context != null && !Context.DeathPerformanceFinished) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not BssDeathState) {
                    stateMachine?.ChangeState(new BssDeathState());
                }
                return false;
            }
            return true;
        }

        public override bool CheckActive() => false;
        #endregion

        #region 运动
        /// <summary>
        /// 把状态声明的运动模式落到速度与旋转上。头的朝向在所有模式下统一经
        /// <see cref="TurnHead"/> 限速：一帧翻身在 1700px 的身体上就是甩颈，
        /// 状态声明了 <see cref="BssStateContext.AimAngle"/> 就盯它，否则跟速度方向。
        /// </summary>
        private void ApplyDeclaredMovement() {
            switch (Context.Mode) {
                case BssMoveMode.Crawl:
                    ApplyCrawl();
                    break;
                case BssMoveMode.Steer: {
                    float phase = Context.SlitherPhase;
                    SteerMovement(NPC, Context.MoveTarget, Context.MoveSpeed,
                        Context.TurnRadius, Context.AccelRate, Context.Slither, ref phase);
                    Context.SlitherPhase = phase;
                    break;
                }
                case BssMoveMode.Direct:
                    if (!float.IsNaN(Context.AimAngle)) {
                        TurnHead(Context.AimAngle, 0.35f);
                    }
                    else if (NPC.velocity.LengthSquared() > 0.2f) {
                        TurnHead(NPC.velocity.ToRotation(), 0.6f);
                    }
                    break;
                default:
                    //未声明：指数刹停，绝不留残余速度漂移；声明了瞄准仍让头慢慢看过去
                    NPC.velocity *= 0.9f;
                    if (!float.IsNaN(Context.AimAngle)) {
                        TurnHead(Context.AimAngle, 0.25f);
                    }
                    break;
            }
        }

        /// <summary>
        /// 头部转向共件：先按 lerp 因子缓动，再把单帧步长钳到 <see cref="BssDirector.HeadTurnRateMax"/>。
        /// 缓动给收尾的柔，限速给大身体的重——180° 掉头至少 14 帧，颈段每帧最多跟转 4°。
        /// </summary>
        private void TurnHead(float aimAngle, float lerp) {
            float target = aimAngle + FacingRot;
            float eased = NPC.rotation.AngleLerp(target, lerp);
            NPC.rotation = NPC.rotation.AngleTowards(eased, BssDirector.HeadTurnRateMax);
        }

        /// <summary>
        /// 耙沙拍相位修正：push 峰值压在呼吸相位的功率段中点（≈0.9 弧度）。
        /// 向左走时走地排换到反相侧（时钟槽差 π），由 ApplyCrawl 给 bodyPhase 补相。
        /// </summary>
        private static readonly float PushAlignPhase = MathHelper.PiOver2 - 0.9f;

        /// <summary>
        /// 蜈蚣爬行：沿地形等高线推进。全身起伏与推进涌动读呼吸相位（步态时钟的低频，
        /// 八腿换步是快拍，身体起伏是慢拍，两者同源不同频）：功率段身体微抬加速、
        /// 恢复段回沉滑行。中速档幅度收敛，身体读作沉稳地被腿托着走，不是上下颠簸。
        /// 急转向时甩一记鞭链行波。
        /// </summary>
        private void ApplyCrawl() {
            float dir = Math.Sign(Context.CrawlDirX);
            if (dir == 0f) {
                dir = 1f;
            }

            //急转检测：行进中掉头 = 鞭波 + 短暂盘紧
            if (Math.Abs(NPC.velocity.X) > 4f && Math.Sign(NPC.velocity.X) != dir) {
                Context.PulseWhip(6f);
                Context.Compression = Math.Min(Context.Compression, 0.94f);
            }

            Vector2 probe = NPC.Center + new Vector2(dir * BssDirector.CrawlLookahead, -150f);
            float groundY = BssVfx.FindGroundY(probe, 1400f);
            float desiredY = groundY - BssDirector.CrawlRideHeight;

            float speedNow = Math.Abs(NPC.velocity.X);
            float bodyPhase = Context.BreathPhase + (dir < 0f ? MathHelper.Pi : 0f);
            //波幅随速：静止近乎不动，爬起来有沉稳的起伏
            float waveAmp = 2f + MathHelper.Clamp(speedNow * 0.5f, 0f, 6f);
            desiredY += MathF.Sin(bodyPhase) * waveAmp;

            //耙沙拍：尖锐脉冲（pow3），身体高度与推进都骑在这一拍上
            float push = MathF.Pow(Math.Max(0f, MathF.Sin(bodyPhase + PushAlignPhase)), 3f);
            desiredY -= push * MathHelper.Clamp(2f + speedNow * 0.2f, 0f, 5f);

            //步频涌动：推进速度围绕目标值脉动（腿在发力的读数）
            float stridePulse = 0.9f + 0.2f * push;
            float vx = MathHelper.Lerp(NPC.velocity.X, dir * Context.CrawlSpeed * stridePulse, 0.1f);
            float vy = MathHelper.Clamp((desiredY - NPC.Center.Y) * 0.1f, -10f, 10f);
            NPC.velocity = new Vector2(vx, vy);

            //行进间攻击：声明了瞄准角就让头看目标，身体继续爬
            if (!float.IsNaN(Context.AimAngle)) {
                TurnHead(Context.AimAngle, 0.25f);
            }
            else if (NPC.velocity.LengthSquared() > 0.2f) {
                TurnHead(NPC.velocity.ToRotation(), 0.18f);
            }
        }

        /// <summary>
        /// 蠕虫寻的转向物理（钻沙/腾空段；镜像世吞重制，旋转改朝下贴图约定）。
        /// 转向量按航迹半径计：每帧最大转角 = 速度 / 转弯半径，半径不低于
        /// <see cref="BssDirector.MinTurnRadius"/>（颈段能弯的极限）。旧式"低速灵巧高速迟钝"的
        /// 角速度公式在低速时允许每帧 0.4 弧度，头原地打转、1700px 的身体跟着甩——按半径算
        /// 之后头走的是火车头的弧线，身体自然铺在弧上。
        /// </summary>
        internal static void SteerMovement(NPC worm, Vector2 targetPos, float moveSpeed,
            float turnRadius, float accelRate, float slither, ref float slitherPhase) {
            Vector2 toTarget = targetPos - worm.Center;
            float distance = toTarget.Length();
            if (distance < 0.01f || moveSpeed <= 0.01f) {
                return;
            }

            float desiredHeading = toTarget.ToRotation();
            float currentSpeed = worm.velocity.Length();
            float currentHeading = currentSpeed > 0.01f ? worm.velocity.ToRotation() : desiredHeading;

            //航迹曲率约束：转角 = 弧长 / 半径；近停时给角速度地板，能慢慢重新对准
            float radius = Math.Max(turnRadius, 1f);
            float maxTurn = Math.Max(currentSpeed / radius, BssDirector.MinTurnRate);
            float newHeading = currentHeading.AngleTowards(desiredHeading, maxTurn);
            float speedFactor = MathHelper.Clamp(currentSpeed / 26f, 0f, 1f);

            //入弯收油出弯全速
            float headingError = Math.Abs(MathHelper.WrapAngle(desiredHeading - newHeading));
            float throttle = MathHelper.Lerp(1f, 0.6f, MathHelper.Clamp(headingError / MathHelper.Pi, 0f, 1f));
            float targetSpeed = moveSpeed * throttle;
            float accel = accelRate;

            if (distance > 1300f) {
                float catchUp = Math.Min(distance / 60f, 42f);
                targetSpeed = Math.Max(targetSpeed, catchUp);
                accel = Math.Max(accel, 0.09f);
            }

            currentSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, accel);

            //相位取模保住浮点精度，也让它在快照里是个有界值（sin 周期性，行为不变）
            if (slither > 0.01f) {
                slitherPhase += 0.075f + currentSpeed * 0.0016f;
                if (slitherPhase > MathHelper.TwoPi) {
                    slitherPhase -= MathHelper.TwoPi;
                }
                float wave = MathF.Sin(slitherPhase);
                newHeading += wave * 0.3f * slither * MathHelper.Lerp(0.5f, 1f, speedFactor);
            }

            worm.velocity = newHeading.ToRotationVector2() * currentSpeed;
            worm.rotation = worm.rotation.AngleTowards(worm.velocity.ToRotation() + FacingRot, BssDirector.HeadTurnRateMax);
        }

        /// <summary>远距回归：钻地瞬移回场（土遁身份），仅允许的状态生效</summary>
        private void FarReturnValve() {
            if (stateMachine?.CurrentState is not BssStateBase state || !AllowFarSnap(state)) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives() || VaultUtils.isClient) {
                farTimer = 0;
                return;
            }
            if (NPC.Distance(targetPlayer.Center) <= BssDirector.FarSnapDistance) {
                farTimer = 0;
                return;
            }
            if (++farTimer < 30) {
                return;
            }
            farTimer = 0;

            Vector2 ground = new(targetPlayer.Center.X, BssVfx.FindGroundY(targetPlayer.Center));
            int side = Math.Sign(NPC.Center.X - targetPlayer.Center.X);
            if (side == 0) {
                side = 1;
            }
            NPC.Center = ground + new Vector2(side * 640f, 480f);
            NPC.velocity = new Vector2(-side * 7f, -20f);
            NPC.rotation = NPC.velocity.ToRotation() + FacingRot;
            NPC.netUpdate = true;
            BssVfx.SandBurst(ground + new Vector2(side * 640f, 0f), 1.2f);
        }

        //入场/转阶段/死亡/撤离不进回归阀（演出不许被瞬移打断）；
        //流沙、盘身刺阵、漩涡冲刺、回环沙瀑、沙柱腾跃/爆震按锁定圆心/锚点/柱做参数化运动，
        //瞬移会把几何撕烂，也不进（各自带超时）；其余战斗态各招自带超时兜底，收招回 hub 后自然触发回归
        private static bool AllowFarSnap(BssStateBase state) {
            return state is BssHubState or BssBurrowLungeState or BssSandSpitState
                or BssCactusBallState or BssNeedleRippleState or BssPetalShakeState
                or BssSandDashState or BssPounceState or BssGeyserMarchState
                or BssClawFlingState or BssSandSurgeState or BssFinHuntState
                or BssDustDevilState or BssPincerSnapState or BssWindBladeState
                or BssSkyWeaveState or BssCoilOrbitState or BssTailSweepState
                or BssPillarSpikeState;
        }
        #endregion

        #region 体节与血池
        /// <summary>生成体节链并汇总统一血池（权威端，入场破土帧调用）</summary>
        internal static void SpawnBodySegments(NPC headNpc) {
            int totalLife = headNpc.lifeMax;
            int frontIndex = headNpc.whoAmI;
            int bodyType = ModContent.NPCType<BssBody>();
            int tailType = ModContent.NPCType<BssTail>();

            for (int i = 0; i <= BssDirector.BodyCount; i++) {
                bool isTail = i == BssDirector.BodyCount;
                int index = NPC.NewNPC(headNpc.FromObjectGetParent(), (int)headNpc.Center.X, (int)headNpc.Center.Y,
                    isTail ? tailType : bodyType,
                    0, ai0: i, ai1: frontIndex, ai2: 0, ai3: headNpc.whoAmI);
                if (index >= Main.maxNPCs) {
                    break;
                }
                Main.npc[index].realLife = headNpc.whoAmI;
                totalLife += Main.npc[index].lifeMax;
                Main.npc[index].netUpdate = true;
                frontIndex = index;
            }

            headNpc.lifeMax = totalLife;
            headNpc.life = totalLife;
            headNpc.ai[SlotUnifiedLifeMax] = totalLife;
            headNpc.ai[SlotSegmentCount] = BssDirector.BodyCount + 1;
            headNpc.netUpdate = true;
        }

        /// <summary>各端按同步槽校正统一血池显示</summary>
        private void SyncSlots() {
            int total = (int)NPC.ai[SlotUnifiedLifeMax];
            if (total > 0 && NPC.lifeMax != total) {
                NPC.lifeMax = total;
                if (NPC.life > total) {
                    NPC.life = total;
                }
            }
        }

        /// <summary>远端玩家周期性强推基础数据（防长虫身位错漂，镜像世吞）</summary>
        internal static void ForcedNetUpdating(NPC npc) {
            if (!VaultUtils.isServer || !npc.active || Main.GameUpdateCount % 80 != 0) {
                return;
            }
            foreach (var findPlayer in Main.ActivePlayers) {
                if (findPlayer.Distance(npc.position) < 1440) {
                    continue;
                }
                npc.SendNPCbasicData(findPlayer.whoAmI);
            }
        }

        /// <summary>清场：撤离/死亡后清全部沙蟒 NPC（权威端）</summary>
        internal static void HandleDespawnAll() {
            int headType = ModContent.NPCType<BssHead>();
            int bodyType = ModContent.NPCType<BssBody>();
            int tailType = ModContent.NPCType<BssTail>();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == headType || n.type == bodyType || n.type == tailType) {
                    n.active = false;
                    n.netUpdate = true;
                    if (Main.dedServ) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n.whoAmI);
                    }
                }
            }
        }
        #endregion

        #region 表现
        /// <summary>沙暴表现：滤镜 + 环境暗化喂值 + 风沙粒子（客户端）</summary>
        private void UpdateStormPresentation() {
            if (Main.dedServ) {
                return;
            }
            float storm = MathHelper.Clamp(Context.StormLevel, 0f, 1f);
            BssStormSystem.AmbientStorm = Math.Max(BssStormSystem.AmbientStorm, storm);

            stormSmooth = MathHelper.Lerp(stormSmooth, storm, 0.05f);
            Filter filter = SceneFilters.Scene[StormFilterName];
            if (stormSmooth > 0.03f) {
                if (!filter.IsActive()) {
                    SceneFilters.Scene.Activate(StormFilterName, NPC.Center);
                }
                filter.GetShader().UseOpacity(0.3f * stormSmooth).UseTargetPosition(NPC.Center);
            }
            else if (filter.IsActive()) {
                SceneFilters.Scene.Deactivate(StormFilterName);
            }

            //全屏横风沙尘：贴地扬沙 + 空中平流沙痕 + 偶发大片沙幕（整场压场的天气，不是局部特效）
            if (storm > 0.05f && !Main.gamePaused) {
                float wind = Context.WindSign;
                int grains = storm > 0.85f ? 6 : storm > 0.4f ? 3 : 1;
                for (int i = 0; i < grains; i++) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    //从上风侧屏外进入，横穿整屏
                    Vector2 pos = Main.screenPosition + new Vector2(
                        Main.rand.NextFloat(-120f, Main.screenWidth + 120f),
                        Main.rand.NextFloat(-40f, Main.screenHeight + 20f));
                    float speed = 8f + 9f * storm;
                    bool sheet = Main.rand.NextBool(7);
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Sand,
                        new Vector2(wind * speed * Main.rand.NextFloat(0.7f, 1.15f), -Main.rand.NextFloat(0.1f, 0.8f)),
                        Main.rand.Next(sheet ? 150 : 90, sheet ? 200 : 140), default,
                        sheet ? Main.rand.NextFloat(1.7f, 2.4f) : Main.rand.NextFloat(0.8f, 1.35f));
                    dust.noGravity = true;
                    dust.fadeIn = sheet ? 1.2f : 0.4f;
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Sand, new Vector2(hit.HitDirection * Main.rand.NextFloat(1.5f, 4f), -Main.rand.NextFloat(1f, 3f)),
                    100, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
            if (Main.rand.NextBool(3)) {
                BssVfx.PetalDrift(NPC.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(hit.HitDirection * 1.2f, -1f));
            }
            if (NPC.life <= 0) {
                BssVfx.SandBurst(NPC.Center, 1.6f);
                for (int i = 0; i < 8; i++) {
                    BssVfx.PetalDrift(NPC.Center + Main.rand.NextVector2Circular(28f, 28f),
                        Main.rand.NextVector2Circular(2.5f, 2f));
                }
            }
        }

        /// <summary>
        /// 整链集中绘制（体节/尾自身 PreDraw 返回 false）：腿 → 远层鳌足 → 体节尾→头逐节压上 →
        /// 颚 → 头本体 → 近层鳌足。前节压后节：每节前端腹板塞进前一节扇冠之下，扇冠成为可见的"领子"；
        /// 交给 whoAmI 顺序会让 0 号节的腹板盖在头顶的白刺冠上。
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Context == null) {
                return true;
            }

            //腿画最底
            LegRig.Draw(spriteBatch, screenPos, Context);

            float clawFade = 1f - NPC.alpha / 255f;
            //远层鳌足：压暗垫在整链之下（深度读数）
            ClawRig.DrawBack(spriteBatch, screenPos, clawFade);

            //体节：尾先头后，前节盖住后节前端
            if (Context.Segments.Count == 0) {
                Context.RefreshSegments();
            }
            for (int i = Context.Segments.Count - 1; i >= 0; i--) {
                NPC seg = Context.Segments[i];
                if (seg.active && seg.ModNPC is BssBody body) {
                    body.DrawSegment(spriteBatch, screenPos, Context);
                }
            }

            Main.instance.LoadNPC(Type);
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            //落足下沉回弹：头随 0 号髋站（最前大腿）落步下沉，被腿撑着走的重量读数（纯绘制偏移）
            Vector2 mainPos = NPC.Center - screenPos + new Vector2(0f, Context.SampleStationBob(0f) * BssLegRig.StationDipPx);
            //出手帧反冲：释放波最初几帧头部向速度反向缩一记（绘制层，位置不动）
            if (Context.GapWaveKind == SerpentChainMath.WaveRelease && Context.GapWaveAge < 4f
                && NPC.velocity.LengthSquared() > 1f) {
                mainPos -= NPC.velocity.SafeNormalize(Vector2.Zero) * ((4f - Context.GapWaveAge) * 1.4f);
            }
            //全身抖动：与体节读同一通道（位置不动，纯绘制偏移；腿保持踩定）
            if (Context.ShakeStrength > 0.02f) {
                mainPos += new Vector2(
                    MathF.Sin(Main.GlobalTimeWrappedHourly * 61f + NPC.whoAmI),
                    MathF.Cos(Main.GlobalTimeWrappedHourly * 47f + NPC.whoAmI * 1.7f))
                    * (4f * Context.ShakeStrength);
            }
            float fade = 1f - NPC.alpha / 255f;

            //高速残影（速度门控，只在冲刺时出现）
            float speed = NPC.velocity.Length();
            float ghostIntensity = MathHelper.Clamp((speed - 14f) / 22f, 0f, 1f);
            if (ghostIntensity > 0.05f) {
                for (int i = NPC.oldPos.Length - 1; i >= 1; i -= 2) {
                    if (NPC.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)NPC.oldPos.Length;
                    Vector2 ghostPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    Color ghost = BssVfx.SandWarm with { A = 0 } * (0.2f * t * ghostIntensity * fade);
                    spriteBatch.Draw(texture, ghostPos, frameRec, ghost, NPC.rotation,
                        origin, NPC.scale * (0.92f + 0.08f * t), SpriteEffects.None, 0f);
                }
            }

            //颚先于本体：红根藏在头底之下，只露出两片刃
            float jawOpen = jawSmooth;
            Vector2 headWorld = mainPos + screenPos;
            BssJawDraw.Draw(spriteBatch, headWorld, NPC.rotation, jawOpen, drawColor * fade, screenPos, NPC.scale);

            //本体
            spriteBatch.Draw(texture, mainPos, frameRec, drawColor * fade, NPC.rotation,
                origin, NPC.scale, SpriteEffects.None, 0f);

            //怒放辉光：头顶花叶在预告/怒放期泛红（加色薄层，体感来自本体遮蔽）
            if (Context.BloomGlow > 0.03f) {
                Color bloom = BssVfx.BloomRed with { A = 0 } * (0.55f * Context.BloomGlow * fade);
                BssJawDraw.Draw(spriteBatch, headWorld, NPC.rotation, jawOpen, bloom, screenPos, NPC.scale);
                spriteBatch.Draw(texture, mainPos, frameRec, bloom, NPC.rotation,
                    origin, NPC.scale * 1.04f, SpriteEffects.None, 0f);
                Lighting.AddLight(NPC.Center, BssVfx.BloomRed.ToVector3() * 0.35f * Context.BloomGlow);
            }

            //近层鳌足：盖在头本体之上（螳臂在前的主剪影）
            ClawRig.DrawFront(spriteBatch, screenPos, clawFade);

            //怒吼声波环（入场破土/转阶段怒吼）：共享冲击环换沙色板，环心钉在点火位不随头走
            if (Context.RoarRingAge >= 0f) {
                float p = MathHelper.Clamp(Context.RoarRingAge / 46f, 0f, 1f);
                float radius = (1f - MathF.Pow(1f - p, 3f)) * 980f;
                float alpha = MathF.Pow(1f - p, 1.2f) * 0.9f;
                ShockRingDraw.Draw(spriteBatch, Context.RoarRingCenter, radius,
                    26f + 34f * p,
                    new Color(242, 214, 158), BssVfx.SandWarm, BssVfx.SandDark,
                    alpha, squish: 0.92f, innerGlow: 0.1f, timeSeed: NPC.whoAmI * 0.61f);
            }

            return false;
        }
        #endregion
    }
}
