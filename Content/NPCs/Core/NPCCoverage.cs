using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Core
{
    /// <summary>
    /// 提供一个强行覆盖目标NPC行为性质的基类，通过OnFrom钩子为基础运行
    /// </summary>
    internal class NPCCoverage
    {
        public virtual int targetID => NPCID.None;

        public virtual bool CanLoad() { return true; }

        public virtual bool? AI(NPC npc, Mod mod) { return null; }

        public virtual bool? Draw(Mod mod, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(Mod mod, NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
