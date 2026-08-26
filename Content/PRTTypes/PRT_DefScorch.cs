using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 灼痕余温:激光命中点/火焰烧灼面的短存残迹。Extra_98 真 alpha:
    /// 暗焦底真正压暗地面,热芯 A=0 加色,热比焦先冷(热芯衰减快于暗底)
    /// </summary>
    internal class PRT_DefScorch : BasePRT
    {
        public override int InGame_World_MaxCount => 80;
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color hotColor;
        private float squish;

        /// <param name="lifetime">存活帧数</param>
        /// <param name="flatSquish">垂直压扁比,贴地痕用 0.5 左右,贴墙用 1</param>
        public PRT_DefScorch Configure(int lifetime, float flatSquish = 0.55f) {
            Lifetime = lifetime;
            hotColor = Color with { A = 0 };
            squish = flatSquish;
            Rotation = Main.rand.NextFloat(-0.25f, 0.25f);
            return this;
        }

        public override void SetProperty() {
            Velocity = Vector2.Zero;
        }

        public override void Reset() {
            base.Reset();
            hotColor = default;
            squish = 1f;
        }

        public override void AI() {
            //灼痕钉在原地,只余温呼吸
            Velocity = Vector2.Zero;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float t = LifetimeCompletion;

            //暗焦底:真 alpha 压暗,慢冷;贴图仅72px,系数放大补足读得见的灼痕
            float darkA = (1f - t) * 0.55f;
            Color dark = new Color(26, 12, 10) * darkA;
            Vector2 scaleDark = new Vector2(0.85f, 0.85f * squish) * Scale;
            spriteBatch.Draw(tex, pos, null, dark, Rotation, origin, scaleDark, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, dark * 0.7f, Rotation + 1.35f, origin, scaleDark * 0.8f, SpriteEffects.None, 0f);

            //热芯:A=0 加色,快冷+微呼吸
            float hotT = MathF.Pow(MathHelper.Clamp(1f - t * 1.7f, 0f, 1f), 1.5f);
            float breath = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Position.X * 0.1f);
            Color hot = hotColor * (hotT * 0.8f * breath);
            spriteBatch.Draw(tex, pos, null, hot, Rotation, origin, scaleDark * 0.55f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, hot * 0.6f, Rotation + 1.35f, origin, scaleDark * 0.34f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
