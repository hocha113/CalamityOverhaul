using InnoVault.GameSystem;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.NPCs.Modifys.Crabulons
{
    /// <summary>
    /// 菌生蟹网络：核心状态走 NPCOverride 通道，此处仅投喂与召回
    /// </summary>
    internal class CrabulonNetworking
    {
        private readonly ModifyCrabulon owner;

        public CrabulonNetworking(ModifyCrabulon owner) {
            this.owner = owner;
        }

        //核心状态读写，各同步通道共用
        public void WriteData(BinaryWriter netMessage) {
            netMessage.Write(owner.Owner.Alives() ? owner.Owner.whoAmI : -1);
            netMessage.Write(owner.FeedValue);
            netMessage.Write(owner.Crouch);
            netMessage.Write(owner.Mount);
            netMessage.Write(owner.MountACrabulon);
            netMessage.Write(owner.DontMount);
            netMessage.Write(owner.DyeItemID);
            owner.SaddleItem ??= new Item();
            ItemIO.Send(owner.SaddleItem, netMessage);
        }

        public void ReadData(BinaryReader reader) {
            int ownerIndex = reader.ReadInt32();
            owner.Owner = ownerIndex >= 0 && ownerIndex < Main.maxPlayers ? Main.player[ownerIndex] : null;
            owner.FeedValue = reader.ReadSingle();
            owner.Crouch = reader.ReadBoolean();
            bool newMount = reader.ReadBoolean();
            owner.MountACrabulon = reader.ReadBoolean();
            owner.DontMount = reader.ReadInt32();
            owner.DyeItemID = reader.ReadInt32();
            owner.SaddleItem = ItemIO.Receive(reader);
            if (!owner.SaddleItem.Alives()) {
                owner.SaddleItem = new Item();
            }

            //远端下马时清本端骑乘痕迹
            if (owner.Mount && !newMount) {
                owner.MountSystem?.ForceDismount();
            }
            owner.Mount = newMount;

            //派生字段，保证各端 friendly/boss 一致
            owner.ApplyStateFields();
        }

        //投喂包直带参数，不反查弹幕
        public void SendFeedPacket(int feederWhoAmI, int dyeItemID) {
            if (!VaultUtils.isClient) {
                return;
            }

            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.CrabulonFeed);
            netMessage.Write((short)owner.npc.whoAmI);
            netMessage.Write((byte)feederWhoAmI);
            netMessage.Write(dyeItemID);
            netMessage.Send();
        }

        public static void ReceiveFeedPacket(BinaryReader reader, int whoAmI) {
            int npcIndex = reader.ReadInt16();
            int feederIndex = reader.ReadByte();
            int dyeItemID = reader.ReadInt32();

            if (!npcIndex.TryGetNPC(out NPC npc)) {
                return;
            }

            if (!npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                return;
            }

            if (feederIndex < 0 || feederIndex >= Main.maxPlayers) {
                return;
            }

            modifyCrabulon.ApplyFeed(Main.player[feederIndex], dyeItemID);

            if (!VaultUtils.isServer) {
                return;
            }

            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.CrabulonFeed);
            netMessage.Write((short)npcIndex);
            netMessage.Write((byte)feederIndex);
            netMessage.Write(dyeItemID);
            netMessage.Send(-1, whoAmI);

            npc.netUpdate = true;//生命与状态由服务器下发
        }

        //召回：客户端请求，服务器移 NPC 后广播特效
        public void SendRecallRequest() {
            if (!VaultUtils.isClient) {
                DoRecall();
                return;
            }

            ModPacket netMessage = CWRMod.Instance.GetPacket();
            netMessage.Write((byte)CWRMessageType.CrabulonRecall);
            netMessage.Write((short)owner.npc.whoAmI);
            netMessage.Send();
        }

        //位置仅权威端改
        internal void DoRecall() {
            if (!owner.Owner.Alives()) {
                return;
            }

            if (!VaultUtils.isClient) {
                owner.npc.Center = owner.Owner.Center + new Vector2(0, CrabulonConstants.TeleportSpawnHeight);
                owner.npc.netUpdate = true;
            }

            if (!Main.dedServ) {
                PlayTeleportEffect();
            }
        }

        //广播传送特效，AI 传送复用
        internal void BroadcastTeleportEffect() {
            if (VaultUtils.isServer) {
                ModPacket netMessage = CWRMod.Instance.GetPacket();
                netMessage.Write((byte)CWRMessageType.CrabulonRecall);
                netMessage.Write((short)owner.npc.whoAmI);
                netMessage.Send();
            }
            else if (VaultUtils.isSinglePlayer) {
                PlayTeleportEffect();
            }
        }

        internal void PlayTeleportEffect() {
            NPC npc = owner.npc;
            SoundEngine.PlaySound(SoundID.Item8, npc.Center);
            for (int i = 0; i < CrabulonConstants.TeleportEffectCount; i++) {
                Vector2 dustPos = npc.Bottom + new Vector2(Main.rand.NextFloat(-npc.width, npc.width), 0);
                int dust = Dust.NewDust(dustPos, 4, 4, DustID.BlueFairy, 0f, -2f, 100, default, 1.5f);
                Main.dust[dust].velocity *= 0.5f;
                Main.dust[dust].velocity.Y *= 300f / Main.rand.NextFloat(160, 230);
                Main.dust[dust].shader = GameShaders.Armor.GetShaderFromItemId(owner.DyeItemID);
            }
        }

        //服务器执行召回，客户端播特效
        public static void ReceiveRecall(BinaryReader reader, int whoAmI) {
            int npcIndex = reader.ReadInt16();

            if (!npcIndex.TryGetNPC(out NPC npc)) {
                return;
            }

            if (!npc.TryGetOverride<ModifyCrabulon>(out var modifyCrabulon)) {
                return;
            }

            if (VaultUtils.isServer) {
                modifyCrabulon.Networking.DoRecall();
                modifyCrabulon.Networking.BroadcastTeleportEffect();
            }
            else {
                modifyCrabulon.Networking.PlayTeleportEffect();
            }
        }

        public static void HandleNetworkMessage(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (type == CWRMessageType.CrabulonFeed) {
                ReceiveFeedPacket(reader, whoAmI);
            }
            else if (type == CWRMessageType.CrabulonRecall) {
                ReceiveRecall(reader, whoAmI);
            }
        }
    }
}
