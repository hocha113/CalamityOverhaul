﻿using CalamityOverhaul.Content.UIs.SupertableUIs;
using InnoVault.RenderHandles;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
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
            Item.CWR().OmigaSnyContent = SupertableRecipeDate.FullItems_CreativePipeline;
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
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.MagicMirror); // 紫色的粒子效果
            return false;
        }

        public override void MouseOver(int i, int j) {
            Player localPlayer = Main.LocalPlayer;
            localPlayer.cursorItemIconEnabled = true;
            localPlayer.cursorItemIconID = ModContent.ItemType<CreativeUEPipeline>();
            localPlayer.noThrow = 2;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

    /// <summary>
    /// 无限电力的创造管道的逻辑核心
    /// </summary>
    internal class CreativeUEPipelineTP : UEPipelineInputTP
    {
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        public static Asset<Texture2D> PipelineCreative { get; private set; }
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        public static Asset<Texture2D> PipelineCreativeCorner { get; private set; }
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        public static Asset<Texture2D> PipelineCreativeCross { get; private set; }
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        public static Asset<Texture2D> PipelineCreativeChannel { get; private set; }
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        public static Asset<Texture2D> PipelineCreativeThreeCrutches { get; private set; }
        public override int TargetTileID => ModContent.TileType<CreativeUEPipelineTile>();
        public override int TargetItem => ModContent.ItemType<CreativeUEPipeline>();
        public override Color BaseColor => Color.Purple;
        /// <summary>
        /// 重写机器更新逻辑
        /// </summary>
        public override void UpdateMachine() {
            base.UpdateMachine();
            if (MachineData != null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }

        internal void HideRenderDraw(SpriteBatch spriteBatch) {
            if (Decussation) {
                return;//十字交叉下不能进行边缘绘制
            }
            foreach (var side in SideState) {
                if (side.canDraw && side.linkID != 2) {//链接其他时绘制在后面
                    if (side.coreTP == null || side.coreTP.MachineData == null || side.externalTP == null) {
                        return;
                    }

                    Vector2 drawPos2 = side.coreTP.PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
                    float drawRot = side.Offset.ToVector2().ToRotation();

                    Vector2 orig = PipelineCreativeChannel.Size() / 2;
                    Color color = side.coreTP.BaseColor * (side.coreTP.MachineData.UEvalue / 10f);

                    spriteBatch.Draw(PipelineCreativeChannel.Value, drawPos2 + orig, null, color
                        , drawRot, orig, 1, SpriteEffects.None, 0);
                }
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Decussation) {
                return;//十字交叉下不能进行边缘绘制
            }
            foreach (var side in SideState) {
                if (side.canDraw && side.linkID != 2) {//链接其他时绘制在后面
                    if (side.coreTP == null || side.coreTP.MachineData == null || side.externalTP == null) {
                        return;
                    }

                    Vector2 drawPos2 = side.coreTP.PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
                    float drawRot = side.Offset.ToVector2().ToRotation();

                    Vector2 orig = PipelineCreativeChannel.Size() / 2;
                    Color color = Lighting.GetColor(Position.ToPoint());
                    spriteBatch.Draw(PipelineChannelSide.Value, drawPos2 + orig, null, color
                        , drawRot, orig, 1, SpriteEffects.None, 0);
                }
            }
        }

        internal void RenderDraw(SpriteBatch spriteBatch) {
            if (!Decussation) {//十字交叉下不能进行边缘绘制
                foreach (var side in SideState) {
                    if (side.canDraw && side.linkID == 2) {//链接管道自己时绘制在前面
                        if (side.coreTP == null || side.coreTP.MachineData == null || side.externalTP == null) {
                            return;
                        }

                        Vector2 drawPos2 = side.coreTP.PosInWorld + side.Offset.ToVector2() * 16 - Main.screenPosition;
                        float drawRot = side.Offset.ToVector2().ToRotation();

                        Vector2 orig = PipelineCreativeChannel.Size() / 2;
                        Color color = side.coreTP.BaseColor * (side.coreTP.MachineData.UEvalue / 10f);

                        spriteBatch.Draw(PipelineCreativeChannel.Value, drawPos2 + orig, null, color
                            , drawRot, orig, 1, SpriteEffects.None, 0);
                    }
                }
            }

            Vector2 drawPos = PosInWorld - Main.screenPosition;

            if (ThreeCrutchesID >= 0) {
                Rectangle rectangle = PipelineCreativeThreeCrutches.Value.GetRectangle(ThreeCrutchesID, 4);
                spriteBatch.Draw(PipelineCreativeThreeCrutches.Value, drawPos, rectangle, BaseColor * (MachineData.UEvalue / 10f), 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                return;
            }

            if (Decussation) {
                drawPos = CenterInWorld - Main.screenPosition;
                spriteBatch.Draw(PipelineCreativeCross.Value, drawPos, null, BaseColor * (MachineData.UEvalue / 10f), 0, PipelineCreativeCross.Size() / 2, 1, SpriteEffects.None, 0);
                return;
            }

            if (Turning) {
                Rectangle rectangle = PipelineCreativeCorner.Value.GetRectangle(TurningID, 4);
                spriteBatch.Draw(PipelineCreativeCorner.Value, drawPos, rectangle, BaseColor * (MachineData.UEvalue / 10f), 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                return;
            }

            int linkCount = 0;
            int linkCount2 = 0;
            foreach (var side in SideState) {
                if (side.linkID != 0) {
                    linkCount++;
                    if (side.linkID != 2) {
                        linkCount2++;
                    }
                }
            }

            if (linkCount != 2 || linkCount2 == 2) {
                spriteBatch.Draw(PipelineCreative.Value, drawPos.GetRectangle(Size), BaseColor * (MachineData.UEvalue / 10f));
            }
        }
    }

    internal class CreativePipelineGlobalDraw : GlobalTileProcessor
    {
        [VaultLoaden(CWRConstant.Effects)]
        internal static Effect StarsShader { get; set; }
        private readonly static List<CreativeUEPipelineTP> creativePipelines = [];
        public override bool PreSingleInstanceUpdate(TileProcessor tileProcessor) {
            creativePipelines.Clear();
            return true;
        }
        //在逻辑更新中统一处理管道实例的添加
        public override void SingleInstanceUpdate(TileProcessor tileProcessor) {
            int id = TileProcessorLoader.GetModuleID<CreativeUEPipelineTP>();
            for (int i = 0; i < TileProcessorLoader.TP_InWorld.Count; i++) {
                TileProcessor tp = TileProcessorLoader.TP_InWorld[i];
                if (!tp.Active || !tp.InScreen || tp.ID != id) {
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
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            //开始特效绘制，设置着色器处理中间屏幕上面的管道纹理
            StarsShader.CurrentTechnique.Passes[0].Apply();
            StarsShader.Parameters["m"].SetValue(0.08f);
            StarsShader.Parameters["n"].SetValue(0.01f);
            StarsShader.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly); // 传入游戏时间
            StarsShader.Parameters["worldSize"].SetValue(Main.ScreenSize.ToVector2()); // 传入屏幕分辨率
            //绘制特效覆盖上底图
            Main.spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            Main.spriteBatch.End();
        }
    }
}
