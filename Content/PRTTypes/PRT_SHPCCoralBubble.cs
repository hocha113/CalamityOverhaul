using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>珊瑚礁气泡，上浮摆动，末段顶破微胀即灭</summary>
    internal class PRT_SHPCCoralBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override bool CanPool => true;

        private float wobblePhase;
        private float baseScale;
        private float riseSpeed;

        public PRT_SHPCCoralBubble Configure(int lifeTime) {
            Lifetime = lifeTime;
            baseScale = Scale;
            riseSpeed = Velocity.Y;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void Reset() {
            base.Reset();
            wobblePhase = 0f;
            baseScale = 0f;
            riseSpeed = 0f;
        }

        public override void AI() {
            wobblePhase += 0.16f;
            //横向水摆，纵向保持上浮
            Velocity = new Vector2(MathF.Sin(wobblePhase) * 0.35f, riseSpeed);
            float t = LifetimeCompletion;
            //末10%顶破，略胀即灭
            float pop = t > 0.9f ? 1f + (t - 0.9f) * 3f : 1f;
            Scale = baseScale * (0.8f + 0.2f * MathF.Sin(t * MathHelper.Pi)) * pop;
            Opacity = MathF.Min(t * 8f, 1f) * (1f - MathF.Pow(t, 6f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            spriteBatch.Draw(tex, pos, null, Color * Opacity, 0f, origin, Scale, SpriteEffects.None, 0f);
            //水膜高光点偏上，随泡径缩放
            Vector2 hl = new Vector2(-0.30f, -0.36f) * (tex.Width * 0.5f * Scale);
            spriteBatch.Draw(tex, pos + hl, null, new Color(255, 255, 250) * Opacity * 0.35f, 0f, origin, Scale * 0.22f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
