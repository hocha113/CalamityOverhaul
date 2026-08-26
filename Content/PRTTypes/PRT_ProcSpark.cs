using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 回收机切割火花:白热芯→橙→暗红冷却,速度拉伸,减速后吃重力,
    /// 可给台面高度做一次弹跳。Additive
    /// </summary>
    internal class PRT_ProcSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 320;

        private Color hotColor;
        private float floorY;
        private int bounces;

        public PRT_ProcSpark Configure(int lifetime, float floorWorldY = 0f) {
            hotColor = Color;
            floorY = floorWorldY;
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(14, 26);
            }
        }

        public override void Reset() {
            base.Reset();
            hotColor = default;
            floorY = 0f;
            bounces = 0;
        }

        public override void AI() {
            Velocity.X *= 0.96f;
            if (Velocity.Length() < 9f) {
                Velocity.Y += 0.30f;
            }
            //台面反弹:金属火花打在钢台上蹦一下
            if (floorY > 0f && Position.Y > floorY && Velocity.Y > 0f && bounces < 2) {
                bounces++;
                Position.Y = floorY;
                Velocity.Y *= -0.38f;
                Velocity.X *= 0.72f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            //冷却:白热→给定橙色→暗红
            float t = LifetimeCompletion;
            Color cooled = t < 0.35f
                ? Color.Lerp(new Color(255, 244, 214), hotColor, t / 0.35f)
                : Color.Lerp(hotColor, new Color(120, 34, 16), (t - 0.35f) / 0.65f);
            Color = cooled * (1f - MathF.Pow(t, 3f));
            Scale *= 0.985f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //速度拉伸:快时拉长条,慢时收短
            float speed = Velocity.Length();
            float stretch = MathHelper.Clamp(0.5f + speed * 0.16f, 0.5f, 2.2f);
            Vector2 scale = new Vector2(0.10f, 0.34f * stretch) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, scale, SpriteEffects.None, 0f);
            //白热窄芯
            spriteBatch.Draw(tex, pos, null, Color.White * (Color.A / 255f * 0.7f), Rotation, origin,
                scale * new Vector2(0.45f, 0.8f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
