using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime
{
    /// <summary>凝胶液滴：重力弧线+速度拉伸+表面张力抖动+贴地摊平</summary>
    internal class PRT_QueenGelDrop : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private float wobblePhase;
        private bool landed;
        private int landedTimer;
        private bool colorCaptured;
        private Color initColor;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = 90;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            landed = false;
            landedTimer = 0;
        }

        public override void Reset() {
            base.Reset();
            wobblePhase = 0f;
            landed = false;
            landedTimer = 0;
            colorCaptured = false;
            initColor = default;
        }

        public override void AI() {
            //Color 在 SetProperty 之后才被赋值，首帧捕获
            if (!colorCaptured) {
                initColor = Color;
                colorCaptured = true;
            }

            if (landed) {
                //贴地摊平渐隐
                landedTimer++;
                Velocity = Vector2.Zero;
                Color = initColor * MathHelper.Clamp(1f - landedTimer / 22f, 0f, 1f);
                if (landedTimer >= 22) {
                    active = false;
                }
                return;
            }

            Velocity.Y += 0.24f;
            if (Velocity.Y > 12f) {
                Velocity.Y = 12f;
            }
            wobblePhase += 0.32f;

            //落地变摊平
            if (Velocity.Y > 0f && Terraria.Collision.SolidCollision(Position - new Vector2(2f, 2f), 4, 4)) {
                landed = true;
                return;
            }

            float t = LifetimeCompletion;
            Color = initColor * (1f - t * t * 0.85f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            float speed = Velocity.Length();
            //速度拉伸+表面张力抖动
            float stretch = MathHelper.Clamp(speed * 0.05f, 0f, 0.85f);
            float wob = 0.1f * (float)Math.Sin(wobblePhase);
            Vector2 drawScale;
            float rot;
            if (landed) {
                //摊平成扁圆
                float flat = MathHelper.Clamp(landedTimer / 6f, 0f, 1f);
                drawScale = new Vector2(1f + flat * 0.9f, 1f - flat * 0.62f) * Scale * 0.36f;
                rot = 0f;
            }
            else {
                drawScale = new Vector2(1f - stretch * 0.4f + wob, 1f + stretch - wob) * Scale * 0.34f;
                rot = speed > 0.5f ? Velocity.ToRotation() - MathHelper.PiOver2 : 0f;
            }

            Vector2 pos = Position - Main.screenPosition;
            //胶体主体
            spriteBatch.Draw(tex, pos, null, Color, rot, tex.Size() / 2f, drawScale, SpriteEffects.None, 0f);
            //高光芯
            spriteBatch.Draw(tex, pos, null, Color.White * (Color.A / 255f * 0.55f), rot,
                tex.Size() / 2f, drawScale * 0.42f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
