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
    /// 菌生蟹网络同步系统。
    /// 核心状态（驯服、骑乘、鞍具等）统一走InnoVault的NPCOverride通道：
    /// <see cref="NPCOverride.OtherNetWorkSend(ModPacket)"/>负责进世界全量同步与服务器推送，
    /// <see cref="NPCOverride.SendNetworkData"/>负责客户端上报。
    /// 这里只保留指令类数据包：投喂与召回
    /// </summary>
    internal class CrabulonNetworking
    {
        private readonly ModifyCrabulon owner;

        public CrabulonNetworking(ModifyCrabulon owner) {
            this.owner = owner;
        }

        //写入核心状态，所有同步通道共用同一对读写以保证一致性
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

        //从网络包读取核心状态
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

            //远端通知下马时清理本端的骑乘痕迹（物理标志、玩家状态）
            if (owner.Mount && !newMount) {
                owner.MountSystem?.ForceDismount();
            }
            owner.Mount = newMount;

            //应用派生字段，保证friendly/boss等状态在所有端一致
            owner.ApplyStateFields();
        }

        //发送投喂数据包，直接携带效果参数，不再依赖服务器反查弹幕（弹幕可能已消亡或identity撞车）
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

        //接收投喂数据包
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

            npc.netUpdate = true;//生命与状态变化由服务器下发
        }

        //请求召回：客户端发起，服务器权威移动NPC后向所有客户端广播特效
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

        //执行召回：位置修改只在权威端进行，特效本地播放
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

        //向所有客户端广播传送特效（服务器跟随AI传送时也复用）
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

        //传送/召回的本地特效
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

        //接收召回数据包：服务器视作请求并执行，客户端视作特效广播
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

        //处理网络消息
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
