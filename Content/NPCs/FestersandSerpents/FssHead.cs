using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.Rendering;
using CalamityOverhaul.Content.NPCs.FestersandSerpents.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using EffectPriority = Terraria.Graphics.Effects.EffectPriority;
using Filter = Terraria.Graphics.Effects.Filter;
using SceneFilters = Terraria.Graphics.Effects.Filters;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>腐沙暴滤镜与环境暗化：头死亡/消失后无 AI 帧驱动淡出，这里兜底收场</summary>
    internal class FssStormSystem : FssModSystem
    {
        /// <summary>腐沙暴环境强度（本地表现量，头部每帧喂值，无头自然衰减）</summary>
        internal static float AmbientStorm;

        public override void PostUpdateNPCs() {
            if (Main.dedServ) {
                return;
            }
            bool anyHead = false;
            int headType = ModContent.NPCType<FssHead>();
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == headType) {
                    anyHead = true;
                    break;
                }
            }
            if (!anyHead) {
                AmbientStorm = Math.Max(AmbientStorm - 0.01f, 0f);
                Filter filter = SceneFilters.Scene[FssHead.StormFilterName];
                if (filter != null && filter.IsActive() && AmbientStorm <= 0.02f) {
                    SceneFilters.Scene.Deactivate(FssHead.StormFilterName);
                }
            }
        }

        /// <summary>腐沙暴压迫：日色勒向病紫污黄的浑浊昏暗（禁真黑屏）</summary>
        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (AmbientStorm <= 0.01f) {
                return;
            }
            Color blightTile = new(126, 108, 96);
            Color blightBg = new(88, 72, 74);
            tileColor = Color.Lerp(tileColor, blightTile, AmbientStorm * 0.3f);
            backgroundColor = Color.Lerp(backgroundColor, blightBg, AmbientStorm * 0.46f);
        }

        public override void ClearWorld() {
            AmbientStorm = 0f;
        }
    }

    /// <summary>
    /// 脓蕾沙蟒头部主控：状态机 + 统一血池 + 爬行/钻沙双身体语言 + 变异四足步态宿主。
    /// 联机契约：转场只在权威端裁决（状态走 ai[3]），各端本地跑同一状态机做表现，
    /// 弹幕只在权威端生成，粒子音效全走 !dedServ 门，腿是纯本地表现。
    /// 整链（腿+头+体节）集中在本类 PreDraw 绘制，体节自身 PreDraw 返回 false。
    /// </summary>
    [AutoloadBossHead]
    internal class FssHead : FssModNPC, ICWRLoader
    {
        #region 数据
        public override string Texture => CWRConstant.NPC + "BSS/Head";
        public override string BossHeadTexture => CWRConstant.NPC + "BSS/Head_Boss";

        /// <summary>贴图前方朝下的旋转修正（BSS 素材表统一约定）</summary>
        internal const float FacingRot = -MathHelper.PiOver2;

        //同步槽：ai[0]=统一血池上限 ai[1]=体节总数 ai[2]=阶段 ai[3]=状态机
        internal const int SlotUnifiedLifeMax = 0;
        internal const int SlotSegmentCount = 1;

        internal const string StormFilterName = "CalamityOverhaul:FssFesterStorm";

        /// <summary>腿贴图（借 BSS 素材；正式贴图到位后换路径即可）</summary>
        [VaultLoaden(CWRConstant.NPC + "BSS/LegUpper")]
        internal static Asset<Texture2D> LegUpperAsset = null;
        [VaultLoaden(CWRConstant.NPC + "BSS/LegLower")]
        internal static Asset<Texture2D> LegLowerAsset = null;

        private NpcStateMachine<FssStateContext> stateMachine;
        internal FssStateContext Context { get; private set; }
        private Player targetPlayer;
        /// <summary>变异四足步态（本地表现）</summary>
        internal FssLegRig LegRig { get; } = new();
        /// <summary>滤镜平滑包络（本地）</summary>
        private float stormSmooth;
        /// <summary>远距滞留帧</summary>
        private int farTimer;

        internal FssStateIndex CurrentStateIndex => (FssStateIndex)(int)NPC.ai[3];
        #endregion

        #region 定义
        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //腐沙暴滤镜：病紫掺污金的浑浊色
            SceneFilters.Scene[StormFilterName] = new Filter(
                new ScreenShaderData("FilterMiniTower")
                    .UseColor(0.5f, 0.38f, 0.3f)
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
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Ichor] = true;
        }

        public override void SetDefaults() {
            NPC.width = 56;
            NPC.height = 56;
            NPC.scale = FssDirector.BodyScale;
            NPC.damage = FssDirector.HeadContact;
            NPC.defense = FssDirector.HeadDefense;
            NPC.lifeMax = FssDirector.HeadLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.npcSlots = 14f;
            NPC.alpha = 255;
            NPC.value = Item.buyPrice(0, 12);
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath5;
            Music = MusicID.Boss2;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange([
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Desert,
                new FlavorTextBestiaryInfoElement("Mods.CalamityOverhaul.NPCs.FssHead.Bestiary"),
            ]);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            //占位掉落：暗影之魂 + 灵液 + 黑曜碎片（腐化沙漠主题），专属掉落后续另定
            npcLoot.Add(ItemDropRule.Common(ItemID.SoulofNight, 1, 8, 14));
            npcLoot.Add(ItemDropRule.Common(ItemID.Ichor, 1, 12, 24));
            npcLoot.Add(ItemDropRule.Common(ItemID.DarkShard, 1, 2, 4));
        }
        #endregion

        #region 状态机装配
        private void InitializeStateMachine() {
            Context = new FssStateContext {
                Npc = NPC,
                Owner = this,
            };
            stateMachine = new NpcStateMachine<FssStateContext>(Context);

            //中途加入的客户端从 ai[3] 恢复状态
            if (VaultUtils.isClient) {
                int syncedIndex = (int)NPC.ai[3];
                IVaultState<FssStateContext> synced = VaultStateRegistry<FssStateContext>.Create(syncedIndex);
                stateMachine.SetInitialState(synced ?? new FssIntroState());
            }
            else {
                stateMachine.SetInitialState(new FssIntroState());
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

            //步态时钟：任何状态都推进，速率取全速度模长
            Context.GaitPhase += FssStateContext.GaitIncrement(NPC.velocity.Length());
            ApplyDeclaredMovement();

            if (Main.GameUpdateCount % 45 == 0 || Context.Segments.Count == 0) {
                Context.RefreshSegments();
            }
            if (!Main.dedServ) {
                LegRig.Update(Context);
            }

            //阶段驱动的腐沙暴底线（各端从同步的 Phase 推导，确定性）
            if (CurrentStateIndex is not FssStateIndex.Death and not FssStateIndex.Despawn) {
                float stormFloor = Context.Phase >= 3 ? 1f : Context.Phase == 2 ? 0.74f : 0f;
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
            if (stateMachine?.CurrentState is not FssIntroState && NPC.alpha > 0) {
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
                || Math.Abs(NPC.position.X - targetPlayer.position.X) > FssDirector.MaxFindDistance
                || Math.Abs(NPC.position.Y - targetPlayer.position.Y) > FssDirector.MaxFindDistance;
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
            IVaultState<FssStateContext> current = stateMachine.CurrentState;
            if (current is FssIntroState or FssMoltGrowthState or FssOverflowState
                or FssDespawnState or FssDeathState) {
                return;
            }

            //血线见底：死亡演出（清弹、锁血、逐腿失力）
            if (NPC.life <= FssDirector.DeathTriggerLife && NPC.ai[SlotUnifiedLifeMax] > 0
                && !Context.DeathPerformanceFinished) {
                stateMachine.ChangeState(new FssDeathState());
                return;
            }

            //目标失效：钻沙遁走
            if (TargetInvalid()) {
                stateMachine.ChangeState(new FssDespawnState());
                return;
            }

            //62%：蜕变生长转阶段
            if (Context.Phase == 1 && NPC.life <= NPC.lifeMax * FssDirector.MoltThreshold) {
                stateMachine.ChangeState(new FssMoltGrowthState());
                return;
            }

            //28%：满溢怒放
            if (Context.Phase == 2 && NPC.life <= NPC.lifeMax * FssDirector.OverflowThreshold) {
                stateMachine.ChangeState(new FssOverflowState());
            }
        }

        /// <summary>锁血：死亡演出没放完不许真死，一击超杀也拦回演出</summary>
        public override bool CheckDead() {
            if (Context != null && !Context.DeathPerformanceFinished) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not FssDeathState) {
                    stateMachine?.ChangeState(new FssDeathState());
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
                case FssMoveMode.Crawl:
                    ApplyCrawl();
                    break;
                case FssMoveMode.Steer: {
                    float phase = Context.SlitherPhase;
                    SteerMovement(NPC, Context.MoveTarget, Context.MoveSpeed,
                        Context.TurnSpeed, Context.AccelRate, Context.Slither, ref phase);
                    Context.SlitherPhase = phase;
                    break;
                }
                case FssMoveMode.Direct:
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

        /// <summary>耙沙拍相位修正（同 BSS：push 峰值压在功率段中点）</summary>
        private static readonly float PushAlignPhase = MathHelper.PiOver2 - 0.9f;

        /// <summary>
        /// 蜈蚣爬行：沿地形等高线推进，全身起伏与推进涌动读步态时钟。
        /// 变异体更重：波幅略大、涌动更沉。
        /// </summary>
        private void ApplyCrawl() {
            float dir = Math.Sign(Context.CrawlDirX);
            if (dir == 0f) {
                dir = 1f;
            }

            //急转检测：行进中掉头 = 鞭波 + 短暂盘紧
            if (Math.Abs(NPC.velocity.X) > 5f && Math.Sign(NPC.velocity.X) != dir) {
                Context.PulseWhip(8f);
                Context.Compression = Math.Min(Context.Compression, 0.93f);
            }

            Vector2 probe = NPC.Center + new Vector2(dir * FssDirector.CrawlLookahead, -150f);
            float groundY = FssVfx.FindGroundY(probe, 1400f);
            float desiredY = groundY - FssDirector.CrawlRideHeight;

            float speedNow = Math.Abs(NPC.velocity.X);
            float bodyPhase = Context.GaitPhase + (dir < 0f ? MathHelper.Pi : 0f);
            float waveAmp = 3.5f + MathHelper.Clamp(speedNow * 0.9f, 0f, 12f);
            desiredY += MathF.Sin(bodyPhase) * waveAmp;

            float push = MathF.Pow(Math.Max(0f, MathF.Sin(bodyPhase + PushAlignPhase)), 3f);
            desiredY -= push * MathHelper.Clamp(3.5f + speedNow * 0.25f, 0f, 10f);

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

        /// <summary>蠕虫寻的转向物理（钻沙/腾空段；同 BSS 口径，旋转朝下贴图约定）</summary>
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
            if (stateMachine?.CurrentState is not FssStateBase state || !AllowFarSnap(state)) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives() || VaultUtils.isClient) {
                farTimer = 0;
                return;
            }
            if (NPC.Distance(targetPlayer.Center) <= FssDirector.FarSnapDistance) {
                farTimer = 0;
                return;
            }
            if (++farTimer < 30) {
                return;
            }
            farTimer = 0;

            Vector2 ground = new(targetPlayer.Center.X, FssVfx.FindGroundY(targetPlayer.Center));
            int side = Math.Sign(NPC.Center.X - targetPlayer.Center.X);
            if (side == 0) {
                side = 1;
            }
            NPC.Center = ground + new Vector2(side * 660f, 500f);
            NPC.velocity = new Vector2(-side * 7f, -21f);
            NPC.rotation = NPC.velocity.ToRotation() + FacingRot;
            NPC.netUpdate = true;
            FssVfx.CorruptSandBurst(ground + new Vector2(side * 660f, 0f), 1.2f);
        }

        //锚定型演出招（瀑洗/引爆/吞沙炮/蜕变）不进回归阀：远距瞬移会撕裂演出，
        //各招自带超时兜底，收招回 hub 后自然触发回归
        private static bool AllowFarSnap(FssStateBase state) {
            return state is FssHubState or FssIchorSpitState or FssVenomSkimState
                or FssStickyCystState or FssBreachFountState or FssFesterRippleState;
        }
        #endregion

        #region 体节与血池
        /// <summary>生成体节链并汇总统一血池（权威端，入场破土帧调用）</summary>
        internal static void SpawnBodySegments(NPC headNpc) {
            int totalLife = headNpc.lifeMax;
            int frontIndex = headNpc.whoAmI;
            int bodyType = ModContent.NPCType<FssBody>();
            int tailType = ModContent.NPCType<FssTail>();

            for (int i = 0; i <= FssDirector.BodyCount; i++) {
                bool isTail = i == FssDirector.BodyCount;
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
            headNpc.ai[SlotSegmentCount] = FssDirector.BodyCount + 1;
            headNpc.netUpdate = true;
        }

        /// <summary>
        /// 蜕变生长：在尾节前插入新体节（权威端，转阶段演出帧调用）。
        /// 新节带 ai[2]=1 生长标记（出生胀大动画），生命不并入统一血池
        /// （血线不回跳 = 不会重触阈值），伤害经 realLife 照旧汇入头。
        /// </summary>
        internal static void GrowBodySegments(NPC headNpc, int count) {
            int bodyType = ModContent.NPCType<FssBody>();
            int tailType = ModContent.NPCType<FssTail>();

            NPC tail = null;
            NPC lastBody = null;
            int maxOrdinal = -1;
            foreach (var n in Main.ActiveNPCs) {
                if ((int)n.ai[3] != headNpc.whoAmI) {
                    continue;
                }
                if (n.type == tailType) {
                    tail = n;
                }
                else if (n.type == bodyType && (int)n.ai[0] > maxOrdinal) {
                    maxOrdinal = (int)n.ai[0];
                    lastBody = n;
                }
            }
            if (tail == null || lastBody == null) {
                return;
            }

            int frontIndex = lastBody.whoAmI;
            int spawned = 0;
            for (int i = 0; i < count; i++) {
                int ordinal = maxOrdinal + 1 + i;
                int index = NPC.NewNPC(headNpc.FromObjectGetParent(),
                    (int)lastBody.Center.X, (int)lastBody.Center.Y,
                    bodyType, 0, ai0: ordinal, ai1: frontIndex, ai2: 1, ai3: headNpc.whoAmI);
                if (index >= Main.maxNPCs) {
                    break;
                }
                Main.npc[index].realLife = headNpc.whoAmI;
                Main.npc[index].netUpdate = true;
                frontIndex = index;
                spawned++;
            }

            //尾节重挂到最后一个新节之后
            tail.ai[0] = maxOrdinal + 1 + spawned;
            tail.ai[1] = frontIndex;
            tail.netUpdate = true;

            headNpc.ai[SlotSegmentCount] = (int)headNpc.ai[SlotSegmentCount] + spawned;
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

        /// <summary>远端玩家周期性强推基础数据（防长虫身位错漂）</summary>
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
            int headType = ModContent.NPCType<FssHead>();
            int bodyType = ModContent.NPCType<FssBody>();
            int tailType = ModContent.NPCType<FssTail>();
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
        /// <summary>腐沙暴表现：滤镜 + 环境暗化喂值 + 暗沙金屑风（客户端）</summary>
        private void UpdateStormPresentation() {
            if (Main.dedServ) {
                return;
            }
            float storm = MathHelper.Clamp(Context.StormLevel, 0f, 1f);
            FssStormSystem.AmbientStorm = Math.Max(FssStormSystem.AmbientStorm, storm);

            stormSmooth = MathHelper.Lerp(stormSmooth, storm, 0.04f);
            Filter filter = SceneFilters.Scene[StormFilterName];
            if (stormSmooth > 0.03f) {
                if (!filter.IsActive()) {
                    SceneFilters.Scene.Activate(StormFilterName, NPC.Center);
                }
                filter.GetShader().UseOpacity(0.24f * stormSmooth).UseTargetPosition(NPC.Center);
            }
            else if (filter.IsActive()) {
                SceneFilters.Scene.Deactivate(StormFilterName);
            }

            //横风腐沙：暗沙平流 + 偶发灵液金屑（变异沙暴的两种材质）
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
                    if (Main.rand.NextBool(9)) {
                        Dust gold = Dust.NewDustPerfect(pos, DustID.IchorTorch,
                            new Vector2(wind * speed * 0.8f, -Main.rand.NextFloat(0.1f, 0.5f)),
                            0, default, Main.rand.NextFloat(0.7f, 1.1f));
                        gold.noGravity = true;
                    }
                    else {
                        Dust dust = Dust.NewDustPerfect(pos, DustID.Sand,
                            new Vector2(wind * speed * Main.rand.NextFloat(0.7f, 1.15f), -Main.rand.NextFloat(0.1f, 0.7f)),
                            Main.rand.Next(90, 140), FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.35f));
                        dust.noGravity = true;
                        dust.fadeIn = 0.4f;
                    }
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(22f, 22f),
                    DustID.Sand, new Vector2(hit.HitDirection * Main.rand.NextFloat(1.5f, 4f), -Main.rand.NextFloat(1f, 3f)),
                    100, FssVfx.TaintedSand, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
            if (Main.rand.NextBool(3)) {
                FssVfx.IchorBurst(NPC.Center + Main.rand.NextVector2Circular(16f, 16f), 0.5f,
                    new Vector2(hit.HitDirection, -0.6f));
            }
            if (NPC.life <= 0) {
                FssVfx.CorruptSandBurst(NPC.Center, 1.8f);
                FssVfx.IchorBurst(NPC.Center, 2f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Context == null) {
                return true;
            }
            //整链集中绘制：腿 → 头 → 体节（体节自身 PreDraw 返回 false）
            FssSkinFX.DrawChain(spriteBatch, screenPos, Context);
            return false;
        }
        #endregion
    }
}
