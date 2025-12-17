using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.Generator.WindGriven;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
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

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe().
                    AddRecipeGroup(CWRRecipes.GoldBarGroup, 8).
                    AddIngredient(ItemID.Lens, 4).
                    AddTile(TileID.Anvils).
                    Register();

                CreateRecipe().
                    AddRecipeGroup(CWRRecipes.GoldBarGroup, 8).
                    AddIngredient(ItemID.Ruby, 1).
                    AddTile(TileID.Anvils).
                    Register();
                return;
            }

            CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddRecipeGroup(CWRRecipes.GoldBarGroup, 8).
                AddIngredient(ItemID.Lens, 4).
                AddTile(TileID.Anvils).
                Register();

            CreateRecipe().
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 10).
                AddRecipeGroup(CWRRecipes.GoldBarGroup, 8).
                AddIngredient(ItemID.Ruby, 1).
                AddTile(TileID.Anvils).
                Register();
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
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> MaskLaserLine = null;
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        private static Asset<Texture2D> LaserEnergyTransHead = null;
        [VaultLoaden(CWRConstant.Asset + "MaterialFlow/")]
        private static Asset<Texture2D> LaserEnergyTransHeadGlow = null;
        public override int TargetTileID => ModContent.TileType<LaserEnergyTransTile>();
        public override int TargetItem => ModContent.ItemType<LaserEnergyTrans>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 20;
        internal Vector2 TrueCenter => CenterInWorld - new Vector2(0, 12);
        internal Player fromePlayer;
        internal MachineTP targetMachine;
        internal const float MaxTransDistance = 1200;
        private int time;
        private Vector2 toMouse;
        private bool oldDownR;
        public override void SetBattery() {
            fromePlayer = null;
            targetMachine = null;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
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
                data.Write(targetMachine.FullName);
                data.WritePoint16(targetMachine.Position);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
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
            base.SaveData(tag);
            if (targetMachine == null || !targetMachine.Active) {
                tag["targetMachine_LoadenName"] = "";
                tag["targetMachine_Position"] = new Point16(0, 0);
            }
            else {
                tag["targetMachine_LoadenName"] = targetMachine.FullName;
                tag["targetMachine_Position"] = targetMachine.Position;
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
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
                SoundEngine.PlaySound(CWRSound.Select);
                targetMachine = null;
            }

            if (fromePlayer != player) {
                SoundEngine.PlaySound(CWRSound.Select);
                fromePlayer = player;
            }
            else {
                SoundEngine.PlaySound(CWRSound.Select);
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
            if (tp.Position == Position) {
                return;//防止链接自己
            }
            if (tp is not MachineTP machine) {
                return;
            }

            toMouse = TrueCenter.To(machine.CenterInWorld);

            if (!oldDownR && fromePlayer.PressKey(false)) {
                SoundEngine.PlaySound(CWRSound.Select);
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

            //计算目标与当前的向量差
            toMouse = TrueCenter.To(targetMachine.CenterInWorld);

            //计算距离（像素）
            float distance = toMouse.Length();

            //计算能量衰减比例：MaxTransDistance 像素时为 0，越近越高，最远为 MaxTransDistance
            float efficiencyScale = 1f - MathHelper.Clamp(distance / MaxTransDistance, 0f, 1f);

            //计算实际可传输的能量值
            float baseTransfer = Math.Min(Efficiency, Math.Min(MachineData.UEvalue, targetMachine.MaxUEValue - targetMachine.MachineData.UEvalue));

            //加入距离衰减影响
            float transferAmount = baseTransfer * efficiencyScale;

            if (transferAmount > 0f) {
                targetMachine.MachineData.UEvalue += transferAmount;
                MachineData.UEvalue -= baseTransfer;
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
                Texture2D value = MaskLaserLine.Value;
                //计算距离（像素）
                float distance = toMouse.Length();
                //计算能量衰减比例：MaxTransDistance 像素时为 0，越近越高，最远为 MaxTransDistance
                float efficiencyScale = 1f - MathHelper.Clamp(distance / MaxTransDistance, 0f, 1f);
                Color drawColor = Color.White;
                drawColor.A = 0;
                Vector2 size = new Vector2(toMouse.Length() / value.Width, 0.03f + MathF.Sin(time * 0.1f) * 0.006f);
                Main.EntitySpriteDraw(value, TrueCenter - Main.screenPosition, null, drawColor * efficiencyScale
                    , toMouse.ToRotation(), new Vector2(0, value.Height / 2f), size, SpriteEffects.None, 0);
            }

            Texture2D head = LaserEnergyTransHead.Value;
            Main.EntitySpriteDraw(head, TrueCenter - Main.screenPosition, null, Lighting.GetColor(TrueCenter.ToTileCoordinates())
                , toMouse.ToRotation(), head.Size() / 2, 1f, SpriteEffects.None, 0);
            head = LaserEnergyTransHeadGlow.Value;
            Main.EntitySpriteDraw(head, TrueCenter - Main.screenPosition, null, Color.White
                , toMouse.ToRotation(), head.Size() / 2, 1f, SpriteEffects.None, 0);
        }

        public override void FrontDraw(SpriteBatch spriteBatch) => DrawChargeBar();
    }
}
