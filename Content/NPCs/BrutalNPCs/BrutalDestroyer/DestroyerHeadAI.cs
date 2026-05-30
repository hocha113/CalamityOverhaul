using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Modifys.ModifyBag;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.Common;
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

        private DestroyerStateMachine stateMachine;
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

        public override void SetProperty() {
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
            stateMachine = new DestroyerStateMachine(stateContext);

            //客户端加入时从npc.ai[2]恢复服务端当前状态，避免状态desync
            if (VaultUtils.isClient) {
                int serverStateIndex = (int)npc.ai[2];
                IDestroyerState syncedState = DestroyerStateMachine.CreateStateFromIndex((DestroyerStateIndex)serverStateIndex);
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
                stateMachine.ForceChangeState(new DestroyerDeathState());
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
                    stateMachine?.ForceChangeState(new DestroyerDespawnState());
                }
            }
        }

        private void UpdateMovement() {
            Vector2 direction = stateContext.TargetPosition - npc.Center;
            float distance = direction.Length();
            if (distance > 0.01f) {
                direction.Normalize();
            }

            float spd = stateContext.MoveSpeed;
            float turn = stateContext.TurnSpeed;

            if (npc.velocity.Length() < spd) {
                npc.velocity += direction * (spd / 20f);
            }

            Vector2 desiredVelocity = direction * spd;
            npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, turn / 20f);

            if (npc.velocity.Length() > spd) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * spd;
            }

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

            //冲刺中——白热高速效果
            if (stateMachine?.CurrentState is DestroyerDashingState) {
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

            //冲刺残影
            if (stateMachine?.CurrentState is DestroyerDashingState) {
                DestroyerRenderHelper.DrawDashTrail(spriteBatch, npc, texture, frameRec, origin, screenPos);
            }

            //外圈8方向描边光环——确保夜晚远距离也能看清Boss轮廓
            MechBossThermalRenderer.DrawOutlineHaloByController(spriteBatch, texture, mainPos, frameRec,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, npc.whoAmI);

            //本体绘制套上机械热感着色器（传入当前帧UV范围，避免4帧贴图邻域采样跨帧）
            bool shaderApplied = MechBossThermalRenderer.BeginThermalShaderByController(spriteBatch, texture, frameRec, npc.whoAmI, seed: 0f);

            spriteBatch.Draw(texture, mainPos, frameRec, drawColor,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            if (shaderApplied) {
                MechBossThermalRenderer.EndThermalShader(spriteBatch);
            }

            //发光层独立绘制——保留原有自发光效果不被滤镜覆盖
            spriteBatch.Draw(Head_Glow.Value, mainPos, glowRec, Color.White,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

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
                stateMachine.ForceChangeState(new DestroyerDeathState());
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

