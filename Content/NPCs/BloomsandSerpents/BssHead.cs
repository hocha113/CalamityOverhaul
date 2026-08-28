using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Melee.BudPiercers;
using CalamityOverhaul.Content.Items.Melee.Budcrowns;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Items.Ranged.Thornstrings;
using CalamityOverhaul.Content.Items.Summon.BloomCallers;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using CalamityOverhaul.Content.NPCs.BloomsandSerpents.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
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
            tileColor = Color.Lerp(tileColor, duskTile, AmbientStorm * 0.28f);
            backgroundColor = Color.Lerp(backgroundColor, duskBg, AmbientStorm * 0.42f);
        }

        public override void ClearWorld() {
            AmbientStorm = 0f;
        }
    }

    /// <summary>
    /// 荒花沙蟒头部主控：状态机 + 统一血池 + 爬行/钻沙双身体语言 + 四足步态宿主。
    /// 联机契约：转场只在权威端裁决（状态走 ai[3]），各端本地跑同一状态机做表现，
    /// 弹幕只在权威端生成，粒子音效全走 !dedServ 门，腿是纯本地表现。
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

        /// <summary>腿贴图（占位=尾节素材；用户腿贴图到位后覆盖同名 png 即换装）</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/LegUpper")]
        internal static Asset<Texture2D> LegUpperAsset = null;
        [VaultLoaden(CWRConstant.NPC + "BSS/LegLower")]
        internal static Asset<Texture2D> LegLowerAsset = null;

        private NpcStateMachine<BssStateContext> stateMachine;
        internal BssStateContext Context { get; private set; }
        private Player targetPlayer;
        /// <summary>四足步态（本地表现）</summary>
        internal BssLegRig LegRig { get; } = new();
        /// <summary>滤镜平滑包络（本地）</summary>
        private float stormSmooth;
        /// <summary>远距滞留帧</summary>
        private int farTimer;

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
            NPC.width = 46;
            NPC.height = 46;
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
            //荒花兵装四件套：沙蟒的签名武器，每次必出一把
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<BudPiercer>(),
                ModContent.ItemType<Thornstring>(),
                ModContent.ItemType<BloomCaller>(),
                ModContent.ItemType<Budcrown>()));
            //荒漠沙器四件套：每次必出一把。这四把原本只在灾厄荒漠灾虫身上，
            //挂到这里之后无灾厄环境也有正经来源（原有的无灾厄合成配方仍留作保底）
            npcLoot.Add(ItemDropRule.OneFromOptions(1,
                ModContent.ItemType<SandDagger>(),
                ModContent.ItemType<WastelandFang>(),
                ModContent.ItemType<UnderTheSand>(),
                ModContent.ItemType<DuneStalker>()));
            //沙中曲：小沙龙卷与本体的旋沙冲同源，越级一档，压低概率当额外彩头
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<MelodyTheSand>(), 4));

            //材料：蚁狮颚是沙器线与花蕾配方共同的瓶颈，一次击杀够换一把
            npcLoot.Add(ItemDropRule.Common(ItemID.AntlionMandible, 1, 8, 14));
            npcLoot.Add(ItemDropRule.Common(ItemID.FossilOre, 1, 12, 20));
            npcLoot.Add(ItemDropRule.Common(ItemID.SandBlock, 1, 40, 70));
            npcLoot.Add(ItemDropRule.Common(ItemID.Cactus, 1, 20, 40));
            npcLoot.Add(ItemDropRule.Common(ItemID.Amber, 3, 1, 2));
        }
        #endregion

        #region 状态机装配
        private void InitializeStateMachine() {
            Context = new BssStateContext {
                Npc = NPC,
                Owner = this,
            };
            stateMachine = new NpcStateMachine<BssStateContext>(Context);

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

            if (Main.GameUpdateCount % 45 == 0 || Context.Segments.Count == 0) {
                Context.RefreshSegments();
            }
            if (!Main.dedServ) {
                LegRig.Update(Context);
            }

            //阶段驱动的沙暴底线（各端从同步的 Phase 推导，确定性）；死亡/撤离让位给演出退场
            if (CurrentStateIndex is not BssStateIndex.Death and not BssStateIndex.Despawn) {
                float stormFloor = Context.Phase >= 3 ? 1f : Context.Phase == 2 ? 0.72f : 0f;
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

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                NPC.netUpdate = true;
            }
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
        /// <summary>把状态声明的运动模式落到速度与旋转上</summary>
        private void ApplyDeclaredMovement() {
            switch (Context.Mode) {
                case BssMoveMode.Crawl:
                    ApplyCrawl();
                    break;
                case BssMoveMode.Steer: {
                    float phase = Context.SlitherPhase;
                    SteerMovement(NPC, Context.MoveTarget, Context.MoveSpeed,
                        Context.TurnSpeed, Context.AccelRate, Context.Slither, ref phase);
                    Context.SlitherPhase = phase;
                    break;
                }
                case BssMoveMode.Direct:
                    if (NPC.velocity.LengthSquared() > 0.2f) {
                        NPC.rotation = NPC.velocity.ToRotation() + FacingRot;
                    }
                    break;
                default:
                    //未声明：指数刹停，绝不留残余速度漂移
                    NPC.velocity *= 0.9f;
                    break;
            }
        }

        /// <summary>
        /// 耙沙拍相位修正：0 号髋站走地腿的抓地拍在 GaitPhase ≡ 0 (mod 2π)，
        /// 功率段约占 [0, PowerFraction·2π]；push 峰值压在功率段中点（≈0.9 弧度）。
        /// 向左走时走地排换到反相侧（时钟槽差 π），由 ApplyCrawl 给 bodyPhase 补相。
        /// </summary>
        private static readonly float PushAlignPhase = MathHelper.PiOver2 - 0.9f;

        /// <summary>
        /// 蜈蚣爬行：沿地形等高线推进。全身起伏与推进涌动读步态时钟（与腿的划桨
        /// 周期同源同拍）：耙沙功率段身体微抬加速、恢复段回沉滑行——"耙一记、滑一段"。
        /// 急转向时甩一记鞭链行波。
        /// </summary>
        private void ApplyCrawl() {
            float dir = Math.Sign(Context.CrawlDirX);
            if (dir == 0f) {
                dir = 1f;
            }

            //急转检测：行进中掉头 = 鞭波 + 短暂盘紧
            if (Math.Abs(NPC.velocity.X) > 5f && Math.Sign(NPC.velocity.X) != dir) {
                Context.PulseWhip(7f);
                Context.Compression = Math.Min(Context.Compression, 0.93f);
            }

            Vector2 probe = NPC.Center + new Vector2(dir * BssDirector.CrawlLookahead, -150f);
            float groundY = BssVfx.FindGroundY(probe, 1400f);
            float desiredY = groundY - BssDirector.CrawlRideHeight;

            float speedNow = Math.Abs(NPC.velocity.X);
            //身体节律 = 步态时钟全频（划桨周期本身从容）；向左走时走地排换侧（槽差 π）补相
            float bodyPhase = Context.GaitPhase + (dir < 0f ? MathHelper.Pi : 0f);
            //波幅随速：静止近乎不动，快爬时全身起伏（灵动的来源）
            float waveAmp = 3f + MathHelper.Clamp(speedNow * 0.85f, 0f, 11f);
            desiredY += MathF.Sin(bodyPhase) * waveAmp;

            //耙沙拍：尖锐脉冲（pow3），相位对齐 0 号髋站功率段中点（每个划桨周期耙一记）
            float push = MathF.Pow(Math.Max(0f, MathF.Sin(bodyPhase + PushAlignPhase)), 3f);
            //耙沙身体微抬、恢复段回沉：身体高度骑在腿的节拍上
            desiredY -= push * MathHelper.Clamp(3f + speedNow * 0.25f, 0f, 9f);

            //步频涌动：推进速度围绕目标值脉动（与贴地呼吸同拍 = 腿在发力的读数）
            float stridePulse = 0.84f + 0.30f * push;
            float vx = MathHelper.Lerp(NPC.velocity.X, dir * Context.CrawlSpeed * stridePulse, 0.12f);
            float vy = MathHelper.Clamp((desiredY - NPC.Center.Y) * 0.1f, -12f, 12f);
            NPC.velocity = new Vector2(vx, vy);

            //行进间攻击：声明了瞄准角就让头看目标，身体继续爬
            if (!float.IsNaN(Context.AimAngle)) {
                NPC.rotation = NPC.rotation.AngleLerp(Context.AimAngle + FacingRot, 0.25f);
            }
            else if (NPC.velocity.LengthSquared() > 0.2f) {
                NPC.rotation = NPC.rotation.AngleLerp(NPC.velocity.ToRotation() + FacingRot, 0.18f);
            }
        }

        /// <summary>蠕虫寻的转向物理（钻沙/腾空段；镜像世吞重制，旋转改朝下贴图约定）</summary>
        internal static void SteerMovement(NPC worm, Vector2 targetPos, float moveSpeed,
            float turnSpeed, float accelRate, float slither, ref float slitherPhase) {
            Vector2 toTarget = targetPos - worm.Center;
            float distance = toTarget.Length();
            if (distance < 0.01f || moveSpeed <= 0.01f) {
                return;
            }

            float desiredHeading = toTarget.ToRotation();
            float currentSpeed = worm.velocity.Length();
            float currentHeading = currentSpeed > 0.01f ? worm.velocity.ToRotation() : desiredHeading;

            //转向随速衰减：低速灵巧高速迟钝
            float speedFactor = MathHelper.Clamp(currentSpeed / 26f, 0f, 1f);
            float maxTurn = turnSpeed / 20f * MathHelper.Lerp(2.0f, 0.72f, speedFactor);
            float newHeading = currentHeading.AngleTowards(desiredHeading, maxTurn);

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

            if (slither > 0.01f) {
                slitherPhase += 0.075f + currentSpeed * 0.0016f;
                float wave = MathF.Sin(slitherPhase);
                newHeading += wave * 0.3f * slither * MathHelper.Lerp(0.5f, 1f, speedFactor);
            }

            worm.velocity = newHeading.ToRotationVector2() * currentSpeed;
            worm.rotation = worm.velocity.ToRotation() + FacingRot;
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

        //漩涡/回环不进回归阀：远距瞬移会把蛇从自己的漩涡/环上拽走造成演出脱节，
        //两招自带超时兜底，收招回 hub 后自然触发回归
        private static bool AllowFarSnap(BssStateBase state) {
            return state is BssHubState or BssBurrowLungeState or BssSandSpitState
                or BssCactusBallState or BssNeedleRippleState or BssPetalShakeState
                or BssSandDashState or BssSkyWeaveState or BssCoilOrbitState
                or BssGeyserMarchState or BssTailSweepState;
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

            stormSmooth = MathHelper.Lerp(stormSmooth, storm, 0.04f);
            Filter filter = SceneFilters.Scene[StormFilterName];
            if (stormSmooth > 0.03f) {
                if (!filter.IsActive()) {
                    SceneFilters.Scene.Activate(StormFilterName, NPC.Center);
                }
                filter.GetShader().UseOpacity(0.22f * stormSmooth).UseTargetPosition(NPC.Center);
            }
            else if (filter.IsActive()) {
                SceneFilters.Scene.Deactivate(StormFilterName);
            }

            //横风沙尘：镜像沙丘风暴氛围包的贴地扬沙 + 空中平流沙痕
            if (storm > 0.05f && !Main.gamePaused) {
                float wind = Context.WindSign;
                int grains = storm > 0.85f ? 3 : storm > 0.4f ? 2 : 1;
                for (int i = 0; i < grains; i++) {
                    if (!Main.rand.NextBool(2)) {
                        continue;
                    }
                    Vector2 pos = Main.screenPosition + new Vector2(
                        Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                        Main.rand.NextFloat(Main.screenHeight));
                    float speed = 7f + 8f * storm;
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Sand,
                        new Vector2(wind * speed * Main.rand.NextFloat(0.7f, 1.15f), -Main.rand.NextFloat(0.1f, 0.7f)),
                        Main.rand.Next(90, 140), default, Main.rand.NextFloat(0.8f, 1.35f));
                    dust.noGravity = true;
                    dust.fadeIn = 0.4f;
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

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Context == null) {
                return true;
            }

            //腿画最底：体节 whoAmI 更高、绘制在后，天然盖住腿根
            LegRig.Draw(spriteBatch, screenPos, Context);

            Main.instance.LoadNPC(Type);
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            //落足下沉回弹：头随 0 号髋站（最前大腿）落步下沉，被腿撑着走的重量读数（纯绘制偏移）
            Vector2 mainPos = NPC.Center - screenPos + new Vector2(0f, Context.SampleStationBob(0f) * BssLegRig.StationDipPx);
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

            //本体
            spriteBatch.Draw(texture, mainPos, frameRec, drawColor * fade, NPC.rotation,
                origin, NPC.scale, SpriteEffects.None, 0f);

            //怒放辉光：头顶花叶在预告/怒放期泛红（加色薄层，体感来自本体遮蔽）
            if (Context.BloomGlow > 0.03f) {
                Color bloom = BssVfx.BloomRed with { A = 0 } * (0.55f * Context.BloomGlow * fade);
                spriteBatch.Draw(texture, mainPos, frameRec, bloom, NPC.rotation,
                    origin, NPC.scale * 1.04f, SpriteEffects.None, 0f);
                Lighting.AddLight(NPC.Center, BssVfx.BloomRed.ToVector3() * 0.35f * Context.BloomGlow);
            }

            return false;
        }
        #endregion
    }
}
