using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs;
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

    /// <summary>
    /// 创造管道 TP，恒满电。完全复用基础管道的几何/外壳/能量遮罩与统一合批管线，
    /// 能量层经 <see cref="CreativeUEPipelineEnergyDraw"/> 走 CreativePipelineFlow 着色器（紫色宇宙能量）。
    /// 不再使用旧的 RT + 星空着色器方案，避免光照耦合与多余开销
    /// </summary>
    internal class CreativeUEPipelineTP : UEPipelineTP
    {
        public override int TargetTileID => ModContent.TileType<CreativeUEPipelineTile>();
        public override int TargetItem => ModContent.ItemType<CreativeUEPipeline>();
        public override Color BaseColor => Color.MediumPurple;
        /// <summary>恒满电，等同能量源</summary>
        public override void UpdateMachine() {
            base.UpdateMachine();
            IsNetworkPowered = true;
            if (MachineData != null) {
                MachineData.UEvalue = MaxUEValue;
            }
        }
    }

    /// <summary>
    /// 创造管道能量层合批绘制：屏内所有创造管道共用一次 CreativePipelineFlow 着色器批次
    /// （墙后物块前），金属外壳由各 TP 在其上叠加。与基础管道 <see cref="UEPipelineEnergyDraw"/> 同套管线
    /// </summary>
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
