using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 防御工事共用火星/能量迸点:激光命中火花与火焰塔余烬共用。
    /// SoftGlow 黑底加色批;速度拉伸+逐帧明灭,重力下坠读作有质量的燃屑
    /// </summary>
    internal class PRT_DefEmber : BasePRT
    {
        public override int InGame_World_MaxCount => 200;
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private float drag;
        private float flicker = 1f;

        public PRT_DefEmber Configure(int lifetime, float gravityPerFrame = 0.1f, float dragMul = 0.97f) {
            Lifetime = lifetime;
            //加色批源因子是 SourceAlpha,A=0 整颗消失,强制 A=255
            initialColor = Color with { A = 255 };
            gravity = gravityPerFrame;
            drag = dragMul;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            drag = 1f;
            flicker = 1f;
        }

        public override void AI() {
            Velocity *= drag;
            Velocity.Y += gravity;
            if (Velocity.Y > 12f) {
                Velocity.Y = 12f;
            }
            //燃屑明灭:逐帧微抖
            flicker = Main.rand.NextFloat(0.8f, 1.15f);

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(t, 1.6f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float speed = Velocity.Length();
            //快成丝、慢成点
            float stretch = MathHelper.Clamp(speed * 0.07f, 0f, 1.4f);
            float rot = Velocity.ToRotation();
            Vector2 scale = new Vector2(0.09f * (1f + stretch * 2.4f), 0.075f * (1f - stretch * 0.3f)) * Scale;

            spriteBatch.Draw(tex, pos, null, Color * flicker, rot, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * (0.55f * flicker), rot, origin, scale * 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
