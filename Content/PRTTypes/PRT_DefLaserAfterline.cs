using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.LaserTurrets;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 激光余辉:光束死后短存的残线,自炮口侧排空塌缩(能量灌进伤口的读法),
    /// 前6帧兼任命中点爆闪。拖尾不许随弹幕一起死,这是四相预算的余相载体
    /// </summary>
    internal class PRT_DefLaserAfterline : BasePRT
    {
        public override int InGame_World_MaxCount => 30;
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override bool CanPool => true;

        private Vector2 beamStart;
        private Vector2 beamEnd;
        private Color baseColor;

        public PRT_DefLaserAfterline Configure(Vector2 start, Vector2 end) {
            Lifetime = 16;
            beamStart = start;
            beamEnd = end;
            baseColor = Color;
            Position = (start + end) * 0.5f;
            Velocity = Vector2.Zero;
            return this;
        }

        public override void Reset() {
            base.Reset();
            beamStart = default;
            beamEnd = default;
            baseColor = default;
        }

        public override void AI() {
            Velocity = Vector2.Zero;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            float t = LifetimeCompletion;

            //残线:宽度塌缩+自炮口侧侵蚀
            DefLaserBeamDraw.Draw(spriteBatch, beamStart, beamEnd,
                1f - 0.55f * t, MathF.Pow(1f - t, 1.7f), t * 0.85f);

            //命中点爆闪:前6帧的星芒十字+白心
            float flashT = MathHelper.Clamp(t / 0.375f, 0f, 1f);
            if (flashT < 1f) {
                Texture2D star = CWRAsset.StarTexture?.Value;
                if (star != null) {
                    float pop = 1f - flashT;
                    Vector2 pos = beamEnd - Main.screenPosition;
                    Vector2 origin = star.Size() * 0.5f;
                    Color red = baseColor with { A = 0 };
                    Color white = new(255, 240, 240, 0);
                    float grow = 0.22f + 0.16f * flashT;
                    spriteBatch.Draw(star, pos, null, red * (0.85f * pop), 0f, origin,
                        new Vector2(grow, grow * 0.8f), SpriteEffects.None, 0f);
                    spriteBatch.Draw(star, pos, null, red * (0.6f * pop), MathHelper.PiOver4, origin,
                        new Vector2(grow * 0.6f, grow * 0.5f), SpriteEffects.None, 0f);
                    spriteBatch.Draw(star, pos, null, white * (0.9f * pop), 0f, origin,
                        grow * 0.45f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
