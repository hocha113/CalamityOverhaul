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
        /// <summary>
        /// 管道传输速率(UE/帧)，决定了整个电网的最大吞吐能力
        /// </summary>
        internal const float TRANSFER_RATE = 10f;
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
        /// 处理与发电机的连接:始终从发电机抽取电力到管道
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
        /// 处理与另一个管道的连接:基于压差均衡电力
        /// </summary>
        private void HandlePipelineConnection(BaseUEPipelineTP otherPipe) {
            if (otherPipe.MachineData == null || coreTP.MachineData == null) return;

            //管道之间基于压差均衡电力，传输量与压差成正比
            float diff = coreTP.MachineData.UEvalue - otherPipe.MachineData.UEvalue;
            //压差越大传输越快，但不超过TRANSFER_RATE
            float transfer = Math.Min(TRANSFER_RATE, Math.Abs(diff) * 0.5f);

            if (diff > 0.1f) {
                coreTP.MachineData.UEvalue -= transfer;
                otherPipe.MachineData.UEvalue += transfer;
            }
            else if (diff < -0.1f) {
                coreTP.MachineData.UEvalue += transfer;
                otherPipe.MachineData.UEvalue -= transfer;
            }

            //双向传播供电状态
            if (otherPipe is UEPipelineTP normalOther) {
                if (coreTP.IsNetworkPowered || normalOther.IsNetworkPowered) {
                    coreTP.IsNetworkPowered = true;
                    normalOther.IsNetworkPowered = true;
                }
            }

            LinkType = PipelineLinkType.Pipeline;
        }

        /// <summary>
        /// 处理与电池/用电器的连接:
        /// - ReceivedEnergy=true(用电器/需要充能的设备): 管道始终向其供电
        /// - ReceivedEnergy=false(储能电池): 双向压差传输，管道电力多则充电池，少则从电池取电
        /// </summary>
        private void HandleBatteryConnection(BaseBattery battery) {
            if (battery.MachineData == null || coreTP.MachineData == null) return;

            if (battery.ReceivedEnergy) {
                //用电器类设备:始终从管道向其供电(单向)
                float available = coreTP.MachineData.UEvalue;
                float deviceSpace = battery.MaxUEValue - battery.MachineData.UEvalue;
                float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, deviceSpace));

                if (transfer > 0) {
                    battery.MachineData.UEvalue += transfer;
                    coreTP.MachineData.UEvalue -= transfer;
                }
            }
            else {
                //储能电池:基于压差双向传输
                //计算管道和电池的填充比例，用比例差决定方向和力度
                float pipeRatio = coreTP.MachineData.UEvalue / coreTP.MaxUEValue;
                float batteryRatio = battery.MachineData.UEvalue / battery.MaxUEValue;
                float ratioDiff = pipeRatio - batteryRatio;

                if (ratioDiff > 0.05f) {
                    //管道比例更高，向电池充电
                    float available = coreTP.MachineData.UEvalue;
                    float batterySpace = battery.MaxUEValue - battery.MachineData.UEvalue;
                    float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, batterySpace));
                    transfer *= Math.Min(ratioDiff * 2f, 1f);

                    if (transfer > 0) {
                        battery.MachineData.UEvalue += transfer;
                        coreTP.MachineData.UEvalue -= transfer;
                    }
                }
                else if (ratioDiff < -0.05f) {
                    //电池比例更高，从电池取电到管道
                    float available = battery.MachineData.UEvalue;
                    float pipeSpace = coreTP.MaxUEValue - coreTP.MachineData.UEvalue;
                    float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, pipeSpace));
                    transfer *= Math.Min(Math.Abs(ratioDiff) * 2f, 1f);

                    if (transfer > 0) {
                        coreTP.MachineData.UEvalue += transfer;
                        battery.MachineData.UEvalue -= transfer;
                    }
                }

                //储能电池有电时也标记网络为有电源(作为后备电源)
                if (battery.MachineData.UEvalue > 0) {
                    coreTP.IsNetworkPowered = true;
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
            float energyRatio = coreTP.MachineData.UEvalue / (coreTP.MaxUEValue * 0.5f);
            Color energyColor = coreTP.BaseColor * energyRatio;
            spriteBatch.Draw(UEPipelineTP.PipelineChannel.Value, drawPos + orig, null, energyColor, drawRot, orig, 1, SpriteEffects.None, 0);

            //绘制管道连接臂本体
            Color lightingColor = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, lightingColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }
}
