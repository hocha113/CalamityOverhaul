using CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes.OldDukeShops;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.GameSystem;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes
{
    internal class ModifyOldDuke : NPCOverride
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

        //场景控制标记
        private bool isFirstMeet = false;
        private bool firstMeetCompleted = false;
        private bool hasTriggeredScenario = false;

        public override bool IsLoadingEnabled(Mod mod) {
            return false;//没做完，禁用
        }

        public override void SetProperty() {
            
        }

        public override bool FindFrame(int frameHeight) {
            if (isFirstMeet && !firstMeetCompleted) {
                //初见场景下使用自定义帧动画
                npc.frameCounter += 0.08f;
                npc.frameCounter %= Main.npcFrameCount[npc.type];
                int frame = (int)npc.frameCounter;
                npc.frame.Y = frame * frameHeight;
                return false;
            }
            return base.FindFrame(frameHeight);
        }

        public override bool AI() {
            npc.TargetClosest();
            Player target = Main.player[npc.target];
            SetTog(target);
            //如果是初见场景,执行特殊AI
            if (isFirstMeet && !firstMeetCompleted) {
                return FirstMeetAI();
            }

            //否则执行原版AI
            return base.AI();
        }

        public void SetTog(Player target) {
            //检查是否为初见场景
            if (target.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                if (!halibutPlayer.ADVSave.FirstMetOldDuke && !halibutPlayer.ADVSave.OldDukeChoseToFight) {
                    //首次相遇且未选择战斗
                    isFirstMeet = true;
                    firstMeetCompleted = false;

                    //标记已遇见（由场景触发后设置）
                    halibutPlayer.ADVSave.FirstMetOldDuke = true;
                }
                else if (halibutPlayer.ADVSave.OldDukeChoseToFight) {
                    //之前选择了战斗，直接进入战斗
                    isFirstMeet = false;
                    firstMeetCompleted = true;
                }
            }
        }

        private bool FirstMeetAI() {
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
                int playerChoice = FirstMetOldDuke.PlayerChoice;

                if (playerChoice == 1) {
                    //选择1：接受合作 - 离开
                    State = (float)OldDukeAIState.LeavingDive;
                    Timer = 0;
                    SubState = 0;
                    SoundEngine.PlaySound(SoundID.Splash, npc.Center);
                }
                else if (playerChoice == 2) {
                    //选择2：拒绝合作 - 离开
                    State = (float)OldDukeAIState.LeavingDive;
                    Timer = 0;
                    SubState = 0;
                    SoundEngine.PlaySound(SoundID.Splash, npc.Center);
                }
                else if (playerChoice == 3) {
                    //选择3：拒绝并战斗 - 开始战斗
                    State = (float)OldDukeAIState.StartBattle;
                    Timer = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
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
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

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
                    npc.active = false;
                    npc.netUpdate = true;

                    //如果接受了合作，打开商店UI
                    if (FirstMetOldDuke.PlayerChoice == 1) {
                        if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                            if (halibutPlayer.ADVSave.OldDukeCooperationAccepted) {
                                OldDukeShopUI.Instance.Active = true;
                            }
                        }
                    }
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
    }
}
