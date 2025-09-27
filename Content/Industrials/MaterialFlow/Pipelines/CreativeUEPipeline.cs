using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.RenderHandles;
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
    /// <summary>
    /// 无限电力的创造管道物品
    /// </summary>
    internal class CreativeUEPipeline : BasePipelineItem
    {
        //为了区分，你需要为这个物品创建一个新的贴图
        public override string Texture => CWRConstant.Asset + "MaterialFlow/CreativePipelineItem";

        //将要放置的物块ID指向新的创造管道物块
        public override int CreateTileID => ModContent.TileType<CreativeUEPipelineTile>();

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Red;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_CreativePipeline;
        }
    }

    /// <summary>
    /// 无限电力的创造管道物块
    /// </summary>
    internal class CreativeUEPipelineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/CreativePipeline";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            AddMapEntry(new Color(170, 130, 200), VaultUtils.GetLocalizedItemName<CreativeUEPipeline>());
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.MagicMirror);//紫色的粒子效果
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

    /// <summary>
    /// 无限电力的创造管道的逻辑核心
    /// </summary>
    [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
    internal class CreativeUEPipelineTP : UEPipelineTP
    {
        public static Asset<Texture2D> PipelineCreative { get; private set; }
        public static Asset<Texture2D> PipelineCreativeCorner { get; private set; }
        public static Asset<Texture2D> PipelineCreativeCross { get; private set; }
        public static Asset<Texture2D> PipelineCreativeChannel { get; private set; }
        public static Asset<Texture2D> PipelineCreativeThreeCrutches { get; private set; }
        public override int TargetTileID => ModContent.TileType<CreativeUEPipelineTile>();
        public override int TargetItem => ModContent.ItemType<CreativeUEPipeline>();
        public override Color BaseColor => Color.Purple;
        /// <summary>
        /// 重写机器更新逻辑
        /// </summary>
        public override void UpdateMachine() {
            base.UpdateMachine();
            IsNetworkPowered = true;//无限能量，自身相当于一个能量源，所以始终有电，不受外部网络影响
            if (MachineData != null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }

        /// <summary>
        /// 绘制连接臂的通用方法，使用创造模式贴图
        /// </summary>
        private void DrawCreativeArm(PipelineSideState side, SpriteBatch spriteBatch, bool drawSide) {
            if (side.coreTP == null || side.externalTP == null) {
                return;
            }

            Vector2 drawPos = side.coreTP.PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = side.Offset.ToVector2().ToRotation();
            Vector2 orig = PipelineCreativeChannel.Size() / 2;

            if (!drawSide) {
                //使用创造管道的能量贴图 (紫色)
                spriteBatch.Draw(PipelineCreativeChannel.Value, drawPos + orig, null, BaseColor * (MachineData.UEvalue / 10f), drawRot, orig, 1, SpriteEffects.None, 0);
            }
            else {
                //使用基础管道的侧边贴图来绘制光照和轮廓
                spriteBatch.Draw(PipelineChannelSide.Value, drawPos + orig, null, Lighting.GetColor(Position.ToPoint()), drawRot, orig, 1, SpriteEffects.None, 0);
            }
        }

        internal void HideRenderDraw(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) {
                return;
            }
            foreach (var side in SideState) {
                if (side.canDraw && side.LinkType != PipelineLinkType.Pipeline) {
                    DrawCreativeArm(side, spriteBatch, false);
                }
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Shape == PipelineShape.Cross) {
                return;
            }
            foreach (var side in SideState) {
                if (side.canDraw && side.LinkType != PipelineLinkType.Pipeline) {
                    DrawCreativeArm(side, spriteBatch, true);
                }
            }
        }

        internal void RenderDraw(SpriteBatch spriteBatch) {
            //首先绘制连接到其他管道的连接臂
            if (Shape != PipelineShape.Cross) {
                foreach (var side in SideState) {
                    if (side.canDraw && side.LinkType == PipelineLinkType.Pipeline) {
                        DrawCreativeArm(side, spriteBatch, false);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color energyColor = BaseColor * (MachineData.UEvalue / 10f);
            //根据基类计算出的形状，使用创造贴图进行绘制
            switch (Shape) {
                case PipelineShape.Cross:
                    drawPos = CenterInWorld - Main.screenPosition;
                    spriteBatch.Draw(PipelineCreativeCross.Value, drawPos, null, energyColor, 0, PipelineCreativeCross.Size() / 2, 1, SpriteEffects.None, 0);
                    break;
                case PipelineShape.ThreeWay:
                    Rectangle threeWayRect = PipelineCreativeThreeCrutches.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(PipelineCreativeThreeCrutches.Value, drawPos, threeWayRect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    break;
                case PipelineShape.Corner:
                    Rectangle cornerRect = PipelineCreativeCorner.Value.GetRectangle(ShapeRotationID, 4);
                    spriteBatch.Draw(PipelineCreativeCorner.Value, drawPos, cornerRect, energyColor, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    break;
                case PipelineShape.Straight:
                    break;
                case PipelineShape.Endpoint:
                    int linkCount = SideState.Count(s => s.LinkType != PipelineLinkType.None);
                    int nonPipeLinkCount = SideState.Count(s => s.LinkType != PipelineLinkType.None && s.LinkType != PipelineLinkType.Pipeline);
                    if (linkCount != 2 || nonPipeLinkCount == 2 || linkCount == 0) {
                        spriteBatch.Draw(PipelineCreative.Value, drawPos.GetRectangle(Size), energyColor);
                    }
                    break;
            }
        }
    }

    internal class CreativePipelineGlobalDraw : GlobalTileProcessor
    {
        [VaultLoaden(CWRConstant.Effects)]
        internal static Effect StarsShader { get; set; }
        private readonly static List<CreativeUEPipelineTP> creativePipelines = [];
        private static int creativePipelineID;
        public override void SetStaticDefaults() => creativePipelineID = TileProcessorLoader.GetModuleID<CreativeUEPipelineTP>();
        //在统一逻辑处理之前先保证集合被清空
        public override bool PreSingleInstanceUpdate(TileProcessor tileProcessor) {
            if (tileProcessor.ID != creativePipelineID) {
                return true;//只在上帝管道的单例上运行
            }

            creativePipelines.Clear();
            return true;
        }
        //在逻辑更新中统一处理管道实例的添加
        public override void SingleInstanceUpdate(TileProcessor tileProcessor) {
            if (tileProcessor.ID != creativePipelineID) {
                return;//只在上帝管道的单例上运行
            }

            for (int i = 0; i < TileProcessorLoader.TP_InWorld.Count; i++) {
                TileProcessor tp = TileProcessorLoader.TP_InWorld[i];
                if (!tp.Active || !tp.InScreen || tp.ID != creativePipelineID) {
                    continue;
                }

                if (tp is not CreativeUEPipelineTP creativePipeline) {
                    continue;
                }

                creativePipelines.Add(creativePipeline);
            }
        }
        //渲染钩子放在这个位置，可以确保图层正确
        public override bool PreTileDrawEverything(SpriteBatch spriteBatch) {
            if (creativePipelines.Count > 0) {
                spriteBatch.End();
            }

            DoRender((tp) => tp.HideRenderDraw(spriteBatch), spriteBatch);

            if (creativePipelines.Count > 0) {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return true;
        }
        //渲染钩子放在这个位置，可以确保图层正确
        public override void PostDrawEverything(SpriteBatch spriteBatch) => DoRender((tp) => tp.RenderDraw(spriteBatch), spriteBatch);
        /// <summary>
        /// 渲染上帝管道的RT效果
        /// </summary>
        /// <param name="spriteBatch"></param>
        /// <param name="graphicsDevice"></param>
        /// <param name="screenSwap"></param>
        internal static void DoRender(Action<CreativeUEPipelineTP> func, SpriteBatch spriteBatch) {
            if (creativePipelines.Count == 0) {
                return;
            }

            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            RenderTarget2D screenSwap = RenderHandleLoader.ScreenSwap;

            //切换到原版的中间屏幕上，缓存原始画面
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            //切换成中间屏幕，开始绘制特效
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent); //改为透明，避免干扰
            //世界画布常用的开启设置
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            //管道中间的纹理在这里被绘制上去，用作后面着色器处理的根据
            foreach (var creativePipeline in creativePipelines) {
                func.Invoke(creativePipeline);
            }
            spriteBatch.End();

            //切换到最终的实际屏幕对象上，开始覆盖绘制，这里先清空准备后续的覆盖
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            //这里先把先前缓存的原始画面绘制上来作为底图
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            //开始特效绘制，设置着色器处理中间屏幕上面的管道纹理
            StarsShader.CurrentTechnique.Passes[0].Apply();
            StarsShader.Parameters["m"].SetValue(0.08f);
            StarsShader.Parameters["n"].SetValue(0.01f);
            StarsShader.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly);//传入游戏时间
            StarsShader.Parameters["worldSize"].SetValue(Main.ScreenSize.ToVector2());//传入屏幕分辨率
            //绘制特效覆盖上底图
            Main.spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
    }
}
