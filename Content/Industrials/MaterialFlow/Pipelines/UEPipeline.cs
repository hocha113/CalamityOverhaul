using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
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
        Endpoint,//端点(连接0个或1个其他管道)
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
            if (!CWRRef.Has) {
                CreateRecipe(333).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
                return;
            }
            CreateRecipe(333).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 5).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
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

        #region 形状查找表
        //使用位掩码表示连接状态:上=1,下=2,左=4,右=8
        private const int UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;

        //形状查找表:根据连接掩码直接获取形状和旋转ID
        private static readonly (PipelineShape shape, int rotation)[] ShapeLookup = new (PipelineShape, int)[16];

        static UEPipelineTP() {
            //初始化形状查找表
            for (int mask = 0; mask < 16; mask++) {
                ShapeLookup[mask] = CalculateShape(mask);
            }
        }

        private static (PipelineShape, int) CalculateShape(int mask) {
            int count = CountBits(mask);
            return count switch {
                4 => (PipelineShape.Cross, 0),
                3 => (PipelineShape.ThreeWay, GetThreeWayRotation(mask)),
                2 => IsOpposite(mask) ? (PipelineShape.Straight, (mask & (UP | DOWN)) != 0 ? 0 : 1)
                                      : (PipelineShape.Corner, GetCornerRotation(mask)),
                _ => (PipelineShape.Endpoint, 0)
            };
        }

        private static int CountBits(int n) => (n & 1) + ((n >> 1) & 1) + ((n >> 2) & 1) + ((n >> 3) & 1);
        private static bool IsOpposite(int mask) => mask == (UP | DOWN) || mask == (LEFT | RIGHT);

        private static int GetThreeWayRotation(int mask) {
            if ((mask & UP) == 0) return 0;    //缺口朝上
            if ((mask & DOWN) == 0) return 1;  //缺口朝下
            if ((mask & LEFT) == 0) return 2;  //缺口朝左
            return 3;                           //缺口朝右
        }

        private static int GetCornerRotation(int mask) {
            if ((mask & (UP | RIGHT)) == (UP | RIGHT)) return 0;   //右上
            if ((mask & (DOWN | RIGHT)) == (DOWN | RIGHT)) return 1;//右下
            if ((mask & (UP | LEFT)) == (UP | LEFT)) return 2;     //左上
            return 3;                                               //左下
        }
        #endregion

        internal List<PipelineSideState> SideState { get; private set; }
        public override int TargetItem => ModContent.ItemType<UEPipeline>();

        /// <summary>
        /// 判断该管道所在的网络是否由发电机供能(由PowerNetworkManager设置)
        /// </summary>
        internal bool IsNetworkPowered { get; set; }

        /// <summary>
        /// 当前管道的计算形状
        /// </summary>
        internal PipelineShape Shape { get; private set; } = PipelineShape.Endpoint;

        /// <summary>
        /// 管道形状的旋转方向ID(用于拐角和三通)
        /// </summary>
        internal int ShapeRotationID { get; private set; } = 0;

        //缓存上一帧的连接掩码，避免重复计算形状
        private int lastConnectionMask = -1;

        public override void SetMachine() {
            Efficiency = 0;//管道不参与基类的导电逻辑，由PowerNetworkManager统一管理
            SideState = [
                new(new Point16(0, -1)), //上:0
                new(new Point16(0, 1)),  //下:1
                new(new Point16(-1, 0)), //左:2
                new(new Point16(1, 0))   //右:3
            ];
        }

        /// <summary>
        /// 更新机器状态
        /// </summary>
        public override void UpdateMachine() {
            //每帧开始时重置供电状态
            IsNetworkPowered = false;

            //更新每个方向的连接状态和电力传输
            foreach (var side in SideState) {
                side.coreTP = this;
                side.Position = Position;
                side.UpdateConnectionState();
            }

            //计算连接掩码
            int connectionMask = 0;
            if (SideState[0].LinkType == PipelineLinkType.Pipeline) connectionMask |= UP;
            if (SideState[1].LinkType == PipelineLinkType.Pipeline) connectionMask |= DOWN;
            if (SideState[2].LinkType == PipelineLinkType.Pipeline) connectionMask |= LEFT;
            if (SideState[3].LinkType == PipelineLinkType.Pipeline) connectionMask |= RIGHT;

            //只有连接状态变化时才重新计算形状
            if (connectionMask != lastConnectionMask) {
                var (shape, rotation) = ShapeLookup[connectionMask];
                Shape = shape;
                ShapeRotationID = rotation;
                lastConnectionMask = connectionMask;
            }

            //更新绘制状态
            foreach (var side in SideState) {
                side.UpdateDrawState();
            }
        }

        /// <summary>
        /// 预绘制，用于绘制管道后方的连接臂
        /// </summary>
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) return;

            foreach (var side in SideState) {
                //只绘制连接到非管道的连接臂(如发电机、电池)
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
            float energyRatio = MachineData.UEvalue / 10f;
            Color energyColor = BaseColor * energyRatio;
            Color lightingColor = Lighting.GetColor(Position.ToPoint());

            switch (Shape) {
                case PipelineShape.Cross:
                    DrawCross(spriteBatch, energyColor, lightingColor);
                    break;
                case PipelineShape.ThreeWay:
                    DrawThreeWay(spriteBatch, drawPos, energyColor, lightingColor);
                    break;
                case PipelineShape.Corner:
                    DrawCorner(spriteBatch, drawPos, energyColor, lightingColor);
                    break;
                case PipelineShape.Straight:
                    //直线形状不需要额外绘制中心
                    break;
                case PipelineShape.Endpoint:
                    DrawEndpoint(spriteBatch, drawPos, energyColor, lightingColor);
                    break;
            }
        }

        private void DrawCross(SpriteBatch spriteBatch, Color energyColor, Color lightingColor) {
            Vector2 drawPos = CenterInWorld - Main.screenPosition;
            Vector2 origin = PipelineCross.Size() / 2;
            spriteBatch.Draw(PipelineCross.Value, drawPos, null, energyColor, 0, origin, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(PipelineCrossSide.Value, drawPos, null, lightingColor, 0, origin, 1, SpriteEffects.None, 0);
        }

        private void DrawThreeWay(SpriteBatch spriteBatch, Vector2 drawPos, Color energyColor, Color lightingColor) {
            Rectangle rect = PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineThreeCrutches.Value, drawPos, rect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(PipelineThreeCrutchesSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawCorner(SpriteBatch spriteBatch, Vector2 drawPos, Color energyColor, Color lightingColor) {
            Rectangle rect = PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineCorner.Value, drawPos, rect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            spriteBatch.Draw(PipelineCornerSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawEndpoint(SpriteBatch spriteBatch, Vector2 drawPos, Color energyColor, Color lightingColor) {
            //统计连接数量
            int linkCount = 0;
            int nonPipeLinkCount = 0;
            foreach (var side in SideState) {
                if (side.LinkType != PipelineLinkType.None) {
                    linkCount++;
                    if (side.LinkType != PipelineLinkType.Pipeline) {
                        nonPipeLinkCount++;
                    }
                }
            }

            //只有作为两个非管道的连接器或完全独立时才绘制中心方块
            if (linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0) {
                spriteBatch.Draw(Pipeline.Value, drawPos.GetRectangle(Size), energyColor);
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), lightingColor);
            }
        }
    }
}