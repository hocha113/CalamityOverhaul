using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.TileEntitys;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public enum CWRMessageType : byte
    {
        DompBool,
        RecoilAcceleration,
        TungstenRiot,
        TEBloodAltar,
        OverBeatBack,
        BrutalSkeletronPrimeAI,
    }

    public class CWRNetCode
    {
        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();
            if (type == CWRMessageType.DompBool) {
                Main.player[reader.ReadInt32()].CWR().HandleDomp(reader);
            }
            else if (type == CWRMessageType.RecoilAcceleration) {
                Main.player[reader.ReadInt32()].CWR().HandleRecoilAcceleration(reader);
            }
            else if (type == CWRMessageType.TungstenRiot) {
                TungstenRiot.Instance.TungstenRiotIsOngoing = reader.ReadBoolean();
                TungstenRiot.Instance.EventKillPoints = reader.ReadInt32();
            }
            else if (type == CWRMessageType.TEBloodAltar) {
                TEBloodAltar.ReadTEData(mod, reader);
            }
            else if (type == CWRMessageType.OverBeatBack) {
                byte npcIdx = reader.ReadByte();
                NPC npc = Main.npc[npcIdx];
                if (npc.type == NPCID.None || !npc.active) {
                    return;
                }
                CWRNpc modnpc = npc.CWR();
                modnpc.OverBeatBackBool = reader.ReadBoolean();
                modnpc.OverBeatBackVr = reader.ReadVector2();
                modnpc.OverBeatBackAttenuationForce = reader.ReadSingle();
            }
            else if (type == CWRMessageType.BrutalSkeletronPrimeAI) {
                BrutalSkeletronPrimeAI.ai4 = reader.ReadByte();
                BrutalSkeletronPrimeAI.ai5 = reader.ReadByte();
                BrutalSkeletronPrimeAI.ai6 = reader.ReadByte();
                BrutalSkeletronPrimeAI.ai7 = reader.ReadByte();
                BrutalSkeletronPrimeAI.ai8 = reader.ReadByte();
                BrutalSkeletronPrimeAI.ai9 = reader.ReadByte();
                BrutalSkeletronPrimeAI.ai10 = reader.ReadByte();
            }
        }
    }
}
