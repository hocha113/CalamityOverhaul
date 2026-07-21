using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
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
    /// <summary>管道连接目标类型</summary>
    public enum PipelineLinkType
    {
        None,     //无连接
        Generator,//连接到发电机
        Pipeline, //连接到另一个管道
        Battery   //连接到电池
    }

    /// <summary>管道几何形状</summary>
    public enum PipelineShape
    {
        Endpoint,//端点(连接0个或1个其他管道)
        Straight,//直线
        Corner,  //拐角
        ThreeWay,//三通
        Cross    //十字交叉
    }
    #endregion

    /// <summary>通用能源管道物品</summary>
    internal class UEPipeline : BasePipelineItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/PipelineItem";
        public override int CreateTileID => ModContent.TileType<UEPipelineTile>();
        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe(333).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 5).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe(333).
                AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>通用能源管道图块</summary>
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

    /// <summary>通用能源管道 TP</summary>
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
        //连接掩码 上1下2左4右8
        private const int UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;

        //掩码查形状与旋转
        private static readonly (PipelineShape shape, int rotation)[] ShapeLookup = new (PipelineShape, int)[16];

        static UEPipelineTP() {
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

        /// <summary>网络是否由发电机供能</summary>
        internal bool IsNetworkPowered { get; set; }

        /// <summary>当前计算形状</summary>
        internal PipelineShape Shape { get; private set; } = PipelineShape.Endpoint;

        /// <summary>拐角/三通旋转 ID</summary>
        internal int ShapeRotationID { get; private set; } = 0;

        //上一帧连接掩码，掩码变才重算形状
        private int lastConnectionMask = -1;

        public override void SetMachine() {
            Efficiency = 0;//不参与基类导电，由电网统一管理
            SideState = [
                new(new Point16(0, -1)), //上:0
                new(new Point16(0, 1)),  //下:1
                new(new Point16(-1, 0)), //左:2
                new(new Point16(1, 0))   //右:3
            ];
        }

        /// <summary>更新连接与形状</summary>
        public override void UpdateMachine() {
            //先重置供电
            IsNetworkPowered = false;

            foreach (var side in SideState) {
                side.coreTP = this;
                side.Position = Position;
                side.UpdateConnectionState();
            }

            int connectionMask = 0;
            if (SideState[0].LinkType == PipelineLinkType.Pipeline) connectionMask |= UP;
            if (SideState[1].LinkType == PipelineLinkType.Pipeline) connectionMask |= DOWN;
            if (SideState[2].LinkType == PipelineLinkType.Pipeline) connectionMask |= LEFT;
            if (SideState[3].LinkType == PipelineLinkType.Pipeline) connectionMask |= RIGHT;

            if (connectionMask != lastConnectionMask) {
                var (shape, rotation) = ShapeLookup[connectionMask];
                Shape = shape;
                ShapeRotationID = rotation;
                lastConnectionMask = connectionMask;
            }

            foreach (var side in SideState) {
                side.UpdateDrawState();
            }
        }

        /// <summary>预画非管道臂外壳</summary>
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) return;

            foreach (var side in SideState) {
                //非管道臂
                if (side.canDraw && side.LinkType != PipelineLinkType.Pipeline) {
                    side.DrawCasing(spriteBatch);
                }
            }
        }

        /// <summary>按形状画管本体外壳</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            Color lightingColor = Lighting.GetColor(Position.ToPoint());

            if (Shape != PipelineShape.Cross) {
                foreach (var side in SideState) {
                    if (side.canDraw && side.LinkType == PipelineLinkType.Pipeline) {
                        side.DrawCasing(spriteBatch);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            switch (Shape) {
                case PipelineShape.Cross:
                    DrawCrossCasing(spriteBatch, lightingColor);
                    break;
                case PipelineShape.ThreeWay:
                    DrawThreeWayCasing(spriteBatch, drawPos, lightingColor);
                    break;
                case PipelineShape.Corner:
                    DrawCornerCasing(spriteBatch, drawPos, lightingColor);
                    break;
                case PipelineShape.Straight:
                    break;
                case PipelineShape.Endpoint:
                    DrawEndpointCasing(spriteBatch, drawPos, lightingColor);
                    break;
            }
        }

        /// <summary>能量层本体+臂，合批器着色器调用</summary>
        internal void DrawEnergy(SpriteBatch spriteBatch, Color energyColor) {
            if (MachineData == null) return;

            if (Shape != PipelineShape.Cross) {
                foreach (var side in SideState) {
                    if (side.canDraw) {
                        side.DrawEnergy(spriteBatch, energyColor);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            switch (Shape) {
                case PipelineShape.Cross:
                    DrawCrossEnergy(spriteBatch, energyColor);
                    break;
                case PipelineShape.ThreeWay:
                    DrawThreeWayEnergy(spriteBatch, drawPos, energyColor);
                    break;
                case PipelineShape.Corner:
                    DrawCornerEnergy(spriteBatch, drawPos, energyColor);
                    break;
                case PipelineShape.Straight:
                    break;
                case PipelineShape.Endpoint:
                    DrawEndpointEnergy(spriteBatch, drawPos, energyColor);
                    break;
            }
        }

        /// <summary>能量色 rgb=BaseColor，a=充盈度0~1</summary>
        internal Color GetEnergyDrawColor() {
            Color c = BaseColor;
            c.A = (byte)(MathHelper.Clamp(MachineData.UEvalue / MaxUEValue, 0f, 1f) * 255);
            return c;
        }

        #region 分形状的能量层 / 外壳层
        private void DrawCrossEnergy(SpriteBatch spriteBatch, Color energyColor) {
            Vector2 drawPos = CenterInWorld - Main.screenPosition;
            spriteBatch.Draw(PipelineCross.Value, drawPos, null, energyColor, 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
        }
        private void DrawCrossCasing(SpriteBatch spriteBatch, Color lightingColor) {
            Vector2 drawPos = CenterInWorld - Main.screenPosition;
            spriteBatch.Draw(PipelineCrossSide.Value, drawPos, null, lightingColor, 0, PipelineCrossSide.Size() / 2, 1, SpriteEffects.None, 0);
        }

        private void DrawThreeWayEnergy(SpriteBatch spriteBatch, Vector2 drawPos, Color energyColor) {
            Rectangle rect = PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineThreeCrutches.Value, drawPos, rect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
        private void DrawThreeWayCasing(SpriteBatch spriteBatch, Vector2 drawPos, Color lightingColor) {
            Rectangle rect = PipelineThreeCrutchesSide.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineThreeCrutchesSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawCornerEnergy(SpriteBatch spriteBatch, Vector2 drawPos, Color energyColor) {
            Rectangle rect = PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineCorner.Value, drawPos, rect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }
        private void DrawCornerCasing(SpriteBatch spriteBatch, Vector2 drawPos, Color lightingColor) {
            Rectangle rect = PipelineCornerSide.Value.GetRectangle(ShapeRotationID, 4);
            spriteBatch.Draw(PipelineCornerSide.Value, drawPos, rect, lightingColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
        }

        private void DrawEndpointEnergy(SpriteBatch spriteBatch, Vector2 drawPos, Color energyColor) {
            if (ShouldDrawEndpointCenter()) {
                spriteBatch.Draw(Pipeline.Value, drawPos.GetRectangle(Size), energyColor);
            }
        }
        private void DrawEndpointCasing(SpriteBatch spriteBatch, Vector2 drawPos, Color lightingColor) {
            if (ShouldDrawEndpointCenter()) {
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), lightingColor);
            }
        }

        /// <summary>双非管道连接或孤立端点才画中心块</summary>
        private bool ShouldDrawEndpointCenter() {
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
            return linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0;
        }
        #endregion
    }

    /// <summary>电力管能量合批，PreTileDraw + <see cref="EffectLoader.UEPipelineFlow"/>；缺着色器则 TP 平涂回退</summary>
    internal class UEPipelineEnergyDraw : GlobalTileProcessor
    {
        public override bool PreTileDrawEverything(SpriteBatch spriteBatch) {
            MachineShaderBatch.DrawBatch(spriteBatch, EffectLoader.UEPipelineFlow, SamplerState.PointClamp,
                //仅基础管，排除创造管子类
                static tp => tp.GetType() == typeof(UEPipelineTP),
                static effect => {
                    effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    effect.Parameters["uAlpha"]?.SetValue(1f);
                },
                tp => {
                    var pipe = (UEPipelineTP)tp;
                    pipe.DrawEnergy(spriteBatch, pipe.GetEnergyDrawColor());
                });
            return true;
        }
    }
}