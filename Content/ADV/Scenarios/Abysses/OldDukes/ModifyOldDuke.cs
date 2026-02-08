using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using CalamityOverhaul.OtherMods.BossChecklist;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.CampsiteInteractionDialogue;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    /// <summary>
    /// 老公爵NPC行为覆盖，负责管理初见剧情、战败潜水、切磋战斗、营地重定向等功能
    /// <para>AI行为模式分为两大类：剧情模式和战斗模式</para>
    /// <para>剧情模式：初见对话(接近玩家->等待对话->根据选择离开或开战)和战败后潜入海中</para>
    /// <para>战斗模式：切磋或选择战斗后执行原版AI</para>
    /// </summary>
    internal class ModifyOldDuke : NPCOverride, ILocalizedModType
    {
        public override int TargetID => CWRID.NPC_OldDuke;

        #region AI状态定义

        /// <summary>
        /// 剧情AI状态，存储在ai[0]中，通过网络自动同步
        /// </summary>
        private enum OldDukeAIState
        {
            //友好接近玩家，飞到玩家上方等待
            FriendlyApproach = 0,
            //悬浮在玩家上方，等待对话结束
            DialoguePause = 1,
            //潜入海中消失（战败或对话后离开）
            LeavingDive = 2,
            //播放战斗准备动画后切换到原版AI
            StartBattle = 3
        }

        //ai槽位分配：
        //ai[0]=State：当前剧情AI状态
        //ai[1]=Timer：状态内计时器
        //ai[2]=SubState：子状态（离开潜水的两个阶段）
        //ai[3]=LeavingDiveFlag：潜水离开标记，1=正在离开，用于多人同步
        private ref float State => ref ai[0];
        private ref float Timer => ref ai[1];
        private ref float SubState => ref ai[2];
        private ref float LeavingDiveFlag => ref ai[3];

        #endregion

        #region 字段和属性

        public string LocalizationCategory => "NPCModifys";
        public static LocalizedText LeavingDiveText { get; private set; }

        //绘制控制，在AI运行前为false，阻止首帧闪烁
        private bool canDraw;
        //避免CheckDead被重复调用
        private int deadCounter;
        //剧情对话是否已触发（本实例生命周期内）
        private bool hasTriggeredDialogue;

        #endregion

        #region 生命周期

        public override bool CanOverride() {
            if (CWRRef.GetBossRushActive()) {
                return false;
            }
            return base.CanOverride();
        }

        public override void SetStaticDefaults() {
            LeavingDiveText = this.GetLocalization(nameof(LeavingDiveText), () => "老公爵潜入了水中...");
        }

        public override void SetProperty() {
            canDraw = false;
            deadCounter = 0;
            hasTriggeredDialogue = false;
        }

        #endregion

        #region 状态判断辅助方法

        /// <summary>
        /// 判断当前NPC是否处于潜水离开状态
        /// </summary>
        private bool IsInLeavingDive => LeavingDiveFlag == 1f;

        /// <summary>
        /// 根据目标玩家的存档数据判断是否需要进入剧情模式。
        /// 剧情模式适用于：首次相遇、已相遇但未选择、拒绝合作后再次相遇
        /// </summary>
        private static bool ShouldEnterStoryMode(Player target) {
            if (!target.TryGetADVSave(out var save)) {
                return false;
            }
            return save.OldDukeState switch {
                OldDukeInteractionState.NotMet => true,
                OldDukeInteractionState.Met => true,
                OldDukeInteractionState.DeclinedCooperation => true,
                _ => false
            };
        }

        /// <summary>
        /// 判断是否需要在合作确认后直接执行离开动画。
        /// 当玩家已选择合作但NPC还在场上时需要执行离开
        /// </summary>
        private static bool ShouldLeaveAfterCooperation(Player target) {
            if (!target.TryGetADVSave(out var save)) {
                return false;
            }
            return save.OldDukeState == OldDukeInteractionState.AcceptedCooperation;
        }

        /// <summary>
        /// 判断是否应该将NPC重定向到营地（消失并触发营地对话）。
        /// 条件：营地已生成、不在切磋状态、当前玩家是本地玩家、不在服务端
        /// </summary>
        private static bool ShouldRedirectToCampsite(Player target) {
            return OldDukeCampsite.IsGenerated
                && !OldDukeCampsite.WannaToFight
                && target.whoAmI == Main.myPlayer
                && !VaultUtils.isServer;
        }

        #endregion

        #region 帧动画

        public override bool FindFrame(int frameHeight) {
            //剧情模式下使用温和的帧动画，减1避免张嘴攻击帧
            if (npc.friendly && npc.dontTakeDamage) {
                npc.frameCounter += 0.08f;
                npc.frameCounter %= (Main.npcFrameCount[npc.type] - 1);
                int frame = (int)npc.frameCounter;
                npc.frame.Y = frame * frameHeight;
                return false;
            }
            return base.FindFrame(frameHeight);
        }

        #endregion

        #region 死亡处理（战败潜水）

        public override bool? CheckDead() {
            //老公爵不会被杀死，每次被击败都只是潜入海中离开
            LeavingDiveFlag = 1f;
            npc.life = npc.lifeMax;
            npc.dontTakeDamage = true;
            if (!VaultUtils.isClient) {
                npc.DropItem();
            }
            if (deadCounter == 0) {
                deadCounter++;
                CWRRef.OldDukeOnKill(npc);
            }
            VaultUtils.Text(LeavingDiveText.Value, Color.YellowGreen);
            //清除残骸粒子，避免影响潜入视觉效果
            foreach (var g in Main.gore) {
                g.active = false;
            }
            return false;
        }

        #endregion

        #region 网络消息处理

        /// <summary>
        /// 处理营地重定向的网络消息。服务端收到后广播给所有其他客户端，
        /// 客户端收到后在本地触发对应的场景对话
        /// </summary>
        internal static void StartCampsiteFindMeScenarioNetWork(BinaryReader reader, int whoAmI) {
            int npcIndex = reader.ReadInt32();
            if (!npcIndex.TryGetNPC(out var npc)) {
                return;
            }

            //隐藏Boss列表中的活跃标记，避免消失时弹出信息
            if (BCKRef.Has) {
                BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);
            }
            npc.active = false;
            npc.netUpdate = true;

            if (VaultUtils.isServer) {
                //服务端转发给其他客户端
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.StartCampsiteFindMeScenario);
                packet.Write(npc.whoAmI);
                packet.Send(-1, whoAmI);
            }
            else {
                //客户端触发对应的场景对话
                TriggerCampsiteScenario();
            }
        }

        /// <summary>
        /// 处理切磋生成老公爵的网络消息。
        /// 服务端收到后设置状态并生成NPC
        /// </summary>
        /// <summary>
        /// 处理切磋生成老公爵的网络消息。
        /// 服务端收到后：先广播WannaToFight=true给所有客户端，再生成NPC。
        /// 这样保证所有客户端在NPC到达前就已知道这是切磋模式，
        /// 避免NPC首帧AI因ShouldLeaveAfterCooperation()而消失。
        /// </summary>
        internal static void SpwanOldDukeByWannaToFightNetWork(BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadInt32();
            Player player = Main.player[playerIndex];

            if (!VaultUtils.isServer) {
                return;
            }

            //步骤1：先设置服务端状态
            OldDukeCampsite.WannaToFight = true;

            //步骤2：先广播WannaToFight=true给所有客户端
            //这样客户端在收到NPC同步包之前就已经知道这是切磋模式
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeEffect);
            packet.Write(false); //IsActive由声明式计算管理，这里写false
            packet.Write(true);  //WannaToFight = true
            packet.Write(playerIndex);
            packet.Write((byte)OldDukeInteractionState.AcceptedCooperation);
            packet.Send();

            //步骤3：最后才生成NPC（NPC同步包会在OldDukeEffect包之后到达客户端）
            NPC.NewNPC(NPC.GetBossSpawnSource(player.whoAmI),
                (int)player.Center.X, (int)player.Center.Y - 200, CWRID.NPC_OldDuke);
        }

        /// <summary>
        /// 根据当前是否在酸雨事件中，触发对应的营地场景对话
        /// </summary>
        private static void TriggerCampsiteScenario() {
            if (CWRRef.GetAcidRainEventIsOngoing()) {
                ScenarioManager.Reset<CampsiteInteractionDialogue_Choice4>();
                ScenarioManager.Start<CampsiteInteractionDialogue_Choice4>();
            }
            else {
                ScenarioManager.Reset<ComeCampsiteFindMe>();
                ScenarioManager.Start<ComeCampsiteFindMe>();
            }
        }

        #endregion

        #region 主AI逻辑

        public override bool AI() {
            canDraw = true;
            npc.alpha = 0;

            //战败后的潜水离开，优先级最高，无论什么状态都要执行
            if (IsInLeavingDive) {
                State = (float)OldDukeAIState.LeavingDive;
                return RunStorylineAI();
            }

            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //切磋模式：WannaToFight已设置，直接执行原版AI
            if (OldDukeCampsite.WannaToFight) {
                KillDukeSummonerProjectiles();
                return true;
            }

            //检查是否需要进入剧情模式（首次相遇或拒绝合作后再次相遇）
            if (ShouldEnterStoryMode(target)) {
                //首次相遇时标记为已遇见
                MarkAsMetIfNeeded(target);
                return RunStorylineAI();
            }

            //接受合作后，需要执行离开潜水动画
            if (ShouldLeaveAfterCooperation(target)) {
                if (State != (float)OldDukeAIState.LeavingDive) {
                    State = (float)OldDukeAIState.LeavingDive;
                    Timer = 0;
                    SubState = 0;
                }
                return RunStorylineAI();
            }

            //营地已建立的情况下，NPC实体应该消失并重定向到营地对话
            if (ShouldRedirectToCampsite(target)) {
                ExecuteCampsiteRedirect();
                return false;
            }

            KillDukeSummonerProjectiles();
            //执行原版AI
            return true;
        }

        /// <summary>
        /// 如果玩家状态为NotMet，标记为Met，确保只标记一次
        /// </summary>
        private static void MarkAsMetIfNeeded(Player target) {
            if (!target.TryGetADVSave(out var save)) {
                return;
            }
            if (save.OldDukeState == OldDukeInteractionState.NotMet) {
                save.OldDukeState = OldDukeInteractionState.Met;
            }
        }

        /// <summary>
        /// 执行营地重定向：隐藏NPC实体，触发营地场景对话，并通知服务端
        /// </summary>
        private void ExecuteCampsiteRedirect() {
            if (BCKRef.Has) {
                BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);
            }
            npc.active = false;
            npc.netUpdate = true;

            TriggerCampsiteScenario();

            //多人模式下通知服务端
            if (VaultUtils.isClient) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.StartCampsiteFindMeScenario);
                packet.Write(npc.whoAmI);
                packet.Send();
            }
        }

        /// <summary>
        /// 清除可能残留的老公爵召唤弹幕
        /// </summary>
        private static void KillDukeSummonerProjectiles() {
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == CWRID.Proj_OverlyDramaticDukeSummoner) {
                    p.active = false;
                    p.netUpdate = true;
                }
            }
        }

        #endregion

        #region 剧情AI状态机

        /// <summary>
        /// 运行剧情AI状态机。设置NPC为友好状态并根据当前State分派到对应的处理方法
        /// </summary>
        private bool RunStorylineAI() {
            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //剧情模式下设置为友好状态
            npc.friendly = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            switch ((OldDukeAIState)State) {
                case OldDukeAIState.FriendlyApproach:
                    HandleFriendlyApproach(target);
                    break;
                case OldDukeAIState.DialoguePause:
                    HandleDialoguePause(target);
                    break;
                case OldDukeAIState.LeavingDive:
                    HandleLeavingDive();
                    break;
                case OldDukeAIState.StartBattle:
                    HandleStartBattle();
                    break;
            }

            return false;
        }

        /// <summary>
        /// 友好接近状态：平滑飞到玩家上方300像素处，到达后触发对话
        /// </summary>
        private void HandleFriendlyApproach(Player target) {
            Vector2 targetPos = target.Center + new Vector2(0, -300);
            Vector2 toTarget = npc.Center.To(targetPos);

            //平滑移动
            const float speed = 8f;
            const float inertia = 20f;
            npc.velocity = (npc.velocity * (inertia - 1f) + toTarget.SafeNormalize(Vector2.Zero) * speed) / inertia;

            //面向玩家
            npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

            //到达目标位置后进入对话状态
            if (toTarget.Length() < 50f) {
                State = (float)OldDukeAIState.DialoguePause;
                Timer = 0;
                npc.velocity *= 0.9f;

                //只在NPC目标玩家的本地客户端触发一次对话场景
                //多人模式下其他客户端不应触发对话UI，只需看到NPC的视觉表现
                //OldDukeEffect.IsActive由声明式计算自动管理，无需手动设置
                if (!hasTriggeredDialogue && !VaultUtils.isServer && npc.target == Main.myPlayer) {
                    hasTriggeredDialogue = true;
                    ScenarioManager.Reset<FirstMetOldDuke>();
                    ScenarioManager.Start<FirstMetOldDuke>();
                }
            }
        }

        /// <summary>
        /// 对话暂停状态：悬浮等待对话结束，根据玩家选择决定下一步行动。
        /// 通过轮询OldDukeEffect.IsActive来检测对话是否结束，
        /// 使用Timer>60的缓冲时间来容忍网络延迟
        /// </summary>
        private void HandleDialoguePause(Player target) {
            Timer++;
            npc.velocity *= 0.95f;

            //轻微上下浮动
            float floatOffset = (float)System.Math.Sin(Timer * 0.05f) * 2f;
            npc.position.Y += floatOffset * 0.1f;

            //面向玩家
            npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

            //等待对话场景结束，并给予足够的网络同步缓冲时间
            //通过ScenarioManager检测对话是否已结束（比轮询IsActive更可靠）
            //只在NPC目标玩家的客户端上检测对话结果并驱动状态转换
            //状态通过ai[]数组自动同步到其他客户端和服务端
            bool isLocalTarget = !VaultUtils.isServer && npc.target == Main.myPlayer;
            if (isLocalTarget && !ScenarioManager.IsActive() && Timer > 60 && target.TryGetADVSave(out var save)) {
                OldDukeInteractionState choice = save.OldDukeState;
                switch (choice) {
                    case OldDukeInteractionState.AcceptedCooperation:
                    case OldDukeInteractionState.DeclinedCooperation:
                        //合作或拒绝都执行离开
                        TransitionToState(OldDukeAIState.LeavingDive);
                        SoundEngine.PlaySound(SoundID.Splash, npc.Center);
                        npc.netUpdate = true;
                        break;
                    case OldDukeInteractionState.ChoseToFight:
                        //选择战斗
                        TransitionToState(OldDukeAIState.StartBattle);
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                        npc.netUpdate = true;
                        break;
                }
            }
        }

        /// <summary>
        /// 离开潜水状态：分两个阶段，先向下加速游动，再淡出消失。
        /// 消失后停止酸雨和场景效果
        /// </summary>
        private void HandleLeavingDive() {
            Timer++;
            //锁定音乐为静音，避免短暂播放Boss音乐
            if (npc.ModNPC is not null) {
                npc.ModNPC.Music = -1;
            }

            if (SubState == 0) {
                //阶段1：加速下潜
                npc.velocity.Y += 0.5f;
                if (npc.velocity.Y > 20f) {
                    npc.velocity.Y = 20f;
                }
                npc.velocity.X = (float)System.Math.Sin(Timer * 0.1f) * 3f;
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                //生成水花粒子
                if (Timer % 5 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Water,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 1f),
                            100, default, Main.rand.NextFloat(1f, 2f));
                    }
                }

                if (Timer > 60) {
                    SubState = 1;
                    Timer = 0;
                    npc.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                //阶段2：淡出消失
                npc.alpha += 15;
                npc.velocity *= 0.98f;

                if (npc.alpha >= 255 || Timer > 300) {
                    FinalizeDespawn();
                }
            }
        }

        /// <summary>
        /// 开始战斗状态：播放60帧的准备动画（后退+绿色粒子），然后恢复原版AI
        /// </summary>
        private void HandleStartBattle() {
            Timer++;

            if (Timer < 60) {
                //战斗准备动画
                npc.velocity.Y = -2f;
                npc.velocity.X *= 0.98f;

                if (Timer % 10 < 5) {
                    for (int i = 0; i < 3; i++) {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.GreenTorch,
                            Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                            100, default, Main.rand.NextFloat(1.5f, 2.5f));
                    }
                }
            }
            else {
                //恢复为战斗状态
                RestoreCombatState();
            }
        }

        #endregion

        #region 状态转换辅助方法

        /// <summary>
        /// 切换到指定的AI状态并重置计时器
        /// </summary>
        private void TransitionToState(OldDukeAIState newState) {
            State = (float)newState;
            Timer = 0;
            SubState = 0;
        }

        /// <summary>
        /// 完成消失流程：停止酸雨，隐藏Boss列表标记，停用NPC
        /// </summary>
        private void FinalizeDespawn() {
            CWRRef.StopAcidRain();
            hasTriggeredDialogue = false;
            LeavingDiveFlag = 0f;

            if (BCKRef.Has) {
                BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);
            }
            npc.active = false;
            npc.netUpdate = true;
            //OldDukeEffect.IsActive由声明式计算自动管理
            //NPC消失后ComputeShouldBeActive()将返回false
        }

        /// <summary>
        /// 恢复原版战斗AI状态：清除友好标记，重置所有AI数据
        /// </summary>
        private void RestoreCombatState() {
            CWRRef.StopAcidRain();
            hasTriggeredDialogue = false;

            npc.friendly = false;
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            npc.alpha = 0;
            npc.rotation = 0;

            //重置所有AI槽位
            State = 0;
            Timer = 0;
            SubState = 0;
            LeavingDiveFlag = 0;
            npc.ai[0] = 0;
            npc.ai[1] = 0;
            npc.ai[2] = 0;
            npc.ai[3] = 0;

            npc.netUpdate = true;
        }

        #endregion

        #region 绘制

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!canDraw) {
                return false;
            }
            return base.Draw(spriteBatch, screenPos, drawColor);
        }

        #endregion
    }
}
