using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹网络同步系统
    /// </summary>
    internal class CrabulonNetworking
    {
        private readonly ModifyCrabulon owner;

        public CrabulonNetworking(ModifyCrabulon owner) {
            this.owner = owner;
        }

        //发送网络数据包
        public void SendNetworkPacket() {
            if (!VaultUtils.isClient) {
                return;
            }

            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.CrabulonModifyNetWork);
            netMessage.Write(owner.npc.whoAmI);
            WriteData(netMessage);
            netMessage.Send();
        }

        //发送投喂数据包
        public void SendFeedPacket(int projIdentity) {
            if (!VaultUtils.isClient) {
                return;
            }

            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.CrabulonFeed);
            netMessage.Write(owner.npc.whoAmI);
            netMessage.Write(projIdentity);
            netMessage.Send();
        }

        //写入数据到网络包
        public void WriteData(ModPacket netMessage) {
            netMessage.Write(owner.Owner.Alives() ? owner.Owner.whoAmI : 0);
            netMessage.Write(owner.FeedValue);
            netMessage.Write(owner.Crouch);
            netMessage.Write(owner.Mount);
            netMessage.Write(owner.MountACrabulon);
            netMessage.Write(owner.DontMount);
            netMessage.Write(owner.DyeItemID);
            netMessage.Write(owner.Physics.JumpHeightUpdate);
            netMessage.Write(owner.Physics.JumpHeightSetFrame);
            netMessage.Write(owner.Physics.GroundClearance);
            owner.SaddleItem ??= new Item();
            ItemIO.Send(owner.SaddleItem, netMessage);
        }

        //从网络包读取数据
        public void ReadData(BinaryReader reader) {
            owner.Owner = Main.player[reader.ReadInt32()];
            owner.FeedValue = reader.ReadSingle();
            owner.Crouch = reader.ReadBoolean();
            owner.Mount = reader.ReadBoolean();
            owner.MountACrabulon = reader.ReadBoolean();
            owner.DontMount = reader.ReadInt32();
            owner.DyeItemID = reader.ReadInt32();
            owner.Physics.JumpHeightUpdate = reader.ReadSingle();
            owner.Physics.JumpHeightSetFrame = reader.ReadSingle();
            float groundClearance = reader.ReadSingle();
            owner.SaddleItem = ItemIO.Receive(reader);

            if (!owner.SaddleItem.Alives()) {
                owner.SaddleItem = new Item();
            }
        }

        //接收网络数据
        public static void ReceiveNetworkData(BinaryReader reader, int whoAmI) {
            int npcIndex = reader.ReadInt32();
            if (!npcIndex.TryGetNPC(out NPC npc)) {
                return;
            }

            if (!npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                return;
            }

            modifyCrabulon.Networking.ReadData(reader);

            if (VaultUtils.isServer) {
                ModPacket netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.CrabulonModifyNetWork);
                netMessage.Write(npcIndex);
                modifyCrabulon.Networking.WriteData(netMessage);
                netMessage.Send(-1, whoAmI);
            }
        }

        //接收投喂数据包
        public static void ReceiveFeedPacket(BinaryReader reader, int whoAmI) {
            int npcIndex = reader.ReadInt32();
            int projIdentity = reader.ReadInt32();

            if (!npcIndex.TryGetNPC(out NPC npc)) {
                return;
            }

            Projectile match = null;
            foreach (var proj in Main.projectile) {
                if (proj.identity == projIdentity) {
                    match = proj;
                    break;
                }
            }

            if (match == null) {
                return;
            }

            if (!npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                return;
            }

            modifyCrabulon.Feed(match);

            if (!VaultUtils.isServer) {
                return;
            }

            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.CrabulonFeed);
            netMessage.Write(npcIndex);
            netMessage.Write(projIdentity);
            netMessage.Send(-1, whoAmI);

            modifyCrabulon.NetAISend();
            modifyCrabulon.NetOtherWorkSend = true;
        }

        //处理网络消息
        public static void HandleNetworkMessage(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.CrabulonFeed) {
                ReceiveFeedPacket(reader, whoAmI);
            }
            else if (type == CWRMessageType.CrabulonModifyNetWork) {
                ReceiveNetworkData(reader, whoAmI);
            }
        }
    }
}
