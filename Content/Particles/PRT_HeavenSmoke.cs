using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Particles
{
    internal class PRT_HeavenSmoke : BaseParticle
    {
        public override bool SetLifetime => true;
        public override int FrameVariants => 7;
        public override bool UseCustomDraw => true;
        public override bool Important => StrongVisual;
        public override bool UseAdditiveBlend => Glowing;
        public override bool UseHalfTransparency => !Glowing;

        public override string Texture => "CalamityMod/Particles/HeavySmoke";

        private Color[] rainbowColors = new Color[] { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Indigo, Color.Violet };

        private Color rainColor => CWRUtils.MultiStepColorLerp(sengs % 30 / 30f, rainbowColors);
        private float Opacity;
        private float Spin;
        private int MaxTime;
        private bool StrongVisual;
        private bool Glowing;
        private float HueShift;
        private float Scale2;
        private int sengs;
        private Player player;
        private static int FrameAmount = 6;

        public PRT_HeavenSmoke(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale, float opacity, float rotationSpeed = 0f, bool glowing = false, float hueshift = 0f, bool required = false, Player player = null) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = Scale2 = scale;
            Variant = Main.rand.Next(7);
            Lifetime = MaxTime = lifetime;
            Opacity = opacity;
            Spin = rotationSpeed;
            StrongVisual = required;
            Glowing = glowing;
            HueShift = hueshift;
            this.player = player;
            sengs = Main.rand.Next(30);
        }

        public override void AI() {
            if (Time / (float)Lifetime < 0.2f)
                Scale += 0.01f;
            else
                Scale *= 0.975f;

            Color = Color == Color.White ? rainColor : Color;
            Opacity *= 0.98f;
            Rotation += Spin * ((Velocity.X > 0) ? 1f : -1f);
            Velocity *= 0.85f;
            float opacity = Utils.GetLerpValue(1f, 0.85f, LifetimeCompletion, true);
            Color *= opacity;
            if (player != null)
                Position += player.velocity;
            sengs++;
        }

        public override void CustomDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.ParticleIDToTexturesDic[Type];
            int animationFrame = (int)Math.Floor(Time / ((float)(Lifetime / (float)FrameAmount)));
            Rectangle frame = new Rectangle(80 * Variant, 80 * animationFrame, 80, 80);
            Vector2 pos = Position - Main.screenPosition;
            Vector2 org = frame.Size() / 2f;
            spriteBatch.Draw(tex, pos, frame, rainColor * Opacity, Rotation, org, Scale, SpriteEffects.None, 0);
        }
    }
}
