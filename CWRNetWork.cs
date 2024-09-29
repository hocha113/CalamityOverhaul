using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.TileModules;
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
        ProjViscosityData,
        BloodAltarModule,
    }

    public class CWRNetWork : ICWRLoader
    {
        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();

            if (type == CWRMessageType.TungstenRiot) {
                TungstenRiot.EventNetWorkReceive(reader);
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
            else if (type == CWRMessageType.ProjViscosityData) {
                CWRProjectile.NetViscosityReceive(mod, reader, whoAmI);
            }
            else if (type == CWRMessageType.BloodAltarModule) {
                BloodAltarModule.NetReceive(mod, reader, whoAmI);
            }
        }
    }
}
