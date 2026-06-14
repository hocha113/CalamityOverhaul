using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
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

        internal const int BodyCount = 60;
        /// <summary>头部 life 低于此值进入死亡演出</summary>
        internal const int DeathPerformanceTriggerLife = 10;

        private VaultStateMachine<DestroyerStateContext> stateMachine;
        private DestroyerStateContext stateContext;
        private Player targetPlayer;
        /// <summary>远距滞留计时：头部远离玩家超过阈值的持续帧数，达到上限触发回归瞬移阀</summary>
        private int farTimer;

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
            //头部 oldPos 缓存，高速光带拖尾
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

            //延迟初始化保护
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();
            CheckDeathPerformanceTrigger();

            //摆动/演出/下颚标志每帧由状态重新声明，未声明的帧自动归零
            stateContext.SlitherStrength = 0f;
            stateContext.JawCommand = 0;

            //更新状态机
            stateMachine?.Update();

            //物理更新（除非状态跳过）
            if (!stateContext.SkipDefaultMovement) {
                UpdateMovement();
            }

            //远距回归瞬移阀：防止Boss在玩家屏幕外盘旋打弹幕
            UpdateFarReturnValve();

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

        /// <summary>生命≤阈值切死亡演出；服务端驱动，客户端经 npc.ai[2] 同步</summary>
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

        /// <summary>生成体节（IntroState 调用）</summary>
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

        /// <summary>航向+标量速度运动：角速度限幅、入弯收油、可叠蛇形摆动</summary>
        private void UpdateMovement() {
            Vector2 toTarget = stateContext.TargetPosition - npc.Center;
            float distance = toTarget.Length();
            if (distance < 0.01f) {
                return;
            }

            float desiredHeading = toTarget.ToRotation();
            float currentSpeed = npc.velocity.Length();
            float currentHeading = currentSpeed > 0.01f ? npc.velocity.ToRotation() : desiredHeading;

            //转向率随速度衰减，越快越难转
            float speedFactor = MathHelper.Clamp(currentSpeed / 32f, 0f, 1f);
            float maxTurn = stateContext.TurnSpeed / 20f * MathHelper.Lerp(1.7f, 0.6f, speedFactor);
            float newHeading = currentHeading.AngleTowards(desiredHeading, maxTurn);

            //转向误差越大油门越小：入弯收油、出弯全速，模拟真实载具过弯
            float headingError = Math.Abs(MathHelper.WrapAngle(desiredHeading - newHeading));
            float throttle = MathHelper.Lerp(1f, 0.55f, MathHelper.Clamp(headingError / MathHelper.Pi, 0f, 1f));
            float targetSpeed = stateContext.MoveSpeed * throttle;
            float accelRate = stateContext.AccelRate;

            //远距回归加速：目标点远在视野外时无视状态给定的低速全力归位，
            //消灭"招式收尾后慢吞吞飞回来"的脱屏死时间
            if (distance > 1400f) {
                float catchUp = Math.Min(distance / 55f, 62f);
                targetSpeed = Math.Max(targetSpeed, catchUp);
                accelRate = Math.Max(accelRate, 0.085f);
            }

            currentSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, accelRate);

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

        /// <summary>远距回归瞬移阀：持续远离时瞬移到视野边缘；<see cref="DestroyerStateBase.AllowFarSnap"/> 可关</summary>
        private void UpdateFarReturnValve() {
            if (stateMachine?.CurrentState is not DestroyerStateBase state || !state.AllowFarSnap) {
                farTimer = 0;
                return;
            }
            if (!targetPlayer.Alives()) {
                farTimer = 0;
                return;
            }

            float dist = npc.Distance(targetPlayer.Center);
            if (dist <= 2600f) {
                farTimer = 0;
                return;
            }

            if (++farTimer < 30) {
                return;
            }
            farTimer = 0;

            //瞬移到视野边缘，调头朝向玩家，体节链屏外重整
            Vector2 dir = (npc.Center - targetPlayer.Center).SafeNormalize(-Vector2.UnitY);
            npc.Center = targetPlayer.Center + dir * 1250f;
            float speed = Math.Max(npc.velocity.Length(), 26f);
            npc.velocity = -dir * speed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            npc.netUpdate = true;
        }

        private void HandleMouth() {
            int gf = stateContext.GlowFrame;
            VaultUtils.ClockFrame(ref gf, 5, 3);
            stateContext.GlowFrame = gf;

            if (stateContext.JawCommand == 1) {
                //强制威吓张口（蓄力/俯冲中），无视冷却
                stateContext.OpenMouth = true;
            }
            else if (stateContext.JawCommand == 2) {
                //猛然咬合：双倍合拢，短暂禁张口
                stateContext.OpenMouth = false;
                if (stateContext.Frame > 0) stateContext.Frame--;
                stateContext.DontOpenMouthTime = Math.Max(stateContext.DontOpenMouthTime, 10);
            }
            else {
                float dotProduct = Vector2.Dot(npc.velocity.UnitVector(), npc.Center.To(targetPlayer.Center).UnitVector());
                float dist = npc.Distance(targetPlayer.Center);

                if (dist < 800 && dotProduct > 0.8f) {
                    if (stateContext.DontOpenMouthTime <= 0) stateContext.OpenMouth = true;
                }
                else {
                    stateContext.OpenMouth = false;
                }
            }

            if (stateContext.OpenMouth) {
                if (stateContext.Frame < 3) stateContext.Frame++;
                if (stateContext.JawCommand != 1) {
                    stateContext.DontOpenMouthTime = 60;
                }
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
            //冲刺中：白热高速
            else if (stateMachine?.CurrentState is DestroyerDashingState) {
                visMode = MechBossVisualMode.Dashing;
                visIntensity = 1f;
                visProgress = 1f;
            }
            //蓄力（冲刺/包围）：红黄警告
            else if (stateContext.IsCharging && (stateContext.ChargeType == 1 || stateContext.ChargeType == 3)) {
                visMode = MechBossVisualMode.Warning;
                visIntensity = 0.85f;
                visProgress = stateContext.ChargeProgress;
            }
            //其他蓄力（激光/探针）：警告滤镜，进度更柔
            else if (stateContext.IsCharging) {
                visMode = MechBossVisualMode.Warning;
                visIntensity = 0.75f;
                visProgress = stateContext.ChargeProgress * 0.7f;
            }
            //狂暴期常态描边稍强一点
            else if (stateContext.IsEnraged) {
                visIntensity = 0.8f;
            }

            //死亡演出：红黄过载脉冲
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

            //外圈8向描边，远距可读
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

            //发光层独立绘制，自发光不受滤镜
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
            return false;
        }
        #endregion

        #region 掉落

        public override void ModifyNPCLoot(NPC thisNPC, NPCLoot npcLoot) {
            IItemDropRuleCondition condition = new DropInDeathMode();
            LeadingConditionRule rule = new LeadingConditionRule(condition);
            rule.SimpleAdd(ModContent.ItemType<DestroyersBlade>(), 4);
            rule.SimpleAdd(ModContent.ItemType<StaffoftheDestroyer>(), 4);
            rule.SimpleAdd(ModContent.ItemType<Observer>(), 4);
            npcLoot.Add(rule);
        }

        public override bool CheckActive() => false;

        /// <summary>演出未完锁血拦截；演出完毕放行掉落；秒杀也先锁血切死亡演出</summary>
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
            index = iconIndex;
        }

        public override void BossHeadRotation(ref float rotation) {
            rotation = npc.rotation + MathHelper.Pi;
        }

        public override void ModifyDrawNPCHeadBoss(ref float x, ref float y, ref int bossHeadId,
            ref byte alpha, ref float headScale, ref float rotation, ref SpriteEffects effects) {
            bossHeadId = iconIndex;
            rotation = npc.rotation + MathHelper.Pi;
        }
        #endregion
    }
}

