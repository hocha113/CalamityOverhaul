using CalamityMod;
using CalamityMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.DrawTools
{
    public class CWRElectricArc : Particle
    {
        public Color LightColor => Color * 0.45f;

        public Vector2[] TrailPositions = new Vector2[10];

        public override bool UseAdditiveBlend => true;
        public override bool UseCustomDraw => true;
        public override bool SetLifetime => true;
        public override string Texture => "CalamityMod/ExtraTextures/SmallGreyscaleCircle";

        public CWRElectricArc(Vector2 position, Vector2 velocity, Color color, float scale, int lifeTime) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = scale;
            Lifetime = lifeTime;
        }

        public override void Update() {
            Vector2 perlinValue = Position * 0.1f;
            Vector2 perlinOffset = (CalamityUtils.PerlinNoise2D(perlinValue.X, perlinValue.Y, 4, ID) * MathHelper.Pi * 3f).ToRotationVector2() * 2f;
            Velocity = (Velocity + perlinOffset).ClampMagnitude(4f, 9f);

            for (int i = TrailPositions.Length - 1; i > 0; i--)
                TrailPositions[i] = TrailPositions[i - 1];
            TrailPositions[0] = Position;
        }

        public override void CustomDraw(SpriteBatch spriteBatch) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 scale = Vector2.One * Scale * 11f / texture.Size() * 0.5f;
            Vector2 origin = texture.Size() * 0.5f;

            int timeLeft = Lifetime - Time;
            int trailCount = TrailPositions.Length;
            if (timeLeft < trailCount)
                trailCount = timeLeft;
            float opacity = Utils.GetLerpValue(0f, 16f, timeLeft, true);
            for (int i = 1; i < trailCount; i++) {
                Vector2 position = TrailPositions[i];
                position = Vector2.Lerp(position, TrailPositions[i - 1], 0.9f);
                float rotation = (TrailPositions[i - 1] - position).ToRotation();

                position -= Main.screenPosition;
                Main.spriteBatch.Draw(texture, position, null, Color.White * opacity, rotation, origin, scale * new Vector2(4f, 0.5f), 0, 0f);
                Main.spriteBatch.Draw(texture, position, null, Color * opacity, rotation, origin, scale * new Vector2(4f, 1f), 0, 0f);
            }
        }
    }
}
