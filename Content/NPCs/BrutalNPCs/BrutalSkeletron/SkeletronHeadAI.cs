using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron
{
    /// <summary>骷髅王头部 NPCOverride，States 驱动，契约见 SkeletronPhase、npc.ai[2]</summary>
    internal class SkeletronHeadAI : BrutalNPCOverride, ICWRLoader
    {
        #region 数据
        public override int TargetID => NPCID.SkeletronHead;

        /// <summary>目标失效判定距离</summary>
        private const int MaxFindDistance = 6200;

        /// <summary>当前活跃头 whoAmI，各端本地登记，无则 -1</summary>
        internal static int ActiveHeadIndex = -1;
        /// <summary>死亡演出中的头 whoAmI，无则 -1</summary>
        internal static int ActivePerformanceHead = -1;

        private VaultStateMachine<SkeletronStateContext> stateMachine;
        private SkeletronStateContext stateContext;
        private Player targetPlayer;

        /// <summary>当前状态上下文（绘制/运镜层只读）</summary>
        internal SkeletronStateContext Context => stateContext;
        #endregion

        #region 加载
        void ICWRLoader.SetupData() {
            //拖影缓存（残影绘制用）
            NPCID.Sets.TrailingMode[NPCID.SkeletronHead] = 1;
            NPCID.Sets.TrailCacheLength[NPCID.SkeletronHead] = 12;
            NPCID.Sets.TrailingMode[NPCID.SkeletronHand] = 1;
            NPCID.Sets.TrailCacheLength[NPCID.SkeletronHand] = 10;
        }

        void ICWRLoader.UnLoadData() {
            ActiveHeadIndex = -1;
            ActivePerformanceHead = -1;
            SkeletronScreenEffects.Clear();
            SkeletronRenderHelper.Unload();
        }

        public override bool? CanBrutalOverride() {
            return null;
        }
        #endregion

        #region 初始化
        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.knockBackResist = 0;
            for (int i = 0; i < ai.Length; i++) {
                ai[i] = 0;
            }
            InitializeStateContext();
        }

        private void InitializeStateContext() {
            stateContext = new SkeletronStateContext {
                Npc = npc,
                Owner = this
            };
            stateMachine = new NpcStateMachine<SkeletronStateContext>(stateContext, aiSlot: SkeletronAiSlots.HeadStateSlot);

            //中途加入从 ai[2] 恢复状态
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[SkeletronAiSlots.HeadStateSlot];
                IVaultState<SkeletronStateContext> syncedState = VaultStateRegistry<SkeletronStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new SkeletronIntroState());
            }
            else {
                stateMachine.SetInitialState(new SkeletronIntroState());
            }
        }
        #endregion

        #region 主AI
        public override bool AI() {
            //延迟初始化
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            npc.aiStyle = -1;
            npc.knockBackResist = 0;
            npc.netOffset = Vector2.Zero;
            npc.dontTakeDamage = false;
            npc.chaseable = true;

            ActiveHeadIndex = npc.whoAmI;

            FindTarget();
            UpdateStateContext();

            //投技冷却（仅权威端消费）
            if (!VaultUtils.isClient && stateContext.SnatchCooldown > 0) {
                stateContext.SnatchCooldown--;
            }

            EvaluateGlobalTransitions();

            //双手健在时头有额外骨甲
            npc.defense = stateContext.AnyHandAlive ? npc.defDefense + 14 : npc.defDefense;

            stateMachine.Update();

            UpdateVisualEnvelope();

            //编队旋转时钟，各端确定性自增
            ai[SkeletronAiSlots.OverrideOrbitClock]++;

            //服务器周期广播，10帧节流
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

            if (TargetInvalid()) {
                npc.TargetClosest();
                targetPlayer = Main.player[npc.target];
            }

            //登场前不脱战
            if (!VaultUtils.isClient && npc.ai[SkeletronAiSlots.HeadPhase] > SkeletronPhase.Intro && TargetInvalid()
                && stateMachine?.CurrentState is not SkeletronDespawnState and not SkeletronDeathState) {
                stateMachine?.ChangeState(new SkeletronDespawnState());
            }
        }

        private bool TargetInvalid() {
            return targetPlayer == null || targetPlayer.dead || !targetPlayer.active
                || Math.Abs(npc.position.X - targetPlayer.position.X) > MaxFindDistance
                || Math.Abs(npc.position.Y - targetPlayer.position.Y) > MaxFindDistance;
        }

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.Owner = this;
            stateContext.BossRush = CWRRef.GetBossRushActive();
            stateContext.DeathMode = CWRRef.GetDeathMode() || stateContext.BossRush;
            stateContext.MasterMode = Main.masterMode || stateContext.BossRush;

            stateContext.HandCount = SkeletronFacts.CountHands(npc, out NPC left, out NPC right);
            stateContext.LeftHand = left;
            stateContext.RightHand = right;
        }

        /// <summary>全局转移，服务端驱动，优先级 死亡&gt;转阶段&gt;大招&gt;白昼</summary>
        private void EvaluateGlobalTransitions() {
            if (VaultUtils.isClient || stateMachine?.CurrentState == null) {
                return;
            }

            IVaultState<SkeletronStateContext> current = stateMachine.CurrentState;
            int phase = (int)npc.ai[SkeletronAiSlots.HeadPhase];

            //死亡演出
            if (phase > SkeletronPhase.Intro && npc.life <= SkeletronDirector.DeathTriggerLife
                && !stateContext.DeathPerformanceFinished && current is not SkeletronDeathState) {
                stateMachine.ChangeState(new SkeletronDeathState());
                return;
            }
            if (current is SkeletronDeathState or SkeletronDespawnState or SkeletronIntroState or SkeletronPhaseTransitionState) {
                return;
            }

            //拍捉持人期间不被转阶段/白昼打断（死亡例外已在上方放行），出口清理由状态 OnExit 保证
            if (current is SkeletronPalmSnatchState && npc.ai[SkeletronAiSlots.HeadParamA] > 0f) {
                return;
            }

            //断手狂化
            if (phase == SkeletronPhase.Bound && ShouldPhaseTransition()) {
                stateMachine.ChangeState(new SkeletronPhaseTransitionState());
                return;
            }

            //白昼狂暴
            if (Main.IsItDay() && !stateContext.BossRush && current is not SkeletronDayEnrageState) {
                stateMachine.ChangeState(new SkeletronDayEnrageState());
                return;
            }
            if (current is SkeletronDayEnrageState) {
                return;
            }

            //低血大招（只在二阶段稳态放行一次）
            if (phase == SkeletronPhase.Unbound && !stateContext.UltUsed
                && npc.life <= npc.lifeMax * SkeletronDirector.UltLifeRatio
                && current is SkeletronHubState) {
                stateContext.UltUsed = true;
                stateMachine.ChangeState(new SkeletronBoneMaelstromState());
            }
        }

        private bool ShouldPhaseTransition() {
            return npc.life <= npc.lifeMax * SkeletronDirector.PhaseLifeRatio || stateContext.HandCount == 0;
        }

        /// <summary>视觉包络：眼火/冠火收敛（各端本地）</summary>
        private void UpdateVisualEnvelope() {
            int phase = (int)npc.ai[SkeletronAiSlots.HeadPhase];

            //冠火只在二阶段常驻
            float crownTarget = phase >= SkeletronPhase.Unbound && phase != SkeletronPhase.DeathShow ? 1f : 0f;
            if (stateMachine?.CurrentState is SkeletronDayEnrageState) {
                crownTarget = 1.35f;
            }
            if (stateMachine?.CurrentState is SkeletronDeathState) {
                crownTarget = 0f;
            }
            stateContext.CrownFlame = MathHelper.Lerp(stateContext.CrownFlame, crownTarget, 0.05f);

            //眼火基线回归（状态每帧主动设值时基线拉力可忽略）
            if (phase > SkeletronPhase.Intro && phase != SkeletronPhase.DeathShow) {
                stateContext.EyeFlame = MathHelper.Lerp(stateContext.EyeFlame, 1f, 0.03f);
            }

            //预警线自然衰减，状态内主动抬升
            stateContext.DashTelegraph *= 0.9f;
            if (stateContext.DashTelegraph < 0.02f) {
                stateContext.DashTelegraph = 0f;
            }
        }
        #endregion

        #region 对外契约与静态工具

        /// <summary>读取头部当前同步状态索引</summary>
        internal static SkeletronStateIndex GetStateIndex(NPC head) {
            return (SkeletronStateIndex)(int)head.ai[SkeletronAiSlots.HeadStateSlot];
        }

        /// <summary>难度伤害修正（沿用机械三王规则）</summary>
        internal static int SetMultiplier(int num) {
            if (!CWRRef.GetBossRushActive() && CWRWorld.Revenge) {
                num = (int)(num * 0.85f);
            }
            return num;
        }

        /// <summary>骷髅弹幕基准伤害（原始值，敌对弹幕由难度自动倍化）</summary>
        internal static int GetSkullDamage(NPC head) {
            int damage = 16;
            if (CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()) {
                damage += 3;
            }
            return SetMultiplier(damage);
        }

        /// <summary>生成双手（服务端）</summary>
        internal void SpawnHands() {
            if (VaultUtils.isClient) {
                return;
            }
            for (int side = -1; side <= 1; side += 2) {
                int hand = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X + side * 120, (int)npc.Center.Y, NPCID.SkeletronHand, npc.whoAmI);
                if (hand < 0 || hand >= Main.maxNPCs) {
                    continue;
                }
                Main.npc[hand].ai[SkeletronAiSlots.HandSide] = side;
                Main.npc[hand].ai[SkeletronAiSlots.HandHeadIndex] = npc.whoAmI;
                Main.npc[hand].target = npc.target;
                Main.npc[hand].netUpdate = true;
            }
        }

        #endregion

        #region 死亡与掉落

        /// <summary>演出未完锁血，播完放行，秒杀也先切死亡演出</summary>
        public override bool? CheckDead() {
            int phase = (int)npc.ai[SkeletronAiSlots.HeadPhase];

            //登场锁血
            if (phase == SkeletronPhase.Uninit || phase == SkeletronPhase.Intro) {
                npc.dontTakeDamage = true;
                npc.life = 1;
                return false;
            }

            //演出完放行真死
            if (stateContext != null && stateContext.DeathPerformanceFinished) {
                return true;
            }

            //秒杀也锁血切死亡演出
            npc.dontTakeDamage = true;
            npc.life = 1;
            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not SkeletronDeathState) {
                stateMachine.ChangeState(new SkeletronDeathState());
            }
            return false;
        }

        #endregion

        #region 绘制

        public override bool FindFrame(int frameHeight) {
            //单帧贴图，无帧动画
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            float alphaFade = 1f - npc.alpha / 255f;
            if (stateContext == null) {
                return true;
            }

            //旋杀涡流（头后加色层）
            if (stateContext.SpinVortex > 0.02f) {
                SkeletronRenderHelper.DrawSpinVortex(spriteBatch, npc.Center,
                    npc.rotation, stateContext.SpinVortex, stateContext.VortexConverge);
            }

            //冲刺预警线
            if (stateContext.DashTelegraph > 0.02f) {
                SkeletronRenderHelper.DrawDashTelegraph(spriteBatch, npc.Center,
                    npc.ai[SkeletronAiSlots.HeadParamB], stateContext.DashTelegraph);
            }

            Main.instance.LoadNPC(NPCID.SkeletronHead);
            Texture2D tex = TextureAssets.Npc[NPCID.SkeletronHead].Value;
            Rectangle rect = new Rectangle(0, 0, tex.Width, tex.Height);
            Vector2 orig = rect.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);

            //运动残影：旋转期做角度错相涂抹，高速平移做位置拖影
            DrawMotionGhosts(spriteBatch, tex, rect, orig, alphaFade);

            //本体
            spriteBatch.Draw(tex, drawPos, rect, drawColor * alphaFade, npc.rotation, orig, npc.scale, SpriteEffects.None, 0f);

            //幽蓝描边呼吸（灵体身份，弱层，预乘批 A=0 加色）
            float breath = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.4f);
            spriteBatch.Draw(tex, drawPos, rect,
                SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * (0.13f * breath * alphaFade),
                npc.rotation, orig, npc.scale * 1.045f, SpriteEffects.None, 0f);

            //眼火走 SkeletronEyeFlame.fx 怨火瞳（二阶段冠火包络借作诅咒紫化）；冠火走 SkeletronCurseFlame.fx 冷焰批
            SkeletronRenderHelper.DrawEyeFlames(spriteBatch, npc, stateContext.EyeFlame, alphaFade,
                MathHelper.Clamp(stateContext.CrownFlame, 0f, 1f));
            SkeletronRenderHelper.DrawCrownFlames(npc, stateContext.CrownFlame, alphaFade);

            return false;
        }

        /// <summary>速度门控的运动涂抹</summary>
        private void DrawMotionGhosts(SpriteBatch spriteBatch, Texture2D tex, Rectangle rect, Vector2 orig, float alphaFade) {
            bool spinning = stateContext.SpinVortex > 0.15f;
            float speed = npc.velocity.Length();

            //旋杀/冲刺轨迹绸带（顶点层，压在头颅之下）
            float ribbonHeat = MathHelper.Clamp((speed - 8f) / 16f, 0f, 1f);
            if (spinning) {
                ribbonHeat = Math.Max(ribbonHeat, stateContext.SpinVortex * 0.8f);
            }
            SkeletronRenderHelper.DrawMotionRibbon(npc, ribbonHeat, 40f * npc.scale, 0.6f * alphaFade);

            if (spinning) {
                //旋转涂抹：着色器对颅骨贴图做逐角回溯残像+颅外风环；缺失回退角度错相三重影
                float spinStrength = MathHelper.Clamp(stateContext.SpinVortex, 0f, 1f);
                float smear = MathHelper.Lerp(0.7f, 1.8f, spinStrength);
                if (!SkeletronRenderHelper.DrawSpinBlur(spriteBatch, npc, tex, rect, smear, spinStrength, alphaFade)) {
                    for (int i = 1; i <= 3; i++) {
                        float back = i * 0.24f;
                        Color col = SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostCyan)
                            * (0.16f * (1f - i / 4f) * alphaFade);
                        spriteBatch.Draw(tex, npc.Center - Main.screenPosition, rect, col,
                            npc.rotation - back * Math.Sign(npc.velocity.X == 0 ? 1 : npc.velocity.X), orig, npc.scale, SpriteEffects.None, 0f);
                    }
                }
            }

            float heat = MathHelper.Clamp((speed - 8f) / 18f, 0f, 1f);
            if (heat > 0.05f) {
                for (int i = 1; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size / 2f - Main.screenPosition;
                    float fade = 1f - i / (float)npc.oldPos.Length;
                    spriteBatch.Draw(tex, ghostPos, rect,
                        SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * (0.3f * fade * heat * alphaFade),
                        npc.rotation, orig, npc.scale, SpriteEffects.None, 0f);
                }
            }
        }

        #endregion
    }
}
