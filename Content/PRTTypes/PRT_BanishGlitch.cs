using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>赛博放逐故障方块，放逐演出</summary>
    internal class PRT_BanishGlitch : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 8000;

        private float initialScale;
        private float rotationSpeed;
        private float aspectRatio;
        private Color edgeColor;
        private float flickerPhase;
        private float driftAngle;
        /// <summary>&gt;0 时坠落，沿速度方向拉伸成烬屑流光</summary>
        private float gravity;

        public override bool CanPool => true;
        public PRT_BanishGlitch() {
            Color = new Color(0.9f, 0.12f, 0.08f);
            edgeColor = new Color(1f, 0.3f, 0.2f);
            aspectRatio = 1f;
        }
        public PRT_BanishGlitch(Vector2 position, Vector2 velocity, float scale, int lifeTime) {
            Position = position;
            Velocity = velocity;
            Color = new Color(0.9f, 0.12f, 0.08f);
            edgeColor = new Color(1f, 0.3f, 0.2f);
            Scale = initialScale = scale;
            Lifetime = lifeTime;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            rotationSpeed = Main.rand.NextFloat(0.06f, 0.18f) * (Main.rand.NextBool() ? 1f : -1f);
            aspectRatio = Main.rand.NextFloat(0.3f, 2.0f);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            driftAngle = Main.rand.NextFloat(MathHelper.TwoPi);
        }
        public PRT_BanishGlitch Configure(int lt) {
            Lifetime = lt;
            initialScale = Scale;
            Color = new Color(0.9f, 0.12f, 0.08f);
            edgeColor = new Color(1f, 0.3f, 0.2f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            rotationSpeed = Main.rand.NextFloat(0.06f, 0.18f) * (Main.rand.NextBool() ? 1f : -1f);
            aspectRatio = Main.rand.NextFloat(0.3f, 2.0f);
            flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            driftAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        /// <summary>带重力变体：烬屑坠落，既有调用方不受影响</summary>
        public PRT_BanishGlitch Configure(int lt, float gravityPull) {
            Configure(lt);
            gravity = gravityPull;
            return this;
        }
        public override void Reset() {
            base.Reset();
            initialScale = 0f;
            rotationSpeed = 0f;
            aspectRatio = 1f;
            Color = new Color(0.9f, 0.12f, 0.08f);
            edgeColor = new Color(1f, 0.3f, 0.2f);
            flickerPhase = 0f;
            driftAngle = 0f;
            gravity = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float life = LifetimeCompletion;

            //后半加速扩散
            float accelPhase = MathF.Pow(MathHelper.Clamp((life - 0.3f) / 0.7f, 0f, 1f), 2f);
            Velocity *= 1f + accelPhase * 0.04f;

            float jitter = MathF.Sin(Time * 0.5f + flickerPhase) * 0.15f;
            Position += new Vector2(MathF.Cos(driftAngle), MathF.Sin(driftAngle)) * jitter;

            //坠落模式姿态锁速度方向并拉伸，与自旋互斥
            if (gravity > 0f) {
                Velocity = new Vector2(Velocity.X, Velocity.Y + gravity);
                Rotation = Velocity.ToRotation();
                aspectRatio = 1f / (1f + Velocity.Length() * 0.22f);
            }
            else {
                Rotation += rotationSpeed * (1f + accelPhase);
            }

            //后40%缩小
            if (life > 0.6f) {
                Scale = initialScale * (1f - MathF.Pow((life - 0.6f) / 0.4f, 1.5f));
            }

            float flicker = 0.5f + 0.5f * MathF.Sin(Time * 1.5f + flickerPhase);
            //偶发近熄一帧
            float glitchBlink = (hash(Time * 0.2f + flickerPhase) > 0.88f) ? 0.1f : 1f;
            Opacity = flicker * glitchBlink * (1f - MathF.Pow(life, 3f));
        }

        private static float hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            float w = 5f * Scale;
            float h = 5f * Scale * aspectRatio;
            Vector2 size = new(w, h);
            Vector2 origin = new(0.5f, 0.5f);

            Color outer = edgeColor * Opacity * 0.5f;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), outer, Rotation,
                origin, size * 1.6f, SpriteEffects.None, 0f);

            Color inner = Color * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), inner, Rotation,
                origin, size, SpriteEffects.None, 0f);

            Color core = new Color(1f, 0.5f, 0.4f) * Opacity * 0.8f;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core, Rotation,
                origin, size * 0.3f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
