using Microsoft.Xna.Framework.Graphics;
using CalamityOverhaul.Content.Items.Materials;
using System;
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
    /// <summary>液体储罐:液体网络的储能件</summary>
    internal class FluidTank : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/FluidTank";
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
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Green;
            Item.createTile = ModContent.TileType<FluidTankTile>();
        }

        public override void AddRecipes() {
            CreateRecipe().
            AddIngredient<CircuitBoard>(12).
            AddIngredient(ItemID.Glass, 30).
            AddRecipeGroup(RecipeGroupID.IronBar, 15).
            AddTile(TileID.Anvils).
            Register();

        }
    }

    internal class FluidTankTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/FluidTankTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(58, 96, 118), VaultUtils.GetLocalizedItemName<FluidTank>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(2, 3);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 20];
            TileObjectData.newTile.LavaDeath = false;

            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.mouseInterface = true;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<FluidTank>();
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Glass);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;
    }

    /// <summary>
    /// 液体储罐TP:液体网的储能件,无 UE 角色(不接 UE 管网)。
    /// 被动均衡:液管按充盈比例差与之双向流动;罐体观察窗透出液面高度。
    /// v1 取舍:拆罐时罐内液体流失,不随物品往返
    /// </summary>
    internal class FluidTankTP : MachineTP, IFluidContainer
    {
        public override int TargetTileID => ModContent.TileType<FluidTankTile>();
        public override int TargetItem => ModContent.ItemType<FluidTank>();
        public override float MaxUEValue => 0;

        #region 液体容器契约
        public int FluidType { get; set; }
        public int FluidAmount { get; set; }
        public virtual int FluidCapacity => 32 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Storage;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        //液面显示的平滑比例
        private float displayRatio;
        private bool ratioInited;
        //观察窗像素矩形(相对物块左上角),对齐罐体贴图玻璃窗
        internal virtual Vector2 ChamberMin => new(18f, 10f);
        internal virtual Vector2 ChamberMax => new(44f, 36f);

        //充放活跃度 0..1(液面涌动与亮线增强),动画时钟(待机冻结)
        private float activityVis;
        private float animTime;
        private int lastFluidAmount = -1;

        public override void SetMachine() {
            Efficiency = 0;//无 UE 角色,不参与 UE 均衡
        }

        public override void UpdateMachine() {
            float ratio = MathHelper.Clamp(FluidAmount / (float)FluidCapacity, 0f, 1f);
            if (!ratioInited) {
                displayRatio = ratio;
                lastFluidAmount = FluidAmount;
                ratioInited = true;
            }
            else {
                displayRatio = MathHelper.Lerp(displayRatio, ratio, 0.1f);
            }

            //充放活跃度:液量变化抬升,静止衰减
            int delta = Math.Abs(FluidAmount - lastFluidAmount);
            lastFluidAmount = FluidAmount;
            float target = MathHelper.Clamp(delta / 12f, 0f, 1f);
            activityVis = MathHelper.Lerp(activityVis, target, delta > 0 ? 0.3f : 0.05f);

            //动画时钟:待机(Disabled)时基类跳过 UpdateMachine,波/泡自然冻住——"断电静止"的状态语言
            animTime += 1f / 60f;
        }

        #region 存档与同步:液体字段追加在基类之后
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write((byte)FluidType);
            data.Write(FluidAmount);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            FluidType = reader.ReadByte();
            FluidAmount = reader.ReadInt32();
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["FluidType"] = FluidType;
            tag["FluidAmount"] = FluidAmount;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            FluidType = tag.TryGet("FluidType", out int type) ? type : LiquidID.Water;
            FluidAmount = tag.TryGet("FluidAmount", out int amount) ? amount : 0;
        }
        #endregion

        /// <summary>
        /// 液面画在物块层之上、观察窗矩形之内(罐体玻璃为实绘,液体盖在玻璃上,液线以上的玻璃高光仍可见):
        /// 波列液面+高光线+按液型气泡(水快泡/岩浆熔泡黑壳/蜜懒泡/微光星尘),
        /// 液面升降平滑插值,充放液时涌动增强
        /// </summary>
        public override void Draw(SpriteBatch spriteBatch) {
            Vector2 basePos = PosInWorld - Main.screenPosition;
            Rectangle chamber = new(
                (int)(basePos.X + ChamberMin.X),
                (int)(basePos.Y + ChamberMin.Y),
                (int)(ChamberMax.X - ChamberMin.X),
                (int)(ChamberMax.Y - ChamberMin.Y));

            FluidVFX.DrawLiquidWindow(spriteBatch, chamber, FluidType,
                MathHelper.Clamp(displayRatio, 0f, 1f), animTime, activityVis, WhoAmI + 7);

            //岩浆/微光的暗处可读性:液窗对场景补一点点光
            FluidStyle style = FluidVFX.GetStyle(FluidType);
            if (style.Glow > 0.3f && displayRatio > 0.02f) {
                Color c = style.Main;
                float k = style.Glow * 0.30f * MathHelper.Clamp(displayRatio * 2f, 0f, 1f);
                Lighting.AddLight(CenterInWorld, c.R / 255f * k, c.G / 255f * k, c.B / 255f * k);
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (!HoverTP) {
                return;
            }
            FluidHelper.DrawFluidBar(this, this, 20);
        }
    }
}
