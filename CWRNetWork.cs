using CalamityOverhaul.Content;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.TileModify.Core;
using System.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public enum CWRMessageType : byte
    {
        OverBeatBack,
        NPCbasicData,
        ModifiIntercept_InGame,
        ModifiIntercept_EnterWorld_Request,
        ModifiIntercept_EnterWorld_ToClient,
        KillTileEntity,
    }

    public static class CWRNetWork
    {
        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();

            if (type == CWRMessageType.OverBeatBack) {
                CWRNpc.OtherBeatBackReceive(reader, whoAmI);
            }
            else if (type == CWRMessageType.NPCbasicData) {
                CWRNpc.NPCbasicDataHandler(reader);
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
            else if (type == CWRMessageType.KillTileEntity) {
                TileModifyLoader.HandlerNetKillTE(reader, whoAmI);
            }
        }
    }
}
