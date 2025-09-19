using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.UIs;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    internal class Spectrometer : ModItem
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/Spectrometer";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(10, 3));
        }
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
            Item.value = Item.buyPrice(0, 2, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<SpectrometerTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 800;
        }
    }

    internal class SpectrometerTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "ElectricPowers/SpectrometerTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(87, 72, 81), VaultUtils.GetLocalizedItemName<Spectrometer>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(1, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override bool CreateDust(int i, int j, ref int type) {
            Dust.NewDust(new Vector2(i, j) * 16f, 16, 16, DustID.Electric);
            return false;
        }

        public override bool CanDrop(int i, int j) => false;

        public override bool RightClick(int i, int j) {
            if (TileProcessorLoader.AutoPositionGetTP(i, j, out SpectrometerTP spectrometer)) {
                spectrometer.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<Spectrometer>());

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out SpectrometerTP spectrometer)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += spectrometer.frame * 18 * 3;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    internal abstract class BaseDyeTP : BaseBattery
    {
        internal Item DyeSlotItem = new();
        internal Item BeDyedItem = new();
        internal Item ResultDyedItem = new();
        public abstract BaseDyeMachineUI DyeMachineUI { get; }
        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            DyeSlotItem ??= new();
            tag["DyeSlotItem"] = ItemIO.Save(DyeSlotItem);
            BeDyedItem ??= new();
            tag["BeDyedItem"] = ItemIO.Save(BeDyedItem);
            ResultDyedItem ??= new();
            tag["ResultDyedItem"] = ItemIO.Save(ResultDyedItem);
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet<TagCompound>("DyeSlotItem", out var value)) {
                DyeSlotItem = ItemIO.Load(value);
            }
            else {
                DyeSlotItem = new();
            }

            if (tag.TryGet<TagCompound>("BeDyedItem", out var value2)) {
                BeDyedItem = ItemIO.Load(value2);
            }
            else {
                BeDyedItem = new();
            }

            if (tag.TryGet<TagCompound>("ResultDyedItem", out var value3)) {
                ResultDyedItem = ItemIO.Load(value3);
            }
            else {
                ResultDyedItem = new();
            }
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            ItemIO.Send(DyeSlotItem, data, true, true);
            ItemIO.Send(BeDyedItem, data, true, true);
            ItemIO.Send(ResultDyedItem, data, true, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            DyeSlotItem = ItemIO.Receive(reader, true, true);
            BeDyedItem = ItemIO.Receive(reader, true, true);
            ResultDyedItem = ItemIO.Receive(reader, true, true);
        }

        public void RightClick(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }

            SoundEngine.PlaySound(CWRSound.ButtonZero);
            var ui = DyeMachineUI;
            if (ui.DyeTP == this) {
                ui.CanOpen = !DyeMachineUI.CanOpen;
            }
            else {
                ui.DyeTP = this;
                ui.CanOpen = true;
                ui.DyeSlot.Item = DyeSlotItem;
                ui.BeDyedItem.Item = BeDyedItem;
                ui.ResultDyedItem.Item = ResultDyedItem;
            }

            foreach (var otherUI in UIHandleLoader.UIHandles) {
                if (otherUI.ID != DyeMachineUI.ID && otherUI is BaseDyeMachineUI baseDyeMachineUI) {
                    baseDyeMachineUI.CanOpen = false;//关闭其他所有同类UI面板
                }
            }
        }

        public override void UpdateMachine() {
            if (DyeMachineUI.CanOpen && Main.LocalPlayer.DistanceSQ(CenterInWorld) > 90000) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
                DyeMachineUI.CanOpen = false;
            }
            DyeMachineUI.DyeSlot.UpdateSlot();
            UpdateDyeMachine();
        }

        public virtual void UpdateDyeMachine() { }
    }

    internal class SpectrometerTP : BaseDyeTP
    {
        public override int TargetTileID => ModContent.TileType<SpectrometerTile>();
        public override int TargetItem => ModContent.ItemType<Spectrometer>();
        public override bool CanDrop => false;
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 800;
        public override BaseDyeMachineUI DyeMachineUI => SpectrometerUI.Instance;
        internal int frame;
        public override void UpdateDyeMachine() {
            base.UpdateDyeMachine();
        }
        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
