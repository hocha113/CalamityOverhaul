using CalamityMod.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
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

        internal const int MaxAISlot = 12;

        public float[] ai = new float[MaxAISlot];

        public float[] localAi = new float[MaxAISlot];

        public NPC npc { get; private set; }

        public CWRNpc cwrNPC { get; private set; }

        public CalamityGlobalNPC calNPC { get; private set; }

        public NPCOverride Clone() => (NPCOverride)Activator.CreateInstance(GetType());

        public virtual bool CanLoad() { return true; }

        public static void SetDefaults(NPC npc, CWRNpc cwr, CalamityGlobalNPC cal) {
            NPCOverride inds = npc.CWR().NPCOverride;
            foreach (var pcc in NPCSystem.NPCSets) {
                if (pcc.TargetID == npc.type) {
                    inds = pcc.Clone();
                }
            }
            inds.ai = new float[MaxAISlot];
            inds.localAi = new float[MaxAISlot];
            inds.npc = npc;
            inds.cwrNPC = cwr;
            inds.calNPC = cal;
            inds.SetProperty();
            npc.CWR().NPCOverride = inds;
        }

        internal void NetAISend() {
            if (CWRUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
                netMessage.Write(npc.whoAmI);

                NPCOverride pCOverride = npc.CWR().NPCOverride;
                float[] ai = pCOverride.ai;

                netMessage.Write(ai[0]);
                netMessage.Write(ai[1]);
                netMessage.Write(ai[2]);
                netMessage.Write(ai[3]);
                netMessage.Write(ai[4]);
                netMessage.Write(ai[5]);
                netMessage.Write(ai[6]);
                netMessage.Write(ai[7]);
                netMessage.Write(ai[8]);
                netMessage.Write(ai[9]);
                netMessage.Write(ai[10]);
                netMessage.Write(ai[11]);
                netMessage.Send();
            }
        }

        internal static void NetAISend(NPC npc) {
            if (CWRUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
                netMessage.Write(npc.whoAmI);

                NPCOverride pCOverride = npc.CWR().NPCOverride;
                float[] ai = pCOverride.ai;

                netMessage.Write(ai[0]);
                netMessage.Write(ai[1]);
                netMessage.Write(ai[2]);
                netMessage.Write(ai[3]);
                netMessage.Write(ai[4]);
                netMessage.Write(ai[5]);
                netMessage.Write(ai[6]);
                netMessage.Write(ai[7]);
                netMessage.Write(ai[8]);
                netMessage.Write(ai[9]);
                netMessage.Write(ai[10]);
                netMessage.Write(ai[11]);
                netMessage.Send();
            }
        }

        internal static void NetAIReceive(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            float ai0 = reader.ReadSingle();
            float ai1 = reader.ReadSingle();
            float ai2 = reader.ReadSingle();
            float ai3 = reader.ReadSingle();
            float ai4 = reader.ReadSingle();
            float ai5 = reader.ReadSingle();
            float ai6 = reader.ReadSingle();
            float ai7 = reader.ReadSingle();
            float ai8 = reader.ReadSingle();
            float ai9 = reader.ReadSingle();
            float ai10 = reader.ReadSingle();
            float ai11 = reader.ReadSingle();
            if (npc.active) {
                NPCOverride pCOverride = npc.CWR().NPCOverride;
                pCOverride.ai[0] = ai0;
                pCOverride.ai[1] = ai1;
                pCOverride.ai[2] = ai2;
                pCOverride.ai[3] = ai3;
                pCOverride.ai[4] = ai4;
                pCOverride.ai[5] = ai5;
                pCOverride.ai[6] = ai6;
                pCOverride.ai[7] = ai7;
                pCOverride.ai[8] = ai8;
                pCOverride.ai[9] = ai9;
                pCOverride.ai[10] = ai10;
                pCOverride.ai[11] = ai11;
            }
        }

        public virtual void SetProperty() { }

        public virtual bool AI() { return true; }

        public virtual bool? On_PreKill() { return null; }

        public virtual bool? CheckDead() { return null; }

        public virtual bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
