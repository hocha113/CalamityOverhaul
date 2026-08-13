using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States;
using InnoVault.Cinematics;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee
{
    /// <summary>
    /// 蜂后主控：蜂群编队艺术<br/>
    /// npc.ai[2]=状态机同步槽；npc.ai[0/1/3]=状态内掷骰暂存<br/>
    /// override.ai[0]=编队时钟 override.ai[3]=阶段位掩码 override.ai[4]=出招环游标
    /// </summary>
    internal class BrutalQueenBeeAI : CWRNPCOverride, ICWRLoader
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

        private VaultStateMachine<QueenBeeStateContext> stateMachine;
        private QueenBeeStateContext stateContext;
        private SwarmDirector swarm;
        private Player targetPlayer;
        //本实例发起过死亡运镜(多女王时不误停别人的演出)
        private bool deathCutsceneStarted;

        /// <summary>状态机与上下文(蜂/渲染侧只读访问)</summary>
        internal QueenBeeStateContext Context => stateContext;
        internal SwarmDirector Swarm => swarm;
        internal VaultStateMachine<QueenBeeStateContext> Machine => stateMachine;
        #endregion

        #region 加载与初始化
        void ICWRLoader.UnLoadData() => SwarmFlowRenderer.Unload();

        public override bool? CanCWROverride() {
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
            //演出/结构态不打断
            if (stateMachine.CurrentState is QBIntroState or QBPhaseTransitionState
                or QBRoyalTideState or QBDespawnState or QBDeathState) {
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
            //编队辉光带垫在女王与蜂群下层(延迟批次内先画的图元后被精灵覆盖)
            SwarmFlowRenderer.DrawRibbons(swarm);
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
