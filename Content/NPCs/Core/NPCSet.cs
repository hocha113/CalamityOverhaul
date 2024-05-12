using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Core
{
    internal class NPCSet
    {
        public virtual int targetID => NPCID.None;

        public virtual bool CanLoad() { return true; }

        public virtual void Setup() { }

        public virtual void Load() { }

        public virtual void UnLoad() { }

        public virtual bool? AI(NPC npc, Mod mod) { return null; }

        public virtual bool? Draw(Mod mod, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(Mod mod, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
