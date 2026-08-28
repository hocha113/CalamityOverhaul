using CalamityOverhaul.Content.Items.Accessories.BrutalRelics.QueenSlime;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime
{
    /// <summary>史莱姆皇后主控：水晶折射与空降芭蕾</summary>
    internal class QueenSlimeAI : BrutalNPCOverride
    {
        #region 数据
        public override int TargetID => NPCID.QueenSlimeBoss;

        /// <summary>life低于此值直接进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>目标失效判定距离</summary>
        private const float MaxFindDistance = 7200f;

        private VaultStateMachine<QueenSlimeStateContext> stateMachine;
        private QueenSlimeStateContext stateContext;
        private Player targetPlayer;

        /// <summary>渲染辅助读取上下文</summary>
        internal QueenSlimeStateContext Context => stateContext;
        #endregion

        #region 加载与初始化
        public override void SetProperty() {
            //oldPos 残影缓存
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 12;
            InitializeStateContext();
        }

        public override bool? CanBrutalOverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new QueenSlimeStateContext {
                Npc = npc,
                IsAsuraMode = CWRWorld.Asura
            };
            stateMachine = new NpcStateMachine<QueenSlimeStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<QueenSlimeStateContext> syncedState = VaultStateRegistry<QueenSlimeStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new QueenIntroState());
            }
            else {
                stateMachine.SetInitialState(new QueenIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckPhaseTransitionTrigger();
            CheckUltimateTrigger();
            CheckDeathPerformanceTrigger();

            //姿态指令每帧重声明，未声明自动
            stateContext.PoseCommand = 0;

            //状态机
            stateMachine?.Update();

            //视觉量衰减
            DecayVisuals();

            Lighting.AddLight(npc.Center, 0.9f, 0.55f, 0.85f);

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }
            ForcedNetUpdating(npc);

            return false;
        }

        private void DecayVisuals() {
            stateContext.SquashPulse *= 0.86f;
            if (System.Math.Abs(stateContext.SquashPulse) < 0.01f) {
                stateContext.SquashPulse = 0f;
            }
            stateContext.AfterimageBoost *= 0.92f;
            if (stateContext.AfterimageBoost < 0.01f) {
                stateContext.AfterimageBoost = 0f;
            }
            stateContext.PrismShimmer *= 0.94f;
            if (stateContext.PrismShimmer < 0.01f) {
                stateContext.PrismShimmer = 0f;
            }
            stateContext.WingFlapBoost *= 0.9f;
            if (stateContext.WingFlapBoost < 0.01f) {
                stateContext.WingFlapBoost = 0f;
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsPhase2 = npc.life * 2 <= npc.lifeMax;
            stateContext.IsAsuraMode = CWRWorld.Asura;

            //中途入场的客户端从无歧义的二阶段专属状态反推翼展标记
            if (!stateContext.Phase2Unfolded && stateContext.IsPhase2
                && stateMachine?.CurrentState is QueenAerialBalletState or QueenWingGaleWaltzState
                    or QueenSpikeRingState or QueenCrystalDiveStompState
                    or QueenSkySpikeCascadeState
                    or QueenChandelierFallState or QueenCrystalCathedralState
                    or QueenCrystalPrisonWaltzState) {
                stateContext.Phase2Unfolded = true;
            }

            //投技冷却走表(服务端，二阶段起算)
            if (!VaultUtils.isClient && stateContext.Phase2Unfolded && stateContext.GrabCooldown > 0) {
                stateContext.GrabCooldown--;
            }
            //分裂召唤冷却走表(服务端，两阶段通用)
            if (!VaultUtils.isClient && stateContext.SummonCooldown > 0) {
                stateContext.SummonCooldown--;
            }

            //二阶段翼展常驻(死亡/撤离演出自己写翼展，状态机晚于此处执行故不冲突)
            if (stateContext.Phase2Unfolded && stateContext.WingSpread < 1f
                && stateMachine?.CurrentState is not QueenPhaseTransitionState) {
                stateContext.WingSpread = MathHelper.Clamp(stateContext.WingSpread + 0.05f, 0f, 1f);
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()
                || npc.Distance(Main.player[npc.target].Center) > MaxFindDistance) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives() || npc.Distance(targetPlayer.Center) > MaxFindDistance) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not QueenDespawnState and not QueenDeathState) {
                    stateMachine?.ChangeState(new QueenDespawnState());
                }
            }
        }

        /// <summary>血量过半切转换演出，服务端驱动，一次性</summary>
        private void CheckPhaseTransitionTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.Phase2Unfolded || !stateContext.IsPhase2) {
                return;
            }
            if (stateMachine.CurrentState is QueenPhaseTransitionState or QueenIntroState
                or QueenDeathState or QueenDespawnState) {
                return;
            }
            stateMachine.ChangeState(new QueenPhaseTransitionState());
        }

        /// <summary>血量≤25%切大招，服务端驱动，一次性</summary>
        private void CheckUltimateTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.UltFired || !stateContext.Phase2Unfolded || npc.life * 4 > npc.lifeMax) {
                return;
            }
            //囚舞/分裂中不夺舞：演出完再放大招
            if (stateMachine.CurrentState is QueenCrystalCathedralState or QueenPhaseTransitionState
                or QueenDeathState or QueenDespawnState or QueenIntroState
                or QueenCrystalPrisonWaltzState or QueenGelSplitSummonState) {
                return;
            }
            stateContext.UltFired = true;
            stateMachine.ChangeState(new QueenCrystalCathedralState());
        }

        /// <summary>life≤阈值切死亡演出，服务端驱动</summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is QueenDeathState or QueenDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new QueenDeathState());
            }
        }

        /// <summary>远端玩家周期性强推基础数据(位置突变多)</summary>
        private static void ForcedNetUpdating(NPC npc) {
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
        #endregion

        #region 帧动画
        public override bool FindFrame(int frameHeight) {
            if (stateContext == null) {
                return true;
            }

            //翼帧推进
            float flapStep = 1f + stateContext.WingFlapBoost * 1.6f;
            stateContext.WingFrameCounter = (int)(stateContext.WingFrameCounter + flapStep) % 24;

            UpdateBodyFrame();

            npc.frame.Y = stateContext.BodyFrame * frameHeight;
            return false;
        }

        /// <summary>逻辑帧驱动，帧索引语义沿用原版(0~3待机 4~7升 8~10落 10~12蹲 13~15喷吐 20~23飞行)</summary>
        private void UpdateBodyFrame() {
            int frame = stateContext.BodyFrame;
            int pose = stateContext.PoseCommand;

            switch (pose) {
                case 1://强制升姿
                    AdvanceRiseFrame(ref frame);
                    break;
                case 2://强制落姿
                    AdvanceFallFrame(ref frame);
                    break;
                case 3://蹲姿(蓄力压身)
                    frame = 12;
                    stateContext.BodyFrameCounter = 0;
                    break;
                case 4://喷吐
                    stateContext.BodyFrameCounter++;
                    frame = 13 + stateContext.BodyFrameCounter / 3 % 3;
                    break;
                case 5://飞行巡航
                    AdvanceFlightFrame(ref frame);
                    break;
                default://自动：按速度/相位选
                    if (stateContext.Phase2Unfolded && npc.noGravity) {
                        AdvanceFlightFrame(ref frame);
                    }
                    else if (npc.velocity.Y < -0.5f) {
                        AdvanceRiseFrame(ref frame);
                    }
                    else if (npc.velocity.Y > 0.5f) {
                        AdvanceFallFrame(ref frame);
                    }
                    else {
                        //地面待机弹性
                        if (frame is < 0 or > 3) {
                            frame = 0;
                            stateContext.BodyFrameCounter = 0;
                        }
                        if (++stateContext.BodyFrameCounter >= 9) {
                            stateContext.BodyFrameCounter = 0;
                            frame = (frame + 1) % 4;
                        }
                    }
                    break;
            }

            stateContext.BodyFrame = System.Math.Clamp(frame, 0, 23);
        }

        private void AdvanceRiseFrame(ref int frame) {
            if (frame is < 4 or > 7) {
                frame = 4;
                stateContext.BodyFrameCounter = 0;
            }
            if (++stateContext.BodyFrameCounter >= 4) {
                stateContext.BodyFrameCounter = 0;
                if (frame < 7) {
                    frame++;
                }
            }
        }

        private void AdvanceFallFrame(ref int frame) {
            if (frame is < 8 or > 10) {
                frame = 8;
                stateContext.BodyFrameCounter = 0;
            }
            if (++stateContext.BodyFrameCounter >= 8) {
                stateContext.BodyFrameCounter = 0;
                if (frame < 10) {
                    frame++;
                }
            }
        }

        private void AdvanceFlightFrame(ref int frame) {
            if (frame is < 20 or > 23) {
                frame = 20;
                stateContext.BodyFrameCounter = 0;
            }
            if (++stateContext.BodyFrameCounter >= 5) {
                stateContext.BodyFrameCounter = 0;
                frame = frame >= 23 ? 20 : frame + 1;
            }
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }
            QueenSlimeRenderHelper.DrawFull(spriteBatch, npc, stateContext, screenPos, drawColor);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion

        #region 掉落
        /// <summary>残酷模式击杀必掉专属遗物「折光华尔兹」</summary>
        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.ByCondition(new DropInBrutalWorld(), ModContent.ItemType<RefractionWaltz>()));
        }
        #endregion

        #region 生死
        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not QueenDeathState) {
                stateMachine.ChangeState(new QueenDeathState());
            }

            return false;
        }
        #endregion
    }
}
