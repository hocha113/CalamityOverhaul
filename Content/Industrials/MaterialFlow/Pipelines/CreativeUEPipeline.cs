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
    /// <summary>创造模式无限电力管道物品</summary>
    internal class CreativeUEPipeline : BasePipelineItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/CreativePipelineItem";

        public override int CreateTileID => ModContent.TileType<CreativeUEPipelineTile>();

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Red;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_CreativePipeline;
        }
    }

    /// <summary>创造模式无限电力管道图块</summary>
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
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.MagicMirror);//紫色粒子
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

    /// <summary>创造管道 TP，恒满电</summary>
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
        /// <summary>恒满电，等同能量源</summary>
        public override void UpdateMachine() {
            base.UpdateMachine();
            IsNetworkPowered = true;
            if (MachineData != null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }

        /// <summary>创造管道连接臂绘制</summary>
        private void DrawCreativeArm(PipelineSideState side, SpriteBatch spriteBatch, bool drawSide) {
            if (side.coreTP == null || side.externalTP == null) {
                return;
            }

            Vector2 drawPos = side.coreTP.PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = side.Offset.ToVector2().ToRotation();
            Vector2 orig = PipelineCreativeChannel.Size() / 2;

            if (!drawSide) {
                //创造管道能量层
                spriteBatch.Draw(PipelineCreativeChannel.Value, drawPos + orig, null, BaseColor * (MachineData.UEvalue / 10f), drawRot, orig, 1, SpriteEffects.None, 0);
            }
            else {
                //基础管道光照层
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
            //管道间连接臂
            if (Shape != PipelineShape.Cross) {
                foreach (var side in SideState) {
                    if (side.canDraw && side.LinkType == PipelineLinkType.Pipeline) {
                        DrawCreativeArm(side, spriteBatch, false);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            Color energyColor = BaseColor * (MachineData.UEvalue / 10f);
            //按形状用创造贴图绘制
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
        //单例更新前清空列表
        public override bool PreSingleInstanceUpdate(TileProcessor tileProcessor) {
            if (tileProcessor.ID != creativePipelineID) {
                return true;//仅创造管道单例
            }

            creativePipelines.Clear();
            return true;
        }
        //单例更新中收集实例
        public override void SingleInstanceUpdate(TileProcessor tileProcessor) {
            if (tileProcessor.ID != creativePipelineID) {
                return;//仅创造管道单例
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
        //PreDraw 分层：能量臂在遮罩下
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
        //PostDraw 分层：本体与 StarsShader 合成
        public override void PostDrawEverything(SpriteBatch spriteBatch) => DoRender((tp) => tp.RenderDraw(spriteBatch), spriteBatch);

        /// <summary>RT 缓存 + StarsShader 合成，防丢屏</summary>
        internal static void DoRender(Action<CreativeUEPipelineTP> func, SpriteBatch spriteBatch) {
            if (creativePipelines.Count == 0) {
                return;
            }

            if (RenderHandleLoader.ScreenSwap == null || StarsShader == null || Main.screenTarget == null || Main.screenTargetSwap == null) {
                return;
            }

            if (Lighting.Mode == Terraria.Graphics.Light.LightMode.Retro || Lighting.Mode == Terraria.Graphics.Light.LightMode.Trippy) {
                return;
            }

            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            RenderTarget2D screenSwap = RenderHandleLoader.ScreenSwap;

            //缓存 screenTarget 到 swap
            graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            //RT 上绘制管道遮罩
            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                , DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            //遮罩供着色器采样
            foreach (var creativePipeline in creativePipelines) {
                func.Invoke(creativePipeline);
            }
            spriteBatch.End();

            //回写 screenTarget：底图 + shader
            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.spriteBatch.Draw(Main.screenTargetSwap, Vector2.Zero, Color.White);
            Main.spriteBatch.End();

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            StarsShader.CurrentTechnique.Passes[0].Apply();
            StarsShader.Parameters["m"].SetValue(0.08f);
            StarsShader.Parameters["n"].SetValue(0.01f);
            StarsShader.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly);
            StarsShader.Parameters["worldSize"].SetValue(Main.ScreenSize.ToVector2());
            Main.spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
    }
}
