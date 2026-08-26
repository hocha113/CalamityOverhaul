using System;
using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace CalamityOverhaul
{
    /// <summary>
    /// 网络分发器：加载期收集本模组程序集内的全部 <see cref="CWRNetChannel"/>，
    /// 按类型全名排序分配编号，收包读一字节编号直达信道，不再维护中心枚举
    /// </summary>
    public static class CWRNetWork
    {
        private static CWRNetChannel[] channels = [];
        private static readonly Dictionary<Type, CWRNetChannel> channelByType = [];

        internal static void Load(CWRMod mod) {
            //只扫描本模组程序集：其他端点可能装有单侧模组，跨模组扫描会让两端编号表分叉
            List<CWRNetChannel> instances = VaultUtils.GetDerivedInstances<CWRNetChannel>(AssemblyManager.GetLoadableTypes(mod.Code));
            //tML 强制两端模组名称+版本+哈希一致，同一二进制下按全名排序即得到跨端一致的编号
            instances.Sort((a, b) => string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName));
            if (instances.Count > byte.MaxValue + 1) {
                throw new InvalidOperationException($"CWRNetWork: {instances.Count} channels exceed byte capacity, widen ID to ushort");
            }

            channels = [.. instances];
            channelByType.Clear();
            for (int i = 0; i < channels.Length; i++) {
                channels[i].ID = (byte)i;
                channelByType[channels[i].GetType()] = channels[i];
            }

            //留一份编号对照表，便于日后核对多端日志
            string[] entries = new string[channels.Length];
            for (int i = 0; i < channels.Length; i++) {
                entries[i] = $"[{i}]{channels[i].GetType().Name}";
            }
            mod.Logger.Info($"CWRNetWork: {channels.Length} channels registered: {string.Join(", ", entries)}");
        }

        internal static void Unload() {
            channels = [];
            channelByType.Clear();
        }

        /// <summary>发送端唯一入口：取包并预写信道编号，随后写载荷、Send</summary>
        public static ModPacket GetPacket<T>() where T : CWRNetChannel {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write(channelByType[typeof(T)].ID);
            return packet;
        }

        public static void HandlePacket(Mod mod, BinaryReader reader, int whoAmI) {
            byte id = reader.ReadByte();
            if (id >= channels.Length) {
                mod.Logger.Warn($"CWRNetWork: unknown channel id {id} from {whoAmI}");
                return;
            }
            channels[id].Receive(reader, whoAmI);
        }
    }
}
