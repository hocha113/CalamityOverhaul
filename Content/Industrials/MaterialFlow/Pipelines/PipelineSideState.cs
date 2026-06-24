using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    /// <summary>管道侧连接：检测/输电/绘制</summary>
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

        /// <summary>更新连接状态并传输电力</summary>
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

            //TileProcessorLoader O(1) 查询
            if (!TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out externalTP)) return;
            if (externalTP == null || !externalTP.Active) {
                externalTP = null;
                return;
            }

            //按类型连网输电
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

        /// <summary>发电机连接，从发电机抽取电力到管道</summary>
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

        /// <summary>管道连接，基于压差均衡电力</summary>
        private void HandlePipelineConnection(BaseUEPipelineTP otherPipe) {
            if (otherPipe.MachineData == null || coreTP.MachineData == null) return;

            //管道之间基于压差均衡电力，传输量与压差成正比
            float diff = coreTP.MachineData.UEvalue - otherPipe.MachineData.UEvalue;
            //压差越大越快，上限 TRANSFER_RATE
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

        /// <summary>电池/用电器连接，ReceivedEnergy 决定单向供电或双向压差</summary>
        private void HandleBatteryConnection(BaseBattery battery) {
            if (battery.MachineData == null || coreTP.MachineData == null) return;

            if (battery.ReceivedEnergy) {
                //用电器：单向从管道供电
                float available = coreTP.MachineData.UEvalue;
                float deviceSpace = battery.MaxUEValue - battery.MachineData.UEvalue;
                float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, deviceSpace));

                if (transfer > 0) {
                    battery.MachineData.UEvalue += transfer;
                    coreTP.MachineData.UEvalue -= transfer;
                }
            }
            else {
                //储能电池：比例差决定充放电方向
                float pipeRatio = coreTP.MachineData.UEvalue / coreTP.MaxUEValue;
                float batteryRatio = battery.MachineData.UEvalue / battery.MaxUEValue;
                float ratioDiff = pipeRatio - batteryRatio;

                if (ratioDiff > 0.05f) {
                    //管道比例高，向电池充电
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
                    //电池比例高，向管道取电
                    float available = battery.MachineData.UEvalue;
                    float pipeSpace = coreTP.MaxUEValue - coreTP.MachineData.UEvalue;
                    float transfer = Math.Min(TRANSFER_RATE, Math.Min(available, pipeSpace));
                    transfer *= Math.Min(Math.Abs(ratioDiff) * 2f, 1f);

                    if (transfer > 0) {
                        coreTP.MachineData.UEvalue += transfer;
                        battery.MachineData.UEvalue -= transfer;
                    }
                }

                //储能电池有电时标记网络有电源
                if (battery.MachineData.UEvalue > 0) {
                    coreTP.IsNetworkPowered = true;
                }
            }

            LinkType = PipelineLinkType.Battery;
        }

        /// <summary>更新绘制状态</summary>
        public void UpdateDrawState() {
            if (!canDraw || externalTP == null) return;

            //拐角/十字/三通不绘制连接臂
            if (externalTP is UEPipelineTP otherPipe) {
                if (otherPipe.Shape is PipelineShape.Cross or PipelineShape.Corner or PipelineShape.ThreeWay) {
                    canDraw = false;
                }
            }
        }

        /// <summary>绘制连接臂能量层（颜色由调用方决定：电网着色器批次 or 平涂回退）</summary>
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
