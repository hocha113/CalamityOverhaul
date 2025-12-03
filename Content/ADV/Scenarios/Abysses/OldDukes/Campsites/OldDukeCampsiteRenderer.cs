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
    internal class OldDukeCampsiteRenderer : RenderHandle, ILocalizedModType, IWorldInfo
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

        //老公爵实体
        private OldDukeEntity oldDukeEntity;
        private bool entityInitialized;

        //对话状态
        private bool inDialogue;

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 对话");
            GreetingText = this.GetLocalization(nameof(GreetingText), () => "嗯？你好啊...");
        }

        void IWorldInfo.OnWorldLoad() {
            entityInitialized = true;
        }

        public void SetEntityInitialized(bool value) => entityInitialized = value;

        public override void UpdateBySystem(int index) {
            if (!OldDukeCampsite.IsGenerated) {
                return;
            }

            //初始化老公爵实体
            if (!entityInitialized && OldDukeCampsite.CampsitePosition != Vector2.Zero) {
                oldDukeEntity = new OldDukeEntity(OldDukeCampsite.CampsitePosition);
                oldDukeEntity.SetPotPositions(OldDukeCampsiteDecoration.GetPotPositions());
                entityInitialized = true;
            }

            if (oldDukeEntity.Position.To(OldDukeCampsite.CampsitePosition).LengthSquared() > 1200 * 1200) {
                oldDukeEntity = new OldDukeEntity(OldDukeCampsite.CampsitePosition);
                oldDukeEntity.SetPotPositions(OldDukeCampsiteDecoration.GetPotPositions());
                oldDukeEntity.Position = OldDukeCampsite.CampsitePosition;
            }

            //更新发光计时器
            glowTimer += 0.03f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            //检测对话状态
            inDialogue = OldDukeEffect.IsActive;

            //更新老公爵实体
            Vector2 dialogueTarget = Vector2.Zero;
            if (inDialogue) {
                Player player = Main.LocalPlayer;
                if (player != null && player.active) {
                    dialogueTarget = player.Center + new Vector2(0, -200f);
                }
            }

            oldDukeEntity?.Update(inDialogue, dialogueTarget);

            //通知锅的访问状态
            if (oldDukeEntity != null) {
                OldDukeCampsiteDecoration.NotifyPotVisit(
                    oldDukeEntity.Position,
                    oldDukeEntity.IsVisitingPot(),
                    oldDukeEntity.GetCurrentTarget()
                );
            }

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

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!OldDukeCampsite.IsGenerated || oldDukeEntity == null) {
                return;
            }

            Vector2 screenPos = oldDukeEntity.Position - Main.screenPosition;

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
            if (OldDukeCampsite.OldDuke == null || oldDukeEntity == null) {
                return;
            }

            Rectangle frame = OldDukeCampsite.GetCurrentFrame();
            Vector2 origin = frame.Size() / 2f;

            //轻微呼吸效果
            float breathScale = 1f + MathF.Sin(glowTimer * 1.5f) * 0.01f;

            //游泳波动偏移
            Vector2 bobOffset = oldDukeEntity.GetSwimBobOffset();

            //游泳倾斜
            float swimTilt = oldDukeEntity.GetSwimTilt();

            //绘制底层发光（硫磺海风格）
            float glowIntensity = (MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f) * 0.4f;
            Color glowColor = new Color(100, 200, 120) with { A = 0 };

            SpriteEffects flip = oldDukeEntity.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 0; i < 3; i++) {
                float glowScale = breathScale * (1.2f + i * 0.1f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                sb.Draw(
                    OldDukeCampsite.OldDuke,
                    screenPos + bobOffset,
                    frame,
                    glowColor * glowAlpha * oldDukeEntity.Sengs,
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
                Lighting.GetColor((oldDukeEntity.Position / 16).ToPoint()) * oldDukeEntity.Sengs,
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
            if (oldDukeEntity == null) {
                return;
            }

            //在老公爵当前位置周围生成
            Vector2 spawnPos = oldDukeEntity.Position;
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
