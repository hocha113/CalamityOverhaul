using CalamityMod.Items.Materials;
using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    #region Enums for Readability //为了代码可读性，引入枚举
    /// <summary>
    /// 管道连接的目标类型枚举
    /// </summary>
    public enum PipelineLinkType
    {
        None,     //无连接
        Generator,//连接到发电机
        Pipeline, //连接到另一个管道
        Battery   //连接到电池
    }

    /// <summary>
    /// 管道的几何形状枚举
    /// </summary>
    public enum PipelineShape
    {
        //注意: 这个枚举的顺序很重要，因为它会影响贴图的绘制优先级
        Endpoint,//端点 (连接0个或1个其他管道)
        Straight,//直线
        Corner,  //拐角
        ThreeWay,//三通
        Cross    //十字交叉
    }
    #endregion

    /// <summary>
    /// 合并后的通用能源管道物品
    /// </summary>
    internal class UEPipeline : BasePipelineItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/PipelineItem";
        public override int CreateTileID => ModContent.TileType<UEPipelineTile>();
        public override void AddRecipes() {
            CreateRecipe(333).
                AddIngredient<DubiousPlating>(5).
                AddIngredient<MysteriousCircuitry>(5).
                AddRecipeGroup(CWRRecipes.TinBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    /// <summary>
    /// 合并后的通用能源管道图块
    /// </summary>
    internal class UEPipelineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/Pipeline";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<UEPipeline>());
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);
        }
        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.GreenTorch);
            return false;
        }
        public override bool CanDrop(int i, int j) => false;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

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
            if (coreTP.IsNetworkPowered) {
                //如果管道网络由发电机供能，则无视电池的设置，强制为其充电
                float transferAmount = Math.Min(efficiency, Math.Min(coreTP.MachineData.UEvalue, battery.MaxUEValue - battery.MachineData.UEvalue));
                if (transferAmount > 0) {
                    battery.MachineData.UEvalue += transferAmount;
                    coreTP.MachineData.UEvalue -= transferAmount;
                }
            }
            else {
                //如果管道网络是独立的(没有发电机)，则尊重电池的设置
                if (battery.ReceivedEnergy) {
                    //电池想要被充电，但独立网络无法提供电力，所以什么都不做
                }
                else {
                    //电池想要放电，管道从中取电为其他设备供能
                    float transferAmount = Math.Min(efficiency, Math.Min(battery.MachineData.UEvalue, coreTP.MaxUEValue - coreTP.MachineData.UEvalue));
                    if (transferAmount > 0) {
                        battery.MachineData.UEvalue -= transferAmount;
                        coreTP.MachineData.UEvalue += transferAmount;
                    }
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

    /// <summary>
    /// 通用能源管道的TileProcessor
    /// </summary>
    [VaultLoaden(CWRConstant.Asset + "MaterialFlow")]
    internal class UEPipelineTP : BaseUEPipelineTP, ICWRLoader
    {
        #region 资源加载
        public override int TargetTileID => ModContent.TileType<UEPipelineTile>();
        public static Asset<Texture2D> Pipeline { get; private set; }
        public static Asset<Texture2D> PipelineSide { get; private set; }
        public static Asset<Texture2D> PipelineCorner { get; private set; }
        public static Asset<Texture2D> PipelineCornerSide { get; private set; }
        public static Asset<Texture2D> PipelineCross { get; private set; }
        public static Asset<Texture2D> PipelineCrossSide { get; private set; }
        public static Asset<Texture2D> PipelineChannel { get; private set; }
        public static Asset<Texture2D> PipelineChannelSide { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutches { get; private set; }
        public static Asset<Texture2D> PipelineThreeCrutchesSide { get; private set; }
        #endregion

        internal List<PipelineSideState> SideState { get; private set; }
        public override int TargetItem => ModContent.ItemType<UEPipeline>();
        /// <summary>
        /// 判断该管道所在的网络是否由发电机供能
        /// </summary>
        internal bool IsNetworkPowered { get; set; }

        /// <summary>
        /// 当前管道的计算形状
        /// </summary>
        internal PipelineShape Shape { get; private set; } = PipelineShape.Endpoint;
        /// <summary>
        /// 管道形状的旋转/方向ID (用于拐角和三通)
        /// </summary>
        internal int ShapeRotationID { get; private set; } = 0;

        public override void SetMachine() {
            Efficiency = 0;
            SideState = [
                new(new Point16(0, -1)), //上: 0
                new(new Point16(0, 1)),  //下: 1
                new(new Point16(-1, 0)), //左: 2
                new(new Point16(1, 0))   //右: 3
            ];
        }

        /// <summary>
        /// 更新机器状态，现在分为更新连接和判断形状两步
        /// </summary>
        public override void UpdateMachine() {
            //在每次更新开始时，先重置自己的供电状态，等待邻居来更新
            IsNetworkPowered = false;
            //第一步: 更新每个方向的连接状态
            foreach (var side in SideState) {
                side.coreTP = this;
                side.Position = Position;
                side.UpdateConnectionState();
            }
            //第二步: 根据新的连接状态，处理与邻居的交互
            foreach (var side in SideState) {
                side.Update();
            }
            //第三步: 根据四周的连接情况，确定管道的几何形状和方向
            DeterminePipelineShape();
        }

        /// <summary>
        /// 根据四周的连接情况，确定管道的几何形状和方向
        /// </summary>
        private void DeterminePipelineShape() {
            bool up = SideState[0].LinkType == PipelineLinkType.Pipeline;
            bool down = SideState[1].LinkType == PipelineLinkType.Pipeline;
            bool left = SideState[2].LinkType == PipelineLinkType.Pipeline;
            bool right = SideState[3].LinkType == PipelineLinkType.Pipeline;
            int pipelineConnections = (up ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);

            switch (pipelineConnections) {
                case 4:
                    Shape = PipelineShape.Cross;
                    break;
                case 3:
                    Shape = PipelineShape.ThreeWay;
                    if (!up) ShapeRotationID = 0; //缺口朝上
                    else if (!down) ShapeRotationID = 1; //缺口朝下
                    else if (!left) ShapeRotationID = 2; //缺口朝左
                    else if (!right) ShapeRotationID = 3; //缺口朝右
                    break;
                case 2:
                    if ((up && down) || (left && right)) {
                        Shape = PipelineShape.Straight;
                        ShapeRotationID = up ? 0 : 1; //0代表垂直, 1代表水平
                    }
                    else {
                        Shape = PipelineShape.Corner;
                        if (up && right) ShapeRotationID = 0; //右上
                        else if (down && right) ShapeRotationID = 1; //右下
                        else if (up && left) ShapeRotationID = 2; //左上
                        else if (down && left) ShapeRotationID = 3; //左下
                    }
                    break;
                default: //case 1 and 0
                    Shape = PipelineShape.Endpoint;
                    break;
            }
        }

        /// <summary>
        /// 预绘制，用于绘制管道后方的连接臂
        /// </summary>
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            //十字交叉时，连接臂的绘制由中心统一处理，此处不画
            if (Shape == PipelineShape.Cross) {
                return;
            }
            foreach (var side in SideState) {
                //只绘制连接到非管道的连接臂 (如发电机、电池)
                if (side.canDraw && side.LinkType != PipelineLinkType.Pipeline) {
                    side.Draw(spriteBatch);
                }
            }
        }

        /// <summary>
        /// 核心绘制逻辑，根据管道形状进行绘制
        /// </summary>
        public override void Draw(SpriteBatch spriteBatch) {
            //先绘制连接到其他管道的连接臂
            if (Shape != PipelineShape.Cross) {
                foreach (var side in SideState) {
                    if (side.canDraw && side.LinkType == PipelineLinkType.Pipeline) {
                        side.Draw(spriteBatch);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color energyColor = BaseColor * (MachineData.UEvalue / 10f);
            Color lightingColor = Lighting.GetColor(Position.ToPoint());

            //使用switch根据形状进行绘制，清晰且易于扩展
            switch (Shape) {
                case PipelineShape.Cross:
                    drawPos = CenterInWorld - Main.screenPosition;
                    spriteBatch.Draw(PipelineCross.Value, drawPos, null, energyColor, 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                    spriteBatch.Draw(PipelineCrossSide.Value, drawPos, null, lightingColor, 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                    break;
                case PipelineShape.ThreeWay:
                    Rectangle threeWayRect = PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(PipelineThreeCrutches.Value, drawPos, threeWayRect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    spriteBatch.Draw(PipelineThreeCrutchesSide.Value, drawPos, threeWayRect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    break;
                case PipelineShape.Corner:
                    Rectangle cornerRect = PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(PipelineCorner.Value, drawPos, cornerRect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    spriteBatch.Draw(PipelineCornerSide.Value, drawPos, cornerRect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    break;
                case PipelineShape.Straight:
                    break;
                case PipelineShape.Endpoint:
                    int linkCount = SideState.Count(s => s.LinkType != PipelineLinkType.None);
                    int nonPipeLinkCount = SideState.Count(s => s.LinkType != PipelineLinkType.None && s.LinkType != PipelineLinkType.Pipeline);
                    //只有在作为两个非管道的连接器，或者完全独立时，才绘制中心方块
                    if (linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0) {
                        spriteBatch.Draw(Pipeline.Value, drawPos.GetRectangle(Size), energyColor);
                        spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), lightingColor);
                    }
                    break;
            }
        }
    }
}