using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Effects;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Dusts
{
    internal class InShootGlow : ModDust
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle2";
        public override void OnSpawn(Dust dust) {
            dust.noGravity = true;
            dust.frame = new(0, 0, 66, 66);
            dust.shader = EffectsRegistry.InShootGlowShader;
        }

        public override Color? GetAlpha(Dust dust, Color lightColor) => dust.color;

        public override bool Update(Dust dust) {
            if (dust.customData is null) {
                dust.position -= Vector2.One * 32 * dust.scale;
                dust.customData = true;
            }
            Vector2 center = dust.position + Vector2.One.RotatedBy(dust.rotation) * 32 * dust.scale;

            dust.scale *= 0.95f;
            Vector2 nextCenter = dust.position + Vector2.One.RotatedBy(dust.rotation + 0.06f) * 32 * dust.scale;
            dust.rotation += 0.06f;
            dust.position += center - nextCenter;

            dust.shader.UseColor(dust.color);

            dust.position += dust.velocity;

            if (!dust.noGravity) {
                dust.velocity.Y += 0.1f;
            }

            dust.velocity *= 0.99f;
            dust.color *= 0.95f;

            if (!dust.noLight) {
                Lighting.AddLight(dust.position, dust.color.ToVector3());
            }

            if (dust.scale < 0.05f) {
                dust.active = false;
            }

            return false;
        }
    }
}
