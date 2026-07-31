using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial
{
    /// <summary>CWRMessageType.OnikiriTutorial 子操作码</summary>
    internal enum OnikiriTutorialOp : byte
    {
        /// <summary>客→服：请求生成/确认练习鬼影；服端权威校验</summary>
        EnsureTarget,
        /// <summary>服→客：练习鬼影 NPC 槽位回执（-1 = 失败）</summary>
        TargetConfirm,
        /// <summary>客→服：教程完成或中止，请求清理练习鬼影</summary>
        ReleaseTarget,
        /// <summary>服→客：确认练习鬼影已清理</summary>
        TargetReleased,
        /// <summary>服→客：鬼影姿态变更同步（阶段编号）</summary>
        PoseSync,
    }

    /// <summary>
    /// 鬼切教程联机通道。挂 CWRNetWork 链尾。
    /// 服务端权威生成/清理练习鬼影，客户端只请求；单人走同一入口（静默无网络开销）。
    /// </summary>
    internal static class OnikiriTutorialNet
    {
        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI)
        {
            if (type != CWRMessageType.OnikiriTutorial) return;
            OnikiriTutorialOp op = (OnikiriTutorialOp)reader.ReadByte();
            switch (op)
            {
                case OnikiriTutorialOp.EnsureTarget:
                    HandleEnsureTarget(reader, whoAmI);
                    break;
                case OnikiriTutorialOp.ReleaseTarget:
                    HandleReleaseTarget(reader, whoAmI);
                    break;
                case OnikiriTutorialOp.TargetConfirm:
                    HandleTargetConfirm(reader);
                    break;
                case OnikiriTutorialOp.TargetReleased:
                    HandleTargetReleased();
                    break;
                case OnikiriTutorialOp.PoseSync:
                    HandlePoseSync(reader);
                    break;
            }
        }

        // ====发送（供 Flow 调用）====

        /// <summary>请求服务端生成练习鬼影（单人等价于直接本地生成）</summary>
        internal static void RequestEnsureTarget()
        {
            if (VaultUtils.isSinglePlayer)
            {
                //单人直接本地生成，无包
                OnikiriTutorialWraith.EnsureLocalTarget();
                return;
            }
            if (!VaultUtils.isClient) return;
            ModPacket pkt = NewPacket(OnikiriTutorialOp.EnsureTarget);
            pkt.Send();
        }

        /// <summary>请求服务端清理练习鬼影</summary>
        internal static void RequestReleaseTarget()
        {
            if (VaultUtils.isSinglePlayer)
            {
                OnikiriTutorialWraith.ReleaseLocalTarget();
                return;
            }
            if (!VaultUtils.isClient) return;
            NewPacket(OnikiriTutorialOp.ReleaseTarget).Send();
        }

        internal static void SendPoseSync(int playerIndex, byte pose)
        {
            if (!VaultUtils.isServer) return;
            ModPacket pkt = NewPacket(OnikiriTutorialOp.PoseSync);
            pkt.Write(pose);
            pkt.Send(playerIndex);
        }

        // ====接收====

        private static void HandleEnsureTarget(BinaryReader _, int whoAmI)
        {
            if (!VaultUtils.isServer) return;
            int npcIndex = OnikiriTutorialWraith.EnsureServerTarget(whoAmI);
            ModPacket reply = NewPacket(OnikiriTutorialOp.TargetConfirm);
            reply.Write(npcIndex);
            reply.Send(whoAmI);
        }

        private static void HandleReleaseTarget(BinaryReader _, int whoAmI)
        {
            if (!VaultUtils.isServer) return;
            OnikiriTutorialWraith.ReleaseServerTarget(whoAmI);
            NewPacket(OnikiriTutorialOp.TargetReleased).Send(whoAmI);
        }

        private static void HandleTargetConfirm(BinaryReader reader)
        {
            if (!VaultUtils.isClient) return;
            int npcIndex = reader.ReadInt32();
            OnikiriTutorialWraith.OnServerTargetConfirmed(npcIndex);
        }

        private static void HandleTargetReleased()
        {
            if (!VaultUtils.isClient) return;
            OnikiriTutorialWraith.OnServerTargetReleased();
        }

        private static void HandlePoseSync(BinaryReader reader)
        {
            if (!VaultUtils.isClient) return;
            byte pose = reader.ReadByte();
            OnikiriTutorialWraith.OnPoseSynced(pose);
        }

        private static ModPacket NewPacket(OnikiriTutorialOp op)
        {
            ModPacket pkt = CWRMod.Instance.GetPacket();
            pkt.Write((byte)CWRMessageType.OnikiriTutorial);
            pkt.Write((byte)op);
            return pkt;
        }
    }
}