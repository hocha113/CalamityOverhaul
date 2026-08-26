using CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 深牢禁室狱火余烬：冷粉小辉点自地面缓慢上飘，途中噪声摆动、明灭喘息，
    /// 尾段收缩熄灭。纯客户端氛围粒子，由 GaolRoomVisualSystem 按房态定率播撒。
    /// </summary>
    internal class PRT_GaolRoomEmber : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Photosphere";

        public override bool CanLoad() => DeepGaolWraithGate.Enabled;

        public override bool CanPool => true;

        /// <summary>摆动相位种子</summary>
        public float Sway;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 150;
            }
        }

        public override void Reset() {
            base.Reset();
            Sway = 0f;
        }

        public override void AI() {
            //上飘 + 横向缓摆（怨气不走直线）
            Velocity.Y = MathHelper.Lerp(Velocity.Y, -0.42f, 0.02f);
            Velocity.X = MathF.Sin(LifetimeCompletion * MathHelper.TwoPi * 1.6f + Sway * 9.4f) * 0.22f;

            //明灭喘息：入场淡入、中段火喘、尾段熄灭
            float breath = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6.2f + Sway * 17f);
            float envelope = MathF.Sin(LifetimeCompletion * MathHelper.Pi);
            Opacity = envelope * breath;
            Scale *= 0.996f;

            Lighting.AddLight(Position, 0.10f * Opacity, 0.04f * Opacity, 0.07f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            //冷粉外晕 + 偏白芯，读作一粒不肯灭的狱火
            spriteBatch.Draw(tex, drawPos, null, Color * (Opacity * 0.55f), 0f, origin,
                Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, new Color(255, 236, 244) * (Opacity * 0.5f),
                0f, origin, Scale * 0.45f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
