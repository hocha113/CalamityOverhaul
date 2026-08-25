using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations
{
    /// <summary>
    /// 传送站TP:世界上的同类站点互为目的地(枚举 TP_InWorld 即得站表,零额外存储)。
    /// 传送为交互客户端权威:本机校验扣电并推送,再走原版 TeleportEntity 广播,
    /// 位置归属本就是拥有端,无越权面
    /// </summary>
    internal class TeleportStationTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<TeleportStationTile>();
        public override int TargetItem => ModContent.ItemType<TeleportStation>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;

        #region 常量

        //费用:10 + 0.02 × 距离(格),封顶 200
        internal const float BaseCost = 10f;
        internal const float CostPerTile = 0.02f;
        internal const float CostCap = 200f;
        //目的站待机门槛,断电站不能当免费落点
        internal const float ArrivalReserveUE = 10f;
        //站名长度上限(字符)
        internal const int MaxNameLength = 20;

        #endregion

        #region 字段与属性

        /// <summary>玩家起的站名,空串表示未命名;UI 编辑为客户端权威推送</summary>
        internal string StationName = "";

        internal int frame;
        internal float GlowIntensity;

        /// <summary>列表显示名:未命名时回退成带坐标的占位名</summary>
        internal string ShowName => string.IsNullOrWhiteSpace(StationName)
            ? TeleportStation.UnnamedText.Format(Position.X, Position.Y)
            : StationName;

        #endregion

        #region 数据同步与存档

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(StationName ?? "");
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            StationName = reader.ReadString();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["_StationName"] = StationName ?? "";
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("_StationName", out string name)) {
                StationName = name;
            }
        }

        #endregion

        #region 站表与费用

        /// <summary>收集世界内全部活跃传送站;枚举 TP_InWorld,拆掉的站自然消失</summary>
        internal static void CollectStations(List<TeleportStationTP> result) {
            result.Clear();
            foreach (var tp in InnoVault.TileProcessors.TileProcessorLoader.TP_InWorld) {
                if (tp is TeleportStationTP station && station.Active) {
                    result.Add(station);
                }
            }
        }

        /// <summary>按距离计费:10 + 0.02 UE/格,封顶 200</summary>
        internal static float TeleportCost(TeleportStationTP from, TeleportStationTP to) {
            float tiles = from.CenterInWorld.Distance(to.CenterInWorld) / 16f;
            return Math.Min(BaseCost + tiles * CostPerTile, CostCap);
        }

        /// <summary>落点:站台顶面中心,玩家脚底贴台面</summary>
        internal Vector2 ArrivalPositionFor(Player player)
            => new(CenterInWorld.X - player.width / 2f, PosInWorld.Y - player.height);

        #endregion

        #region 传送执行(交互客户端)

        /// <summary>
        /// 把本地玩家送往目标站。本机校验起点电量与目的站待机电,
        /// 扣电推送后本地 Teleport,再发原版 TeleportEntity 包让全端回放
        /// </summary>
        internal bool TryTeleportLocalPlayer(TeleportStationTP target) {
            if (target == null || target == this || !target.Active) {
                return false;
            }

            Player player = Main.LocalPlayer;
            float cost = TeleportCost(this, target);

            if (MachineData.UEvalue < cost) {
                CombatText.NewText(HitBox, Color.DimGray, TeleportStation.NoEnergyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }
            if (target.MachineData.UEvalue < ArrivalReserveUE) {
                CombatText.NewText(HitBox, Color.DimGray, TeleportStation.TargetNoEnergyText.Value);
                SoundEngine.PlaySound(SoundID.MenuClose);
                return false;
            }

            //起点站扣电:客户端权威 TP 编辑 + 推送,是框架接受的取舍
            MachineData.UEvalue -= cost;
            SendData();

            Vector2 arrive = target.ArrivalPositionFor(player);
            player.Teleport(arrive, TeleportationStyleID.TeleportationPylon);
            if (VaultUtils.isClient) {
                //number=0 的玩家传送包:服务器回放后转发旁观端,特效各端自播
                NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0,
                    player.whoAmI, arrive.X, arrive.Y, TeleportationStyleID.TeleportationPylon);
            }
            return true;
        }

        #endregion

        #region 更新与交互

        public override void UpdateMachine() {
            bool powered = MachineData.UEvalue >= BaseCost;
            GlowIntensity = powered
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);
            if (powered) {
                //门户待机动画,复用热能电池六帧表
                VaultUtils.ClockFrame(ref frame, 5, 5);
            }
        }

        public void RightClickByTile() {
            var ui = UIHandleLoader.GetUIHandleOfType<TeleportStationUI>();
            ui?.Interactive(this);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();

        #endregion
    }
}
