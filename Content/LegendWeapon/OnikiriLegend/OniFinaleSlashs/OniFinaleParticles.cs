using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>斩痕碎晶. 对比 PRT_CrimsonSpark,沿滚转轴拉长</summary>
    internal class PRT_OniShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4000;

        private Color initialColor;
        private float spin;
        private float stretch;
        private bool gravity;

        public PRT_OniShard Configure(int lifetime, float rotSpeed, float lengthStretch, bool affectedByGravity) {
            Lifetime = lifetime;
            initialColor = Color;
            spin = rotSpeed;
            stretch = lengthStretch;
            gravity = affectedByGravity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spin = stretch = 0f;
            gravity = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Velocity *= 0.955f;
            if (gravity && LifetimeCompletion > 0.35f) {
                Velocity.Y += 0.22f;
            }
            Rotation += spin;
            spin *= 0.975f;
            Scale *= 0.986f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 2.0f));
            if (Scale < 0.04f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //沿自身滚转轴拉长的窄条 + 更窄的白热芯，双层叠出晶片的锐利截面

            Vector2 scale = new Vector2(0.20f, MathF.Max(stretch, 0.8f)) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation
                , tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.85f, Rotation
                , tex.Size() * 0.5f, scale * new Vector2(0.42f, 0.86f), SpriteEffects.None, 0);
            return false;
        }
    }
}
