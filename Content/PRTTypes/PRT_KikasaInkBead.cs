using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 墨珠:伞缘甩出/命中迸溅的小墨滴——重力弧线、速度拉伸、
    /// 暗缘压边给体积、新鲜期一点湿反光,渐干转沉、尾段陡淡(墨雨普攻自有件)
    /// </summary>
    internal class PRT_KikasaInkBead : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 400;

        private Color initialColor;
        private float gravity;
        private float drag;

        public PRT_KikasaInkBead Configure(int lifetime, float gravityPerFrame = 0.32f, float dragMul = 0.985f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            drag = dragMul;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            drag = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 24;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= drag;
            Velocity.Y = MathF.Min(Velocity.Y + gravity, 15f);

            float t = LifetimeCompletion;
            Scale *= 0.987f;
            //墨越放越沉,透明度尾段陡降
            Color = Color.Lerp(initialColor, KikasaInk.InkBody, MathF.Pow(t, 1.5f) * 0.7f);
            Opacity = 1f - MathF.Pow(t, 3f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.34f * (1f - stretch * 0.4f), 0.55f * (1f + stretch * 1.8f)) * Scale;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, KikasaInk.InkDeep, 0.6f) * Opacity;
            //暗缘略宽一圈,珠有体积不是点
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin, scale * new Vector2(1.35f, 1.06f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale, SpriteEffects.None, 0f);

            //新鲜期湿面反光:A=0 加色小玻头
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2.2f, 0f, 1f);
            if (fresh > 0.05f) {
                Color glint = KikasaInk.WetSheen with { A = 0 };
                spriteBatch.Draw(tex, pos, null, glint * (0.35f * fresh * Opacity), Rotation, origin,
                    scale * new Vector2(0.2f, 0.5f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
