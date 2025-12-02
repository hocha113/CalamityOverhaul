using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites;
using CalamityOverhaul.OtherMods.BossChecklist;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes
{
    internal class ModifyOldDuke : NPCOverride, ILocalizedModType
    {
        public override int TargetID => CWRID.NPC_OldDuke;

        //AI状态枚举
        private enum OldDukeAIState
        {
            FriendlyApproach = 0,    //友好接近状态
            DialoguePause = 1,       //对话暂停状态
            LeavingDive = 2,         //离开（潜入海中）
            StartBattle = 3          //开始战斗
        }

        private ref float State => ref ai[0];
        private ref float Timer => ref ai[1];
        private ref float SubState => ref ai[2];

        public string LocalizationCategory => "NPCModifys";

        public static LocalizedText LeavingDiveText { get; private set; }

        private bool IsLeavingDive;
        private bool CanDraw;

        //场景控制标记
        private bool isFirstMeet = false;
        private bool firstMeetCompleted = false;
        private bool hasTriggeredScenario = false;

        public override bool CanOverride() {
            if (CWRRef.GetBossRushActive()) {
                return false;//在Boss Rush模式下不覆盖AI
            }
            return base.CanOverride();
        }

        public override void SetStaticDefaults() {
            LeavingDiveText = this.GetLocalization(nameof(LeavingDiveText), () => "老公爵潜入了水中...");
        }

        public override void SetProperty() {
            IsLeavingDive = false;
            CanDraw = false;
        }

        public override bool FindFrame(int frameHeight) {
            if (isFirstMeet && !firstMeetCompleted) {
                //初见场景下使用自定义帧动画
                npc.frameCounter += 0.08f;
                npc.frameCounter %= (Main.npcFrameCount[npc.type] - 1);//这里使用一个减1是避免张嘴动画
                int frame = (int)npc.frameCounter;
                npc.frame.Y = frame * frameHeight;
                return false;
            }
            return base.FindFrame(frameHeight);
        }

        public override bool? CheckDead() {
            //这里的修改用于制作一个效果：
            //老公爵不会被玩家杀死，每次被击败都只是潜入海中离开
            IsLeavingDive = true;
            npc.life = npc.lifeMax;
            npc.dontTakeDamage = true;
            npc.DropItem();
            CWRRef.SetDownedBoomerDuke(true);//标记老公爵已被击败
            if (VaultUtils.isServer) {//同步世界数据
                NetMessage.SendData(MessageID.WorldData);
            }
            VaultUtils.Text(LeavingDiveText.Value, Color.YellowGreen);
            foreach (var g in Main.gore) {
                g.active = false;//清除老公爵的残骸，避免影响潜入效果
            }
            return false;
        }

        public override bool AI() {
            CanDraw = true;
            if (IsLeavingDive) {
                State = (float)OldDukeAIState.LeavingDive;
                return StorylineAI();
            }

            if (OldDukeCampsite.IsGenerated && !OldDukeCampsite.WannaToFight) {
                if (BCKRef.Has) {
                    BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);//对于Boss列表的适配，隐藏活跃状态，避免消失时弹出信息破坏氛围
                }
                npc.active = false;
                npc.netUpdate = true;
                IsLeavingDive = false;
                if (!VaultUtils.isServer) {
                    ScenarioManager.Reset<ComeCampsiteFindMe>();
                    ScenarioManager.Start<ComeCampsiteFindMe>();
                }
                return false;
            }

            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //检查并更新初见场景状态
            UpdateFirstMeetState(target);

            OldDukeAIState currentState = (OldDukeAIState)State;

            if ((isFirstMeet && !firstMeetCompleted)//如果是初见场景,执行特殊AI
                || currentState == OldDukeAIState.LeavingDive//正在离开潜入海中
                ) {
                return StorylineAI();
            }

            //否则执行原版AI
            return base.AI();
        }

        /// <summary>
        /// 更新初见场景状态
        /// </summary>
        private void UpdateFirstMeetState(Player target) {
            if (!target.TryGetADVSave(out var save)) {
                return;
            }

            OldDukeInteractionState state = save.OldDukeState;

            //根据保存的状态决定是否触发初见场景
            switch (state) {
                case OldDukeInteractionState.NotMet:
                    //首次相遇，触发初见场景
                    isFirstMeet = true;
                    firstMeetCompleted = false;
                    //标记已遇见（设置为Met状态）
                    save.OldDukeState = OldDukeInteractionState.Met;
                    break;

                case OldDukeInteractionState.DeclinedCooperation:
                    //拒绝了合作但没有战斗，可以重新触发对话
                    isFirstMeet = true;
                    firstMeetCompleted = false;
                    break;

                case OldDukeInteractionState.ChoseToFight:
                case OldDukeInteractionState.AcceptedCooperation:
                    //已选择战斗或已接受合作，不再触发初见场景
                    isFirstMeet = false;
                    firstMeetCompleted = true;
                    break;

                case OldDukeInteractionState.Met:
                    //已遇见但未做选择，继续初见场景
                    if (!hasTriggeredScenario) {
                        isFirstMeet = true;
                        firstMeetCompleted = false;
                    }
                    break;
            }
        }

        private bool StorylineAI() {
            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //设置为友好NPC
            npc.friendly = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            OldDukeAIState currentState = (OldDukeAIState)State;

            switch (currentState) {
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

            return false; //阻止原版AI运行
        }

        /// <summary>
        /// 友好接近玩家
        /// </summary>
        private void HandleFriendlyApproach(Player target) {
            //飞到玩家上方约300像素的位置
            Vector2 targetPos = target.Center + new Vector2(0, -300);
            Vector2 toTarget = npc.Center.To(targetPos);

            //平滑移动
            float speed = 8f;
            float inertia = 20f;
            npc.velocity = (npc.velocity * (inertia - 1f) + toTarget.SafeNormalize(Vector2.Zero) * speed) / inertia;

            //面向玩家
            npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

            //到达目标位置后进入对话状态
            if (toTarget.Length() < 50f) {
                State = (float)OldDukeAIState.DialoguePause;
                Timer = 0;
                npc.velocity *= 0.9f;

                //触发对话场景（只触发一次）
                if (!hasTriggeredScenario && Main.myPlayer == npc.target) {
                    hasTriggeredScenario = true;
                    OldDukeEffect.IsActive = true;
                    OldDukeEffect.Send();
                    ScenarioManager.Reset<FirstMetOldDuke>();
                    ScenarioManager.Start<FirstMetOldDuke>();
                }
            }
        }

        /// <summary>
        /// 对话暂停状态
        /// </summary>
        private void HandleDialoguePause(Player target) {
            Timer++;
            npc.velocity *= 0.95f;

            //轻微上下浮动
            float floatOffset = (float)System.Math.Sin(Timer * 0.05f) * 2f;
            npc.position.Y += floatOffset * 0.1f;

            //面向玩家
            npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

            //检查对话是否结束并根据玩家选择决定下一步
            if (!OldDukeEffect.IsActive && Timer > 60) {
                //对话结束，根据玩家选择执行相应逻辑
                OldDukeInteractionState playerChoice = FirstMetOldDuke.CurrentPlayerChoice;

                switch (playerChoice) {
                    case OldDukeInteractionState.AcceptedCooperation:
                        //接受合作 - 离开
                        State = (float)OldDukeAIState.LeavingDive;
                        Timer = 0;
                        SubState = 0;
                        SoundEngine.PlaySound(SoundID.Splash, npc.Center);
                        break;

                    case OldDukeInteractionState.DeclinedCooperation:
                        //拒绝合作 - 离开
                        State = (float)OldDukeAIState.LeavingDive;
                        Timer = 0;
                        SubState = 0;
                        SoundEngine.PlaySound(SoundID.Splash, npc.Center);
                        break;

                    case OldDukeInteractionState.ChoseToFight:
                        //选择战斗 - 开始战斗
                        State = (float)OldDukeAIState.StartBattle;
                        Timer = 0;
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                        break;
                }
            }
        }

        /// <summary>
        /// 离开场景（潜入海中）
        /// </summary>
        private void HandleLeavingDive() {
            Timer++;

            //向下游动
            if (SubState == 0) {
                //加速向下
                npc.velocity.Y += 0.5f;
                if (npc.velocity.Y > 20f) {
                    npc.velocity.Y = 20f;
                }

                //轻微摆动
                npc.velocity.X = (float)System.Math.Sin(Timer * 0.1f) * 3f;

                //旋转效果
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                //生成水花粒子
                if (Timer % 5 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Water,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 1f), 100, default, Main.rand.NextFloat(1f, 2f));
                    }
                }

                //下潜一定距离后开始淡出
                if (Timer > 60) {
                    SubState = 1;
                    Timer = 0;
                }
            }
            //淡出消失
            else if (SubState == 1) {
                npc.alpha += 15;
                npc.velocity *= 0.98f;

                if (npc.alpha >= 255) {
                    //完成离开
                    CompleteFirstMeet();
                    if (BCKRef.Has) {
                        BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);//对于Boss列表的适配，隐藏活跃状态，避免消失时弹出信息破坏氛围
                    }
                    npc.active = false;
                    npc.netUpdate = true;
                    IsLeavingDive = false;
                }
            }
        }

        /// <summary>
        /// 开始战斗
        /// </summary>
        private void HandleStartBattle() {
            Timer++;

            //准备战斗的动画
            if (Timer < 60) {
                //后退一小段距离
                npc.velocity.Y = -2f;
                npc.velocity.X *= 0.98f;

                //闪烁效果
                if (Timer % 10 < 5) {
                    for (int i = 0; i < 3; i++) {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.GreenTorch,
                            Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                    }
                }
            }
            else {
                //恢复战斗状态
                CompleteFirstMeet();

                //恢复原版AI
                npc.friendly = false;
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
                npc.alpha = 0;
                npc.rotation = 0;

                //重置AI
                State = 0;
                Timer = 0;
                SubState = 0;
                npc.ai[0] = 0;
                npc.ai[1] = 0;
                npc.ai[2] = 0;
                npc.ai[3] = 0;

                npc.netUpdate = true;
            }
        }

        private void CompleteFirstMeet() {
            firstMeetCompleted = true;
            isFirstMeet = false;
            hasTriggeredScenario = false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!CanDraw) {
                return false;
            }
            return base.Draw(spriteBatch, screenPos, drawColor);
        }
    }
}
