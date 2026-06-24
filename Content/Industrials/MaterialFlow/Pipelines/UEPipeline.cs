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
        //连接掩码：上1下2左4右8
        private const int UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;

        //掩码查形状与旋转
        private static readonly (PipelineShape shape, int rotation)[] ShapeLookup = new (PipelineShape, int)[16];

        static UEPipelineTP() {
            //初始化查找表
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
            //每帧先重置供电，连接检测会重设
            IsNetworkPowered = false;

            //四向连接与输电
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

            //掩码变才重算形状
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
        /// 能量层是否由电网着色器统一批次绘制：仅基础管道 + 着色器可用时为 true。
        /// 创造管道(子类)与着色器缺失时，能量层在本 TP 内联平涂回退
        /// </summary>
        private bool EnergyDrawnByNetwork => GetType() == typeof(UEPipelineTP) && EffectLoader.UEPipelineFlow?.Value != null;

        /// <summary>预绘制非管道连接臂（金属外壳；能量层视情况内联回退）</summary>
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) return;

            bool inlineEnergy = !EnergyDrawnByNetwork;
            Color energyColor = inlineEnergy ? GetEnergyDrawColor(false) : default;
            foreach (var side in SideState) {
                //发电机/电池等非管道臂
                if (side.canDraw && side.LinkType != PipelineLinkType.Pipeline) {
                    if (inlineEnergy) {
                        side.DrawEnergy(spriteBatch, energyColor);
                    }
                    side.DrawCasing(spriteBatch);
                }
            }
        }

        /// <summary>按形状绘制管道本体（金属外壳；能量层视情况内联回退）</summary>
        public override void Draw(SpriteBatch spriteBatch) {
            bool inlineEnergy = !EnergyDrawnByNetwork;
            Color energyColor = inlineEnergy ? GetEnergyDrawColor(false) : default;
            Color lightingColor = Lighting.GetColor(Position.ToPoint());

            //管道间连接臂
            if (Shape != PipelineShape.Cross) {
                foreach (var side in SideState) {
                    if (side.canDraw && side.LinkType == PipelineLinkType.Pipeline) {
                        if (inlineEnergy) {
                            side.DrawEnergy(spriteBatch, energyColor);
                        }
                        side.DrawCasing(spriteBatch);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            switch (Shape) {
                case PipelineShape.Cross:
                    if (inlineEnergy) DrawCrossEnergy(spriteBatch, energyColor);
                    DrawCrossCasing(spriteBatch, lightingColor);
                    break;
                case PipelineShape.ThreeWay:
                    if (inlineEnergy) DrawThreeWayEnergy(spriteBatch, drawPos, energyColor);
                    DrawThreeWayCasing(spriteBatch, drawPos, lightingColor);
                    break;
                case PipelineShape.Corner:
                    if (inlineEnergy) DrawCornerEnergy(spriteBatch, drawPos, energyColor);
                    DrawCornerCasing(spriteBatch, drawPos, lightingColor);
                    break;
                case PipelineShape.Straight:
                    break;
                case PipelineShape.Endpoint:
                    if (inlineEnergy) DrawEndpointEnergy(spriteBatch, drawPos, energyColor);
                    DrawEndpointCasing(spriteBatch, drawPos, lightingColor);
                    break;
            }
        }

        /// <summary>能量层：本体 + 所有连接臂，由 <see cref="UEPipelineEnergyDraw"/> 着色器批次统一调用</summary>
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

        /// <summary>
        /// 能量绘制色。着色器模式：rgb=色调(BaseColor)，a=电量比例(0~1)供着色器读取强度；
        /// 回退模式：BaseColor×电量比例 直接平涂
        /// </summary>
        internal Color GetEnergyDrawColor(bool shaderMode) {
            if (shaderMode) {
                //着色器读取真实充盈度(0~1)作强度，使管内含量随亮度真实变化
                Color c = BaseColor;
                c.A = (byte)(MathHelper.Clamp(MachineData.UEvalue / MaxUEValue, 0f, 1f) * 255);
                return c;
            }
            return BaseColor * (MachineData.UEvalue / (MaxUEValue * 0.5f));
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

    /// <summary>
    /// 电力管道能量层统一绘制：在墙后物块前(PreTileDraw 层)用 <see cref="EffectLoader.UEPipelineFlow"/>
    /// 单批次渲染全网管道的流动能量，金属外壳由各 TP 在其上叠加。着色器缺失时各 TP 内联平涂回退
    /// </summary>
    internal class UEPipelineEnergyDraw : GlobalTileProcessor
    {
        private static readonly List<UEPipelineTP> visiblePipes = [];

        public override bool PreTileDrawEverything(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return true;
            }
            Effect effect = EffectLoader.UEPipelineFlow?.Value;
            if (effect == null) {
                return true;//着色器缺失：能量层由各管道内联平涂回退
            }

            visiblePipes.Clear();
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                //仅基础管道；创造管道(子类)有自己的星空渲染，排除
                if (tp.GetType() == typeof(UEPipelineTP) && tp.Active
                    && VaultUtils.IsPointOnScreen(tp.PosInWorld - Main.screenPosition, tp.DrawExtendMode)) {
                    visiblePipes.Add((UEPipelineTP)tp);
                }
            }
            if (visiblePipes.Count == 0) {
                return true;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uAlpha"]?.SetValue(1f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.Transform);
            foreach (var pipe in visiblePipes) {
                pipe.DrawEnergy(spriteBatch, pipe.GetEnergyDrawColor(true));
            }
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            return true;
        }
    }
}