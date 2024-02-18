using CalamityMod;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class GildedProboscisLightningArc : StormArc
    {
        public override Color PrimitiveColorFunction(float completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = CalamityUtils.MulticolorLerp(colorInterpolant, Color.Gold);
            return color;
        }
    }
}
