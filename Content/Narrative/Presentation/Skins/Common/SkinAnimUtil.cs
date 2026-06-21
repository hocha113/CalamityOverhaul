namespace CalamityOverhaul.Content.Narrative.Presentation.Skins.Common
{
    internal static class SkinAnimUtil
    {
        public static float WrapTimer(float value, float delta) {
            value += delta;
            if (value > MathHelper.TwoPi) {
                value -= MathHelper.TwoPi;
            }
            return value;
        }

        public static void Advance(ref float timer, float speed) {
            timer += speed;
            if (timer > MathHelper.TwoPi) {
                timer -= MathHelper.TwoPi;
            }
        }

        public static float AdvanceShaderTime(float shaderTime, float delta = 0.016f) {
            shaderTime += delta;
            if (shaderTime > 10000f) {
                shaderTime -= 10000f;
            }
            return shaderTime;
        }
    }
}
