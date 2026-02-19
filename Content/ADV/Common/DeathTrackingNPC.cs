using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Common
{
    internal class DeathTrackingNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        /// <summary>
        /// 当NPC被击杀时调用，在客户端或者服务端上均会运行
        /// </summary>
        /// <param name="npc"></param>
        public override void OnKill(NPC npc) { }

        /// <summary>
        /// 遍历NPC上所有DeathTrackingNPC子类并调用OnKill
        /// </summary>
        internal static void DispatchOnKill(NPC npc) {
            foreach (var n in npc.EntityGlobals) {
                if (n is not DeathTrackingNPC tracker) {
                    continue;
                }
                tracker.OnKill(npc);
            }
        }

        /// <summary>
        /// 从服务端向所有客户端发送击杀同步包
        /// </summary>
        internal static void SendKillSync(int npcWhoAmI, int npcType) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.DeathTrackingNPCKill);
            packet.Write(npcWhoAmI);
            packet.Write(npcType);
            packet.Send();
        }

        /// <summary>
        /// 客户端接收击杀同步包并触发OnKill调用
        /// </summary>
        internal static void HandleKillSync(BinaryReader reader, int whoAmI) {
            int npcWhoAmI = reader.ReadInt32();
            int npcType = reader.ReadInt32();
            if (npcWhoAmI.TryGetNPC(out NPC npc) && npc.type == npcType) {
                DispatchOnKill(npc);
            }
        }
    }
}
