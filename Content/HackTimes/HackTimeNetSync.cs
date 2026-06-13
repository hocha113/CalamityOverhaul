using CalamityOverhaul.Content.HackTimes.Scannables;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客协议多人同步，远端仅复刻视觉</summary>
    internal static class HackTimeNetSync
    {
        /// <summary>当前是否处于远端复刻，OnApply 据此跳过权威写入</summary>
        public static bool IsRemoteApply { get; private set; }

        /// <summary>本端施加成功后广播给其它客户端</summary>
        /// <param name="hack">已上传完成的协议</param>
        /// <param name="target">协议作用目标</param>
        /// <param name="casterPlayerIndex">施法者<see cref="Player.whoAmI"/></param>
        public static void SendApplyPacket(QuickHackDef hack, IHackTarget target, int casterPlayerIndex) {
            if (Main.netMode == NetmodeID.SinglePlayer) return;
            if (hack == null || target == null) return;

            HackTargetKind kind = target.TargetType?.Kind ?? HackTargetKind.None;
            //仅同步 NPC 与物块，索引/坐标跨端稳定
            if (kind != HackTargetKind.Npc && kind != HackTargetKind.Tile) return;

            int npcIndex = -1;
            int tileX = -1;
            int tileY = -1;
            if (kind == HackTargetKind.Npc) {
                if (target is not NpcScannable n) return;
                npcIndex = n.NpcIndex;
                if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
            }
            else if (kind == HackTargetKind.Tile) {
                if (target is not TileScannable t) return;
                tileX = t.TileCoordX;
                tileY = t.TileCoordY;
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return;
            }

            //协议槽位用 byte，节省包体并防负值
            if (hack.SlotIndex < 0 || hack.SlotIndex > byte.MaxValue) return;

            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.HackProtocolApply);
            packet.Write((byte)casterPlayerIndex);
            packet.Write((byte)hack.SlotIndex);
            packet.Write((byte)kind);
            if (kind == HackTargetKind.Npc) {
                packet.Write((short)npcIndex);
            }
            else {
                packet.Write((short)tileX);
                packet.Write((short)tileY);
            }
            //发送时排除本机，服务端再转发其它客户端
            packet.Send(-1, casterPlayerIndex);
        }

        /// <summary>收到远端协议施加请求，本端复刻视觉</summary>
        public static void HandleApplyPacket(BinaryReader reader, int whoAmI) {
            //单人不应收到此包，仍读完字节流防污染
            byte casterPlayerIndex = reader.ReadByte();
            byte hackSlotIndex = reader.ReadByte();
            HackTargetKind kind = (HackTargetKind)reader.ReadByte();

            short npcIndex = -1;
            short tileX = -1;
            short tileY = -1;
            if (kind == HackTargetKind.Npc) {
                npcIndex = reader.ReadInt16();
            }
            else if (kind == HackTargetKind.Tile) {
                tileX = reader.ReadInt16();
                tileY = reader.ReadInt16();
            }

            //单人模式兜底防护
            if (Main.netMode == NetmodeID.SinglePlayer) return;

            //专用服务器不复刻视觉，仅转发
            bool runVisuals = !VaultUtils.isServer;

            if (runVisuals) {
                ApplyRemote(casterPlayerIndex, hackSlotIndex, kind, npcIndex, tileX, tileY);
            }

            //专用服务器广播给除来源外所有客户端
            if (VaultUtils.isServer) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.HackProtocolApply);
                packet.Write(casterPlayerIndex);
                packet.Write(hackSlotIndex);
                packet.Write((byte)kind);
                if (kind == HackTargetKind.Npc) {
                    packet.Write(npcIndex);
                }
                else if (kind == HackTargetKind.Tile) {
                    packet.Write(tileX);
                    packet.Write(tileY);
                }
                packet.Send(-1, whoAmI);
            }
        }

        private static void ApplyRemote(byte casterPlayerIndex, byte hackSlotIndex
            , HackTargetKind kind, short npcIndex, short tileX, short tileY) {
            //协议槽位校验
            QuickHackDef hack = QuickHackDef.GetByIndex(hackSlotIndex);
            if (hack == null) return;

            IHackTarget target = null;
            if (kind == HackTargetKind.Npc) {
                if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;
                if (!Main.npc[npcIndex].active) return;
                target = new NpcScannable(npcIndex);
            }
            else if (kind == HackTargetKind.Tile) {
                if (tileX < 0 || tileX >= Main.maxTilesX
                    || tileY < 0 || tileY >= Main.maxTilesY) return;
                target = new TileScannable(tileX, tileY);
            }
            if (target == null || !target.IsValid) return;

            //施法者断线时回退 LocalPlayer，远端复刻仅播视觉
            Player caster = casterPlayerIndex < Main.maxPlayers ? Main.player[casterPlayerIndex] : null;
            if (caster == null || !caster.active) caster = Main.LocalPlayer;

            //IsRemoteApply 下 OnApply 仅播视觉，跳过伤害/状态/入队
            bool prev = IsRemoteApply;
            IsRemoteApply = true;
            try {
                hack.OnApply(target, caster);
            } finally {
                IsRemoteApply = prev;
            }
        }
    }
}
