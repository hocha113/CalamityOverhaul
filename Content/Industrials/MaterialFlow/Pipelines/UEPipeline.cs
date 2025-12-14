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
    #region 枚举
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
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 5).
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