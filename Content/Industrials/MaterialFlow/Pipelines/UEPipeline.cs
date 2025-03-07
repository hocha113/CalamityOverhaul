using CalamityOverhaul.Content.Industrials.Generator;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    internal class UEPipeline : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/UEPipeline";
        public static int ID { get; private set; }
        public ref float UEValue => ref Item.CWR().UEValue;
        public override void SetStaticDefaults() => ID = Type;
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(0, 2, 0, 0);
            Item.rare = ItemRarityID.Quest;
            Item.createTile = ModContent.TileType<UEPipelineTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 20;
        }
    }

    internal class UEPipelineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/UEPipeline";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<UEPipeline>());
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
        public override void MouseOver(int i, int j) {
            Player localPlayer = Main.LocalPlayer;
            localPlayer.cursorItemIconEnabled = true;
            localPlayer.cursorItemIconID = ModContent.ItemType<UEPipeline>();
            localPlayer.noThrow = 2;
        }
        public override bool CanDrop(int i, int j) => false;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    }

    internal class SideState(Point16 point16)
    {
        internal Point16 Position;
        internal readonly Point16 Offset = point16;
        /// <summary>
        /// 对于外部的物块数据
        /// </summary>
        internal Tile externalTile;
        /// <summary>
        /// 对于外部的TP实体
        /// </summary>
        internal TileProcessor externalTP;
        /// <summary>
        /// 自身的核心TP实体
        /// </summary>
        internal UEPipelineTP coreTP;
        /// <summary>
        /// 链接ID
        /// </summary>
        internal int linkID = 0;
        internal bool canDraw;
        /// <summary>
        /// 更新逻辑
        /// </summary>
        public void Update() {
            // 初始化
            externalTile = default;
            externalTP = null;
            linkID = 0;
            canDraw = true;
            // 获取当前 Tile 和相邻的 TileProcessor
            externalTile = Framing.GetTileSafely(Position + Offset);

            if (externalTile.HasTile 
                && VaultUtils.SafeGetTopLeft(Position + Offset, out var point) 
                && TileProcessorLoader.ByPositionGetTP(point, out externalTP)) {
                // 如果相邻的 TileProcessor 是发电机
                if (externalTP is BaseGeneratorTP baseGeneratorTP) {
                    // 如果发电机的 UEvalue 大于 0，从发电机到管道传递值
                    if (baseGeneratorTP.MachineData.UEvalue > 0 && coreTP.MachineData.UEvalue < 20) {
                        baseGeneratorTP.MachineData.UEvalue--;  // 从发电机减去能量
                        coreTP.MachineData.UEvalue++;      // 给管道增加能量
                    }
                    linkID = 1;
                }

                // 如果有能量传递的需求，且相邻的是管道
                if (externalTP is UEPipelineTP battery) {
                    // 计算总能量
                    float totalUE = coreTP.MachineData.UEvalue + battery.MachineData.UEvalue;

                    // 计算均衡后应该有多少能量
                    float averageUE = totalUE / 2;

                    // 直接平衡
                    coreTP.MachineData.UEvalue = averageUE;
                    battery.MachineData.UEvalue = averageUE;

                    if (battery.Decussation || battery.Turning) {
                        canDraw = false;
                    }

                    linkID = 2;
                }

                //如果挨着的是电池
                else if (externalTP is BaseBattery baseBattery) {
                    if (coreTP.MachineData.UEvalue > 0 && baseBattery.MachineData.UEvalue < baseBattery.MaxUEValue) {
                        baseBattery.MachineData.UEvalue++;
                        coreTP.MachineData.UEvalue--;
                    }
                    
                    linkID = 3;
                }
            }

            if (linkID == 0) {
                canDraw = false;
            }
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (coreTP == null || coreTP.MachineData == null || externalTP == null) {
                return;
            }

            Vector2 drawPos = coreTP.PosInWorld + Offset.ToVector2() * 16 - Main.screenPosition;
            float drawRot = Offset.ToVector2().ToRotation();

            Vector2 orig = UEPipelineTP.PipelineChannel.Size() / 2;
            Color color = Color.White * (coreTP.MachineData.UEvalue / 10f);

            spriteBatch.Draw(UEPipelineTP.PipelineChannel.Value, drawPos + orig, null, color
                , drawRot, orig, 1, SpriteEffects.None, 0);

            color = Lighting.GetColor(Position.ToPoint());
            spriteBatch.Draw(UEPipelineTP.PipelineChannelSide.Value, drawPos + orig, null, color
                , drawRot, orig, 1, SpriteEffects.None, 0);
        }
    }

    internal class UEPipelineTP : MachineTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<UEPipelineTile>();
        public static Asset<Texture2D> Pipeline { get; private set; }
        public static Asset<Texture2D> PipelineSide { get; private set; }
        public static Asset<Texture2D> PipelineCross { get; private set; }
        public static Asset<Texture2D> PipelineCrossSide { get; private set; }
        public static Asset<Texture2D> PipelineChannel { get; private set; }
        public static Asset<Texture2D> PipelineChannelSide { get; private set; }
        internal List<SideState> SideState { get; private set; }
        internal bool Turning { get; private set; }
        internal bool Decussation { get; private set; }
        public override int TargetItem => ModContent.ItemType<UEPipeline>();
        public override float MaxUEValue => 20;
        void ICWRLoader.LoadAsset() {
            PipelineChannel = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineChannel");
            PipelineChannelSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineChannelSide");
            Pipeline = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipeline");
            PipelineSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineSide");
            PipelineCross = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineCross");
            PipelineCrossSide = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/UEPipelineCrossSide");
        }
        void ICWRLoader.UnLoadData() {
            PipelineChannel = null;
            PipelineChannelSide = null;
            Pipeline = null;
            PipelineSide = null;
        }

        public override void SetMachine() {
            SideState = new List<SideState>() {
            new (new Point16(0, -1)),//上0
            new (new Point16(0, 1)),//下1
            new (new Point16(-1, 0)),//左2
            new (new Point16(1, 0))//右3
            };
        }

        public override void Update() {
            foreach (var side in SideState) {
                side.coreTP = this;
                side.Position = Position;
                side.Update();
            }

            Turning = false;
            Decussation = false;

            if (SideState[0].linkID == 2 && (SideState[2].linkID == 2 || SideState[3].linkID == 2)) {
                Turning = true;//这种情况判定为拐角
            }
            if (SideState[1].linkID == 2 && (SideState[2].linkID == 2 || SideState[3].linkID == 2)) {
                Turning = true;//这种情况判定为拐角
            }

            if (SideState[0].linkID == 2 && SideState[1].linkID == 2 && SideState[2].linkID == 2 && SideState[3].linkID == 2) {
                Decussation = true;//这种情况判定为十字交叉
            }
        }

        public override void PreTileDraw(SpriteBatch spriteBatch) {
            if (Decussation) {
                return;//十字交叉下不能进行边缘绘制
            }
            foreach (var side in SideState) {
                if (side.canDraw && side.linkID != 2) {//链接其他时绘制在后面
                    side.Draw(spriteBatch);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!Decussation) {//十字交叉下不能进行边缘绘制
                foreach (var side in SideState) {
                    if (side.canDraw && side.linkID == 2) {//链接管道自己时绘制在前面
                        side.Draw(spriteBatch);
                    }
                }
            }
            

            Vector2 drawPos = PosInWorld - Main.screenPosition;
            int linkCount = 0;
            foreach (var side in SideState) {
                if (side.linkID != 0) {
                    linkCount++;
                }
            }

            if (Decussation) {
                drawPos = CenterInWorld - Main.screenPosition;
                spriteBatch.Draw(PipelineCross.Value, drawPos, null, Color.White * (MachineData.UEvalue / 10f), 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(PipelineCrossSide.Value, drawPos, null, Lighting.GetColor(Position.ToPoint()), 0, PipelineCross.Size() / 2, 1, SpriteEffects.None, 0);
                return;
            }

            if (Turning) {
                spriteBatch.Draw(Pipeline.Value, drawPos.GetRectangle(Size), Color.White * (MachineData.UEvalue / 10f));
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), Lighting.GetColor(Position.ToPoint()));
                return;
            }

            if (linkCount != 2) {
                spriteBatch.Draw(Pipeline.Value, drawPos.GetRectangle(Size), Color.White * (MachineData.UEvalue / 10f));
                spriteBatch.Draw(PipelineSide.Value, drawPos.GetRectangle(Size), Lighting.GetColor(Position.ToPoint()));
            }

            //if (GeneratorData != null) {
            //    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, ((int)GeneratorData.UEvalue).ToString()
            //        , drawPos.X + 6, drawPos.Y - 8, Color.White, Color.Black, new Vector2(0.1f), 0.5f);
            //}
        }
    }
}
