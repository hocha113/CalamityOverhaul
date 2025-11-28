using InnoVault.GameSystem;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Sulfseas.OldDukes
{
    internal class ModifyOldDuke : NPCOverride
    {
        public override int TargetID => CWRID.NPC_OldDuke;

        //场景状态标记
        private bool isFirstMeet = false;
        private bool firstMeetCompleted = false;

        //AI状态枚举
        private enum OldDukeState
        {
            FriendlyApproach = 0,//友好接近状态
            DialoguePause = 1//对话暂停状态
        }

        private ref float State => ref ai[0];
        private ref float Timer => ref ai[1];

        public override bool FindFrame(int frameHeight) {
            if (isFirstMeet && !firstMeetCompleted) {
                //初见场景下使用自定义帧动画
                npc.frameCounter += 0.15f;
                npc.frameCounter %= Main.npcFrameCount[npc.type];
                int frame = (int)npc.frameCounter;
                npc.frame.Y = frame * frameHeight;
                return false;
            }
            return base.FindFrame(frameHeight);
        }

        public override bool AI() {
            //检查是否为初见场景
            if (!firstMeetCompleted) {
                if (Main.LocalPlayer.TryGetADVSave(out ADVSave save)) {
                    //如果从未遇见过Old Duke,启动初见场景
                    if (!save.FirstMetOldDuke) {
                        isFirstMeet = true;
                        save.FirstMetOldDuke = true;//标记已遇见
                    }
                }
            }

            //如果是初见场景,执行特殊AI
            if (isFirstMeet && !firstMeetCompleted) {
                return FirstMeetAI();
            }

            //否则执行原版AI
            return base.AI();
        }

        private bool FirstMeetAI() {
            npc.TargetClosest();
            Player target = Main.player[npc.target];

            //设置为友好NPC
            npc.friendly = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            OldDukeState currentState = (OldDukeState)State;

            switch (currentState) {
                case OldDukeState.FriendlyApproach:
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
                        State = (float)OldDukeState.DialoguePause;
                        Timer = 0;
                        npc.velocity *= 0.9f;

                        OldDukeEffect.IsActive = true;
                        //触发对话场景
                        if (Main.myPlayer == npc.target) {
                            ScenarioManager.Reset<FirstMetOldDuke>();
                            ScenarioManager.Start<FirstMetOldDuke>();
                        }
                    }
                    break;

                case OldDukeState.DialoguePause:
                    //对话期间保持悬停
                    Timer++;
                    npc.velocity *= 0.95f;

                    //轻微上下浮动
                    float floatOffset = (float)System.Math.Sin(Timer * 0.05f) * 2f;
                    npc.position.Y += floatOffset;

                    //面向玩家
                    npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

                    //检查对话是否结束
                    if (!OldDukeEffect.IsActive) {
                        //对话结束
                        CompleteFirstMeet();
                    }
                    break;
            }

            //自定义帧计数
            npc.frameCounter += 0.1f;
            if (npc.frameCounter >= Main.npcFrameCount[npc.type]) {
                npc.frameCounter = 0;
            }

            return false;//阻止原版AI运行
        }

        private void CompleteFirstMeet() {
            firstMeetCompleted = true;
            isFirstMeet = false;

            //恢复战斗状态
            npc.friendly = false;
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;

            //重置AI状态
            State = 0;
            Timer = 0;
            npc.netUpdate = true;
        }
    }
}
