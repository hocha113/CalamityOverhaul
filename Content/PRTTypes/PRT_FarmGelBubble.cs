using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 培养槽凝胶气泡:凝胶体内一圈薄亮缘带着高光点缓升,升到指定高度胀破。
    /// DiffusionCircle4 黑底薄锐缘,只进加色批
    /// </summary>
    internal class PRT_FarmGelBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 100;

        private float popY;
        private float wobblePhase;
        private bool popping;

        /// <summary>burstWorldY:升至该世界 Y 即顶破</summary>
        public PRT_FarmGelBubble Configure(float burstWorldY) {
            popY = burstWorldY;
            return this;
        }

        public override void Reset() {
            base.Reset();
            popY = 0f;
            wobblePhase = 0f;
            popping = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            //护栏:未配置顶破高度时靠寿命回收
            if (Lifetime <= 0) {
                Lifetime = 150;
            }
        }

        public override void AI() {
            if (!popping) {
                wobblePhase += 0.11f;
                Velocity = new Vector2(MathF.Sin(wobblePhase) * 0.14f, -0.32f);
                Scale += 0.003f;
                Opacity = MathF.Min(Time * 0.12f, 0.85f);
                if (popY != 0f && Position.Y <= popY) {
                    popping = true;
                    ai[0] = 0f;
                    Velocity = Vector2.Zero;
                }
            }
            else {
                //顶破:薄环猛胀猛淡几帧,读作泡膜绷开
                Scale *= 1.16f;
                Opacity *= 0.55f;
                if (++ai[0] >= 4f) {
                    Kill();
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float drawScale = Scale * 14f / tex.Width;
            spriteBatch.Draw(tex, drawPos, null, Color * Opacity, 0f, origin, drawScale, SpriteEffects.None, 0f);
            //泡壁高光点,挂在环缘左上,泡有受光面才不是一个抽象圆
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            float r = tex.Width * drawScale * 0.5f;
            spriteBatch.Draw(glowTex, drawPos + new Vector2(-r * 0.4f, -r * 0.42f), null,
                new Color(235, 255, 245) * (Opacity * 0.5f), 0f, glowTex.Size() * 0.5f, 0.05f * Scale + 0.02f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
