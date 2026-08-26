using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 自动合成台入料流光:向目标点滑入的拉伸光条,到位即灭。Additive
    /// </summary>
    internal class PRT_ProcIntake : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private Vector2 target;
        private Color baseColor;

        public PRT_ProcIntake Configure(Vector2 targetWorld, int lifetime) {
            target = targetWorld;
            baseColor = Color;
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 26;
            }
        }

        public override void Reset() {
            base.Reset();
            target = default;
            baseColor = default;
        }

        public override void AI() {
            Vector2 toTarget = target - Position;
            float dist = toTarget.Length();
            if (dist < 5f) {
                Time = Lifetime;
                return;
            }
            //向目标点收束,近端减速
            Vector2 desired = toTarget * 0.16f;
            float maxSpeed = MathHelper.Clamp(dist * 0.2f, 1.6f, 8.5f);
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Velocity = Vector2.Lerp(Velocity, desired, 0.3f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            float t = LifetimeCompletion;
            float envelope = MathF.Min(t / 0.18f, 1f) * (1f - MathHelper.Clamp((t - 0.8f) / 0.2f, 0f, 1f));
            Color = baseColor * envelope;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(0.6f + Velocity.Length() * 0.22f, 0.6f, 2.6f);
            Vector2 scale = new Vector2(0.09f, 0.4f * stretch) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //流光头部亮点
            spriteBatch.Draw(tex, pos + Velocity * 0.8f, null, Color.White * (Color.A / 255f * 0.6f),
                Rotation, origin, scale * new Vector2(0.5f, 0.4f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
