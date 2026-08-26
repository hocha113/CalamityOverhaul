using CalamityOverhaul.Content.Industrials.ElectricPowers.Sundials;
using CalamityOverhaul.Content.Industrials.ElectricPowers.WeatherControllers;
using InnoVault.TileProcessors;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    /// <summary>
    /// 工业玩家服务的网络承载:电动日晷与天气控制机。
    /// 时间与天气是服务器权威世界状态,统一走
    /// "客户端请求 → 服务端校验扣电落地 → 广播全员演出":
    /// 世界数据靠原版 WorldData 包同步,机器电量靠 TP 全量包锚定,
    /// 广播回执只承载演出(聊天线各端按本地语言渲染)。
    /// 读侧一律先吃净负载再判断丢弃,防止流错位
    /// </summary>
    internal static class IndustrialServiceNet
    {
        private const byte OpRequest = 0;
        private const byte OpApply = 1;

        /// <summary>电动日晷时间快进信道</summary>
        internal sealed class IndustrialTimeSkipNet : CWRNetChannel
        {
            public override void Receive(BinaryReader reader, int whoAmI) => HandleTimeSkip(reader, whoAmI);
        }

        /// <summary>天气控制机求雨/止雨信道</summary>
        internal sealed class IndustrialWeatherSetNet : CWRNetChannel
        {
            public override void Receive(BinaryReader reader, int whoAmI) => HandleWeatherSet(reader, whoAmI);
        }

        #region 电动日晷:时间快进到黎明

        /// <summary>交互客户端发起时间快进;单人直接落地</summary>
        internal static void RequestTimeSkip(ElectricSundialTP tp) {
            if (VaultUtils.isSinglePlayer) {
                ExecuteTimeSkip(tp, Main.myPlayer);
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<IndustrialTimeSkipNet>();
            packet.Write(OpRequest);
            packet.Write(tp.Position.X);
            packet.Write(tp.Position.Y);
            packet.Send();
        }

        private static void HandleTimeSkip(BinaryReader reader, int whoAmI) {
            byte op = reader.ReadByte();
            if (op == OpRequest) {
                //先吃净负载
                Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
                if (!VaultUtils.isServer) {
                    return;
                }
                //服务端权威校验:站点存在、未在快进、电量足额;不满足静默丢弃
                if (!TileProcessorLoader.ByPositionGetTP(pos, out ElectricSundialTP tp)) {
                    return;
                }
                if (Main.IsFastForwardingTime()) {
                    return;
                }
                if (tp.MachineData.UEvalue < ElectricSundialTP.SkipCost) {
                    return;
                }
                ExecuteTimeSkip(tp, whoAmI);
            }
            else if (op == OpApply) {
                Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
                byte requester = reader.ReadByte();
                PlayTimeSkipCeremony(pos, requester);
            }
        }

        /// <summary>权威端落地:扣电、写时间旗标、WorldData 同步、锚定电量、广播演出</summary>
        private static void ExecuteTimeSkip(ElectricSundialTP tp, int requester) {
            tp.MachineData.UEvalue -= ElectricSundialTP.SkipCost;
            //自带电源的快进:不碰原版 sundialCooldown,绕开七天冷却
            Main.fastForwardTimeToDawn = true;

            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
                tp.SendData();

                ModPacket packet = CWRNetWork.GetPacket<IndustrialTimeSkipNet>();
                packet.Write(OpApply);
                packet.Write(tp.Position.X);
                packet.Write(tp.Position.Y);
                packet.Write((byte)requester);
                packet.Send();
                return;
            }
            //单人:直接本地演出
            PlayTimeSkipCeremony(tp.Position, requester);
        }

        /// <summary>各端演出:全员聊天线(本地语言渲染)+ 机器旁的金光与音效</summary>
        private static void PlayTimeSkipCeremony(Point16 pos, int requester) {
            string name = requester >= 0 && requester < Main.maxPlayers && Main.player[requester].active
                ? Main.player[requester].name : "???";
            VaultUtils.Text(ElectricSundial.SkipBroadcast.Format(name), new Color(255, 216, 120));

            if (TileProcessorLoader.ByPositionGetTP(pos, out ElectricSundialTP tp)) {
                tp.PlayCeremony();
            }
        }

        #endregion

        #region 天气控制机:求雨/止雨

        /// <summary>交互客户端发起天气切换;单人直接落地</summary>
        internal static void RequestWeatherSet(WeatherControllerTP tp, bool wantRain) {
            if (VaultUtils.isSinglePlayer) {
                ExecuteWeatherSet(tp, wantRain, Main.myPlayer);
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<IndustrialWeatherSetNet>();
            packet.Write(OpRequest);
            packet.Write(tp.Position.X);
            packet.Write(tp.Position.Y);
            packet.Write(wantRain);
            packet.Send();
        }

        private static void HandleWeatherSet(BinaryReader reader, int whoAmI) {
            byte op = reader.ReadByte();
            if (op == OpRequest) {
                //先吃净负载
                Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
                bool wantRain = reader.ReadBoolean();
                if (!VaultUtils.isServer) {
                    return;
                }
                //服务端权威校验:机器存在、电量足额、目标态与当前态不同;不满足静默丢弃
                if (!TileProcessorLoader.ByPositionGetTP(pos, out WeatherControllerTP tp)) {
                    return;
                }
                if (tp.MachineData.UEvalue < WeatherControllerTP.ToggleCost) {
                    return;
                }
                if (Main.raining == wantRain) {
                    return;
                }
                ExecuteWeatherSet(tp, wantRain, whoAmI);
            }
            else if (op == OpApply) {
                Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
                bool wantRain = reader.ReadBoolean();
                byte requester = reader.ReadByte();
                PlayWeatherCeremony(pos, wantRain, requester);
            }
        }

        /// <summary>权威端落地:扣电、起雨/止雨、WorldData 同步、锚定电量、广播演出</summary>
        private static void ExecuteWeatherSet(WeatherControllerTP tp, bool wantRain, int requester) {
            tp.MachineData.UEvalue -= WeatherControllerTP.ToggleCost;
            if (wantRain) {
                Main.StartRain();
            }
            else {
                Main.StopRain();
            }

            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
                tp.SendData();

                ModPacket packet = CWRNetWork.GetPacket<IndustrialWeatherSetNet>();
                packet.Write(OpApply);
                packet.Write(tp.Position.X);
                packet.Write(tp.Position.Y);
                packet.Write(wantRain);
                packet.Write((byte)requester);
                packet.Send();
                return;
            }
            //单人:直接本地演出
            PlayWeatherCeremony(tp.Position, wantRain, requester);
        }

        /// <summary>各端演出:全员聊天线(本地语言渲染)+ 机器旁的雨云尘与音效</summary>
        private static void PlayWeatherCeremony(Point16 pos, bool wantRain, int requester) {
            string name = requester >= 0 && requester < Main.maxPlayers && Main.player[requester].active
                ? Main.player[requester].name : "???";
            string line = wantRain
                ? WeatherController.RainBroadcast.Format(name)
                : WeatherController.ClearBroadcast.Format(name);
            VaultUtils.Text(line, new Color(140, 180, 250));

            if (TileProcessorLoader.ByPositionGetTP(pos, out WeatherControllerTP tp)) {
                tp.PlayCeremony(wantRain);
            }
        }

        #endregion
    }
}
