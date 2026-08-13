using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 滴淌墨:伞缘闲滴/蓄墨溢缘/渍斑垂流的细长墨线——
    /// 近零初速被重力拽下,越坠越细长,末段断成一点(墨雨普攻自有件)
    /// </summary>
    internal class PRT_KikasaInkDrip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 240;

        private Color initialColor;

        public PRT_KikasaInkDrip Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 30;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //挂着的那一瞬几乎不动,断线后越坠越快
            Velocity.X *= 0.96f;
            Velocity.Y = MathF.Min(Velocity.Y + 0.24f, 12f);

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, KikasaInk.InkBody, MathF.Pow(t, 1.4f) * 0.6f);
            Opacity = 1f - MathF.Pow(t, 2.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //越快越细长:滴线不是滴珠
            float stretch = MathHelper.Clamp(Velocity.Y * 0.11f, 0.1f, 1.4f);
            Vector2 scale = new Vector2(0.1f * (1f - stretch * 0.3f), 0.34f * (1f + stretch * 2.2f)) * Scale;

            Color body = Color * Opacity;
            spriteBatch.Draw(tex, pos, null, Color.Lerp(Color, KikasaInk.InkDeep, 0.5f) * (0.8f * Opacity),
                0f, origin, scale * new Vector2(1.4f, 1.03f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
