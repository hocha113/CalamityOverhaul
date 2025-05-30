using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.MaterialFlow
{
    internal class LaserEnergyTrans : ModItem
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/LaserEnergyTrans";
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
            Item.value = Item.buyPrice(0, 0, 40, 0);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<LaserEnergyTransTile>();
            Item.CWR().StorageUE = true;
            Item.CWR().ConsumeUseUE = 20;
        }
    }

    internal class LaserEnergyTransTile : ModTile
    {
        public override string Texture => CWRConstant.Asset + "MaterialFlow/LaserEnergyTransTile";
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(67, 72, 81), VaultUtils.GetLocalizedItemName<LaserEnergyTrans>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(0, 2);
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
            if (TileProcessorLoader.AutoPositionGetTP(i, j, out LaserEnergyTransTP transTP)) {
                transTP.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j);
        }
    }

    internal class LaserEnergyTransTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<LaserEnergyTransTile>();
        public override int TargetItem => ModContent.ItemType<LaserEnergyTrans>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 20;
        internal Vector2 TrueCenter => CenterInWorld - new Vector2(0, 12);
        internal Player fromePlayer;
        internal MachineTP targetMachine;
        private int time;
        private Vector2 toMouse;
        private bool oldDownR;
        public override void SetBattery() {
            fromePlayer = null;
            targetMachine = null;
        }

        public override void SendData(ModPacket data) {
            if (fromePlayer == null) {
                data.Write(-1);
            }
            else {
                data.Write(fromePlayer.whoAmI);
            }

            if (targetMachine == null) {
                data.Write("");
                data.WritePoint16(new Point16(0, 0));
            }
            else {
                data.Write(targetMachine.LoadenName);
                data.WritePoint16(targetMachine.Position);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            int playerIndex = reader.ReadInt32();
            string tpName = reader.ReadString();
            Point16 tpPos = reader.ReadPoint16();

            if (playerIndex >= 0) {
                fromePlayer = Main.player[playerIndex];
            }
            else {
                fromePlayer = null;
            }

            if (tpName != "" && tpPos.X > 0 && tpPos.Y > 0) {
                if (TileProcessorLoader.ByPositionGetTP(tpName, tpPos, out var tp) && tp is MachineTP machine) {
                    targetMachine = machine;
                }
                else {
                    targetMachine = null;
                }
            }
            else {
                targetMachine = null;
            }
        }

        public override void SaveData(TagCompound tag) {
            if (targetMachine == null || !targetMachine.Active) {
                tag["targetMachine_LoadenName"] = "";
                tag["targetMachine_Position"] = new Point16(0, 0);
            }
            else {
                tag["targetMachine_LoadenName"] = targetMachine.LoadenName;
                tag["targetMachine_Position"] = targetMachine.Position;
            }
        }

        public override void LoadData(TagCompound tag) {
            if (!tag.TryGet("targetMachine_LoadenName", out string tpName)) {
                tpName = "";
            }
            if (!tag.TryGet("targetMachine_Position", out Point16 tpPos)) {
                tpPos = new Point16(0, 0);
            }

            if (tpName != "" && tpPos.X > 0 && tpPos.Y > 0) {
                if (TileProcessorLoader.ByPositionGetTP(tpName, tpPos, out var tp) && tp is MachineTP machine) {
                    targetMachine = machine;
                }
                else {
                    targetMachine = null;
                }
            }
            else {
                targetMachine = null;
            }
        }

        internal void RightClick(Player player) {
            if (targetMachine != null) {
                targetMachine = null;
            }

            if (fromePlayer != player) {
                fromePlayer = player;
            }
            else {
                fromePlayer = null;
            }

            SendData();
        }

        private void HanderMouse() {
            toMouse = TrueCenter.To(Main.MouseWorld);
            Point16 point = Main.MouseWorld.ToTileCoordinates16();

            if (!VaultUtils.SafeGetTopLeft(point, out var truePoint)) {
                return;
            }
            if (!TileProcessorLoader.ByPositionGetTP(truePoint, out var tp)) {
                return;
            }
            if (tp is not MachineTP machine) {
                return;
            }

            toMouse = TrueCenter.To(machine.CenterInWorld);

            if (!oldDownR && fromePlayer.PressKey(false)) {
                targetMachine = machine;
                fromePlayer = null;
                SendData();
            }
        }

        private void HandleTargetMachine() {
            if (targetMachine == null) {
                return;
            }

            if (!targetMachine.Active) {
                targetMachine = null;
                return;
            }

            toMouse = TrueCenter.To(targetMachine.CenterInWorld);
            float transferAmount = Math.Min(Efficiency, Math.Min(MachineData.UEvalue, targetMachine.MaxUEValue - targetMachine.MachineData.UEvalue));
            if (transferAmount > 0) {
                targetMachine.MachineData.UEvalue += transferAmount;
                MachineData.UEvalue -= transferAmount;
            }
        }

        public override void UpdateMachine() {
            if (fromePlayer.Alives()) {
                if (fromePlayer.whoAmI == Main.myPlayer) {
                    HanderMouse();
                    if (fromePlayer != null) {
                        oldDownR = fromePlayer.PressKey(false);
                    }
                }
            }
            else {
                fromePlayer = null;
            }

            HandleTargetMachine();

            time++;
        }

        public override void BackDraw(SpriteBatch spriteBatch) {
            if (fromePlayer.Alives() || targetMachine != null) {
                Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Masking + "MaskLaserLine");
                Color drawColor = Color.White;
                drawColor.A = 0;
                Vector2 size = new Vector2(toMouse.Length() / value.Width, 0.03f + MathF.Sin(time * 0.1f) * 0.006f);
                Main.EntitySpriteDraw(value, TrueCenter - Main.screenPosition, null, drawColor
                    , toMouse.ToRotation(), new Vector2(0, value.Height / 2f), size, SpriteEffects.None, 0);
            }
            Texture2D head = CWRUtils.GetT2DValue(CWRConstant.Asset + "MaterialFlow/LaserEnergyTransHead");
            Main.EntitySpriteDraw(head, TrueCenter - Main.screenPosition, null, Lighting.GetColor(TrueCenter.ToTileCoordinates())
                , toMouse.ToRotation(), head.Size() / 2, 1f, SpriteEffects.None, 0);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
