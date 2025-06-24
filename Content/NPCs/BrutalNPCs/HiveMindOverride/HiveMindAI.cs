using CalamityMod.NPCs.HiveMind;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.HiveMindOverride
{
    internal class HiveMindAI : CWRNPCOverride
    {
        public override int TargetID => ModContent.NPCType<HiveMind>();
        public override bool CanLoad() {
            return false;
        }
        public override void SetProperty() {
            base.SetProperty();
        }

        public override bool AI() {
            return base.AI();
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return base.Draw(spriteBatch, screenPos, drawColor);
        }
    }
}
