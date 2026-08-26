using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    internal enum ElysiumNetOp : byte
    {
        /// <summary>客户端→服务器：请求把某城镇NPC转化为门徒</summary>
        RequestConvert = 0,
        /// <summary>服务器→全体：转化裁定通过(各端演出，主人端落座生成门徒)</summary>
        ConvertResolved = 1,
    }

    /// <summary>
    /// 天国极乐净通道：城镇NPC转化走请求-裁定-广播。
    /// NPC移除只由服务器执行(单人直通)；门徒弹幕由主人端生成(弹幕自带同步)
    /// </summary>
    internal class ElysiumNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) {
            ElysiumNetOp op = (ElysiumNetOp)reader.ReadByte();
            switch (op) {
                case ElysiumNetOp.RequestConvert:
                    ReceiveRequest(reader, whoAmI);
                    break;
                case ElysiumNetOp.ConvertResolved:
                    ReceiveResolved(reader);
                    break;
            }
        }

        /// <summary>主人端入口：单人直接裁定，联机发请求</summary>
        internal static void RequestConvert(Player player, int npcIndex, int seat) {
            if (Main.netMode == NetmodeID.SinglePlayer) {
                ResolveConvert(player.whoAmI, npcIndex, seat);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient || player.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<ElysiumNet>();
            packet.Write((byte)ElysiumNetOp.RequestConvert);
            packet.Write((byte)player.whoAmI);
            packet.Write((byte)seat);
            packet.Write((short)npcIndex);
            packet.Send();
        }

        private static void ReceiveRequest(BinaryReader reader, int whoAmI) {
            //先读净负载再守卫
            int playerIndex = reader.ReadByte();
            int seat = reader.ReadByte();
            int npcIndex = reader.ReadInt16();

            if (Main.netMode != NetmodeID.Server || playerIndex != whoAmI) {
                return;
            }
            ResolveConvert(playerIndex, npcIndex, seat);
        }

        /// <summary>裁定与执行(服务器或单人)：校验→移除NPC→广播/本地演出</summary>
        private static void ResolveConvert(int playerIndex, int npcIndex, int seat) {
            if (playerIndex < 0 || playerIndex >= Main.maxPlayers
                || seat < 0 || seat >= ElysiumPlayer.SeatCount
                || npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return;
            }
            Player player = Main.player[playerIndex];
            NPC npc = Main.npc[npcIndex];
            if (player?.active != true || !npc.active || !npc.townNPC || npc.life <= 0) {
                CWRMod.Instance.Logger.Info(
                    $"[ElysiumNet] convert request rejected: player {playerIndex}, npc {npcIndex}, seat {seat}");
                return;
            }

            short npcType = (short)npc.type;
            Vector2 pos = npc.Center;

            //居民升入圣位：静默移除(非死亡)，由服务器/单人权威执行
            npc.active = false;
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                //广播裁定，各端演出、主人端落座
                ModPacket packet = CWRNetWork.GetPacket<ElysiumNet>();
                packet.Write((byte)ElysiumNetOp.ConvertResolved);
                packet.Write((byte)playerIndex);
                packet.Write((byte)seat);
                packet.Write(npcType);
                packet.Write(pos.X);
                packet.Write(pos.Y);
                packet.Send();
                return;
            }

            //单人：直接演出+落座
            ApplyResolved(playerIndex, seat, pos);
        }

        private static void ReceiveResolved(BinaryReader reader) {
            int playerIndex = reader.ReadByte();
            int seat = reader.ReadByte();
            _ = reader.ReadInt16();//npcType 预留给后续演出差分
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();

            if (Main.netMode != NetmodeID.MultiplayerClient
                || playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                return;
            }
            ApplyResolved(playerIndex, seat, new Vector2(x, y));
        }

        /// <summary>各端：升华演出；主人端：登记席位并生成门徒</summary>
        private static void ApplyResolved(int playerIndex, int seat, Vector2 pos) {
            PlayConversionFX(pos);

            if (playerIndex != Main.myPlayer) {
                return;
            }
            Player player = Main.player[playerIndex];
            if (!player.TryGetModPlayer(out ElysiumPlayer ep)) {
                return;
            }
            ep.SeatConverted[seat] = true;
            int projType = ElysiumPlayer.SeatToProjType(seat);
            if (projType > 0) {
                Projectile.NewProjectile(player.GetSource_Misc("ElysiumConvert"),
                    pos, Vector2.Zero, projType, 0, 0f, playerIndex);
            }
        }

        /// <summary>升华演出：天光落柱 + 光尘升腾(纯本地)</summary>
        internal static void PlayConversionFX(Vector2 pos) {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.1f, Pitch = 0.2f }, pos);
            SoundEngine.PlaySound(SoundID.Item123 with { Volume = 0.7f, Pitch = 0.35f }, pos);
            if (Main.dedServ) {
                return;
            }

            //天光自穹顶落下
            PRTLoader.NewParticle<PRT_SkyBolt>(pos, Vector2.Zero, new Color(255, 240, 200), 1f)
                ?.Configure(pos - new Vector2(0f, 620f), pos, 30);

            //升腾光尘
            for (int i = 0; i < 12; i++) {
                Vector2 dustPos = pos + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-20f, 12f));
                Vector2 vel = new(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(2f, 5.5f));
                PRTLoader.NewParticle<PRT_Light>(dustPos, vel, new Color(255, 236, 185), Main.rand.NextFloat(0.26f, 0.46f))
                    ?.Configure(Main.rand.Next(26, 44), 0.95f);
            }
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, angle.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f)
                    , new Color(255, 226, 150), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(14, 20));
            }
        }
    }
}
