using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    /// <summary>管道侧连接，检测/输电/绘制</summary>
    internal class PipelineSideState(Point16 point16)
    {
        internal Point16 Position;
        internal readonly Point16 Offset = point16;
        /// <summary>管道传输速率 UE/帧，决定电网吞吐上限</summary>
        internal const float TRANSFER_RATE = 10f;
        internal TileProcessor externalTP;
        internal UEPipelineTP coreTP;
        internal PipelineLinkType LinkType { get; private set; } = PipelineLinkType.None;
        internal bool canDraw;

        /// <summary>更新连接并输电</summary>
        public void UpdateConnectionState() {
            externalTP = null;
            LinkType = PipelineLinkType.None;
            canDraw = false;

            Point16 checkPos = Position + Offset;

            Tile tile = Framing.GetTileSafely(checkPos);
            if (!tile.HasTile) return;

            if (!VaultUtils.SafeGetTopLeft(checkPos, out var topLeft)) return;

            if (!TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out externalTP)) return;
            if (externalTP == null || !externalTP.Active) {
                externalTP = null;
                return;
            }

            //按类型连网
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

        /// <summary>连发电机，抽电入管</summary>
        private void HandleGeneratorConnection(BaseGeneratorTP generator) {
            if (generator.MachineData == null || coreTP.MachineData == null) return;

            float available = generator.MachineData.UEvalue;
            float pipeSpace = coreTP.MaxUEValue - coreTP.MachineData.UEvalue;
            float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, pipeSpace));

            if (transfer > 0) {
                generator.MachineData.UEvalue -= transfer;
                coreTP.MachineData.UEvalue += transfer;
            }

            //有发电机=有电源
            coreTP.IsNetworkPowered = true;
            LinkType = PipelineLinkType.Generator;
        }

        /// <summary>连管，压差均衡</summary>
        private void HandlePipelineConnection(BaseUEPipelineTP otherPipe) {
            if (otherPipe.MachineData == null || coreTP.MachineData == null) return;

            //压差均衡
            float diff = coreTP.MachineData.UEvalue - otherPipe.MachineData.UEvalue;
            //上限 TRANSFER_RATE
            float transfer = Math.Min(TRANSFER_RATE, Math.Abs(diff) * 0.5f);

            if (diff > 0.1f) {
                coreTP.MachineData.UEvalue -= transfer;
                otherPipe.MachineData.UEvalue += transfer;
            }
            else if (diff < -0.1f) {
                coreTP.MachineData.UEvalue += transfer;
                otherPipe.MachineData.UEvalue -= transfer;
            }

            if (otherPipe is UEPipelineTP normalOther) {
                if (coreTP.IsNetworkPowered || normalOther.IsNetworkPowered) {
                    coreTP.IsNetworkPowered = true;
                    normalOther.IsNetworkPowered = true;
                }
            }

            LinkType = PipelineLinkType.Pipeline;
        }

        /// <summary>连电池/用电器，ReceivedEnergy 定单向或双向</summary>
        private void HandleBatteryConnection(BaseBattery battery) {
            if (battery.MachineData == null || coreTP.MachineData == null) return;

            if (battery.ReceivedEnergy) {
                //用电器单向供电
                float available = coreTP.MachineData.UEvalue;
                float deviceSpace = battery.MaxUEValue - battery.MachineData.UEvalue;
                float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, deviceSpace));

                if (transfer > 0) {
                    battery.MachineData.UEvalue += transfer;
                    coreTP.MachineData.UEvalue -= transfer;
                }
            }
            else {
                //储能，比例差定方向
                float pipeRatio = coreTP.MachineData.UEvalue / coreTP.MaxUEValue;
                float batteryRatio = battery.MachineData.UEvalue / battery.MaxUEValue;
                float ratioDiff = pipeRatio - batteryRatio;

                if (ratioDiff > 0.05f) {
                    //管充电池
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
                    //电池回灌
                    float available = battery.MachineData.UEvalue;
                    float pipeSpace = coreTP.MaxUEValue - coreTP.MachineData.UEvalue;
                    float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, pipeSpace));
                    transfer *= Math.Min(Math.Abs(ratioDiff) * 2f, 1f);

                    if (transfer > 0) {
                        coreTP.MachineData.UEvalue += transfer;
                        battery.MachineData.UEvalue -= transfer;
                    }
                }

                //储能有电=有电源
                if (battery.MachineData.UEvalue > 0) {
                    coreTP.IsNetworkPowered = true;
                }
            }

            LinkType = PipelineLinkType.Battery;
        }

        /// <summary>更新绘制状态</summary>
        public void UpdateDrawState() {
            if (!canDraw || externalTP == null) return;

            //拐角/十字/三通不画臂
            if (externalTP is UEPipelineTP otherPipe) {
                if (otherPipe.Shape is PipelineShape.Cross or PipelineShape.Corner or PipelineShape.ThreeWay) {
                    canDraw = false;
                }
            }
        }

        /// <summary>连接臂能量层，色由调用方(合批 or 平涂)</summary>
        public void DrawEnergy(SpriteBatch spriteBatch, Color energyColor) {
            if (coreTP?.MachineData == null || externalTP == null) return;

            Vector2 drawPos = coreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();
            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;
            spriteBatch.Draw(UEPipelineTP.PipelineChannel.Value, drawPos + orig, null, energyColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }

        /// <summary>绘制连接臂金属外壳层</summary>
        public void DrawCasing(SpriteBatch spriteBatch) {
            if (coreTP?.MachineData == null || externalTP == null) return;

            Vector2 drawPos = coreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();
            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;
            Color lightingColor = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, lightingColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }
}
