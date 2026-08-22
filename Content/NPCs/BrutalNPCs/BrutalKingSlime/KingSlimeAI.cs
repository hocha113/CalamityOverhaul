using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime
{
    /// <summary>残酷史莱姆王主控：液态质量与地形吞没</summary>
    internal class KingSlimeAI : CWRNPCOverride
    {
        #region 数据
        public override int TargetID => NPCID.KingSlime;

        /// <summary>life低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;
        /// <summary>阶段转换血量阈值</summary>
        internal const float Phase2LifeRatio = 0.6f;
        /// <summary>低血大招血量阈值</summary>
        internal const float DecreeLifeRatio = 0.3f;

        private VaultStateMachine<KingSlimeStateContext> stateMachine;
        private KingSlimeStateContext stateContext;
        private Player targetPlayer;
        /// <summary>上一帧处于狂暴，用于释放免伤</summary>
        private bool wasEnraged;

        /// <summary>当前死亡演出中的本体索引，运镜玩家侧查询；-1无</summary>
        internal static int ActivePerformanceIndex = -1;
        #endregion

        #region 加载与初始化
        public override void SetProperty() {
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new KingSlimeStateContext {
                Npc = npc,
                Host = this,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<KingSlimeStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态；镜像旗须先于初始OnEnter恢复，
            //防中途加入时状态以默认旗定格(travelMode/passesLeft在OnEnter一次性读取)
            if (VaultUtils.isClient) {
                stateContext.Phase2Started = ai[3] == 1f;
                stateContext.IsPhase2 = stateContext.Phase2Started;
                stateContext.TideTravelActive = ai[5] == 1f;
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<KingSlimeStateContext> syncedState = VaultStateRegistry<KingSlimeStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new KingSlimeIntroState());
            }
            else {
                stateMachine.SetInitialState(new KingSlimeIntroState());
            }
        }
        #endregion

        #region 主要AI行为
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            //完全接管重力
            npc.noGravity = true;

            FindTarget();
            UpdateStateContext();
            CheckPhaseTriggers();
            CheckPursuitValve();
            CheckDeathPerformanceTrigger();

            //每帧重声明项：状态在Update里声明，未声明回默认(照抄毁灭者模式)
            stateContext.ContactDamageScale = 1f;
            stateContext.SkipGravity = false;
            stateContext.AuraMode = 0;
            stateContext.AuraProgress = 0f;
            stateContext.NinjaGlow = 0f;
            stateContext.HideBodySprite = false;
            stateContext.BodyLean = 0f;
            stateContext.IntroCrownDrop = 0f;
            stateContext.HideCrown = false;

            stateMachine?.Update();

            //P2无状态声明时的常态：凝胶沸腾微光
            if (stateContext.AuraMode == 0 && stateContext.Phase2Started) {
                stateContext.AuraMode = 2;
                stateContext.AuraProgress = Math.Max(stateContext.AuraProgress, 0.28f);
            }

            UpdateEnrage();

            ApplyPhysics();
            DetectLanding();
            UpdateScaleAndHitbox();

            //接触伤害由状态声明(狂暴期已乘增伤)
            npc.damage = (int)(npc.defDamage * stateContext.ContactDamageScale);

            //阶段旗镜像到重制ai槽，供王冠与远端读取；ai[5]=液化掠近旗
            if (!VaultUtils.isClient) {
                ai[3] = stateContext.Phase2Started ? 1f : 0f;
                ai[5] = stateContext.TideTravelActive ? 1f : 0f;
            }
            else {
                stateContext.Phase2Started = ai[3] == 1f;
            }

            //视觉弹簧本地推进
            KingSlimeRenderer.UpdateSpring(stateContext);

            //防原版自然脱战计时干扰(撤离交给Despawn状态)
            if (npc.timeLeft < 300) {
                npc.timeLeft = 300;
            }

            Lighting.AddLight(npc.Center, KingSlimeGelFX.GelMid.ToVector3() * 0.5f * npc.scale);

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }
        #endregion

        #region 上下文与触发

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsPhase2 = stateContext.Phase2Started;
            stateContext.IsLowHP = npc.life <= npc.lifeMax * DecreeLifeRatio;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            //液化掠近冷却递减；客户端在状态机推进前从 ai[5] 恢复位移旗
            //(潮汐 OnEnter 读取该旗，须先于状态创建到位)
            if (stateContext.TideTravelCooldown > 0) {
                stateContext.TideTravelCooldown--;
            }
            //吞没投技冷却递减(服务端消费)
            if (stateContext.EngulfCooldown > 0) {
                stateContext.EngulfCooldown--;
            }
            if (VaultUtils.isClient) {
                stateContext.TideTravelActive = ai[5] == 1f;
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not KingSlimeDespawnState and not KingSlimeDeathState) {
                    stateMachine?.ChangeState(new KingSlimeDespawnState());
                }
            }
        }

        /// <summary>阶段转换与低血大招，只在连接器拍打断(服务端)</summary>
        private void CheckPhaseTriggers() {
            if (VaultUtils.isClient || stateMachine?.CurrentState is not KingSlimeStateBase current || !current.Interruptible) {
                return;
            }

            float lifeRatio = npc.life / (float)npc.lifeMax;

            if (!stateContext.Phase2Started && lifeRatio <= Phase2LifeRatio) {
                stateMachine.ChangeState(new KingSlimePhaseShiftState());
                return;
            }

            if (stateContext.Phase2Started && !stateContext.DecreeDone && lifeRatio <= DecreeLifeRatio) {
                stateMachine.ChangeState(new KingSlimeRoyalDecreeState());
            }
        }

        /// <summary>追击阀：远离/失联过久，化胶潜地追击(服务端)</summary>
        private void CheckPursuitValve() {
            if (VaultUtils.isClient || !targetPlayer.Alives()) {
                return;
            }

            //失联计时
            float hDist = Math.Abs(targetPlayer.Center.X - npc.Center.X);
            bool los = Collision.CanHitLine(npc.Center, 0, 0, targetPlayer.Center, 0, 0);
            if (!los || hDist > 1100f) {
                stateContext.LostContactTimer++;
            }
            else {
                stateContext.LostContactTimer = 0;
            }

            if (stateMachine?.CurrentState is not KingSlimeStateBase current || !current.Interruptible) {
                return;
            }

            if (hDist > 1900f || stateContext.LostContactTimer > 240) {
                stateContext.LostContactTimer = 0;
                stateMachine.ChangeState(new KingSlimePursuitBurrowState());
            }
        }

        /// <summary>
        /// 狂暴阀：目标存活但长期够不着(极远/失联超时，环境性脱战)时不撤离，
        /// 转入狂暴，AI照常，免伤+接触增伤；贴近后解除。服务端判定，ai[4]镜像
        /// </summary>
        private void UpdateEnrage() {
            //吞没投技持人期也豁免：狂暴免伤会消解"队友集火救人"的反制窗口
            bool inPerformance = stateMachine?.CurrentState
                is KingSlimeDeathState or KingSlimeDespawnState or KingSlimeIntroState
                or KingSlimeEngulfState;

            if (!VaultUtils.isClient) {
                if (inPerformance || !targetPlayer.Alives()) {
                    ai[4] = 0f;
                }
                else {
                    float hDist = Math.Abs(targetPlayer.Center.X - npc.Center.X);
                    float vDist = Math.Abs(targetPlayer.Center.Y - npc.Center.Y);
                    if (hDist > 3000f || vDist > 2200f || stateContext.LostContactTimer > 540) {
                        ai[4] = 1f;
                    }
                    else if (ai[4] == 1f && hDist < 1100f && vDist < 900f && stateContext.LostContactTimer == 0) {
                        ai[4] = 0f;
                    }
                }
            }

            bool enraged = ai[4] == 1f;
            if (enraged) {
                wasEnraged = true;
                npc.dontTakeDamage = true;
                stateContext.ContactDamageScale *= 1.5f;
                stateContext.AuraMode = 2;
                stateContext.AuraProgress = 1f;
            }
            else if (wasEnraged) {
                wasEnraged = false;
                if (!inPerformance) {
                    npc.dontTakeDamage = false;
                }
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
            if (stateMachine.CurrentState is KingSlimeDeathState or KingSlimeDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new KingSlimeDeathState());
            }
        }

        #endregion

        #region 王冠接口(供离体弹幕查询/回冲)

        /// <summary>状态上下文只读暴露，吞没渲染层读形变参数用；未初始化为null</summary>
        internal KingSlimeStateContext StateContext => stateContext;

        /// <summary>
        /// 取NPC上的本重制实例(验接管在场)，玩家侧/渲染侧查询吞没状态用；无则false。
        /// 原版史莱姆王ai[2]是原生用槽，可能撞状态索引值，必须验证接管
        /// </summary>
        internal static bool TryGetKingAI(NPC npc, out KingSlimeAI kingAI) {
            kingAI = null;
            if (npc == null || !npc.active || npc.type != NPCID.KingSlime) {
                return false;
            }
            return npc.TryGetOverride(out kingAI);
        }

        /// <summary>扣冠锚点(世界系头顶)，离体王冠归位砸扣的落点</summary>
        internal Vector2 GetCrownAnchor() {
            return stateContext == null ? npc.Top : KingSlimeRenderer.CrownAnchorWorld(npc, stateContext);
        }

        /// <summary>王冠砸扣命中：本体凝胶受压微陷+扣冠回弹(各端本地表现)</summary>
        internal void NotifyCrownMounted(float power) {
            stateContext?.CrownMountImpact(power);
        }

        #endregion

        #region 物理与形体

        /// <summary>自管重力：上升轻、下坠重，凝胶质量感</summary>
        private void ApplyPhysics() {
            if (stateContext.SkipGravity) {
                return;
            }
            float gravity = npc.velocity.Y < 0f ? 0.38f : 0.56f;
            npc.velocity.Y += gravity;
            if (npc.velocity.Y > 26f) {
                npc.velocity.Y = 26f;
            }
        }

        /// <summary>落地检测：冲量交给状态与表现层</summary>
        private void DetectLanding() {
            bool onGround = npc.velocity.Y == 0f || npc.collideY;
            stateContext.JustLanded = false;

            if (onGround && stateContext.PrevVelY > 2.5f) {
                stateContext.JustLanded = true;
                stateContext.LandingPower = stateContext.PrevVelY;

                //形变+飞溅+闷响+震屏，强度随冲量
                float power = stateContext.PrevVelY;
                stateContext.ImpactSquash(MathHelper.Clamp(power * 0.026f, 0.08f, 0.6f));
                //落地帧砸地光环闪(映射 RoyalAura 的 Slamming 模式)
                if (power > 8f) {
                    stateContext.AuraMode = 3;
                    stateContext.AuraProgress = 1f;
                }
                if (!VaultUtils.isServer && KingSlimeGelFX.OnScreen(npc.Bottom)) {
                    KingSlimeGelFX.LandingBurst(npc.Bottom, power, npc.scale * stateContext.LandingSplashMul);
                    KingSlimeGelFX.ThudSound(npc.Bottom, power);
                    KingSlimeGelFX.CameraPunch(npc.Bottom, MathHelper.Clamp(power * 0.32f, 2f, 8.5f),
                        (int)MathHelper.Clamp(power * 0.7f, 8f, 18f), "BKSLanding", Vector2.UnitY);
                }

                //状态预定的落地冲击波(服务端)
                if (!VaultUtils.isClient && stateContext.PendingLandingShockwave >= 0) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom, Vector2.Zero,
                        ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer,
                        stateContext.PendingLandingShockwave);
                    stateContext.PendingLandingShockwave = -1;
                }
            }

            stateContext.PrevVelY = npc.velocity.Y;
        }

        /// <summary>血量驱动体积(失血=凝胶流失)，保持底部中心重设碰撞盒</summary>
        private void UpdateScaleAndHitbox() {
            float lifeRatio = MathHelper.Clamp(npc.life / (float)npc.lifeMax, 0f, 1f);
            float baseScale = 0.95f + lifeRatio * 0.55f;
            if (stateContext.IsDeathMode) {
                baseScale += 0.18f;
            }
            float newScale = baseScale * stateContext.ScaleMul;

            if (Math.Abs(newScale - npc.scale) > 0.002f) {
                npc.position.X += npc.width / 2;
                npc.position.Y += npc.height;
                npc.scale = newScale;
                npc.width = (int)(98f * npc.scale);
                npc.height = (int)(92f * npc.scale);
                npc.position.X -= npc.width / 2;
                npc.position.Y -= npc.height;
            }
        }

        #endregion

        #region 帧与绘制

        public override bool FindFrame(int frameHeight) {
            int frameCount = Main.npcFrameCount[npc.type];

            int frameIndex;
            if (npc.velocity.Y < -0.5f) {
                //上升：短暂压缩后拉长
                frameIndex = npc.velocity.Y < -6f ? 5 : 4;
            }
            else if (npc.velocity.Y > 0.5f) {
                //下坠：拉长
                frameIndex = 5;
            }
            else if (stateContext != null && stateContext.VisualSquash < 0.82f) {
                //蹲缩蓄势：压缩帧
                frameIndex = 4;
            }
            else {
                //地面波顿循环
                npc.frameCounter += 1.0 + Math.Abs(npc.velocity.X) * 0.08;
                if (npc.frameCounter >= 8.0) {
                    npc.frameCounter = 0.0;
                    npc.localAI[3] = (npc.localAI[3] + 1) % 4;
                }
                frameIndex = (int)npc.localAI[3];
            }

            frameIndex = Math.Clamp(frameIndex, 0, frameCount - 1);
            npc.frame.Y = frameIndex * frameHeight;
            npc.frame.Height = frameHeight;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (stateContext == null) {
                return true;
            }
            KingSlimeRenderer.DrawBody(spriteBatch, npc, stateContext, screenPos, drawColor);
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
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

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not KingSlimeDeathState) {
                stateMachine.ChangeState(new KingSlimeDeathState());
            }

            return false;
        }

        #endregion
    }
}
