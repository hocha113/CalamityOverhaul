using System.IO;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TileModules.Core
{
    internal class TMEInWorldNetWork : INetWork
    {
        public static INetWork NetInstance;
        void INetWork.LoadNet() => NetInstance = this;

        void INetWork.NetSendBehavior(ModPacket netMessage, params object[] args) {
            if (CWRUtils.isSinglePlayer) {
                return;
            }
            //$"即将开始世界同步 TileModuleInWorld最大值为{TileModuleLoader.TileModuleInWorld.Count}".Domp();
            netMessage.Write((byte)CWRMessageType.NetWorks);
            netMessage.Write(NetInstance.messageID);
            netMessage.Write(TileModuleLoader.TileModuleInWorld.Count);
            for (int i = 0; i < TileModuleLoader.TileModuleInWorld.Count; i++) {
                BaseTileModule value = TileModuleLoader.TileModuleInWorld[i];
                netMessage.Write(value.ModuleID);
                value.NetCloneSend(ref netMessage);
            }
            netMessage.Send();
        }

        void INetWork.NetReceive(Mod mod, BinaryReader reader, int whoAmI) {
            TileModuleLoader.TileModuleInWorld = [];
            int count = reader.ReadInt32();

            for (int i = 0; i < count; i++) {
                BaseTileModule value = TileModuleLoader.ModuleIDToModuleInstance[reader.ReadInt32()].Clone();
                value.NetCloneRead(reader);
                TileModuleLoader.TileModuleInWorld.Add(value);
            }

            //$"世界同步完成 TileModuleInWorld最大值为{TileModuleLoader.TileModuleInWorld.Count}".Domp();
        }
    }
}
