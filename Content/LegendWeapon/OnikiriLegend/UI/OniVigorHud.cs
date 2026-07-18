using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 气力墨脉 HUD 的表现状态。真实数据只有当前值/上限；
    /// 墨锋追随、消耗残痕与恢复脉动均在此按帧差推导
    /// </summary>
    internal sealed class OniVigorHud
    {
        private bool initialized;
        private bool wasFull;
        private float availability;
        private float targetFill;
        private float displayFill;
        private float trailFill;
        private float flowVelocity;
        private float spendPulse;
        private float gainPulse;
        private float fullPulse;
        private int trailHold;

        public void Update(Player player, bool holdingOnikiri) {
            DecayImpulses();

            if (!holdingOnikiri || !OniVigorData.TryGet(player, out OniVigorSnapshot snapshot)) {
                availability = Math.Max(0f, availability - 0.10f);
                flowVelocity *= 0.82f;
                if (availability <= 0.001f) {
                    initialized = false;
                }
                return;
            }

            availability = Math.Min(1f, availability + 0.12f);
            float nextTarget = snapshot.Ratio;
            if (!initialized) {
                initialized = true;
                targetFill = displayFill = trailFill = nextTarget;
                wasFull = nextTarget >= 0.999f;
                flowVelocity = 0f;
                trailHold = 0;
                return;
            }

            float change = nextTarget - targetFill;
            float flowTarget = MathHelper.Clamp(change * 22f, -1f, 1f);
            flowVelocity = MathHelper.Lerp(flowVelocity, flowTarget, 0.24f);

            if (change < -0.0005f) {
                trailFill = Math.Max(trailFill, targetFill);
                trailHold = 10;
                spendPulse = Math.Max(spendPulse, MathHelper.Clamp(0.28f - change * 5f, 0f, 1f));
            }
            else if (change > 0.0005f) {
                gainPulse = Math.Max(gainPulse, MathHelper.Clamp(0.18f + change * 4f, 0f, 1f));
            }

            bool fullNow = nextTarget >= 0.999f;
            if (fullNow && !wasFull) {
                fullPulse = 1f;
            }
            wasFull = fullNow;
            targetFill = nextTarget;

            float follow = targetFill < displayFill ? 0.36f : 0.11f;
            displayFill = MathHelper.Lerp(displayFill, targetFill, follow);
            if (Math.Abs(displayFill - targetFill) < 0.0005f) {
                displayFill = targetFill;
            }

            if (trailFill < displayFill) {
                trailFill = displayFill;
            }
            else if (trailHold > 0) {
                trailHold--;
            }
            else {
                trailFill = MathHelper.Lerp(trailFill, displayFill, 0.075f);
            }
        }

        private void DecayImpulses() {
            spendPulse *= 0.86f;
            gainPulse *= 0.90f;
            fullPulse *= 0.92f;
        }

        public void Draw(SpriteBatch spriteBatch, Vector2 anchor, Vector2 linkFrom, float alpha, float time) {
            float drawAlpha = alpha * availability;
            if (!initialized || drawAlpha <= 0.01f) {
                return;
            }

            Vector2 offset = OnikiriUITheme.VigorHudOffset;
            Vector2 size = OnikiriUITheme.VigorHudSize;
            Rectangle destination = new(
                (int)(anchor.X + offset.X),
                (int)(anchor.Y + offset.Y),
                (int)size.X,
                (int)size.Y);

            OniVigorRenderer.Draw(spriteBatch, destination, linkFrom, drawAlpha, time,
                displayFill, trailFill, flowVelocity, spendPulse, gainPulse, fullPulse);
        }
    }
}
