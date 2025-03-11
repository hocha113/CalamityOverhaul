using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events.TungstenRiotEvent;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
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
        ModifiIntercept_InGame,
        ModifiIntercept_EnterWorld_Request,
        ModifiIntercept_EnterWorld_ToClient,
        DestroyerData,
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
            else if (type == CWRMessageType.ModifiIntercept_InGame) {
                HandlerCanOverride.NetModifiIntercept_InGame(reader, whoAmI);
            }
            else if (type == CWRMessageType.ModifiIntercept_EnterWorld_Request) {
                HandlerCanOverride.NetModifiInterceptEnterWorld_Server(reader, whoAmI);
            }
            else if (type == CWRMessageType.ModifiIntercept_EnterWorld_ToClient) {
                HandlerCanOverride.NetModifiInterceptEnterWorld_Client(reader, whoAmI);
            }
            else if (type == CWRMessageType.DestroyerData) {
                DestroyerHeadAI.HandlerBodyNetWork(reader);
            }
        }
    }
}
