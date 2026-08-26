using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend
{
    /// <summary>
    /// SHPC 模块配装同步：本机客户端上报当前装配，服务器校验后镜像并广播。
    /// 专用服务器上处决伤害、模块钩子都按服务器侧 <see cref="SHPCPlayer.Modules"/>
    /// 解析，不同步则一律按空模块计算。
    /// </summary>
    internal class SHPCModuleNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => HandleNet(reader, whoAmI);

        /// <summary>客户端上报本机配装</summary>
        internal static void SendLoadout(SHPCPlayer state) {
            if (Main.netMode != NetmodeID.MultiplayerClient || state == null
                || state.Player.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = NewPacket();
            packet.Write((byte)state.Player.whoAmI);
            WriteLoadout(packet, state);
            packet.Send();
        }

        /// <summary>服务器广播指定玩家配装镜像</summary>
        internal static void BroadcastLoadout(SHPCPlayer state, int toWho,
            int ignoreClient) {
            if (Main.netMode != NetmodeID.Server
                || state?.Player?.active != true) {
                return;
            }
            ModPacket packet = NewPacket();
            packet.Write((byte)state.Player.whoAmI);
            WriteLoadout(packet, state);
            packet.Send(toWho, ignoreClient);
        }

        internal static void HandleNet(BinaryReader reader, int whoAmI) {
            try {
                //先读净载荷再做守卫
                int playerIndex = reader.ReadByte();
                Item[] items = ReadLoadout(reader);
                if (Main.netMode == NetmodeID.Server) {
                    //服务器以发送者为准，忽略载荷里的索引
                    playerIndex = whoAmI;
                }
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                    return;
                }
                Player player = Main.player[playerIndex];
                if (player?.active != true) {
                    return;
                }
                //本机玩家配装以本地为权威，丢弃服务器回声
                if (Main.netMode == NetmodeID.MultiplayerClient
                    && playerIndex == Main.myPlayer) {
                    return;
                }
                SHPCPlayer state = player.GetModPlayer<SHPCPlayer>();
                state.ApplyReplicatedLoadout(items);
                if (Main.netMode == NetmodeID.Server) {
                    BroadcastLoadout(state, -1, playerIndex);
                }
            } catch (EndOfStreamException) {
            } catch (IOException) {
            }
        }

        private static void WriteLoadout(ModPacket packet, SHPCPlayer state) {
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                Item item = state.GetModule(i) ?? new Item();
                ItemIO.Send(item, packet);
            }
        }

        private static Item[] ReadLoadout(BinaryReader reader) {
            Item[] items = new Item[SHPCData.SlotCount];
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                items[i] = SanitizeModule(ItemIO.Receive(reader), i);
            }
            return items;
        }

        //槽位类别不匹配或不是改件的一律置空，不信任客户端载荷
        private static Item SanitizeModule(Item item, int slotIndex) {
            if (item == null || item.IsAir) {
                return new Item();
            }
            if (item.ModItem is SHPCModuleItem module
                && (int)module.SlotCategory == slotIndex) {
                item.stack = 1;
                return item;
            }
            return new Item();
        }

        private static ModPacket NewPacket() => CWRNetWork.GetPacket<SHPCModuleNet>();
    }
}
