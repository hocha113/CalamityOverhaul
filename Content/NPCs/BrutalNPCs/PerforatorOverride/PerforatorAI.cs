using CalamityMod.NPCs.Perforator;
using CalamityOverhaul.Content.NPCs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.PerforatorOverride
{
    internal class PerforatorAI : NPCOverride
    {
        public override int TargetID => ModContent.NPCType<PerforatorHive>();
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
