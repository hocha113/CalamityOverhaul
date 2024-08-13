using CalamityMod.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Core
{
    /// <summary>
    /// 提供一个强行覆盖目标NPC行为性质的基类，通过On钩子为基础运行
    /// </summary>
    internal class NPCOverride
    {
        public virtual int TargetID => NPCID.None;

        public static Mod mod => CWRMod.Instance;

        public NPC npc { get; private set; }

        public CWRNpc cwrNPC { get; private set; }

        public CalamityGlobalNPC calNPC { get; private set; }

        public NPCOverride Clone() => (NPCOverride)Activator.CreateInstance(GetType());

        public virtual bool CanLoad() { return true; }

        public static void SetDefaults(NPCOverride inds, NPC npc, CWRNpc cwr, CalamityGlobalNPC cal) {
            inds = new NPCOverride();
            foreach (var pcc in NPCSystem.NPCSets) {
                if (pcc.TargetID == npc.type) {
                    inds = pcc.Clone();
                }
            }
            inds.npc = npc;
            inds.cwrNPC = cwr;
            inds.calNPC = cal;
            inds.SetProperty();
        }

        public virtual void SetProperty() { }

        public virtual bool AI() { return true; }

        public virtual bool? On_PreKill(NPC npc) { return null; }

        public virtual bool? CheckDead() { return null; }

        public virtual bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
