using CalamityOverhaul.Content.Industrials.Generator;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow.Pipelines
{
    internal class BasicPipeline : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/Pipeline";
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
            Item.createTile = ModContent.TileType<BasicPipelineTile>();
        }
    }

    internal class BasicPipelineTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/Pipeline";
        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.None, 0, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);
        }
        public override void MouseOver(int i, int j) {
            Player localPlayer = Main.LocalPlayer;
            localPlayer.cursorItemIconEnabled = true;
            localPlayer.cursorItemIconID = ModContent.ItemType<BasicPipeline>();
            localPlayer.noThrow = 2;
        }
    }

    internal class SideState(Point16 point16)
    {
        internal Point16 Position;
        internal readonly Point16 Offset = point16;
        internal Tile Tile;
        internal TileProcessor TileProcessor;
        internal BasicPipelineTP coreTP;
        public void Update() {
            // 初始化
            Tile = default;
            TileProcessor = null;

            // 获取当前 Tile 和相邻的 TileProcessor
            Tile = Framing.GetTileSafely(Position + Offset);

            if (Tile.HasTile && VaultUtils.SafeGetTopLeft(Position.X + Offset.X, Position.Y + Offset.Y, out var point)) {
                // 寻找相邻的 TileProcessor
                foreach (var tp in TileProcessorLoader.TP_InWorld) {
                    if (tp.Position != point) {
                        continue;
                    }
                    TileProcessor = tp;
                }
            }

            // 如果相邻的 TileProcessor 是发电机
            if (TileProcessor is BaseGeneratorTP baseGeneratorTP) {
                // 如果发电机的 UEvalue 大于 0，从发电机到管道传递值
                if (baseGeneratorTP.GeneratorData.UEvalue > 0) {
                    baseGeneratorTP.GeneratorData.UEvalue--;  // 从发电机减去能量
                    coreTP.GeneratorData.UEvalue++;      // 给管道增加能量
                }
            }

            // 如果有能量传递的需求，且相邻的是管道
            if (coreTP.GeneratorData.UEvalue > 0) {
                if (TileProcessor is BasicPipelineTP basicPipelineTP) {
                    if (basicPipelineTP.GeneratorData.UEvalue < coreTP.GeneratorData.UEvalue) {
                        basicPipelineTP.GeneratorData.UEvalue++;
                        coreTP.GeneratorData.UEvalue--;
                    }
                }//如果挨着的是电池
                else if (TileProcessor is BaseBattery baseBattery) {
                    baseBattery.GeneratorData.UEvalue++;
                    coreTP.GeneratorData.UEvalue--;
                }
            }
        }
    }

    internal class BasicPipelineTP : TileProcessor, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<BasicPipelineTile>();
        public static Asset<Texture2D> PipelineAsset;
        void ICWRLoader.LoadAsset() => PipelineAsset = CWRUtils.GetT2DAsset(CWRConstant.Asset + "MaterialFlow/Pipeline");
        void ICWRLoader.UnLoadData() => PipelineAsset = null;
        internal List<SideState> SideState;
        internal GeneratorData GeneratorData;
        internal bool links;
        public override void SetProperty() {
            SideState = new List<SideState>() {
            new SideState(new Point16(0, -1)),
            new SideState(new Point16(0, 1)),
            new SideState(new Point16(-1, 0)),
            new SideState(new Point16(1, 0))
            };
            GeneratorData = new GeneratorData();
        }
        public override void Update() {
            foreach (var side in SideState) {
                side.coreTP = this;
                side.Position = Position;
                side.Update();
            }

            int linkCount = 0;
            foreach (var side in SideState) {
                if (side.TileProcessor != null) {
                    linkCount++;
                }
            }

            links = linkCount >= 2;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (links) {
                Main.spriteBatch.Draw(PipelineAsset.Value, PosInWorld - Main.screenPosition
                , null, Color.Blue, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            }
            if (GeneratorData != null) {
                Vector2 drawPos = PosInWorld - Main.screenPosition + new Vector2(0, -6);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, GeneratorData.UEvalue.ToString()
                    , drawPos.X, drawPos.Y, Color.White, Color.Black, new Vector2(0.1f), 0.5f);
            }
        }
    }
}
