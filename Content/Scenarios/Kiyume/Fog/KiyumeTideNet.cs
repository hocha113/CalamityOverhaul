using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Fog
{
    /// <summary>
    /// 潮汐对钟信道：下行仅一个 long 钟值。潮位是钟 + 几何的纯函数，
    /// 对上钟两端名义浓度即严格一致；Sim/Suppression/犬影是纯客户端表现，不过线。<br/>
    /// 对钟采用硬设：稳态钟差只剩单程延迟（几 tick，雾线偏移亚像素级）；
    /// 大钟差只出现在入梦首次对钟（本地 Reset 从涨满起步，最差可跳数十行），
    /// 由密度场驱散/回聚半衰期低通成一次快速涨落。实测刺眼再上退化方案（60t 匀速追平）。单人零包
    /// </summary>
    internal class KiyumeTideNet : CWRNetChannel
    {
        /// <summary>周期广播间隔（tick）：丢包/断线重连的自愈上限</summary>
        internal const int BroadcastIntervalTicks = 600;

        /// <summary>服务器下发钟值；toClient=-1 广播，&gt;=0 新玩家单发</summary>
        internal static void SendClock(int toClient = -1) {
            if (Main.netMode != NetmodeID.Server) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<KiyumeTideNet>();
            packet.Write(KiyumeFogTide.ClockTicks);
            packet.Send(toClient);
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读满再校验（流对齐纪律）；服务器不受客户端对钟
            long clock = reader.ReadInt64();
            if (Main.netMode != NetmodeID.MultiplayerClient || clock < 0) {
                return;
            }
            KiyumeFogTide.ClockTicks = clock;
        }
    }

    /// <summary>
    /// 潮汐钟服务器广播驱动：600t 周期 + 新玩家进入单发，OnWorldLoad 会话复位。
    /// 钟本体的推进在 KiyumeFogSystem.PostUpdateEverything 的 dedServ 分支
    /// </summary>
    internal class KiyumeTideAuthority : ModSystem
    {
        private int broadcastTimer;
        //已对过钟的槽位（服务器侧会话状态；世界级而非 per-player 游戏状态，static 禁令不适用）
        private readonly bool[] clockSent = new bool[Main.maxPlayers];

        public override void OnWorldLoad() => ResetSession();
        public override void OnWorldUnload() => ResetSession();

        private void ResetSession() {
            broadcastTimer = 0;
            Array.Clear(clockSent);
        }

        public override void PostUpdateEverything() {
            if (Main.netMode != NetmodeID.Server || !KiyumeWorld.Active) {
                return;
            }
            //新玩家进入单发：active 上升沿即对钟，600t 周期广播兜底丢包
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (!Main.player[i].active) {
                    clockSent[i] = false;
                    continue;
                }
                if (!clockSent[i]) {
                    clockSent[i] = true;
                    KiyumeTideNet.SendClock(i);
                }
            }
            if (++broadcastTimer >= KiyumeTideNet.BroadcastIntervalTicks) {
                broadcastTimer = 0;
                KiyumeTideNet.SendClock();
            }
        }
    }
}
