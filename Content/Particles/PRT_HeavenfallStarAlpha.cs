
namespace CalamityOverhaul.Content.Particles
{
    internal class PRT_HeavenfallStarAlpha : PRT_HeavenfallStar
    {
        public PRT_HeavenfallStarAlpha(Vector2 relativePosition, Vector2 velocity, bool affectedByGravity, int lifetime, float scale, Color color) 
            : base(relativePosition, velocity, affectedByGravity, lifetime, scale, color) {
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            SetLifetime = true;
        }
    }
}
