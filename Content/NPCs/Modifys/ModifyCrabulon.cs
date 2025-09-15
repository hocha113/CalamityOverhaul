using CalamityMod.NPCs.Crabulon;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Modifys
{
    internal class ModifyCrabulon : NPCOverride//驯养菌生蟹，不依赖生物大修
    {
        public override int TargetID => ModContent.NPCType<Crabulon>();
        public float FeedValue = 0;
        public Player Owner;
        public bool Mount;
        public bool hoverNPC;
        public void Feed(int i) {
            Owner = Main.player[i];
            npc.friendly = true;
            npc.npcSlots = 0;
            //每次喂食增加500点驯服值
            FeedValue += 500;
        }

        public override bool FindFrame(int frameHeight) {
            if (FeedValue > 0f && Mount) {
                // 空中：跳跃 / 下落帧
                if (!npc.collideY) {
                    if (npc.velocity.Y < 0) {
                        ai[11] = MathHelper.Lerp(ai[11], 1, 0.1f);
                    }
                    else {
                        ai[11] = MathHelper.Lerp(ai[11], 4, 0.2f);
                    }
                    npc.frame.Y = frameHeight * (int)ai[11]; //假设第5帧是下落
                    npc.frameCounter = 0; // 避免和跑动动画冲突
                }
                else {
                    if (Math.Abs(npc.velocity.X) > 0.1f) // 跑动
                    {
                        npc.frameCounter += Math.Abs(npc.velocity.X) * 0.04;
                        if (npc.frameCounter >= Main.npcFrameCount[npc.type])
                            npc.frameCounter = 0;

                        int frame = (int)npc.frameCounter % 4; // 假设0~3帧是跑动动画
                        npc.frame.Y = frame * frameHeight;
                    }
                    else // Idle
                    {
                        npc.frameCounter += 0.05;
                        if (npc.frameCounter >= Main.npcFrameCount[npc.type])
                            npc.frameCounter = 0;

                        int frame = (int)npc.frameCounter % 2; // 假设0~1帧是Idle动画
                        npc.frame.Y = frame * frameHeight;
                    }
                }
            }
            return true;
        }

        public bool MountAI() {
            if (!Mount) {
                //按下交互键骑乘
                if (hoverNPC && UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                    Mount = true;
                }
            }
            else {
                //--- 移动控制 ---
                float accel = 0.5f;     //加速度
                float maxSpeed = 12f;   //最大速度
                float friction = 0.85f; //摩擦系数

                Vector2 input = Vector2.Zero;

                //横向输入
                if (Owner.holdDownCardinalTimer[2] > 2) { //→ 右
                    input.X += 1f;
                }
                if (Owner.holdDownCardinalTimer[3] > 2) { //← 左
                    input.X -= 1f;
                }

                if (Owner.holdDownCardinalTimer[0] == 2 && !Collision.SolidCollision(npc.position, npc.width, npc.height + 20)) {//下平台
                    npc.velocity.Y = 12;
                    npc.noTileCollide = true;
                }

                //跳跃（只在接触地面时生效，防止无限连跳）
                if (Owner.justJumped && npc.collideY) {
                    npc.velocity.Y = -maxSpeed * 2f;
                }

                //横向速度控制：加速度 + 限速
                if (input.X != 0f) {
                    npc.velocity.X = MathHelper.Clamp(npc.velocity.X + input.X * accel, -maxSpeed, maxSpeed);
                }
                else {
                    //没有输入时逐渐减速
                    npc.velocity.X *= friction;
                    if (Math.Abs(npc.velocity.X) < 0.1f) {
                        npc.velocity.X = 0f;
                    }
                }

                //--- 状态标记（供AI或动画用） ---
                npc.ai[0] = (Math.Abs(npc.velocity.X) > 0.1f) ? 1f : 0f;
                if (Math.Abs(npc.velocity.Y) > 1f) {
                    npc.ai[0] = 3f;
                }

                //--- 玩家位置同步 ---
                Owner.Center = npc.Top;
                Owner.velocity = Vector2.Zero; //禁用玩家自身移动

                if (hoverNPC && UIHandleLoader.keyRightPressState == KeyPressState.Pressed) {
                    Mount = false;
                }

                //根据移动方向设置NPC的图像朝向
                npc.spriteDirection = npc.direction = Math.Sign(npc.velocity.X);
                return false; //阻止默认AI
            }

            return true;
        }

        public override bool CheckActive() {
            return false;
        }

        public override bool AI() {
            //当FeedValue大于0时，进入驯服状态
            if (FeedValue > 0f) {
                npc.timeLeft = 1800;
                npc.ModNPC.Music = -1;
                //取消Boss状态，设置为对玩家友好
                npc.boss = false;
                npc.friendly = true;
                npc.townNPC = true;
                npc.damage = 0;

                //如果没有找到可跟随的玩家，则原地减速并进入站立动画
                if (!Owner.Alives()) {
                    npc.velocity.X *= 0.9f;
                    //使用站立动画
                    npc.ai[0] = 0f;
                    //返回false以阻止原版AI运行
                    return false;
                }

                hoverNPC = npc.Hitbox.Intersects(Main.MouseWorld.GetRectangle(1));
                //首先，默认尝试在地面移动，受重力影响
                npc.noGravity = false;
                npc.noTileCollide = false;

                if (!MountAI()) {
                    return false;
                }

                //定义AI所需的参数
                float moveSpeed = 4f; //移动速度
                float inertia = 15f; //惯性，数值越大，转向和加减速越平滑
                float followDistance = 150f; //开始跟随的水平距离

                //计算从NPC指向玩家的向量
                Vector2 toPlayer = Owner.Center - npc.Center;

                //水平移动逻辑
                if (Math.Abs(toPlayer.X) > followDistance) {
                    //如果玩家在右边，向右移动
                    if (toPlayer.X > 0) {
                        npc.velocity.X = (npc.velocity.X * inertia + moveSpeed) / (inertia + 1f);
                        npc.direction = 1;
                    }
                    //如果玩家在左边，向左移动
                    else {
                        npc.velocity.X = (npc.velocity.X * inertia - moveSpeed) / (inertia + 1f);
                        npc.direction = -1;
                    }
                    //使用行走动画，这会触发Crabulon原代码中的FindFrame逻辑来播放对应动画
                    npc.ai[0] = 1f;
                }
                else {
                    //当离玩家足够近时，水平速度逐渐减慢
                    npc.velocity.X *= 0.9f;
                    //使用站立动画
                    npc.ai[0] = 0f;
                }

                //根据移动方向设置NPC的图像朝向
                npc.spriteDirection = npc.direction;

                if (Owner.Bottom.Y < npc.Bottom.Y - 400) {
                    npc.velocity += new Vector2(0, -2);
                }

                return false;
            }
            //如果不在驯服状态，则执行原版AI
            return true;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (hoverNPC) {
                Vector2 drawPos = npc.Top + new Vector2(0, -22) - Main.screenPosition;
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, "骑乘"
                            , drawPos.X, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), 1.6f);
            }
            //这里保持默认，不修改原版绘制逻辑
            return base.Draw(spriteBatch, screenPos, drawColor);
        }
    }
}
