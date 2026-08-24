using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Cinematics;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee
{
    /// <summary>
    /// 蜂后主控：蜂群编队艺术<br/>
    /// npc.ai[2]=状态机同步槽；npc.ai[0/1/3]=状态内掷骰暂存<br/>
    /// override.ai[0]=编队时钟 override.ai[3]=阶段位掩码 override.ai[4]=出招环游标<br/>
    /// override.ai[5]=标记玩家whoAmI+1 override.ai[6]=投技冷却 override.ai[7]=标记进度0~1
    /// </summary>
    internal class BrutalQueenBeeAI : BrutalNPCOverride
    {
        #region 数据
        public override int TargetID => NPCID.QueenBee;

        /// <summary>触发死亡演出的生命阈值</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>二阶段血线</summary>
        internal const float Phase2LifeRatio = 0.6f;
        /// <summary>大招血线</summary>
        internal const float UltimateLifeRatio = 0.25f;

        //override.ai[3] 阶段位
        internal const int FlagPhase2 = 1;
        internal const int FlagUltimateDone = 2;

        //override.ai 投技槽位
        internal const int AiSlotMarkTarget = 5;
        internal const int AiSlotGrabCooldown = 6;
        internal const int AiSlotMarkProgress = 7;
        /// <summary>标记积满所需的蜂蜜滞留帧数(离开黏滞环境会更快消退)</summary>
        internal const float MarkFullTicks = 165f;
        /// <summary>满标后收网机会窗，过窗未收网则解除</summary>
        internal const int MarkLatchWindow = 600;
        /// <summary>投技命中后的冷却，空挥减半</summary>
        internal const int GrabCooldownTicks = 1500;

        private VaultStateMachine<QueenBeeStateContext> stateMachine;
        private QueenBeeStateContext stateContext;
        private SwarmDirector swarm;
        private Player targetPlayer;
        //本实例发起过死亡运镜(多女王时不误停别人的演出)
        private bool deathCutsceneStarted;
        //每玩家蜂蜜滞留计量，服务端裁定专用(实例字段，随女王生命周期)
        private readonly float[] honeyMarks = new float[Main.maxPlayers];
        //满标机会窗倒计时(服务端)
        private int markLatchTimer;
        //满标锁定提示音的边沿检测(客户端表现)
        private bool markCueLatched;

        /// <summary>状态机与上下文(蜂/渲染侧只读访问)</summary>
        internal QueenBeeStateContext Context => stateContext;
        internal SwarmDirector Swarm => swarm;
        internal VaultStateMachine<QueenBeeStateContext> Machine => stateMachine;
        #endregion

        #region 加载与初始化
        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            //oldPos 残影缓存
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 12;
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            swarm = new SwarmDirector(npc);
            stateContext = new QueenBeeStateContext {
                Npc = npc,
                Swarm = swarm,
                OverrideAi = ai,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<QueenBeeStateContext>(stateContext, aiSlot: 2);

            //客户端从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<QueenBeeStateContext> syncedState = VaultStateRegistry<QueenBeeStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new QBIntroState());
            }
            else {
                stateMachine.SetInitialState(new QBIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null || swarm == null) {
                InitializeStateContext();
            }

            FindTarget();
            SyncSwarmClock();
            UpdateHoneyMark();
            UpdateStateContext();
            CheckPhaseTriggers();
            CheckDeathPerformanceTrigger();

            //每帧重声明：接触伤默认关(状态显式开)，冲刺帧组/蓄力表现默认关
            npc.damage = 0;
            stateContext.UseChargePose = false;
            stateContext.ResetChargeState();

            //编队帧首复位，状态在 OnUpdate 里声明编队
            swarm.FrameReset();

            stateMachine?.Update();

            swarm.RefreshBees();
            stateContext.DecayDashVisuals();
            UpdateVisuals();
            UpdateMarkVisuals();
            UpdateDeathCutscene();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }
        #endregion

        #region 上下文与阶段
        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not QBDespawnState and not QBDeathState) {
                    stateMachine?.ChangeState(new QBDespawnState());
                }
            }
        }

        /// <summary>编队时钟：各端本地自增，客户端漂移过阈值吸附服务端值</summary>
        private void SyncSwarmClock() {
            if (VaultUtils.isClient) {
                float serverClock = ai[0];
                if (Math.Abs(serverClock - swarm.Clock) > 20f) {
                    swarm.Clock = serverClock;
                }
            }
            else {
                ai[0] = swarm.Clock;
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            stateContext.EnrageScale = ComputeEnrageScale();

            int flags = (int)ai[3];
            stateContext.IsPhase2 = (flags & FlagPhase2) != 0;
            stateContext.UltimateDone = (flags & FlagUltimateDone) != 0;
            stateContext.AttackCycleIndex = (int)ai[4];

            //投技就绪：满标+冷却完+标记对象仍有效(服务端裁定，Reposition 末帧消费)
            int markedWho = (int)ai[AiSlotMarkTarget] - 1;
            stateContext.MarkedPlayerWhoAmI = markedWho;
            stateContext.GrabReady = !VaultUtils.isClient
                && markedWho >= 0 && markedWho < Main.maxPlayers
                && ai[AiSlotMarkProgress] >= 1f
                && ai[AiSlotGrabCooldown] <= 0f
                && Main.player[markedWho].Alives()
                && Main.player[markedWho].Distance(npc.Center) <= 1600f;
        }

        /// <summary>环境激怒 0~2：地表+1 离丛林+1 FTW+0.5 死亡模式垫底0.5；蜂巢墙内豁免</summary>
        private float ComputeEnrageScale() {
            if (!targetPlayer.Alives()) {
                return 0f;
            }

            int tileX = (int)(targetPlayer.Center.X / 16f);
            int tileY = (int)(targetPlayer.Center.Y / 16f);
            Tile tile = Framing.GetTileSafely(tileX, tileY);
            bool exempt = tile.WallType == WallID.HiveUnsafe;

            float scale = stateContext.IsDeathMode ? 0.5f : 0f;
            if (!exempt) {
                if (npc.position.Y / 16f < Main.worldSurface) {
                    scale += 1f;
                }
                if (!targetPlayer.ZoneJungle) {
                    scale += 1f;
                }
            }
            if (Main.getGoodWorld) {
                scale += 0.5f;
            }
            return MathHelper.Clamp(scale, 0f, 2f);
        }

        /// <summary>服务端血线宏观切换：60%蜕变、25%大招</summary>
        private void CheckPhaseTriggers() {
            if (VaultUtils.isClient || stateMachine == null) {
                return;
            }
            //演出/结构态不打断(投技演出结束后下一帧自然补触发)
            if (stateMachine.CurrentState is QBIntroState or QBPhaseTransitionState
                or QBRoyalTideState or QBDespawnState or QBDeathState or QBSwarmLiftState) {
                return;
            }

            float lifeRatio = npc.life / (float)npc.lifeMax;
            int flags = (int)ai[3];

            if ((flags & FlagPhase2) == 0 && lifeRatio < Phase2LifeRatio) {
                ai[3] = flags | FlagPhase2;
                npc.netUpdate = true;
                stateMachine.ChangeState(new QBPhaseTransitionState());
                return;
            }

            if ((flags & FlagPhase2) != 0 && (flags & FlagUltimateDone) == 0 && lifeRatio < UltimateLifeRatio) {
                ai[3] = flags | FlagUltimateDone;
                npc.netUpdate = true;
                stateMachine.ChangeState(new QBRoyalTideState());
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
            if (stateMachine.CurrentState is QBDeathState or QBDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new QBDeathState());
            }
        }
        #endregion

        #region 蜂蜜标记(投技触发)
        /// <summary>
        /// 服务端：投技冷却递减+蜂蜜滞留计量+满标锁定<br/>
        /// ai[5]=当前计量最高的候选(锁定后固定为猎物) ai[7]=进度，均骑 netUpdate 同步
        /// </summary>
        private void UpdateHoneyMark() {
            if (VaultUtils.isClient) {
                return;
            }

            if (ai[AiSlotGrabCooldown] > 0f) {
                ai[AiSlotGrabCooldown] -= 1f;
            }

            //投技进行中标记簿冻结，ai[5]归状态管理
            if (stateMachine?.CurrentState is QBSwarmLiftState) {
                return;
            }

            bool combatState = stateMachine?.CurrentState is not (QBIntroState or QBPhaseTransitionState
                or QBRoyalTideState or QBDespawnState or QBDeathState or null);
            bool canMark = stateContext.IsPhase2 && combatState
                && ai[AiSlotGrabCooldown] <= 0f && !CWRWorld.CanTimeFrozen();

            //已满标锁定：维持机会窗，猎物失效或超窗则解除
            if ((int)ai[AiSlotMarkTarget] - 1 >= 0 && ai[AiSlotMarkProgress] >= 1f) {
                int lockedWho = (int)ai[AiSlotMarkTarget] - 1;
                Player victim = lockedWho < Main.maxPlayers ? Main.player[lockedWho] : null;
                markLatchTimer--;
                if (!canMark || victim == null || !victim.Alives()
                    || markLatchTimer <= 0 || victim.Distance(npc.Center) > 2600f) {
                    ClearMark();
                }
                return;
            }

            if (!canMark) {
                if (ai[AiSlotMarkTarget] != 0f || ai[AiSlotMarkProgress] != 0f) {
                    ClearMark();
                }
                return;
            }

            //滞留计量：进蜜加、离蜜快退
            float best = 0f;
            int bestWho = -1;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.Alives() || player.Distance(npc.Center) > 2400f) {
                    honeyMarks[i] = 0f;
                    continue;
                }
                honeyMarks[i] = IsInHoneySnare(player)
                    ? Math.Min(honeyMarks[i] + 1f, MarkFullTicks)
                    : Math.Max(honeyMarks[i] - 1.5f, 0f);
                if (honeyMarks[i] > best) {
                    best = honeyMarks[i];
                    bestWho = i;
                }
            }

            if (bestWho >= 0 && best >= MarkFullTicks) {
                //满标锁定：开机会窗，等下一个连接段收网
                ai[AiSlotMarkTarget] = bestWho + 1;
                ai[AiSlotMarkProgress] = 1f;
                markLatchTimer = MarkLatchWindow;
                npc.netUpdate = true;
            }
            else {
                //候选与进度镜像给各端做渐进警示(搭每10帧兜底包)
                ai[AiSlotMarkTarget] = bestWho >= 0 && best > 0f ? bestWho + 1 : 0f;
                ai[AiSlotMarkProgress] = bestWho >= 0 ? best / MarkFullTicks * 0.99f : 0f;
            }
        }

        /// <summary>服务端：解除标记并清空计量簿(投技状态收尾/异常出口共用)</summary>
        internal void ClearMark() {
            ai[AiSlotMarkTarget] = 0f;
            ai[AiSlotMarkProgress] = 0f;
            markLatchTimer = 0;
            Array.Clear(honeyMarks, 0, honeyMarks.Length);
            npc.netUpdate = true;
        }

        /// <summary>蜂蜜黏滞环境判定：蜜液浸没/蜂蜜buff/蜜洼区域，三判其一</summary>
        internal static bool IsInHoneySnare(Player player) {
            if (player.honeyWet || player.HasBuff(BuffID.Honey)) {
                return true;
            }
            int zoneType = ModContent.ProjectileType<HoneyPuddleZone>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == zoneType && proj.ModProjectile is HoneyPuddleZone zone
                    && zone.SnareRect.Intersects(player.Hitbox)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>各客户端：标记渐进金尘环绕+满标锁定戏(读同步的 ai[5]/ai[7]，全端可见)</summary>
        private void UpdateMarkVisuals() {
            if (Main.dedServ) {
                return;
            }
            //投技进行中由状态自己演，这里只演标记期
            if (stateMachine?.CurrentState is QBSwarmLiftState) {
                markCueLatched = false;
                return;
            }

            int marked = (int)ai[AiSlotMarkTarget] - 1;
            float progress = ai[AiSlotMarkProgress];
            Player target = marked >= 0 && marked < Main.maxPlayers ? Main.player[marked] : null;
            bool latched = target != null && target.Alives() && progress >= 1f;

            //满标瞬间的锁定戏：脆响+金环+女王低吼(边沿检测防重播)
            if (latched && !markCueLatched) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.85f, Pitch = 0.75f }, target.Center);
                SoundEngine.PlaySound(SoundID.Zombie125 with { Volume = 0.55f, Pitch = 0.4f, MaxInstances = 2 }, npc.Center);
                PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero, QueenBeeMotion.HoneyGold, 0.22f)?
                    .Configure(Vector2.One, 0f, 1.15f, 16);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_BeeGlint>(target.Center + Main.rand.NextVector2Circular(30f, 42f),
                        Main.rand.NextVector2Circular(2f, 2f), QueenBeeMotion.HoneyGold, 1.3f);
                }
            }
            markCueLatched = latched;

            if (target == null || !target.Alives() || progress <= 0.18f) {
                return;
            }

            //绕身金尘：进度越满越密越亮，锁定后加急并周期蜂鸣
            int spawnGap = latched ? 2 : 5;
            if (Main.GameUpdateCount % (ulong)spawnGap == 0) {
                float orbAngle = swarm.Clock * MathHelper.Lerp(0.07f, 0.17f, progress);
                for (int k = 0; k < 2; k++) {
                    float angle = orbAngle + k * MathHelper.Pi;
                    Vector2 pos = target.Center + new Vector2(
                        (float)Math.Cos(angle) * 46f, (float)Math.Sin(angle) * 32f);
                    PRTLoader.NewParticle<PRT_BeeGlint>(pos, target.velocity * 0.3f,
                        QueenBeeMotion.HoneyGold * (0.35f + progress * 0.65f), 0.75f + progress * 0.6f);
                }
            }
            if (latched && Main.GameUpdateCount % 42 == 0) {
                QueenBeeMotion.WingHum(target.Center, 0.42f, 0.35f);
            }
        }
        #endregion

        #region 视觉与运镜
        private void UpdateVisuals() {
            //二阶段常驻怒辉，蓄力时增强
            float glow = stateContext.IsPhase2 ? 0.45f : 0f;
            if (stateContext.IsCharging) {
                glow = Math.Max(glow, 0.3f + stateContext.ChargeProgress * 0.6f);
            }
            if (stateMachine?.CurrentState is QBRoyalTideState) {
                glow = Math.Max(glow, 0.85f);
            }
            stateContext.RageGlow = glow;

            Lighting.AddLight(npc.Center, QueenBeeMotion.HoneyGold.ToVector3() * (0.35f + glow * 0.5f));

            //高速残影
            if (npc.velocity.Length() > 19f) {
                stateContext.PushAfterimage(MathHelper.Clamp((npc.velocity.Length() - 19f) / 18f, 0f, 1f));
            }
        }

        /// <summary>死亡运镜本地启停：只有发起者能收掉自己的运镜，超时由导演自动收</summary>
        private void UpdateDeathCutscene() {
            if (Main.dedServ) {
                return;
            }
            bool playing = CutsceneDirector.CurrentClip is QueenBeeDeathCutscene;
            bool inDeath = stateMachine?.CurrentState is QBDeathState;
            if (inDeath && npc.active) {
                if (!playing) {
                    CutsceneDirector.Play<QueenBeeDeathCutscene, NPC>(npc, restartSameClip: false);
                    deathCutsceneStarted = true;
                }
            }
            else if (playing && deathCutsceneStarted) {
                deathCutsceneStarted = false;
                CutsceneDirector.Stop();
            }
        }
        #endregion

        #region 帧动画与绘制
        public override bool FindFrame(int frameHeight) {
            //冲刺帧组0~3(快循环)，悬停帧组4~11
            npc.frameCounter += 1.0;
            if (stateContext != null && stateContext.UseChargePose) {
                if (npc.frameCounter >= 3.0) {
                    npc.frameCounter = 0.0;
                    npc.frame.Y += frameHeight;
                }
                if (npc.frame.Y >= frameHeight * 4 || npc.frame.Y < 0) {
                    npc.frame.Y = 0;
                }
            }
            else {
                if (npc.frameCounter >= 3.0) {
                    npc.frameCounter = 0.0;
                    npc.frame.Y += frameHeight;
                }
                if (npc.frame.Y < frameHeight * 4 || npc.frame.Y >= frameHeight * 12) {
                    npc.frame.Y = frameHeight * 4;
                }
            }
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }

            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            QueenBeeRenderHelper.DrawQueen(spriteBatch, npc, stateContext, texture, screenPos, drawColor);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion

        #region 死亡与活跃
        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not QBDeathState) {
                stateMachine.ChangeState(new QBDeathState());
            }

            return false;
        }
        #endregion
    }
}
