using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Summon;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.RemakeItems.ModifyBag;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
            if (CWRWorld.MachineRebellion) {
                npc.life = npc.lifeMax *= 32;
                npc.defDefense = npc.defense = 40;
                npc.defDamage = npc.damage *= 3;
                npc.scale = 1.2f;
            }
            InitializeStateContext();
        }

        public override bool? CanCWROverride() {
            return CWRWorld.MachineRebellion ? true : null;
        }

        private void InitializeStateContext() {
            stateContext = new DestroyerStateContext {
                Npc = npc,
                IsMachineRebellion = CWRWorld.MachineRebellion,
                IsDeathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive()
            };
            stateMachine = new DestroyerStateMachine(stateContext);
            stateMachine.SetInitialState(new DestroyerIntroState());
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

            CWRPlayer.TheDestroyer = npc.whoAmI;

            //延迟初始化保护
            if (stateContext == null || stateMachine == null) {
                InitializeStateContext();
            }

            FindTarget();
            UpdateStateContext();

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

                if (CWRWorld.MachineRebellion) {
                    Main.npc[index].lifeMax = headNpc.lifeMax;
                    Main.npc[index].life = headNpc.life;
                    Main.npc[index].defense = 50;
                }
            }
        }

        private void FindTarget() {
            if (npc.target < 0 || npc.target >= 255 || !targetPlayer.Alives()) {
                npc.TargetClosest();
            }
            targetPlayer = Main.player[npc.target];

            if (!targetPlayer.Alives()) {
                if (stateMachine?.CurrentState is not DestroyerDespawnState) {
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

            //绘制本体
            spriteBatch.Draw(texture, mainPos, frameRec, drawColor,
                npc.rotation + MathHelper.Pi, origin, npc.scale, SpriteEffects.None, 0f);

            //绘制发光层
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

