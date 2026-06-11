using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
using InnoVault.StateMachines;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer
{
    internal class DestroyerHeadAI : CWRNPCOverride, ICWRLoader
    {
        #region Data
        public override int TargetID => NPCID.TheDestroyer;

        [VaultLoaden(CWRConstant.NPC + "BTD/BTD_Head")]
        internal static Asset<Texture2D> HeadIcon = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Head")]
        internal static Asset<Texture2D> Head = null;
        [VaultLoaden(CWRConstant.NPC + "BTD/Head_Glow")]
        internal static Asset<Texture2D> Head_Glow = null;
        internal static int iconIndex;
        internal static int iconIndex_Void;

        internal const int StretchTime = 360;
        internal const int BodyCount = 60;
        /// <summary>
        /// 头部生命值低于该值时进入死亡演出阶段
        /// </summary>
        internal const int DeathPerformanceTriggerLife = 10;

        private VaultStateMachine<DestroyerStateContext> stateMachine;
        private DestroyerStateContext stateContext;
        private Player targetPlayer;

        #endregion

        #region 加载与初始化
        void ICWRLoader.LoadData() {
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.NPC + "BTD/BTD_Head", -1);
            iconIndex = ModContent.GetModBossHeadSlot(CWRConstant.NPC + "BTD/BTD_Head");
            CWRMod.Instance.AddBossHeadTexture(CWRConstant.Placeholder, -1);
            iconIndex_Void = ModContent.GetModBossHeadSlot(CWRConstant.Placeholder);
        }

        void ICWRLoader.UnLoadData() => DestroyerMotionFX.Unload();

        public override void SetProperty() {
            //头部位置缓存用于高速运动光带拖尾
            NPCID.Sets.TrailingMode[npc.type] = 1;
            NPCID.Sets.TrailCacheLength[npc.type] = 24;
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return null;
        }

        private void InitializeStateContext() {
            stateContext = new DestroyerStateContext {
                Npc = npc,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new NpcStateMachine<DestroyerStateContext>(stateContext, aiSlot: 2);

            //客户端加入时从npc.ai[2]恢复服务端当前状态，避免状态desync
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IVaultState<DestroyerStateContext> syncedState = VaultStateRegistry<DestroyerStateContext>.Create(serverStateIndex);
                stateMachine.SetInitialState(syncedState ?? new DestroyerIntroState());
            }
            else {
                stateMachine.SetInitialState(new DestroyerIntroState());
            }
        }
        #endregion

        #region 主要AI行为
        public override bool AI() {
            if (CWRWorld.CanTimeFrozen()) {
                CWRNpc.DoTimeFrozen(npc);
                return false;
            }

            if (HeadPrimeAI.DontReform()) {
                return true;
            }

            //延迟初始化保护
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();

            //摆动/演出标志每帧由状态重新声明，未声明的帧自动归零
            stateContext.SlitherStrength = 0f;

            //更新状态机
            stateMachine?.Update();

            //物理更新（除非状态跳过）
            if (!stateContext.SkipDefaultMovement) {
                UpdateMovement();
            }

            HandleMouth();
            UpdateVisuals();

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }
        #endregion

        #region 上下文更新

        private void UpdateStateContext() {
            stateContext.Npc = npc;
            stateContext.Target = targetPlayer;
            stateContext.IsEnraged = npc.life < npc.lifeMax * 0.5f;
            stateContext.IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();

            if (Main.GameUpdateCount % 60 == 0) {
                stateContext.RefreshBodySegments();
            }
        }

        /// <summary>
        /// 生命值低于阈值时切入死亡演出。仅服务端/单人端驱动状态转移，客户端经 npc.ai[2] 同步。
        /// </summary>
        private void CheckDeathPerformanceTrigger() {
            if (VaultUtils.isClient || stateContext == null || stateMachine == null) {
                return;
            }
            if (stateContext.DeathPerformanceFinished) {
                return;
            }
            if (stateMachine.CurrentState is DestroyerDeathState or DestroyerDespawnState) {
                return;
            }
            if (npc.life <= DeathPerformanceTriggerLife) {
                stateMachine.ChangeState(new DestroyerDeathState());
            }
        }

        #endregion

        #region 辅助方法（供状态类调用的静态方法）

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

        internal static void SendDespawn() {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            var packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.DespawnDestroyer);
            packet.Send();
        }

        internal static void HandleDespawn() {
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.TheDestroyer || n.type == NPCID.TheDestroyerBody
                    || n.type == NPCID.TheDestroyerTail || n.type == NPCID.Probe) {
                    n.life = 0;
                    n.HitEffect();
                    n.active = false;
                    n.netUpdate = true;
                }
            }
        }

        /// <summary>
        /// 生成体节（供IntroState调用）
        /// </summary>
        internal static void SpawnBodySegments(NPC headNpc) {
            int index = headNpc.whoAmI;
            int oldIndex;
            for (int i = 0; i < BodyCount; i++) {
                oldIndex = index;
                index = NPC.NewNPC(headNpc.FromObjectGetParent(), (int)headNpc.Center.X, (int)headNpc.Center.Y,
                    i == (BodyCount - 1) ? NPCID.TheDestroyerTail : NPCID.TheDestroyerBody,
                    0, ai0: oldIndex, ai1: index, ai2: 0, ai3: headNpc.whoAmI);
                Main.npc[index].realLife = headNpc.whoAmI;
                Main.npc[index].netUpdate = true;
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (!VaultUtils.isClient && stateMachine?.CurrentState is not DestroyerDespawnState and not DestroyerDeathState) {
                    stateMachine?.ChangeState(new DestroyerDespawnState());
                }
            }
        }

        /// <summary>
        /// 航向转向运动模型：维护"航向角+标量速度"而非直接Lerp速度向量。
        /// <br/>- 角速度限幅且随速度衰减：高速时只能划出大弧线、低速时才能急转弯，产生重量与惯性感；
        /// <br/>- 速度向目标速度做指数趋近，转向误差大时自动收油门（入弯减速、出弯加速）；
        /// <br/>- 可叠加蛇形摆动：在航向上加正弦扰动，蠕虫呈现"游动"姿态而不是匀速漂移。
        /// </summary>
        private void UpdateMovement() {
            Vector2 toTarget = stateContext.TargetPosition - npc.Center;
            float distance = toTarget.Length();
            if (distance < 0.01f) {
                return;
            }

            float desiredHeading = toTarget.ToRotation();
            float currentSpeed = npc.velocity.Length();
            float currentHeading = currentSpeed > 0.01f ? npc.velocity.ToRotation() : desiredHeading;

            //转向率：TurnSpeed（约0.2~1.5）换算为弧度/帧，并按当前速度衰减——越快越难转
            float speedFactor = MathHelper.Clamp(currentSpeed / 32f, 0f, 1f);
            float maxTurn = stateContext.TurnSpeed / 20f * MathHelper.Lerp(1.7f, 0.6f, speedFactor);
            float newHeading = currentHeading.AngleTowards(desiredHeading, maxTurn);

            //转向误差越大油门越小：入弯收油、出弯全速，模拟真实载具过弯
            float headingError = Math.Abs(MathHelper.WrapAngle(desiredHeading - newHeading));
            float throttle = MathHelper.Lerp(1f, 0.55f, MathHelper.Clamp(headingError / MathHelper.Pi, 0f, 1f));
            float targetSpeed = stateContext.MoveSpeed * throttle;
            currentSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, stateContext.AccelRate);

            //蛇形摆动：航向角上的正弦扰动，幅度随速度增强（高速游动摆幅更明显）
            float slither = stateContext.SlitherStrength;
            if (slither > 0.01f) {
                stateContext.SlitherPhase += 0.055f + currentSpeed * 0.0012f;
                float wave = (float)Math.Sin(stateContext.SlitherPhase);
                newHeading += wave * 0.24f * slither * MathHelper.Lerp(0.45f, 1f, speedFactor);
            }

            npc.velocity = newHeading.ToRotationVector2() * currentSpeed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
        }

        private void HandleMouth() {
            int gf = stateContext.GlowFrame;
            VaultUtils.ClockFrame(ref gf, 5, 3);
            stateContext.GlowFrame = gf;

            float dotProduct = Vector2.Dot(npc.velocity.UnitVector(), npc.Center.To(targetPlayer.Center).UnitVector());
            float dist = npc.Distance(targetPlayer.Center);

            if (dist < 800 && dotProduct > 0.8f) {
                if (stateContext.DontOpenMouthTime <= 0) stateContext.OpenMouth = true;
            }
            else {
                stateContext.OpenMouth = false;
            }

            if (stateContext.OpenMouth) {
                if (stateContext.Frame < 3) stateContext.Frame++;
                stateContext.DontOpenMouthTime = 60;
            }
            else {
                if (stateContext.Frame > 0) stateContext.Frame--;
            }

            if (stateContext.DontOpenMouthTime > 0) stateContext.DontOpenMouthTime--;
        }

        private void UpdateVisuals() {
            Lighting.AddLight(npc.Center, 0.8f, 0.2f, 0.2f);

            //驱动机械热感着色器：根据当前状态机确定模式与强度，整条蠕虫共用 head.whoAmI 索引
            MechBossVisualMode visMode = MechBossVisualMode.Idle;
            float visIntensity = 0.65f;//常态保持较明显的红橙描边以解决"夜晚看不清"问题
            float visProgress = 0f;

            //轨道绞杀演出：蓄能撤离=警告脉冲 / 高速俯冲=白热 / 破土回场=低强度散热
            if (stateContext.OrbitalVisual == 2) {
                visMode = MechBossVisualMode.Dashing;
                visIntensity = 1f;
                visProgress = 1f;
            }
            else if (stateContext.OrbitalVisual == 1) {
                visMode = MechBossVisualMode.Warning;
                visIntensity = 0.9f;
                visProgress = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f);
            }
            else if (stateContext.OrbitalVisual == 3) {
                visIntensity = 0.5f;
            }
            //冲刺中——白热高速效果
            else if (stateMachine?.CurrentState is DestroyerDashingState) {
                visMode = MechBossVisualMode.Dashing;
                visIntensity = 1f;
                visProgress = 1f;
            }
            //蓄力（冲刺/包围）——红黄警告
            else if (stateContext.IsCharging && (stateContext.ChargeType == 1 || stateContext.ChargeType == 3)) {
                visMode = MechBossVisualMode.Warning;
                visIntensity = 0.85f;
                visProgress = stateContext.ChargeProgress;
            }
            //其他蓄力（激光弹幕、探针阵列）——同样使用警告滤镜，进度更柔
            else if (stateContext.IsCharging) {
                visMode = MechBossVisualMode.Warning;
                visIntensity = 0.75f;
                visProgress = stateContext.ChargeProgress * 0.7f;
            }
            //狂暴期常态描边稍强一点
            else if (stateContext.IsEnraged) {
                visIntensity = 0.8f;
            }

            //死亡演出——整条蠕虫剧烈红黄过载脉冲，表现"严重故障"
            if (stateMachine?.CurrentState is DestroyerDeathState) {
                visMode = MechBossVisualMode.Warning;
                visIntensity = 1f;
                visProgress = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f);
            }

            MechBossVisualState.Push(npc.whoAmI, visMode, visIntensity, visProgress);
        }

        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (HeadPrimeAI.DontReform()) return true;
            if (stateContext == null) return true;

            Texture2D texture = Head.Value;
            Rectangle frameRec = texture.GetRectangle(stateContext.Frame, 4);
            Rectangle glowRec = texture.GetRectangle(stateContext.GlowFrame, 4);
            Vector2 origin = frameRec.Size() / 2;
            Vector2 mainPos = npc.Center - screenPos;

            //蓄力特效
            DestroyerRenderHelper.DrawChargeEffect(spriteBatch, stateContext);

            //高速光带拖尾：速度驱动，冲刺/俯冲时自动出现（替代旧的逐帧贴图残影）
            float trailIntensity = MathHelper.Clamp((npc.velocity.Length() - 16f) / 30f, 0f, 1f);
            DestroyerMotionFX.DrawHeadTrail(npc, trailIntensity);

            //读取共享视觉状态并叠加头部位置充能波
            var (visMode, visIntensity, visProgress) = MechBossVisualState.Read(npc.whoAmI);
            float wave = DestroyerChargeWave.Read(npc.whoAmI, 0f);
            if (wave > 0.01f) {
                if (visMode == MechBossVisualMode.Idle) {
                    visMode = MechBossVisualMode.Warning;
                }
                visIntensity = Math.Max(visIntensity, wave);
                visProgress = Math.Max(visProgress, wave);
            }

            //外圈8方向描边光环——确保夜晚远距离也能看清Boss轮廓
            MechBossThermalRenderer.DrawOutlineHalo(spriteBatch, texture, mainPos, frameRec,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None,
                visMode, visIntensity, visProgress);

            //本体绘制套上机械热感着色器（传入当前帧UV范围，避免4帧贴图邻域采样跨帧）
            bool shaderApplied = MechBossThermalRenderer.BeginThermalShader(spriteBatch, texture, frameRec,
                visMode, visIntensity, visProgress, seed: 0f);

            spriteBatch.Draw(texture, mainPos, frameRec, drawColor,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            //发光层独立绘制——保留原有自发光效果不被滤镜覆盖
            spriteBatch.Draw(Head_Glow.Value, mainPos, glowRec, Color.White,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            //充能波白热叠加
            if (wave > 0.05f) {
                Color hot = new Color(255, 165, 75, 0) * wave;
                spriteBatch.Draw(Head_Glow.Value, mainPos, glowRec, hot,
                    npc.rotation + MathHelper.Pi, origin, npc.scale * 1.04f, SpriteEffects.None, 0f);
            }

            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return HeadPrimeAI.DontReform();
        }
        #endregion

        #region 掉落物处理
        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.SimpleAdd(ModContent.ItemType<DestroyersBlade>(), 4);
            rule.SimpleAdd(ModContent.ItemType<StaffoftheDestroyer>(), 4);
            rule.SimpleAdd(ModContent.ItemType<Observer>(), 4);
            npcLoot.Add(rule);
        }

        public override bool CheckActive() => false;

        /// <summary>
        /// 死亡演出未结束前一律锁血拦截死亡；演出完毕后放行，触发正常掉落与击杀标记。
        /// 同时作为高额伤害一击致死的兜底，确保必定播放死亡演出。
        /// </summary>
        public override bool? CheckDead() {
            if (stateContext == null || stateContext.DeathPerformanceFinished) {
                return true;
            }

            npc.life = 1;
            npc.dontTakeDamage = true;

            if (!VaultUtils.isClient && stateMachine != null && stateMachine.CurrentState is not DestroyerDeathState) {
                stateMachine.ChangeState(new DestroyerDeathState());
            }

            return false;
        }
        #endregion

        #region 地图图标
        public override void BossHeadSlot(ref int index) {
            if (!HeadPrimeAI.DontReform()) {
                index = iconIndex;
            }
        }

        public override void BossHeadRotation(ref float rotation) {
            if (!HeadPrimeAI.DontReform()) {
                rotation = npc.rotation + MathHelper.Pi;
            }
        }

        public override void ModifyDrawNPCHeadBoss(ref float x, ref float y, ref int bossHeadId,
            ref byte alpha, ref float headScale, ref float rotation, ref SpriteEffects effects) {
            if (!HeadPrimeAI.DontReform()) {
                bossHeadId = iconIndex;
                rotation = npc.rotation + MathHelper.Pi;
            }
        }
        #endregion
    }
}

