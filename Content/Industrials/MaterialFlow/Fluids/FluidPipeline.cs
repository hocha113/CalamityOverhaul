using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines;
using CalamityOverhaul.Content.Items.Materials;
using InnoVault.Concurrent;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids
{
    /// <summary>液体管道物品</summary>
    internal class FluidPipeline : BasePipelineItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/FluidPipelineItem";
        public override int CreateTileID => ModContent.TileType<FluidPipelineTile>();
        public override void SetDefaults() {
            base.SetDefaults();
            //液管不承载 UE,清掉管道物品基类的电量旗标
            Item.CWR().StorageUE = false;
            Item.CWR().ConsumeUseUE = 0;
        }
        public override void AddRecipes() {
            CreateRecipe(333).
            AddIngredient<CircuitBoard>(5).
            AddRecipeGroup(CWRCrafted.TinBarGroup, 5).
            AddIngredient(ItemID.Gel, 10).
            AddTile(TileID.Anvils).
            Register();

        }
    }

    /// <summary>液体管道图块</summary>
    internal class FluidPipelineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/Pipeline";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(52, 84, 96), VaultUtils.GetLocalizedItemName<FluidPipeline>());
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);
        }
        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Water);
            return false;
        }
        public override bool CanDrop(int i, int j) => false;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

    /// <summary>
    /// 液体管道TP:独立于 UE 网的自有子树(不继承 MachineTP,不卷入 UE 均衡)。
    /// 均衡镜像 UE 管网的形状:外圈岛屿并行声明 + 四邻探测 + 成对压差限步转移。
    /// 一管一液:载液后只与同类液邻居均衡,排空后才可重绑类型
    /// </summary>
    internal class FluidPipelineTP : TileProcessor, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<FluidPipelineTile>();

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public int FluidCapacity => FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Pipe;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        /// <summary>单邻单帧的液体转移上限(单位)</summary>
        internal const int TransferStep = 25;

        #region 形状查找表(独立复刻,掩码 上1下2左4右8)
        private const int UP = 1, DOWN = 2, LEFT = 4, RIGHT = 8;
        private static readonly (PipelineShape shape, int rotation)[] ShapeLookup = new (PipelineShape, int)[16];

        static FluidPipelineTP() {
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
            if ((mask & UP) == 0) return 0;
            if ((mask & DOWN) == 0) return 1;
            if ((mask & LEFT) == 0) return 2;
            return 3;
        }

        private static int GetCornerRotation(int mask) {
            if ((mask & (UP | RIGHT)) == (UP | RIGHT)) return 0;
            if ((mask & (DOWN | RIGHT)) == (DOWN | RIGHT)) return 1;
            if ((mask & (UP | LEFT)) == (UP | LEFT)) return 2;
            return 3;
        }
        #endregion

        /// <summary>单侧连接状态:探测结果与绘制开关</summary>
        private sealed class FluidSideLink(Point16 offset)
        {
            internal readonly Point16 Offset = offset;
            internal TileProcessor externalTP;
            internal bool isPipe;
            internal bool linked;
            internal bool canDraw;
            /// <summary>本帧带符号转移量(正=流出本管),表现用</summary>
            internal int flowMoved;
            /// <summary>平滑流动包络 -1..1,符号=方向;无流量自然衰减归零(断电/无液即静)</summary>
            internal float flowVis;
        }

        private FluidSideLink[] sides;
        internal PipelineShape Shape { get; private set; } = PipelineShape.Endpoint;
        internal int ShapeRotationID { get; private set; }
        private int lastConnectionMask = -1;

        public override void SetProperty() {
            PlaceNet = true;//放置联网,同步初始液体状态
            sides = [
                new(new Point16(0, -1)), //上
                new(new Point16(0, 1)),  //下
                new(new Point16(-1, 0)), //左
                new(new Point16(1, 0))   //右
            ];
        }

        /// <summary>相连的液管/液体机器落入同一并行岛屿,岛内串行使邻居读写安全</summary>
        public override ParallelExecutionKind ParallelKind => ParallelExecutionKind.Grouped;

        public override void CollectGroupLinks(ref TPGroupLinkBuilder builder) {
            builder.Link(Position.X, Position.Y - 1);
            builder.Link(Position.X, Position.Y + 1);
            builder.Link(Position.X - 1, Position.Y);
            builder.Link(Position.X + 1, Position.Y);
        }

        public override void Update() {
            UpdateSidesAndFlow();
            UpdateShape();
        }

        /// <summary>四邻探测与液体输运:管管均衡,源单向抽入,耗液机单向灌出,储罐比例双向</summary>
        private void UpdateSidesAndFlow() {
            foreach (var side in sides) {
                side.externalTP = null;
                side.isPipe = false;
                side.linked = false;
                side.canDraw = false;
                side.flowMoved = 0;

                Point16 checkPos = Position + side.Offset;
                Tile tile = Framing.GetTileSafely(checkPos);
                if (!tile.HasTile) {
                    continue;
                }
                if (!VaultUtils.SafeGetTopLeft(checkPos, out var topLeft)) {
                    continue;
                }
                if (!TileProcessorLoader.TP_Point_To_Instance.TryGetValue(topLeft, out TileProcessor externalTP)) {
                    continue;
                }
                if (externalTP == null || !externalTP.Active) {
                    continue;
                }

                if (externalTP is FluidPipelineTP otherPipe) {
                    side.flowMoved = FluidHelper.EqualizePair(this, otherPipe, TransferStep);
                    side.externalTP = otherPipe;
                    side.isPipe = true;
                    side.linked = true;
                }
                else if (externalTP is IFluidContainer machine) {
                    switch (machine.FluidRole) {
                        case FluidNetRole.Source:
                            //机器→管,记为流入(负)
                            side.flowMoved = -FluidHelper.MoveFluid(machine, this, TransferStep);
                            break;
                        case FluidNetRole.Consumer:
                            side.flowMoved = FluidHelper.MoveFluid(this, machine, TransferStep);
                            break;
                        case FluidNetRole.Storage:
                            side.flowMoved = FluidHelper.EqualizePair(this, machine, TransferStep);
                            break;
                    }
                    side.externalTP = externalTP;
                    side.linked = true;
                }

                side.canDraw = side.linked;
            }

            //流动包络:有流量抬升,断流自然衰减(每侧每帧恰好平滑一次)
            foreach (var side in sides) {
                float target = MathHelper.Clamp(side.flowMoved / 18f, -1f, 1f);
                side.flowVis = MathHelper.Lerp(side.flowVis, target, 0.18f);
            }
        }

        /// <summary>连接掩码变化才重算形状;拐角/三通/十字邻居不画臂</summary>
        private void UpdateShape() {
            int connectionMask = 0;
            if (sides[0].isPipe) connectionMask |= UP;
            if (sides[1].isPipe) connectionMask |= DOWN;
            if (sides[2].isPipe) connectionMask |= LEFT;
            if (sides[3].isPipe) connectionMask |= RIGHT;

            if (connectionMask != lastConnectionMask) {
                var (shape, rotation) = ShapeLookup[connectionMask];
                Shape = shape;
                ShapeRotationID = rotation;
                lastConnectionMask = connectionMask;
            }

            foreach (var side in sides) {
                if (side.canDraw && side.externalTP is FluidPipelineTP otherPipe
                    && otherPipe.Shape is PipelineShape.Cross or PipelineShape.Corner or PipelineShape.ThreeWay) {
                    side.canDraw = false;
                }
            }
        }

        /// <summary>拆管掉落管道物品,管内液体流失(权威端)</summary>
        public override void OnKill() {
            if (VaultUtils.isClient) {
                return;
            }
            DeferSpawnItem(new EntitySource_WorldEvent(), HitBox, new Item(ModContent.ItemType<FluidPipeline>()), type => {
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            });
        }

        #region 存档与同步(管道不参与周期锚定,放置联网/入世快照/机器锚点自愈)
        public override void SendData(ModPacket data) {
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
        }

        public override void LoadData(TagCompound tag) {
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Water;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
        }
        #endregion

        #region 绘制:外壳走 TP 钩子;载液层走 FluidPipelineFlowDraw 合批着色器(缺着色器回退平涂)
        /// <summary>液体层颜色:rgb=液色,a=充盈度(合批着色器的顶点色契约)</summary>
        internal Color GetFluidDrawColor() {
            Color c = FluidHelper.GetColor(FluidType);
            c.A = (byte)(MathHelper.Clamp(FluidAmount / (float)FluidCapacity, 0f, 1f) * 255);
            return c;
        }

        /// <summary>载液着色器缺失时,外壳钩子内平涂回退</summary>
        private static bool FlowShaderReady => EffectLoader.FluidPipeFlow?.Value != null;

        private void DrawArmCasing(SpriteBatch spriteBatch, FluidSideLink side) {
            Vector2 drawPos = PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = side.Offset.ToVector2().ToRotation();
            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;
            Color lightingColor = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, lightingColor
                , drawRot, orig, 1, SpriteEffects.None, 0);
        }

        /// <summary>臂内液层;flip=水平翻转让流团反向(方向由 C# 承担,着色器恒 +u 行进)</summary>
        private void DrawArmFluid(SpriteBatch spriteBatch, FluidSideLink side, Color fluidColor, SpriteEffects flip = SpriteEffects.None) {
            Vector2 drawPos = PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = side.Offset.ToVector2().ToRotation();
            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;
            spriteBatch.Draw(UEPipelineTP.PipelineChannel.Value, drawPos + orig, null, fluidColor
                , drawRot, orig, 1, flip, 0);
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) {
                return;
            }
            Color fluidColor = GetFluidDrawColor();
            foreach (var side in sides) {
                //非管道臂(通向机器)画在物块层之下
                if (side.canDraw && !side.isPipe) {
                    DrawArmCasing(spriteBatch, side);
                    if (!FlowShaderReady && fluidColor.A > 0) {
                        DrawArmFluid(spriteBatch, side, fluidColor);
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Color lightingColor = Lighting.GetColor(Position.ToPoint());
            Color fluidColor = GetFluidDrawColor();
            bool fluidFallback = !FlowShaderReady && fluidColor.A > 0;

            if (Shape != PipelineShape.Cross) {
                foreach (var side in sides) {
                    if (side.canDraw && side.isPipe) {
                        DrawArmCasing(spriteBatch, side);
                        if (fluidFallback) {
                            DrawArmFluid(spriteBatch, side, fluidColor);
                        }
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            switch (Shape) {
                case PipelineShape.Cross: {
                    Vector2 centerPos = CenterInWorld - Main.screenPosition;
                    spriteBatch.Draw(UEPipelineTP.PipelineCrossSide.Value, centerPos, null, lightingColor
                        , 0, UEPipelineTP.PipelineCrossSide.Size() / 2, 1, SpriteEffects.None, 0);
                    if (fluidFallback) {
                        spriteBatch.Draw(UEPipelineTP.PipelineCross.Value, centerPos, null, fluidColor
                            , 0, UEPipelineTP.PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                    }
                    break;
                }
                case PipelineShape.ThreeWay: {
                    Rectangle rectSide = UEPipelineTP.PipelineThreeCrutchesSide.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(UEPipelineTP.PipelineThreeCrutchesSide.Value, drawPos, rectSide, lightingColor
                        , 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    if (fluidFallback) {
                        Rectangle rect = UEPipelineTP.PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
                        spriteBatch.Draw(UEPipelineTP.PipelineThreeCrutches.Value, drawPos, rect, fluidColor
                            , 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    }
                    break;
                }
                case PipelineShape.Corner: {
                    Rectangle rectSide = UEPipelineTP.PipelineCornerSide.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(UEPipelineTP.PipelineCornerSide.Value, drawPos, rectSide, lightingColor
                        , 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    if (fluidFallback) {
                        Rectangle rect = UEPipelineTP.PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
                        spriteBatch.Draw(UEPipelineTP.PipelineCorner.Value, drawPos, rect, fluidColor
                            , 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    }
                    break;
                }
                case PipelineShape.Endpoint: {
                    if (ShouldDrawEndpointCenter()) {
                        spriteBatch.Draw(UEPipelineTP.PipelineSide.Value, drawPos.GetRectangle(Size), lightingColor);
                        if (fluidFallback) {
                            spriteBatch.Draw(UEPipelineTP.Pipeline.Value, drawPos.GetRectangle(Size), fluidColor);
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>载液本体层:充盈度进顶点色 a,着色器静态呈现"管里有多少液"</summary>
        internal void DrawFluidBody(SpriteBatch spriteBatch) {
            Color vc = GetFluidDrawColor();
            if (vc.A <= 0) {
                return;
            }

            if (Shape != PipelineShape.Cross) {
                foreach (var side in sides) {
                    if (side.canDraw) {
                        DrawArmFluid(spriteBatch, side, vc);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            switch (Shape) {
                case PipelineShape.Cross: {
                    Vector2 centerPos = CenterInWorld - Main.screenPosition;
                    spriteBatch.Draw(UEPipelineTP.PipelineCross.Value, centerPos, null, vc
                        , 0, UEPipelineTP.PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                    break;
                }
                case PipelineShape.ThreeWay: {
                    Rectangle rect = UEPipelineTP.PipelineThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(UEPipelineTP.PipelineThreeCrutches.Value, drawPos, rect, vc
                        , 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    break;
                }
                case PipelineShape.Corner: {
                    Rectangle rect = UEPipelineTP.PipelineCorner.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(UEPipelineTP.PipelineCorner.Value, drawPos, rect, vc
                        , 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    break;
                }
                case PipelineShape.Endpoint: {
                    if (ShouldDrawEndpointCenter()) {
                        spriteBatch.Draw(UEPipelineTP.Pipeline.Value, drawPos.GetRectangle(Size), vc);
                    }
                    break;
                }
            }
        }

        /// <summary>该管在指定快慢桶里是否有活跃流动臂(流团批的收集谓词)</summary>
        internal bool HasFlowArm(bool slowBucket) {
            if (FluidVFX.IsSlowFluid(FluidType) != slowBucket) {
                return false;
            }
            foreach (var side in sides) {
                if (side.canDraw && System.MathF.Abs(side.flowVis) > 0.06f) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>流团层:顶点色 a=活跃度,流入臂水平翻转让行进方向反向</summary>
        internal void DrawFluidFlow(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) {
                return;
            }
            Color baseColor = FluidHelper.GetColor(FluidType);
            foreach (var side in sides) {
                if (!side.canDraw) {
                    continue;
                }
                float act = System.MathF.Abs(side.flowVis);
                if (act <= 0.06f) {
                    continue;
                }
                Color vc = baseColor;
                vc.A = (byte)(MathHelper.Clamp(act, 0f, 1f) * 255);
                SpriteEffects flip = side.flowVis < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                DrawArmFluid(spriteBatch, side, vc, flip);
            }
        }

        /// <summary>双非管连接或孤立端点才画中心块</summary>
        private bool ShouldDrawEndpointCenter() {
            int linkCount = 0;
            int nonPipeLinkCount = 0;
            foreach (var side in sides) {
                if (side.linked) {
                    linkCount++;
                    if (!side.isPipe) {
                        nonPipeLinkCount++;
                    }
                }
            }
            return linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0;
        }
        #endregion
    }

    /// <summary>
    /// 液管载液合批:本体层 + 快液流团 + 慢液流团三批,PreTileDraw 层
    /// (<see cref="EffectLoader.FluidPipeFlow"/>);缺着色器时各管在外壳钩子里平涂回退。
    /// 流团方向由臂的水平翻转承担,快慢桶给水/微光与岩浆/蜜不同行进速度(粘度身份)
    /// </summary>
    internal class FluidPipelineFlowDraw : GlobalTileProcessor
    {
        public override bool PreTileDrawEverything(SpriteBatch spriteBatch) {
            //本体:充盈度亮度,静止——断电/无液即静是状态语言
            MachineShaderBatch.DrawBatch(spriteBatch, EffectLoader.FluidPipeFlow, SamplerState.PointClamp,
                static tp => tp is FluidPipelineTP pipe && pipe.FluidAmount > 0,
                static effect => {
                    effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    effect.Parameters["uAlpha"]?.SetValue(1f);
                    effect.Parameters["uFlowMode"]?.SetValue(0f);
                    effect.Parameters["uSpeed"]?.SetValue(0f);
                },
                tp => ((FluidPipelineTP)tp).DrawFluidBody(spriteBatch));

            //快液流团(水/微光)
            MachineShaderBatch.DrawBatch(spriteBatch, EffectLoader.FluidPipeFlow, SamplerState.PointClamp,
                static tp => tp is FluidPipelineTP pipe && pipe.HasFlowArm(slowBucket: false),
                static effect => {
                    effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    effect.Parameters["uAlpha"]?.SetValue(1f);
                    effect.Parameters["uFlowMode"]?.SetValue(1f);
                    effect.Parameters["uSpeed"]?.SetValue(0.9f);
                },
                tp => ((FluidPipelineTP)tp).DrawFluidFlow(spriteBatch));

            //慢液流团(岩浆/蜜):行进放慢读出粘度
            MachineShaderBatch.DrawBatch(spriteBatch, EffectLoader.FluidPipeFlow, SamplerState.PointClamp,
                static tp => tp is FluidPipelineTP pipe && pipe.HasFlowArm(slowBucket: true),
                static effect => {
                    effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                    effect.Parameters["uAlpha"]?.SetValue(1f);
                    effect.Parameters["uFlowMode"]?.SetValue(1f);
                    effect.Parameters["uSpeed"]?.SetValue(0.34f);
                },
                tp => ((FluidPipelineTP)tp).DrawFluidFlow(spriteBatch));
            return true;
        }
    }
}
