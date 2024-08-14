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

        private bool _netWorkSend;

        public bool netWorkSend {
            get {
                if (!CWRUtils.isServer) {
                    return false;
                }
                return _netWorkSend;
            }
            set => _netWorkSend = value;
        }

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

        #region NetWork

        internal void OtherNetWorkSendHander() {
            if (!CWRUtils.isServer || !netWorkSend) {
                return;
            }
            var netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.NPCOverrideOtherAI);
            netMessage.Write(npc.whoAmI);
            OtherNetWorkSend(netMessage);
            netWorkSend = false;
        }

        internal virtual void OtherNetWorkSend(ModPacket netMessage) { }

        internal static void OtherNetWorkReceiveHander(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            npc.CWR().NPCOverride.OtherNetWorkReceive(reader);
        }

        internal virtual void OtherNetWorkReceive(BinaryReader reader) {

        }

        internal void NetAISend() {
            if (CWRUtils.isServer) {
                var netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.NPCOverrideAI);
                netMessage.Write(npc.whoAmI);

                NPCOverride pCOverride = npc.CWR().NPCOverride;
                float[] ai = pCOverride.ai;

                foreach (var aiValue in ai) {
                    netMessage.Write(aiValue);
                }

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

                foreach (var aiValue in ai) {
                    netMessage.Write(aiValue);
                }

                netMessage.Send();
            }
        }

        internal static void NetAIReceive(BinaryReader reader) {
            NPC npc = Main.npc[reader.ReadInt32()];
            float[] receiveAI = new float[MaxAISlot];
            for (int i = 0; i < MaxAISlot; i++) {
                receiveAI[i] = reader.ReadSingle();
            }
            if (npc.active) {
                NPCOverride pCOverride = npc.CWR().NPCOverride;
                for (int i = 0; i < MaxAISlot; i++) {
                    pCOverride.ai[i] = receiveAI[i];
                }
            }
        }

        #endregion

        public virtual void SetProperty() { }

        public virtual bool AI() { return true; }

        public virtual bool? On_PreKill() { return null; }

        public virtual bool? CheckDead() { return null; }

        public virtual bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return null; }

        public virtual bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { return true; }
    }
}
