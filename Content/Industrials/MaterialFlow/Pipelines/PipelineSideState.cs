using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    /// <summary>
    /// 管道侧面连接状态，处理连接检测、电力传输和绘制逻辑
    /// </summary>
    internal class PipelineSideState(Point16 point16)
    {
        internal Point16 Position;
        internal readonly Point16 Offset = point16;
        internal const float TRANSFER_RATE = 2f;
        internal TileProcessor externalTP;
        internal UEPipelineTP coreTP;
        internal PipelineLinkType LinkType { get; private set; } = PipelineLinkType.None;
        internal bool canDraw;

        /// <summary>
        /// 更新连接状态并处理电力传输
        /// </summary>
        public void UpdateConnectionState() {
            //重置状态
            externalTP = null;
            LinkType = PipelineLinkType.None;
            canDraw = false;

            Point16 checkPos = Position + Offset;

            //获取相邻物块
            Tile tile = Framing.GetTileSafely(checkPos);
            if (!tile.HasTile) return;

            if (!VaultUtils.SafeGetTopLeft(checkPos, out var topLeft)) return;

            //使用TileProcessorLoader提供的O(1)字典查询
            if (!TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out externalTP)) return;
            if (externalTP == null || !externalTP.Active) {
                externalTP = null;
                return;
            }

            //根据类型处理连接和电力传输
            switch (externalTP) {
                case BaseGeneratorTP generator:
                    HandleGeneratorConnection(generator);
                    break;
                case BaseUEPipelineTP otherPipe:
                    HandlePipelineConnection(otherPipe);
                    break;
                case BaseBattery battery:
                    HandleBatteryConnection(battery);
                    break;
                default:
                    return;
            }

            canDraw = LinkType != PipelineLinkType.None;
        }

        /// <summary>
        /// 处理与发电机的连接
        /// </summary>
        private void HandleGeneratorConnection(BaseGeneratorTP generator) {
            if (generator.MachineData == null || coreTP.MachineData == null) return;

            //从发电机抽取电力
            float available = generator.MachineData.UEvalue;
            float pipeSpace = coreTP.MaxUEValue - coreTP.MachineData.UEvalue;
            float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, pipeSpace));

            if (transfer > 0) {
                generator.MachineData.UEvalue -= transfer;
                coreTP.MachineData.UEvalue += transfer;
            }

            //连接发电机意味着网络有电源
            coreTP.IsNetworkPowered = true;
            LinkType = PipelineLinkType.Generator;
        }

        /// <summary>
        /// 处理与另一个管道的连接
        /// </summary>
        private void HandlePipelineConnection(BaseUEPipelineTP otherPipe) {
            if (otherPipe.MachineData == null || coreTP.MachineData == null) return;

            //管道之间均衡电力
            float totalUE = coreTP.MachineData.UEvalue + otherPipe.MachineData.UEvalue;
            float averageUE = totalUE / 2f;
            float diff = coreTP.MachineData.UEvalue - averageUE;
            float transfer = Math.Min(TRANSFER_RATE, Math.Abs(diff));

            if (diff > 0) {
                coreTP.MachineData.UEvalue -= transfer;
                otherPipe.MachineData.UEvalue += transfer;
            }
            else if (diff < 0) {
                coreTP.MachineData.UEvalue += transfer;
                otherPipe.MachineData.UEvalue -= transfer;
            }

            //传播供电状态
            if (otherPipe is UEPipelineTP normalOther) {
                if (coreTP.IsNetworkPowered || normalOther.IsNetworkPowered) {
                    coreTP.IsNetworkPowered = true;
                    normalOther.IsNetworkPowered = true;
                }
            }

            LinkType = PipelineLinkType.Pipeline;
        }

        /// <summary>
        /// 处理与电池的连接
        /// </summary>
        private void HandleBatteryConnection(BaseBattery battery) {
            if (battery.MachineData == null || coreTP.MachineData == null) return;

            if (coreTP.IsNetworkPowered || battery.ReceivedEnergy) {
                //充电:管道向电池传输
                float available = coreTP.MachineData.UEvalue;
                float batterySpace = battery.MaxUEValue - battery.MachineData.UEvalue;
                float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, batterySpace));

                if (transfer > 0) {
                    battery.MachineData.UEvalue += transfer;
                    coreTP.MachineData.UEvalue -= transfer;
                }
            }
            else {
                //放电:电池向管道传输
                float available = battery.MachineData.UEvalue;
                float pipeSpace = coreTP.MaxUEValue - coreTP.MachineData.UEvalue;
                float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, pipeSpace));

                if (transfer > 0) {
                    coreTP.MachineData.UEvalue += transfer;
                    battery.MachineData.UEvalue -= transfer;
                }
            }

            LinkType = PipelineLinkType.Battery;
        }

        /// <summary>
        /// 更新绘制状态
        /// </summary>
        public void UpdateDrawState() {
            if (!canDraw || externalTP == null) return;

            //如果另一个管道是拐角或十字，则不绘制连接臂避免穿帮
            if (externalTP is UEPipelineTP otherPipe) {
                if (otherPipe.Shape is PipelineShape.Cross or PipelineShape.Corner or PipelineShape.ThreeWay) {
                    canDraw = false;
                }
            }
        }

        /// <summary>
        /// 绘制连接臂
        /// </summary>
        public void Draw(SpriteBatch spriteBatch) {
            if (coreTP?.MachineData == null || externalTP == null) return;

            Vector2 drawPos = coreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();
            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;

            //绘制能量流动效果
            float energyRatio = coreTP.MachineData.UEvalue / 10f;
            Color energyColor = coreTP.BaseColor * energyRatio;
            spriteBatch.Draw(UEPipelineTP.PipelineChannel.Value, drawPos + orig, null, energyColor, drawRot, orig, 1, SpriteEffects.None, 0);

            //绘制管道连接臂本体
            Color lightingColor = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, lightingColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }
}
