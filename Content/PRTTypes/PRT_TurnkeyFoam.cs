using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 沉波狱吏水面沫斑：暴起水花/划水冲程留在水面的白沫团。
    /// 出生后被浮力拽回给定水面行贴平，横向漂移衰减、随水面轻微起伏，摊宽后溶散。
    /// 余韵层：水记得它闹过（BOSS-REWORK §5 第四相）。
    /// </summary>
    internal class PRT_TurnkeyFoam : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 260;

        private Color initialColor;
        private float surfaceY;
        private float bobSeed;

        /// <param name="lifetime">寿命帧</param>
        /// <param name="waterlineY">水面世界 Y（沫斑贴平的目标高度）</param>
        public PRT_TurnkeyFoam Configure(int lifetime, float waterlineY) {
            Lifetime = lifetime;
            surfaceY = waterlineY;
            initialColor = Color;
            bobSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            surfaceY = 0f;
            bobSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 50;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //浮力回贴水面：越远拽得越急，贴上后只剩起伏
            float targetY = surfaceY + MathF.Sin(Time * 0.11f + bobSeed) * 1.6f;
            Velocity.Y = MathHelper.Lerp(Velocity.Y, (targetY - Position.Y) * 0.16f, 0.3f);
            Velocity.X *= 0.955f;

            float t = LifetimeCompletion;
            //先聚后散：前 20% 微缩聚拢，之后摊宽
            Scale *= t < 0.2f ? 0.995f : 1.006f;
            if (t > 0.55f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.55f) / 0.45f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //贴水横扁沫团：宽体 + 半宽亮心（撕裂感靠双层错位）
            float wob = 1f + MathF.Sin(Time * 0.23f + bobSeed) * 0.14f;
            Vector2 body = new Vector2(0.30f * wob, 0.085f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, 0f, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos + new Vector2(2f * MathF.Sin(bobSeed), -1f), null,
                Color * 0.75f, 0f, origin, body * new Vector2(0.5f, 0.9f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
