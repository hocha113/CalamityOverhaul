using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Duskwatch
{
    /// <summary>
    /// 日食灰翳尘：Extra_98 真 alpha 小灰翳，缓落带横摆微翻滚，
    /// 像日冕昏光里飘散的细灰，读作光被啃噬后落下的渣
    /// </summary>
    internal class PRT_DuskwatchAsh : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private Color initialColor;
        private float swayPhase;
        private float spinRate;

        public PRT_DuskwatchAsh Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            spinRate = Main.rand.NextFloat(-0.05f, 0.05f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            swayPhase = 0f;
            spinRate = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 240;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //缓落+横摆：随风微移，摆幅读作无重量的细灰
            Velocity.X = MathHelper.Lerp(Velocity.X,
                Main.windSpeedCurrent * 1.2f + MathF.Sin(swayPhase + Time * 0.045f) * 0.35f, 0.03f);
            Velocity.Y = MathHelper.Lerp(Velocity.Y,
                0.45f + MathF.Sin(swayPhase * 1.7f + Time * 0.06f) * 0.18f, 0.04f);
            Rotation += spinRate;

            float t = LifetimeCompletion;
            float env = MathHelper.Clamp(t / 0.12f, 0f, 1f)
                * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
            Color = initialColor * env;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //小灰片：近方形微片，双层压出一点厚度
            Vector2 body = new Vector2(0.05f, 0.07f) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * 0.5f, Rotation, origin,
                body * new Vector2(0.5f, 1.05f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
