using CalamityOverhaul.Content;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.NPCs.Core;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    public enum CWRMessageType : byte
    {
        DompBool,
        RecoilAcceleration,
        TungstenRiot,
        OverBeatBack,
        NPCOverrideAI,
        NPCOverrideOtherAI,
        ProjViscosityData,
        NetWorks,
    }

    public interface INetWork
    {
        /// <summary>
        /// 默认为-1值，当返回值小于0时，系统将自动分配一个网络ID用于数据匹配
        /// ，如果大于0，将会指向性的寻找对应ID的网络接收点
        /// </summary>
        public short MessageType => -1;
        public short messageID {
            get {
                if (MessageType > 0) {
                    return MessageType;
                }
                return CWRNetCode.NetWorkIDDic[GetType()];
            }
        }
        internal static ModPacket netMessage => CWRMod.Instance.GetPacket();
        /// <summary>
        /// 发送数据
        /// </summary>
        public void NetSend() {
            if (CWRUtils.isSinglePlayer) {
                return;
            }
            netMessage.Write((byte)CWRMessageType.NetWorks);
            netMessage.Write(messageID);
            NetSendBehavior(netMessage);
        }
        /// <summary>
        /// 发送数据的具体行为
        /// </summary>
        public void NetSendBehavior(ModPacket netMessage);
        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="reader"></param>
        /// <param name="whoAmI"></param>
        public void NetReceive(Mod mod, BinaryReader reader, int whoAmI);
    }

    public class CWRNetCode : ILoader
    {
        internal static List<INetWork> INetWorks { get; private set; } = [];
        internal static Dictionary<Type, short> NetWorkIDDic { get; private set; } = [];
        void ILoader.LoadData() {
            INetWorks = CWRUtils.GetSubInterface<INetWork>();
            for (int index = 0; index < INetWorks.Count; index++) {
                INetWork netWork = INetWorks[index];
                Type instanceType = netWork.GetType();
                if (!NetWorkIDDic.TryAdd(instanceType, (short)index)) {
                    string errorText = $"在添加网络对象{instanceType.Name}时出现异常，字典含有重复的值";
                    CWRMod.Instance.Logger.Info(errorText);
                }
            }
        }
        void ILoader.UnLoadData() {
            INetWorks = null;
        }

        private static void NetWorksReceiveHander(Mod mod, BinaryReader reader, int whoAmI) {
            short netWorkID = reader.ReadInt16();
            foreach (var netWork in INetWorks) {
                int targetNetID = netWork.MessageType;
                if (targetNetID < 0) {
                    targetNetID = NetWorkIDDic[netWork.GetType()];
                }
                if (targetNetID == netWorkID) {
                    netWork.NetReceive(mod, reader, whoAmI);
                }
            }
        }

        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            CWRMessageType type = (CWRMessageType)reader.ReadByte();
            if (type == CWRMessageType.DompBool) {
                Main.player[reader.ReadInt32()].CWR().HandleDomp(reader);
            }
            else if (type == CWRMessageType.RecoilAcceleration) {
                Main.player[reader.ReadInt32()].CWR().HandleRecoilAcceleration(reader);
            }
            else if (type == CWRMessageType.TungstenRiot) {
                TungstenRiot.EventNetWorkReceive(reader);
            }
            else if (type == CWRMessageType.OverBeatBack) {
                CWRNpc.OverBeatBackReceive(reader);
            }
            else if (type == CWRMessageType.NPCOverrideAI) {
                NPCOverride.NetAIReceive(reader);
            }
            else if (type == CWRMessageType.NPCOverrideOtherAI) {
                NPCOverride.OtherNetWorkReceiveHander(reader);
            }
            else if (type == CWRMessageType.ProjViscosityData) {
                CWRProjectile.NetViscosityReceive(reader);
            }
            else if (type == CWRMessageType.NetWorks) {
                NetWorksReceiveHander(mod, reader, whoAmI);
            }
        }
    }
}
