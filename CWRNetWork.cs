using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Industrials.Modifys;
using CalamityOverhaul.Content.NPCs.Modifys;
using CalamityOverhaul.Content.RemakeItems;
using System.IO;
using Terraria;
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
        CrabulonFeed,
        CrabulonJustJumped,
        CrabulonModifyNetWork,
        HoldDownCardinalTimer,
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
                ModifyTurretLoader.HandlerNetKillTE(reader, whoAmI);
            }
            else if (type == CWRMessageType.CrabulonFeed) {
                ModifyCrabulon.ReceiveFeedPacket(reader, whoAmI);
            }
            else if (type == CWRMessageType.CrabulonJustJumped) {
                ModifyCrabulon.ReceiveJustJumped(reader, whoAmI);
            }
            else if (type == CWRMessageType.CrabulonModifyNetWork) {
                ModifyCrabulon.ReceiveNetWork(reader, whoAmI);
            }
            else if (type == CWRMessageType.HoldDownCardinalTimer) {
                ReceiveHoldDownCardinalTimer(reader, whoAmI);
            }
        }
        /// <summary>
        /// 发送玩家按住方向键的计时器数据
        /// </summary>
        /// <param name="player"></param>
        public static void SendHoldDownCardinalTimer(Player player) {
            if (!VaultUtils.isClient) {//为了防止迭代发送，这里只在客户端发送
                return;
            }
            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.HoldDownCardinalTimer);
            netMessage.Write(player.whoAmI);
            for (int i = 0; i < player.holdDownCardinalTimer.Length; i++) {
                netMessage.Write(player.holdDownCardinalTimer[i]);
            }
            netMessage.Send();
        }
        /// <summary>
        /// 接收玩家按住方向键的计时器数据
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="whoAmI"></param>
        public static void ReceiveHoldDownCardinalTimer(BinaryReader reader, int whoAmI) {
            if (!reader.ReadInt32().TryGetPlayer(out Player player)) {
                return;
            }
            for (int i = 0; i < player.holdDownCardinalTimer.Length; i++) {
                player.holdDownCardinalTimer[i] = reader.ReadInt32();
            }
        }
    }
}
