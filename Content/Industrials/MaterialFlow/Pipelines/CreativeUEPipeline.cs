using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
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
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_MiracleMatter, CWRID.Item_ShadowspecBar)) {
                return;
            }
            //原吃无尽锭，现奇迹物质+暗影耀斑锭
            CreateRecipe()
                .AddIngredient<UEPipeline>()
                .AddIngredient(CWRID.Item_MiracleMatter)
                .AddIngredient(CWRID.Item_ShadowspecBar, 5)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
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

    /// <summary>创造管道 TP，恒满电；能量层走 CreativePipelineFlow，复用基础管几何/合批</summary>
    internal class CreativeUEPipelineTP : UEPipelineTP
    {
        public override int TargetTileID => ModContent.TileType<CreativeUEPipelineTile>();
        public override int TargetItem => ModContent.ItemType<CreativeUEPipeline>();
        public override Color BaseColor => Color.MediumPurple;
        /// <summary>恒满电</summary>
        public override void UpdateMachine() {
            base.UpdateMachine();
            IsNetworkPowered = true;
            if (MachineData != null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }
    }

    /// <summary>创造管道能量合批，CreativePipelineFlow，同 <see cref="UEPipelineEnergyDraw"/></summary>
    internal class CreativeUEPipelineEnergyDraw : GlobalTileProcessor
    {
        public override bool PreTileDrawEverything(SpriteBatch spriteBatch) {
            MachineShaderBatch.DrawBatch(spriteBatch, EffectLoader.CreativePipelineFlow, SamplerState.PointClamp,
                static tp => tp.GetType() == typeof(CreativeUEPipelineTP),
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
