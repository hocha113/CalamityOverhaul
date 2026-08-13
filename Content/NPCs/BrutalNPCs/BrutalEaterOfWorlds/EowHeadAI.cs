using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds
{
    /// <summary>绿雾滤镜孤儿清理：头死亡/消失后没有AI帧驱动淡出，这里兜底关掉</summary>
    internal class EowMiasmaSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (Main.dedServ) {
                return;
            }
            Filter filter = Filters.Scene[EowHeadAI.MiasmaFilterName];
            if (filter == null || !filter.IsActive()) {
                return;
            }
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.EaterofWorldsHead) {
                    return; //仍有头在场，交由头部AI驱动
                }
            }
            Filters.Scene.Deactivate(EowHeadAI.MiasmaFilterName);
        }
    }

    /// <summary>世界吞噬者头部主控：状态机+统一血池+分裂协同驾驶</summary>
    internal class EowHeadAI : CWRNPCOverride, ICWRLoader, ILocalizedModType
    {
        #region 数据
        public override int TargetID => NPCID.EaterofWorldsHead;

        public string LocalizationCategory => "BrutalNPCs";
        /// <summary>大招入场广播</summary>
        public static LocalizedText EowApex_Text { get; private set; }

        /// <summary>常规体节数(不含头尾)</summary>
        internal const int NormalBodyCount = 54;
        /// <summary>死亡模式体节数</summary>
        internal const int DeathBodyCount = 60;
        /// <summary>触发死亡演出的生命阈值</summary>
        internal const int DeathPerformanceTriggerLife = 30;
        /// <summary>蜕皮阈值(生命比)</summary>
        internal const float MoltThreshold = 0.55f;
        /// <summary>大招阈值(生命比)</summary>
        internal const float ApexThreshold = 0.28f;
        /// <summary>脱离腐化区撤离阈值(帧)</summary>
        internal const int OutOfZoneDespawnTime = 600;

        //override ai 同步槽位分配(12槽)
        /// <summary>统一血池上限</summary>
        internal const int SlotUnifiedLifeMax = 0;
        /// <summary>分裂组数(0/1=整体)</summary>
        internal const int SlotSplitGroups = 1;
        /// <summary>分裂形变进度0~1</summary>
        internal const int SlotSplitProgress = 2;
        /// <summary>体节总数(身+尾)</summary>
        internal const int SlotSegmentCount = 3;

        /// <summary>绿雾滤镜注册名</summary>
        internal const string MiasmaFilterName = "CalamityOverhaul:EowMiasma";

        private VaultStateMachine<EowStateContext> stateMachine;
        private EowStateContext stateContext;
        private Player targetPlayer;
        /// <summary>远距滞留帧，达上限触发回归</summary>
        private int farTimer;
        /// <summary>绿雾平滑包络(本地)</summary>
        private float miasmaSmooth;

        /// <summary>状态上下文(体节绘制读取脉冲通道)</summary>
        internal EowStateContext Context => stateContext;
        #endregion

        #region 加载与初始化
        void ICWRLoader.LoadData() {
            if (Main.dedServ) {
                return;
            }
            //腐化绿雾滤镜(客户端观测状态驱动，不走网络)
            Filters.Scene[MiasmaFilterName] = new Filter(
                new ScreenShaderData("FilterMiniTower")
                    .UseColor(0.36f, 0.52f, 0.2f)
                    .UseOpacity(0.22f),
                EffectPriority.High);
        }

        public override void SetStaticDefaults() {
            EowApex_Text = this.GetLocalization(nameof(EowApex_Text), () => "大地在你脚下裂成了无数张嘴……");
        }

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 18;
            npc.BossBar = ModContent.GetInstance<EowBossBar>();
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new EowStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<EowStateContext>(stateContext, aiSlot: 2);

            //客户端从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<EowStateContext> syncedState = VaultStateRegistry<EowStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new EowIntroState());
            }
            else {
                stateMachine.SetInitialState(new EowIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();
            CheckForcedMolt();

            //每帧重声明，未声明回落默认
            stateContext.SlitherStrength = 0f;
            stateContext.MawGlow = 0f;
            stateContext.MiasmaLevel = 0f;
            stateContext.ResetSplitDeclaration();

            stateMachine?.Update();

            //权威声明→同步槽；客户端回读权威值
            SyncSlots();

            if (!stateContext.SkipDefaultMovement) {
                float slitherPhase = stateContext.SlitherPhase;
                UpdateMovement(npc, stateContext.TargetPosition, stateContext.MoveSpeed,
                    stateContext.TurnSpeed, stateContext.AccelRate,
                    stateContext.SlitherStrength, ref slitherPhase);
                stateContext.SlitherPhase = slitherPhase;
            }

            //分裂协同：驾驶各组首节
            UpdateSplitSteering();

            UpdateFarReturnValve();
            EnforceUnifiedLife();
            UpdateVisuals();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }
        #endregion

        #region 目标与上下文
        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not EowDespawnState and not EowDeathState) {
                    stateMachine?.ChangeState(new EowDespawnState());
                }
                return;
            }

            //离开腐化/猩红环境累计，过阈撤离(尊重原版脱战语义)
            if (!VaultUtils.isClient) {
                if (!targetPlayer.ZoneCorrupt && !targetPlayer.ZoneCrimson && !CWRRef.GetBossRushActive()) {
                    stateContext.OutOfZoneTimer++;
                    if (stateContext.OutOfZoneTimer > OutOfZoneDespawnTime
                        && stateMachine?.CurrentState is not EowDespawnState and not EowDeathState) {
                        stateMachine?.ChangeState(new EowDespawnState());
                    }
                }
                else {
                    stateContext.OutOfZoneTimer = 0;
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            stateContext.SplitGroups = (int)ai[SlotSplitGroups];
            stateContext.TotalSegments = (int)ai[SlotSegmentCount] > 0
                ? (int)ai[SlotSegmentCount] : stateContext.Segments.Count;
            //二阶段判定各端从同步血量推导(中途加入的客户端也能收敛)，蜕皮标记补强
            stateContext.IsPhase2 = stateContext.MoltDone
                || (ai[SlotUnifiedLifeMax] > 0 && npc.life <= npc.lifeMax * MoltThreshold);

            if (Main.GameUpdateCount % 45 == 0 || stateContext.Segments.Count == 0) {
                stateContext.RefreshSegments();
            }
        }

        /// <summary>life≤阈值切死亡演出，服务端驱动</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is EowDeathState or EowDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife && ai[SlotUnifiedLifeMax] > 0) {
                stateMachine.ChangeState(new EowDeathState());
            }
        }

        /// <summary>深跌破蜕皮线仍未蜕皮→强制转阶段(防长招错过节点)</summary>
        private void CheckForcedMolt() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.MoltDone || ai[SlotUnifiedLifeMax] <= 0) {
                return;
            }
            if (npc.life > npc.lifeMax * (MoltThreshold - 0.1f)) {
                return;
            }
            if (stateMachine.CurrentState is EowIntroState or EowMoltTransitionState or EowSplitPincerState
                or EowApexFrenzyState or EowDeathState or EowDespawnState) {
                return;
            }
            stateMachine.ChangeState(new EowMoltTransitionState());
        }
        #endregion

        #region 同步槽
        /// <summary>权威端把状态声明写入同步槽；客户端回读关键量</summary>
        private void SyncSlots() {
            if (!VaultUtils.isClient) {
                ai[SlotSplitGroups] = stateContext.SplitGroups;
                ai[SlotSplitProgress] = stateContext.SplitProgress;
            }
            else {
                stateContext.SplitGroups = (int)ai[SlotSplitGroups];
                stateContext.SplitProgress = ai[SlotSplitProgress];
            }
        }

        /// <summary>各端按同步槽校正统一血池显示</summary>
        private void EnforceUnifiedLife() {
            int total = (int)ai[SlotUnifiedLifeMax];
            if (total > 0 && npc.lifeMax != total) {
                npc.lifeMax = total;
                if (npc.life > total) {
                    npc.life = total;
                }
            }
        }
        #endregion

        #region 运动
        /// <summary>蠕虫寻的转向物理：头与分组首节共用</summary>
        internal static void UpdateMovement(NPC worm, Vector2 targetPos, float moveSpeed,
            float turnSpeed, float accelRate, float slither, ref float slitherPhase) {
            Vector2 toTarget = targetPos - worm.Center;
            float distance = toTarget.Length();
            if (distance < 0.01f || moveSpeed <= 0.01f) {
                return;
            }

            float desiredHeading = toTarget.ToRotation();
            float currentSpeed = worm.velocity.Length();
            float currentHeading = currentSpeed > 0.01f ? worm.velocity.ToRotation() : desiredHeading;

            //转向随速衰减：低速灵巧高速迟钝(有机)
            float speedFactor = MathHelper.Clamp(currentSpeed / 26f, 0f, 1f);
            float maxTurn = turnSpeed / 20f * MathHelper.Lerp(2.0f, 0.72f, speedFactor);
            float newHeading = currentHeading.AngleTowards(desiredHeading, maxTurn);

            //入弯收油出弯全速
            float headingError = Math.Abs(MathHelper.WrapAngle(desiredHeading - newHeading));
            float throttle = MathHelper.Lerp(1f, 0.6f, MathHelper.Clamp(headingError / MathHelper.Pi, 0f, 1f));
            float targetSpeed = moveSpeed * throttle;
            float accel = accelRate;

            //远距追赶
            if (distance > 1300f) {
                float catchUp = Math.Min(distance / 60f, 46f);
                targetSpeed = Math.Max(targetSpeed, catchUp);
                accel = Math.Max(accel, 0.09f);
            }

            currentSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, accel);

            //蛇形扰动
            if (slither > 0.01f) {
                slitherPhase += 0.075f + currentSpeed * 0.0016f;
                float wave = (float)Math.Sin(slitherPhase);
                newHeading += wave * 0.3f * slither * MathHelper.Lerp(0.5f, 1f, speedFactor);
            }

            worm.velocity = newHeading.ToRotationVector2() * currentSpeed;
            worm.rotation = worm.velocity.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>分裂期驾驶各组首节(头领第0组由常规运动接管)</summary>
        private void UpdateSplitSteering() {
            int groups = stateContext.SplitGroups;
            if (groups <= 1) {
                return;
            }
            int totalSegs = stateContext.TotalSegments;
            if (totalSegs <= 0 || stateContext.Segments.Count == 0) {
                return;
            }

            for (int g = 1; g < groups && g < EowSplitLayout.MaxGroups; g++) {
                int leadOrdinal = EowSplitLayout.LeaderOrdinal(totalSegs, groups, g);
                if (leadOrdinal < 0 || leadOrdinal >= stateContext.Segments.Count) {
                    continue;
                }
                NPC leader = stateContext.Segments[leadOrdinal];
                if (!leader.Alives()) {
                    continue;
                }

                //合体回链：追front邻居身后
                if (stateContext.MergeHoming) {
                    int frontIdx = (int)leader.ai[1];
                    if (frontIdx >= 0 && frontIdx < Main.maxNPCs && Main.npc[frontIdx].active) {
                        NPC front = Main.npc[frontIdx];
                        float phase = 0f;
                        UpdateMovement(leader, front.Center, 30f, 1.9f, 0.11f, 0f, ref phase);
                    }
                }
                else if (stateContext.GroupDirectVelocity[g] is Vector2 direct) {
                    leader.velocity = direct;
                    leader.rotation = direct.ToRotation() + MathHelper.PiOver2;
                }
                else if (stateContext.GroupSpeeds[g] > 0.01f) {
                    float phase = 0f;
                    UpdateMovement(leader, stateContext.GroupTargets[g], stateContext.GroupSpeeds[g],
                        stateContext.GroupTurns[g], 0.085f, 0.45f, ref phase);
                }

                //分组首节周期强制同步
                if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                    leader.netUpdate = true;
                }
            }
        }

        /// <summary>合体回链完成度：所有分组首节均已贴近front邻</summary>
        internal bool AllLeadersDocked(float dockDist = 90f) {
            int groups = stateContext.SplitGroups;
            int totalSegs = stateContext.TotalSegments;
            if (groups <= 1 || totalSegs <= 0) {
                return true;
            }
            for (int g = 1; g < groups; g++) {
                int leadOrdinal = EowSplitLayout.LeaderOrdinal(totalSegs, groups, g);
                if (leadOrdinal < 0 || leadOrdinal >= stateContext.Segments.Count) {
                    continue;
                }
                NPC leader = stateContext.Segments[leadOrdinal];
                if (!leader.Alives()) {
                    continue;
                }
                int frontIdx = (int)leader.ai[1];
                if (frontIdx < 0 || frontIdx >= Main.maxNPCs || !Main.npc[frontIdx].active) {
                    continue;
                }
                if (leader.Distance(Main.npc[frontIdx].Center) > dockDist) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>远距回归：钻地瞬移回场，体节地下重整(比机械闪现更贴土遁身份)</summary>
        private void UpdateFarReturnValve() {
            if (stateMachine?.CurrentState is not EowStateBase state || !state.AllowFarSnap) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives()) {
                farTimer = 0;
                return;
            }

            float dist = npc.Distance(targetPlayer.Center);
            if (dist <= 2400f) {
                farTimer = 0;
                return;
            }
            if (++farTimer < 30) {
                return;
            }
            farTimer = 0;

            //从玩家侧下地底钻出归位
            Vector2 ground = EowMotionFX.FindGroundBelow(targetPlayer.Center);
            int side = Math.Sign(npc.Center.X - targetPlayer.Center.X);
            if (side == 0) {
                side = 1;
            }
            npc.Center = ground + new Vector2(side * 680f, 520f);
            npc.velocity = new Vector2(-side * 8f, -22f);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            npc.netUpdate = true;
            EowMotionFX.SpawnDirtBurst(ground + new Vector2(side * 680f, 0f), 1.2f);
        }
        #endregion

        #region 体节生成与统一血池
        /// <summary>生成体节链并汇总统一血池(服务端，Intro破土帧调用)</summary>
        internal static void SpawnBodySegments(NPC headNpc, bool deathMode) {
            int bodyCount = deathMode ? DeathBodyCount : NormalBodyCount;
            int totalLife = headNpc.lifeMax;
            int frontIndex = headNpc.whoAmI;

            for (int i = 0; i <= bodyCount; i++) {
                bool isTail = i == bodyCount;
                int index = NPC.NewNPC(headNpc.FromObjectGetParent(), (int)headNpc.Center.X, (int)headNpc.Center.Y,
                    isTail ? NPCID.EaterofWorldsTail : NPCID.EaterofWorldsBody,
                    0, ai0: i, ai1: frontIndex, ai2: 0, ai3: headNpc.whoAmI);
                if (index >= Main.maxNPCs) {
                    break;
                }
                Main.npc[index].realLife = headNpc.whoAmI;
                totalLife += Main.npc[index].lifeMax;
                Main.npc[index].netUpdate = true;
                frontIndex = index;
            }

            //统一血池：吸收各端 SetDefaults 的实际数值(兼容灾厄重平衡)
            headNpc.lifeMax = totalLife;
            headNpc.life = totalLife;
            if (headNpc.TryGetOverride<EowHeadAI>(out var headOverride)) {
                headOverride.ai[SlotUnifiedLifeMax] = totalLife;
                headOverride.ai[SlotSegmentCount] = bodyCount + 1;
            }
            headNpc.netUpdate = true;
        }

        /// <summary>远端玩家周期性强推基础数据(防长虫身位错漂)</summary>
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

        /// <summary>清场：撤离/死亡后清全部世吞NPC(服务端)</summary>
        internal static void HandleDespawnAll() {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody
                    || n.type == NPCID.EaterofWorldsTail) {
                    n.active = false;
                    n.netUpdate = true;
                    if (Main.dedServ) {
                        Terraria.NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n.whoAmI);
                    }
                }
            }
        }
        #endregion

        #region 视觉
        private void UpdateVisuals() {
            Lighting.AddLight(npc.Center, EowMotionFX.CorruptPurple.ToVector3() * 0.35f
                + EowMotionFX.AcidGreen.ToVector3() * 0.3f * stateContext.MawGlow);

            if (Main.dedServ) {
                return;
            }

            //绿雾滤镜：本地平滑包络，由状态声明驱动
            miasmaSmooth = MathHelper.Lerp(miasmaSmooth, MathHelper.Clamp(stateContext.MiasmaLevel, 0f, 1f), 0.045f);
            if (miasmaSmooth > 0.02f) {
                if (!Filters.Scene[MiasmaFilterName].IsActive()) {
                    Filters.Scene.Activate(MiasmaFilterName, npc.Center);
                }
                Filters.Scene[MiasmaFilterName].GetShader()
                    .UseOpacity(0.3f * miasmaSmooth)
                    .UseTargetPosition(npc.Center);
            }
            else if (Filters.Scene[MiasmaFilterName].IsActive()) {
                Filters.Scene.Deactivate(MiasmaFilterName);
            }
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = texture.Bounds;
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos;
            float fade = 1f - npc.alpha / 255f;

            //高速酸绿残影(速度门控)
            float speed = npc.velocity.Length();
            float ghostIntensity = MathHelper.Clamp((speed - 17f) / 26f, 0f, 1f);
            if (ghostIntensity > 0.05f) {
                for (int i = npc.oldPos.Length - 1; i >= 1; i -= 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float t = 1f - i / (float)npc.oldPos.Length;
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    Color ghost = EowMotionFX.AcidGreen with { A = 0 } * (0.24f * t * ghostIntensity * fade);
                    spriteBatch.Draw(texture, ghostPos, frameRec, ghost, npc.rotation,
                        origin, npc.scale * (0.92f + 0.08f * t), SpriteEffects.None, 0f);
                }
            }

            //本体
            spriteBatch.Draw(texture, mainPos, frameRec, drawColor * fade, npc.rotation,
                origin, npc.scale, SpriteEffects.None, 0f);

            //腭部酸光(蓄势/喷吐时点亮)
            if (stateContext.MawGlow > 0.03f) {
                Color maw = EowMotionFX.AcidGreen with { A = 0 } * (0.75f * stateContext.MawGlow * fade);
                spriteBatch.Draw(texture, mainPos, frameRec, maw, npc.rotation,
                    origin, npc.scale * 1.05f, SpriteEffects.None, 0f);
                Texture2D soft = CWRAsset.SoftGlow.Value;
                Vector2 mawTip = mainPos + (npc.rotation - MathHelper.PiOver2).ToRotationVector2() * 22f * npc.scale;
                spriteBatch.Draw(soft, mawTip, null, maw * 0.8f, 0f, soft.Size() / 2f,
                    0.4f + stateContext.MawGlow * 0.35f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion

        #region 生死
        public override bool CheckActive() => false;

        /// <summary>演出中锁血；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not EowDeathState) {
                stateMachine.ChangeState(new EowDeathState());
            }

            return false;
        }
        #endregion
    }
}
