using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses
{
    internal enum FishingParticleType
    {
        Fish,
        Crate,
        Seashell
    }

    //钓鱼粒子类
    internal class FishingParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public FishingParticleType Type;
        public float Scale;
        public float Rotation;
        public int Life;
        private float alpha = 1f;

        public void Update(Vector2 targetPos) {
            Life--;

            //向目标移动
            Vector2 toTarget = targetPos - Position;
            float distance = toTarget.Length();

            if (distance > 10f) {
                Velocity = Vector2.Lerp(Velocity, toTarget.SafeNormalize(Vector2.Zero) * 6f, 0.1f);
                Position += Velocity;
            }
            else {
                alpha -= 0.1f;
            }

            Rotation += 0.1f;

            //添加一些随机晃动
            Position += Main.rand.NextVector2Circular(0.5f, 0.5f);
        }

        public bool ShouldRemove() => Life <= 0 || alpha <= 0;

        public void Draw(SpriteBatch spriteBatch) {
            Texture2D texture = Type switch {
                FishingParticleType.Fish => TextureAssets.Item[ItemID.Bass].Value,
                FishingParticleType.Crate => TextureAssets.Item[ItemID.WoodenCrate].Value,
                _ => TextureAssets.Item[ItemID.Seashell].Value
            };

            float fadeProgress = 1f - Life / 120f;
            Color drawColor = Color.White * alpha * (1f - fadeProgress * 0.5f);
            Vector2 drawPos = Position - Main.screenPosition;

            spriteBatch.Draw(texture, drawPos, null, drawColor, Rotation,
                texture.Size() / 2, Scale * 0.5f, SpriteEffects.None, 0);
        }
    }
}
