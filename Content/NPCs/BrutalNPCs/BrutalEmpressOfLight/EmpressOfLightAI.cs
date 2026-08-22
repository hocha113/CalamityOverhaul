using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States;
using InnoVault.PRT;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight
{
    /// <summary>
    /// 光之女皇主控：InnoVault状态机全接管，棱彩弹幕艺术；
    /// npc.ai[0]/ai[1]=姿态通道（原版绘制语义） npc.ai[2]=状态机 npc.ai[3]=形态位（原版语义）
    /// </summary>
    internal class EmpressOfLightAI : CWRNPCOverride
    {
        #region 数据
        public override int TargetID => NPCID.HallowBoss;

        /// <summary>life低于此值进死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;

        private VaultStateMachine<EmpressStateContext> stateMachine;
        private EmpressStateContext stateContext;
        private Player targetPlayer;
        /// <summary>上一帧昼形态标志，检测破晓/入夜的换形瞬间</summary>
        private bool lastDayEmpowered;
        #endregion

        #region 加载与初始化
        public override void SetProperty() {
            //oldPos 供原版冲刺彩虹残影使用
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 24;
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new EmpressStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<EmpressStateContext>(stateContext, aiSlot: 2);

            //客户端从ai[2]恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<EmpressStateContext> syncedState = VaultStateRegistry<EmpressStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new EmpressIntroState());
                //中途加入的端上 SetDefaults 留下 Opacity=0/dontTakeDamage=true（原版靠case0自愈，
                //接管后的战斗状态没人清）：恢复到非入场态时补回战斗常态，防隐形且本地打不动
                if (syncedState is not null and not EmpressIntroState) {
                    npc.Opacity = 1f;
                    npc.dontTakeDamage = false;
                }
            }
            else {
                stateMachine.SetInitialState(new EmpressIntroState());
            }
            lastDayEmpowered = NPC.ShouldEmpressBeEnraged();
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化（联机中途加入）
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();

            //原版契约：满血白昼升格真昼形态（ai[3]|2），Terraprisma掉落条件依赖此位；升格后不清除、不再触发离场
            if (!VaultUtils.isClient && npc.life == npc.lifeMax
                && NPC.ShouldEmpressBeEnraged() && ((int)npc.ai[3] & 2) == 0) {
                npc.ai[3] = (int)npc.ai[3] | 2;
                npc.netUpdate = true;
            }

            UpdateDayNightForm();

            //姿态/蓄力每帧重声明，未声明回落
            stateContext.Pose = EmpressPose.Idle;
            stateContext.PoseTimer = 0f;

            //接触伤默认归零，冲刺态自行开窗
            npc.damage = 0;

            //状态机
            stateMachine?.Update();

            //姿态通道写入原版槽位（各端本地写，值确定一致；服务端负责同步）
            npc.ai[0] = (float)stateContext.Pose;
            npc.ai[1] = stateContext.PoseTimer;

            //体态细节：随横速轻微倾身（原版不设spriteDirection，保持不动）
            npc.rotation = npc.velocity.X * 0.005f;

            //翅膀扑动帧（原版绘制消费localAI[0]）
            if ((npc.localAI[0] += 1f) >= 44f) {
                npc.localAI[0] = 0f;
            }

            //二阶段防御补正（原版规约）
            npc.defense = stateContext.IsSecondPhase ? (int)(npc.defDefense * 1.2f) : npc.defDefense;

            //照明与环境
            Lighting.AddLight(npc.Center, Vector3.One * npc.Opacity * (0.9f + stateContext.DayFormBlend * 0.4f));
            UpdateAmbientVisuals();

            //投技冷却递减（服务端权威）
            if (!VaultUtils.isClient && stateContext.GrabCooldown > 0) {
                stateContext.GrabCooldown--;
            }

            //周期强制同步（服务端节流）
            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not EmpressDespawnState and not EmpressDeathState) {
                    stateMachine?.ChangeState(new EmpressDespawnState());
                }
            }
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            //二阶段从ai[3]位读出（服务端写入，客户端经同步获得，外部模组兼容原版语义）
            stateContext.IsSecondPhase = ((int)npc.ai[3] & 1) != 0;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            //昼形态：全局昼夜标志各端一致
            stateContext.DayEmpowered = NPC.ShouldEmpressBeEnraged();
            //形态视觉过渡：跨昼夜战斗平滑换形
            float blendTarget = stateContext.DayEmpowered ? 1f : 0f;
            stateContext.DayFormBlend = MathHelper.Lerp(stateContext.DayFormBlend, blendTarget, 0.012f);
            if (Math.Abs(stateContext.DayFormBlend - blendTarget) < 0.004f) {
                stateContext.DayFormBlend = blendTarget;
            }
        }

        /// <summary>昼夜换形瞬间：纯音画传达，破晓棱彩强拍，入夜柔光敛息</summary>
        private void UpdateDayNightForm() {
            bool now = stateContext.DayEmpowered;
            if (now == lastDayEmpowered) {
                return;
            }
            lastDayEmpowered = now;

            //服务端写形态位（原版ai[3]语义：+2=真昼位），契约：只有满血白昼才升格，
            //残血拖到破晓不白给Terraprisma判定；升格后永不清除
            if (!VaultUtils.isClient && now && npc.life == npc.lifeMax && ((int)npc.ai[3] & 2) == 0) {
                npc.ai[3] = (int)npc.ai[3] | 2;
                npc.netUpdate = true;
            }

            //死亡/离场演出中不叠加换形演出
            if (stateMachine?.CurrentState is EmpressDeathState or EmpressDespawnState) {
                return;
            }

            if (!VaultUtils.isServer) {
                if (now) {
                    //破晓升格：全屏棱彩强拍+双音高叠+身周迸光
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.85f, 40);
                    SoundEngine.PlaySound(SoundID.Item161 with { Volume = 0.9f, Pitch = 0.25f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.7f, Pitch = 0.4f }, npc.Center);
                    for (int i = 0; i < 14; i++) {
                        float hue = i / 14f;
                        PRTLoader.NewParticle<PRT_EmpressSpark>(npc.Center,
                            VaultUtils.RandVr(3f, 10f), EmpressMotion.Prism(hue, 0.72f),
                            Main.rand.NextFloat(0.8f, 1.3f))?.Configure(20, hue);
                    }
                }
                else {
                    //入夜敛锋：柔和的低音与一圈冷色涟漪
                    EmpressScreenFX.PushPrismPulse(npc.Center, 0.35f, 30);
                    SoundEngine.PlaySound(SoundID.Item165 with { Volume = 0.75f, Pitch = -0.3f }, npc.Center);
                    PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center, Vector2.Zero,
                        new Color(190, 160, 255), 0.7f)?.Configure(16, 0.72f);
                }
                EmpressMotion.Shake(npc.Center, 4f, 20);
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
            if (stateMachine.CurrentState is EmpressDeathState or EmpressDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new EmpressDeathState());
            }
        }

        /// <summary>环境层：昼形态屏幕棱彩描边+周身光羽（客户端）</summary>
        private void UpdateAmbientVisuals() {
            if (VaultUtils.isServer) {
                return;
            }
            if (stateContext.DayFormBlend > 0.05f) {
                EmpressScreenFX.DeclareAmbient(stateContext.DayFormBlend * 0.42f);
            }
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //底层：身后辉光与手部蓄力（画在原版本体之下）
            EmpressRenderHelper.DrawUnderGlow(spriteBatch, npc, stateContext);
            //返回null让原版多层绘制（翅膀/双臂/裙裾/二形态辉光）继续
            return null;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //顶层：手部蓄力星芒+昼形态过驱层；返回true不拦其他PostDraw
            EmpressRenderHelper.DrawOverGlow(spriteBatch, npc, stateContext);
            return true;
        }
        #endregion

        #region 杂项覆写
        /// <summary>死亡演出进行中（运镜侧查询）</summary>
        internal bool InDeathPerformance => stateMachine?.CurrentState is EmpressDeathState;

        /// <summary>死亡演出计时（运镜对表）</summary>
        internal int DeathTimer => stateMachine?.CurrentState is EmpressDeathState death ? death.Timer : 0;

        public override bool CheckActive() => false;

        /// <summary>演出中锁血，完后放行；秒杀也先切演出</summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not EmpressDeathState) {
                stateMachine.ChangeState(new EmpressDeathState());
            }

            return false;
        }
        #endregion
    }
}
