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
    /// <summary>液体储罐:液体网络的储能件(占位贴图沿用热能电池,待专属罐体美术)</summary>
    internal class FluidTank : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBattery";
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
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 12).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 12).
                AddIngredient(ItemID.Glass, 30).
                AddRecipeGroup(RecipeGroupID.IronBar, 15).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.Glass, 30).
                AddRecipeGroup(RecipeGroupID.IronBar, 15).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    internal class FluidTankTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/ThermalBatteryTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;

            AddMapEntry(new Color(58, 96, 118), VaultUtils.GetLocalizedItemName<FluidTank>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 4;
            TileObjectData.newTile.Origin = new Point16(1, 1);
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16, 16];
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
        public int FluidCapacity => 32 * FluidHelper.UnitsPerTile;
        public FluidNetRole FluidRole => FluidNetRole.Storage;
        public bool CanAcceptFluid(int liquidId) => FluidHelper.DefaultCanAccept(this, liquidId);
        #endregion

        //液面显示的平滑比例
        private float displayRatio;
        private bool ratioInited;
        //观察窗本地 UV 范围,对齐占位罐体贴图的透明窗
        private static readonly Vector2 ChamberMin = new(0.21f, 0.20f);
        private static readonly Vector2 ChamberMax = new(0.71f, 0.82f);

        public override void SetMachine() {
            Efficiency = 0;//无 UE 角色,不参与 UE 均衡
        }

        public override void UpdateMachine() {
            float ratio = MathHelper.Clamp(FluidAmount / (float)FluidCapacity, 0f, 1f);
            if (!ratioInited) {
                displayRatio = ratio;
                ratioInited = true;
            }
            else {
                displayRatio = MathHelper.Lerp(displayRatio, ratio, 0.1f);
            }
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

        /// <summary>液面画在物块层之下,从罐体观察窗透出;顶缘亮线示意液面</summary>
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            float ratio = MathHelper.Clamp(displayRatio, 0f, 1f);
            if (ratio <= 0.005f) {
                return;
            }

            Vector2 basePos = PosInWorld - Main.screenPosition;
            float chamberLeft = basePos.X + ChamberMin.X * Width;
            float chamberTop = basePos.Y + ChamberMin.Y * Height;
            float chamberWidth = (ChamberMax.X - ChamberMin.X) * Width;
            float chamberHeight = (ChamberMax.Y - ChamberMin.Y) * Height;

            float fluidHeight = chamberHeight * ratio;
            float fluidTop = chamberTop + chamberHeight - fluidHeight;

            Color body = FluidHelper.GetColor(FluidType);
            Texture2D px = VaultAsset.placeholder2.Value;
            //液体本体(微暗)与液面亮线
            spriteBatch.Draw(px, new Rectangle((int)chamberLeft, (int)fluidTop, (int)chamberWidth, (int)fluidHeight)
                , new Color((int)(body.R * 0.8f), (int)(body.G * 0.8f), (int)(body.B * 0.8f)));
            spriteBatch.Draw(px, new Rectangle((int)chamberLeft, (int)fluidTop, (int)chamberWidth, 2)
                , Color.Lerp(body, Color.White, 0.35f));
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            if (!HoverTP) {
                return;
            }
            FluidHelper.DrawFluidBar(this, this, 20);
        }
    }
}
