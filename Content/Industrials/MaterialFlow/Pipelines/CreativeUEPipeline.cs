using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Graphics;
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
        public override int TargetTileID => ModContent.TileType<CreativeUEPipelineTile>();
        public override int TargetItem => ModContent.ItemType<CreativeUEPipeline>();
        public override Color BaseColor => Color.Purple;
        /// <summary>
        /// 重写机器更新逻辑
        /// </summary>
        public override void UpdateMachine() {
            //首先，调用基类（UEPipelineInputTP）的更新方法
            //这样做可以完全复用它所有复杂的连接判断、状态更新（是否为拐角、十字等）逻辑，无需重写
            base.UpdateMachine();

            //核心逻辑：在所有状态更新完毕后，强行将自身的电力值设置为最大值
            //这使得它在任何时候都是一个满额的电力输出源
            if (MachineData != null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }
    }
}
