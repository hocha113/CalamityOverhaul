using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地渲染器
    /// </summary>
    internal class OldDukeCampsiteRenderer : RenderHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV.OldDukeCampsite";

        //本地化文本
        public static LocalizedText InteractHint;
        public static LocalizedText GreetingText;

        //硫磺海粒子效果
        private readonly List<ToxicBubblePRT> toxicBubbles = [];
        private int bubbleSpawnTimer;

        //发光效果
        private float glowTimer;

        //老公爵游走AI
        private Vector2 oldDukePosition;
        private Vector2 oldDukeVelocity;
        private Vector2 wanderTarget;
        private int wanderTimer;
        private const float WanderRadius = 180f;
        private const float MoveSpeed = 1.2f;
        private const float MaxSpeed = 2.8f;
        private float swimPhase;
        private bool facingLeft;

        //对话状态
        private bool inDialogue;
        private Vector2 dialogueTargetPos;

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 对话");
            GreetingText = this.GetLocalization(nameof(GreetingText), () => "嗯？你好啊...");
        }

        public override void UpdateBySystem(int index) {
            if (!OldDukeCampsite.IsGenerated) {
                return;
            }

            //更新发光计时器
            glowTimer += 0.03f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            //更新游走动画相位
            swimPhase += 0.08f;
            if (swimPhase > MathHelper.TwoPi) {
                swimPhase -= MathHelper.TwoPi;
            }

            //检测对话状态
            inDialogue = OldDukeEffect.IsActive;

            //更新老公爵位置
            UpdateOldDukeMovement();

            //生成毒泡粒子
            bubbleSpawnTimer++;
            if (bubbleSpawnTimer >= 15 && toxicBubbles.Count < 12) {
                bubbleSpawnTimer = 0;
                SpawnToxicBubble();
            }

            //更新粒子
            for (int i = toxicBubbles.Count - 1; i >= 0; i--) {
                if (toxicBubbles[i].Update()) {
                    toxicBubbles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 更新老公爵移动逻辑
        /// </summary>
        private void UpdateOldDukeMovement() {
            Vector2 campsiteCenter = OldDukeCampsite.CampsitePosition;

            //首次初始化位置
            if (oldDukePosition == Vector2.Zero) {
                oldDukePosition = campsiteCenter;
                GenerateNewWanderTarget(campsiteCenter);
            }

            if (inDialogue) {
                //对话模式：移动到玩家上方
                Player player = Main.LocalPlayer;
                if (player != null && player.active) {
                    dialogueTargetPos = player.Center + new Vector2(0, -200f);

                    //平滑移动到目标位置
                    Vector2 toTarget = dialogueTargetPos - oldDukePosition;
                    float distance = toTarget.Length();

                    if (distance > 5f) {
                        Vector2 direction = toTarget.SafeNormalize(Vector2.Zero);
                        float approachSpeed = MathHelper.Clamp(distance * 0.08f, 0.5f, 3.5f);

                        oldDukeVelocity = Vector2.Lerp(oldDukeVelocity, direction * approachSpeed, 0.15f);

                        //限制速度
                        if (oldDukeVelocity.Length() > MaxSpeed * 1.2f) {
                            oldDukeVelocity = oldDukeVelocity.SafeNormalize(Vector2.Zero) * MaxSpeed * 1.2f;
                        }
                    }
                    else {
                        //到达目标位置后保持轻微漂浮
                        oldDukeVelocity *= 0.88f;
                        Vector2 floatOffset = new Vector2(
                            MathF.Sin(swimPhase) * 0.3f,
                            MathF.Cos(swimPhase * 0.7f) * 0.2f
                        );
                        oldDukePosition += floatOffset;
                    }

                    //更新朝向
                    if (Math.Abs(oldDukeVelocity.X) > 0.1f) {
                        facingLeft = oldDukeVelocity.X < 0;
                    }
                }
            }
            else {
                //游走模式：在营地范围内随机移动
                wanderTimer++;

                //到达目标或超时后重新选择目标
                float distanceToTarget = Vector2.Distance(oldDukePosition, wanderTarget);
                if (distanceToTarget < 30f || wanderTimer > 180) {
                    GenerateNewWanderTarget(campsiteCenter);
                    wanderTimer = 0;
                }

                //向目标移动
                Vector2 toTarget = wanderTarget - oldDukePosition;
                Vector2 direction = toTarget.SafeNormalize(Vector2.Zero);

                //加速度控制，更自然的加减速
                float desiredSpeed = MoveSpeed * (0.6f + MathF.Sin(swimPhase * 0.5f) * 0.4f);
                Vector2 desiredVelocity = direction * desiredSpeed;

                oldDukeVelocity = Vector2.Lerp(oldDukeVelocity, desiredVelocity, 0.08f);

                //限制速度
                if (oldDukeVelocity.Length() > MaxSpeed) {
                    oldDukeVelocity = oldDukeVelocity.SafeNormalize(Vector2.Zero) * MaxSpeed;
                }

                //添加游泳波动
                Vector2 swimWave = new Vector2(
                    MathF.Sin(swimPhase * 1.2f) * 0.4f,
                    MathF.Cos(swimPhase * 0.8f) * 0.3f
                );
                oldDukeVelocity += swimWave;

                //更新朝向
                if (Math.Abs(toTarget.X) > 5f) {
                    facingLeft = toTarget.X < 0;
                }
            }

            //应用速度
            oldDukePosition += oldDukeVelocity;

            //速度衰减
            oldDukeVelocity *= 0.96f;

            //边界限制（保持在营地范围内）
            Vector2 toCampsite = oldDukePosition - campsiteCenter;
            float distanceFromCenter = toCampsite.Length();

            if (distanceFromCenter > WanderRadius) {
                //超出范围，推回范围内
                Vector2 pushBack = toCampsite.SafeNormalize(Vector2.Zero) * (distanceFromCenter - WanderRadius);
                oldDukePosition -= pushBack * 0.2f;

                //如果远离中心，设置新目标朝向中心
                if (!inDialogue && distanceFromCenter > WanderRadius * 1.2f) {
                    wanderTarget = campsiteCenter + Main.rand.NextVector2Circular(WanderRadius * 0.5f, WanderRadius * 0.5f);
                }
            }
        }

        /// <summary>
        /// 生成新的游走目标点
        /// </summary>
        private void GenerateNewWanderTarget(Vector2 center) {
            //在营地范围内随机选择一个点
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(WanderRadius * 0.3f, WanderRadius * 0.85f);

            wanderTarget = center + angle.ToRotationVector2() * distance;

            //稍微偏向上方，避免贴地
            wanderTarget.Y -= Main.rand.NextFloat(20f, 60f);
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!OldDukeCampsite.IsGenerated) {
                return;
            }

            Vector2 screenPos = oldDukePosition - Main.screenPosition;

            //检查是否在屏幕内
            if (!VaultUtils.IsPointOnScreen(screenPos, 400)) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制粒子效果
            foreach (var bubble in toxicBubbles) {
                bubble.Draw(spriteBatch);
            }

            //绘制老公爵
            DrawOldDuke(spriteBatch, screenPos);

            //绘制交互提示
            DrawInteractPrompt(spriteBatch, screenPos);

            Main.spriteBatch.End();
        }

        /// <summary>
        /// 绘制老公爵
        /// </summary>
        private void DrawOldDuke(SpriteBatch sb, Vector2 screenPos) {
            if (OldDukeCampsite.OldDuke == null) {
                return;
            }

            Rectangle frame = OldDukeCampsite.GetCurrentFrame();
            Vector2 origin = frame.Size() / 2f;

            //轻微呼吸效果
            float breathScale = 1f + MathF.Sin(glowTimer * 1.5f) * 0.03f;

            //游泳时的上下波动
            float swimBob = MathF.Sin(swimPhase) * 3f;
            Vector2 bobOffset = new Vector2(0, swimBob);

            //游泳时的轻微倾斜
            float swimTilt = 0f;
            if (!inDialogue && oldDukeVelocity.Length() > 0.5f) {
                swimTilt = MathHelper.Clamp(oldDukeVelocity.Y * 0.08f, -0.15f, 0.15f);
            }

            //绘制底层发光（硫磺海风格）
            float glowIntensity = (MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f) * 0.4f;
            Color glowColor = new Color(100, 200, 120) with { A = 0 };

            SpriteEffects flip = facingLeft ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            for (int i = 0; i < 3; i++) {
                float glowScale = breathScale * (1.2f + i * 0.1f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                sb.Draw(
                    OldDukeCampsite.OldDuke,
                    screenPos + bobOffset,
                    frame,
                    glowColor * glowAlpha,
                    swimTilt,
                    origin,
                    glowScale,
                    flip,
                    0f
                );
            }

            //绘制主体
            sb.Draw(
                OldDukeCampsite.OldDuke,
                screenPos + bobOffset,
                frame,
                Color.White,
                swimTilt,
                origin,
                breathScale,
                flip,
                0f
            );
        }

        /// <summary>
        /// 绘制交互提示
        /// </summary>
        private void DrawInteractPrompt(SpriteBatch sb, Vector2 screenPos) {
            float alpha = OldDukeCampsite.GetInteractPromptAlpha();
            if (alpha <= 0.01f) {
                return;
            }

            //提示文字位置（在老公爵上方）
            Vector2 textPos = screenPos + new Vector2(0, -150);

            //绘制提示背景
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hintText = InteractHint.Value;
            Vector2 textSize = font.MeasureString(hintText) * 0.9f;

            Rectangle bgRect = new Rectangle(
                (int)(textPos.X - textSize.X / 2 - 10),
                (int)(textPos.Y - textSize.Y / 2 - 6),
                (int)(textSize.X + 20),
                (int)(textSize.Y + 12)
            );

            Texture2D pixel = VaultAsset.placeholder2.Value;

            //背景
            sb.Draw(pixel, bgRect, new Color(20, 30, 25) * (alpha * 0.85f));

            //边框（硫磺海风格）
            Color borderColor = new Color(100, 200, 120) * (alpha * 0.8f);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, 2), borderColor);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Bottom - 2, bgRect.Width, 2), borderColor * 0.7f);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, 2, bgRect.Height), borderColor * 0.85f);
            sb.Draw(pixel, new Rectangle(bgRect.Right - 2, bgRect.Y, 2, bgRect.Height), borderColor * 0.85f);

            //文字
            Color textColor = new Color(200, 240, 220) * alpha;
            Utils.DrawBorderString(sb, hintText, textPos - textSize / 2, textColor, 0.9f);

            //脉动图标
            float iconPulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
            string iconText = "▼";
            Vector2 iconSize = font.MeasureString(iconText) * 0.7f;
            Vector2 iconPos = textPos + new Vector2(0, textSize.Y / 2 + 8);
            Utils.DrawBorderString(sb, iconText, iconPos - iconSize / 2,
                new Color(150, 230, 180) * (alpha * iconPulse), 0.7f);
        }

        /// <summary>
        /// 生成毒泡粒子
        /// </summary>
        private void SpawnToxicBubble() {
            //在老公爵当前位置周围生成
            Vector2 spawnPos = oldDukePosition;
            spawnPos += new Vector2(
                Main.rand.NextFloat(-80f, 80f),
                Main.rand.NextFloat(-60f, 60f)
            );

            toxicBubbles.Add(new ToxicBubblePRT(spawnPos));
        }

        /// <summary>
        /// 毒泡粒子
        /// </summary>
        private class ToxicBubblePRT
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Life;
            public float MaxLife;
            public Color Color;

            public ToxicBubblePRT(Vector2 startPos) {
                Position = startPos;
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.3f, 0.3f),
                    Main.rand.NextFloat(-1.2f, -0.5f)
                );
                Scale = Main.rand.NextFloat(0.6f, 1.2f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 120f);
                Color = Main.rand.NextBool()
                    ? new Color(120, 220, 140, 180)
                    : new Color(150, 200, 100, 200);
            }

            public bool Update() {
                Life++;
                Position += Velocity;

                //横向漂移
                Velocity.X += MathF.Sin(Life * 0.1f) * 0.02f;

                //速度衰减
                Velocity *= 0.99f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float alpha = (float)Math.Sin((Life / MaxLife) * MathHelper.Pi);
                Vector2 screenPos = Position - Main.screenPosition;

                //外圈
                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * (alpha * 0.6f),
                    0f,
                    new Vector2(0.5f),
                    Scale * 8f,
                    SpriteEffects.None,
                    0f
                );

                //内核
                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * alpha,
                    0f,
                    new Vector2(0.5f),
                    Scale * 4f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
