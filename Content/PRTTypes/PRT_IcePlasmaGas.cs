using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_IcePlasmaGas : BasePRT
    {
        public override string Texture => "CalamityMod/Projectiles/Summon/SmallAresArms/MinionPlasmaGas";
        private bool StrongVisual;
        private float sengsValue;

        public PRT_IcePlasmaGas(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale, bool required = false) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = scale;
            Lifetime = lifetime;
            StrongVisual = required;
            sengsValue = 0;
        }
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            SetLifetime = true;
        }
        public override void AI() {
            Scale = 0.5f + (sengsValue * 0.01f);
            Velocity *= 0.85f;
            sengsValue++;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Position - Main.screenPosition;
            float opacity = 0.9f;
            opacity *= Lifetime / sengsValue;
            Color drawColor = new Color(118, 217, 222) * opacity;
            Vector2 scale = new Vector2(184, 184) / texture.Size() * Scale * 1.35f;
            spriteBatch.Draw(texture, drawPosition, null, drawColor, Main.rand.NextFloat(0f, MathHelper.TwoPi), origin, scale, 0, 0f);
            spriteBatch.Draw(texture, drawPosition, null, drawColor, Main.rand.NextFloat(0f, MathHelper.TwoPi), origin, scale, 0, 0f);
            return false;
        }
    }
}
