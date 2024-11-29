using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.NPCs.Core;
using System.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public enum CWRMessageType : byte
    {
        TungstenRiot,
        OverBeatBack,
        NPCOverrideAI,
        NPCOverrideOtherAI,
    }

    public class CWRNetWork : ICWRLoader
    {
        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();

            if (type == CWRMessageType.TungstenRiot) {
                TungstenRiot.EventNetWorkReceive(reader, whoAmI);
            }
            else if (type == CWRMessageType.OverBeatBack) {
                CWRNpc.OtherBeatBackReceive(reader, whoAmI);
            }
            else if (type == CWRMessageType.NPCOverrideAI) {
                NPCOverride.NetAIReceive(reader);
            }
            else if (type == CWRMessageType.NPCOverrideOtherAI) {
                NPCOverride.OtherNetWorkReceiveHander(reader);
            }
        }
    }
}
