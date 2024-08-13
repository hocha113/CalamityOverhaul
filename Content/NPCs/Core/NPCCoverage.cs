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
        public virtual int TargetID => NPCID.None;

        private NPC _npc;

        private Mod mod => CWRMod.Instance;

        public NPC npc {
            get {
                if (_npc == null) {
                    foreach (var entity in Main.npc) {
                        if (!entity.active) {
                            continue;
                        }
                        if (entity.type == TargetID) {
                            _npc = entity;
                        }
                    }
                }
                if (_npc == null) {
                    return null;
                }
                if (_npc.type != TargetID && _npc.type != NPCID.None) {
                    _npc = null;
                }
                return _npc;
            }
            internal set => _npc = value;
        }

        public virtual bool CanLoad() { return true; }

        public virtual void SetProperty() { }

        public virtual bool AI() { return true; }

        public virtual bool? On_PreKill(NPC npc) { return null; }

        public virtual bool? CheckDead() { return null; }

        public virtual bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
