using CalamityOverhaul.Content.HackTimes.Scannables;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇客协议的多人同步封装
    /// <br/>多人模式下骇客时间不再冻结世界，因此协议上传完成时需要把"作用到目标"的事件
    /// 同步给其它客户端，否则只有施法端能看到粒子/状态变化等视觉效果。
    /// <br/>同步策略：
    /// <list type="bullet">
    /// <item>本机（施法端）正常走 <see cref="IHackTarget.ApplyHack"/>，效果由 <see cref="HackEffectTracker"/> 推进。</item>
    /// <item>其他客户端只复刻一次 <see cref="QuickHackDef.OnApply"/>，期间设置 <see cref="IsRemoteApply"/> 标志，
    /// 协议自身在该标志为 true 时跳过权威伤害与状态变更，仅保留粒子、声音等视觉表现。</item>
    /// </list>
    /// 仅同步具备稳定跨端标识的 NPC / 物块两类目标；其它种类（灵异、炮台、信号塔等）由各自的 Actor 同步链路自行负责。
    /// </summary>
    internal static class HackTimeNetSync
    {
        /// <summary>
        /// 当前是否正处于"远端复刻"中
        /// <br/>协议子类在 <see cref="QuickHackDef.OnApply"/> 中应据此跳过本机权威性的伤害、状态写入操作，
        /// 只保留粒子、音效等纯视觉/听觉效果，避免在多人模式下出现重复伤害或重复状态翻转。
        /// </summary>
        public static bool IsRemoteApply { get; private set; }

        /// <summary>
        /// 在本端施加协议成功后调用：把这次施加广播给其它客户端做视觉复刻。
        /// </summary>
        /// <param name="hack">已上传完成的协议</param>
        /// <param name="target">协议作用的目标</param>
        /// <param name="casterPlayerIndex">施法者的 <see cref="Player.whoAmI"/></param>
        public static void SendApplyPacket(QuickHackDef hack, IHackTarget target, int casterPlayerIndex) {
            if (Main.netMode == NetmodeID.SinglePlayer) return;
            if (hack == null || target == null) return;

            HackTargetKind kind = target.TargetType?.Kind ?? HackTargetKind.None;
            //仅同步 NPC 与物块——这两类目标的索引/坐标在所有客户端上都是稳定的
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

            //协议数量本就在 byte 范围内（按目前注册顺序约 17 条），用 byte 节省 3 字节并避免负值
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
            //发送时排除自己（自己已经本地施加过）；服务端收到后会再转发给其它客户端
            packet.Send(-1, casterPlayerIndex);
        }

        /// <summary>
        /// 收到远端发来的协议施加请求，本端复刻视觉表现。
        /// 服务端同时把数据再转发给除来源外的所有客户端。
        /// </summary>
        public static void HandleApplyPacket(BinaryReader reader, int whoAmI) {
            //单人模式不应该收到这种包，但仍要把字节流读完，避免污染后续解包
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

            //单人模式理论上不会走到这里，做兜底防护
            if (Main.netMode == NetmodeID.SinglePlayer) return;

            //专用服务器无法渲染，OnApply 几乎都是粒子/声音/CombatText 等本地视觉
            //逻辑，故服务端不复刻、仅做转发；其它客户端各自复刻视觉。
            bool runVisuals = !VaultUtils.isServer;

            if (runVisuals) {
                ApplyRemote(casterPlayerIndex, hackSlotIndex, kind, npcIndex, tileX, tileY);
            }

            //专用服务器：把同样的数据广播给除了来源以外的所有客户端
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
            //协议槽位校验，防止越界访问
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

            //找不到对应的施法者（断线、未加入等）时回退到本端 LocalPlayer，仅用于提供
            //OnApply 的 caster 参数；远端复刻只播放视觉，不依赖具体施法者属性
            Player caster = casterPlayerIndex < Main.maxPlayers ? Main.player[casterPlayerIndex] : null;
            if (caster == null || !caster.active) caster = Main.LocalPlayer;

            //标记本次 OnApply 是远端复刻，协议在这一标志下应仅播放视觉/听觉效果，
            //不再做"打伤害、改实体状态、入队 HackEffectTracker"等会被双重应用的操作。
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
