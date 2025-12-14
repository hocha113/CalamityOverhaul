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
    /// 用于处理管道侧面连接逻辑的类
    /// </summary>
    internal class PipelineSideState(Point16 point16)
    {
        internal Point16 Position;
        internal readonly Point16 Offset = point16;
        internal const int efficiency = 2;
        internal Tile externalTile;
        internal TileProcessor externalTP;
        internal UEPipelineTP coreTP;
        internal PipelineLinkType LinkType { get; private set; } = PipelineLinkType.None;
        internal bool canDraw;
        public void UpdateConnectionState() {
            //初始化状态
            externalTile = default;
            externalTP = null;
            LinkType = PipelineLinkType.None;
            canDraw = true;

            //获取相邻的物块和TileProcessor
            externalTile = Framing.GetTileSafely(Position + Offset);
            if (!externalTile.HasTile || !VaultUtils.SafeGetTopLeft(Position + Offset, out var point)
                || !TileProcessorLoader.ByPositionGetTP(point, out externalTP)) {
                canDraw = false;
                return;
            }

            switch (externalTP) {
                case BaseGeneratorTP:
                    //当管道连接到发电机时，将自己的"供电状态"激活
                    coreTP.IsNetworkPowered = true;
                    break;
                case UEPipelineTP otherPipe:
                    //传播"供电状态"。只要两者中有一个是供电的，就将两者都设置为供电状态
                    if (coreTP.IsNetworkPowered || otherPipe.IsNetworkPowered) {
                        coreTP.IsNetworkPowered = true;
                        otherPipe.IsNetworkPowered = true;
                    }
                    break;
            }
        }
        /// <summary>
        /// 核心更新逻辑，用于检测和处理与相邻物块的交互
        /// </summary>
        public void Update() {
            if (!canDraw) {
                return;
            }

            switch (externalTP) {
                case BaseGeneratorTP gen:
                    HandleGeneratorConnection(gen);
                    break;
                case UEPipelineTP otherPipe:
                    HandlePipelineConnection(otherPipe);
                    break;
                case BaseBattery battery:
                    HandleBatteryConnection(battery);
                    break;
            }

            if (LinkType == PipelineLinkType.None) {
                canDraw = false;
            }
        }

        //处理与发电机的连接
        private void HandleGeneratorConnection(BaseGeneratorTP generator) {
            float transferAmount = Math.Min(efficiency, generator.MachineData.UEvalue);
            if (transferAmount > 0 && coreTP.MachineData.UEvalue < 20) {
                generator.MachineData.UEvalue -= transferAmount;
                coreTP.MachineData.UEvalue += transferAmount;
            }
            //当管道连接到发电机时，将自己的"供电状态"激活
            coreTP.IsNetworkPowered = true;

            LinkType = PipelineLinkType.Generator;
        }

        //处理与另一个管道的连接
        private void HandlePipelineConnection(UEPipelineTP otherPipe) {
            float totalUE = coreTP.MachineData.UEvalue + otherPipe.MachineData.UEvalue;
            float averageUE = totalUE / 2;
            float transferUE = Math.Min(efficiency, Math.Abs(coreTP.MachineData.UEvalue - averageUE));

            if (coreTP.MachineData.UEvalue > averageUE) {
                coreTP.MachineData.UEvalue -= transferUE;
                otherPipe.MachineData.UEvalue += transferUE;
            }
            else {
                coreTP.MachineData.UEvalue += transferUE;
                otherPipe.MachineData.UEvalue -= transferUE;
            }

            //传播"供电状态"。只要两者中有一个是供电的，就将两者都设置为供电状态
            if (coreTP.IsNetworkPowered || otherPipe.IsNetworkPowered) {
                coreTP.IsNetworkPowered = true;
                otherPipe.IsNetworkPowered = true;
            }

            //如果另一个管道是拐角或十字，则不绘制连接臂，避免穿帮
            if (otherPipe.Shape is PipelineShape.Cross or PipelineShape.Corner or PipelineShape.ThreeWay) {
                canDraw = false;
            }
            LinkType = PipelineLinkType.Pipeline;
        }

        //处理与电池的连接
        private void HandleBatteryConnection(BaseBattery battery) {
            //电池交互逻辑
            //如果管道网络由发电机供能，则无视电池的设置，强制为其充电
            //如果管道网络是独立的(没有发电机)，则尊重电池的设置
            if (coreTP.IsNetworkPowered || battery.ReceivedEnergy) {
                float transferAmount = Math.Min(efficiency, Math.Min(coreTP.MachineData.UEvalue, battery.MaxUEValue - battery.MachineData.UEvalue));
                if (transferAmount > 0) {
                    battery.MachineData.UEvalue += transferAmount;
                    coreTP.MachineData.UEvalue -= transferAmount;
                }
            }
            else {
                //电池想要放电，管道从中取电为其他设备供能
                float transferAmount = Math.Min(efficiency, Math.Min(battery.MachineData.UEvalue, coreTP.MaxUEValue - coreTP.MachineData.UEvalue));
                if (transferAmount > 0) {
                    battery.MachineData.UEvalue -= transferAmount;
                    coreTP.MachineData.UEvalue += transferAmount;
                }
            }

            LinkType = PipelineLinkType.Battery;
        }

        /// <summary>
        /// 绘制连接臂的逻辑
        /// </summary>
        public void Draw(SpriteBatch spriteBatch) {
            if (coreTP == null || coreTP.MachineData == null || externalTP == null) {
                return;
            }

            Vector2 drawPos = coreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();
            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;

            //绘制能量流动效果
            Color energyColor = coreTP.BaseColor * (coreTP.MachineData.UEvalue / 10f);
            spriteBatch.Draw(UEPipelineTP.PipelineChannel.Value, drawPos + orig, null, energyColor, drawRot, orig, 1, SpriteEffects.None, 0);

            //绘制管道连接臂的本体
            Color lightingColor = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, lightingColor, drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }
}
