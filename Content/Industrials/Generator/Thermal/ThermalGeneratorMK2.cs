using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Industrials.Generator.Thermal
{
    internal class ThermalGeneratorMK2 : ModItem
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGeneratorMK2";
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
            Item.createTile = ModContent.TileType<ThermalGeneratorMK2Tile>();
            Item.CWR().StorageUE = true;
        }
    }

    internal class ThermalGeneratorMK2Tile : BaseGeneratorTile
    {
        public override string Texture => CWRConstant.Asset + "Generator/ThermalGeneratorMK2Tile";
        public override int GeneratorTP => TileProcessorLoader.GetModuleID<ThermalGeneratorMK2TP>();
        public override int GeneratorUI => UIHandleLoader.GetUIHandleID<ThermalGeneratorUI>();
        public override int TargetItem => ModContent.ItemType<ThermalGeneratorMK2>();
        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileSolidTop[Type] = true;
            AddMapEntry(new Color(67, 72, 81), CWRUtils.SafeGetItemName<ThermalGeneratorMK2>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 4;
            TileObjectData.newTile.Height = 3;
            TileObjectData.newTile.Origin = new Point16(2, 2);
            TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
            TileObjectData.newTile.StyleWrapLimit = 36;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }
        public override bool CanDrop(int i, int j) => false;
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out ThermalGeneratorMK2TP thermal)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += thermal.frame * 3 * 18;
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

    internal class ThermalGeneratorMK2TP : BaseGeneratorTP
    {
        public override int TargetTileID => ModContent.TileType<ThermalGeneratorMK2Tile>();
        internal ThermalData ThermalData => GeneratorData as ThermalData;
        public override int TargetItem => ModContent.ItemType<ThermalGeneratorMK2>();
        public override float MaxUEValue => 10000;
        internal int frame;
        public override MachineData GetGeneratorDataInds() {
            var inds = new ThermalData();
            inds.MaxChargeCool = 4;
            inds.MaxTemperature = 2000;
            inds.MaxUEValue = MaxUEValue;
            return inds;
        }
        public override void GeneratorUpdate() {
            if (PosInWorld.Distance(Main.LocalPlayer.Center) > MaxFindMode) {
                if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                    UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }

            if (ThermalData.FuelItem != null && ThermalData.FuelItem.type != ItemID.None && ThermalData.Temperature <= ThermalData.MaxTemperature - 250) {
                if (++ThermalData.ChargeCool > ThermalData.MaxChargeCool) {
                    ThermalData.FuelItem.stack--;

                    if (FuelItems.FuelItemToCombustion.ContainsKey(ThermalData.FuelItem.type)) {
                        ThermalData.Temperature += FuelItems.FuelItemToCombustion[ThermalData.FuelItem.type];
                    }

                    if (ThermalData.Temperature > ThermalData.MaxTemperature) {
                        ThermalData.Temperature = ThermalData.MaxTemperature;
                    }
                    if (ThermalData.FuelItem.stack <= 0) {
                        ThermalData.FuelItem.TurnToAir();
                    }
                    ThermalData.ChargeCool = 0;
                }
            }

            if (ThermalData.Temperature > 0 && ThermalData.UEvalue <= ThermalData.MaxUEValue) {
                ThermalData.Temperature--;
                ThermalData.UEvalue += 2;
            }

            if (ThermalData.Temperature > 0) {
                CWRUtils.ClockFrame(ref frame, 5, 5, 1);
            }
            else {
                frame = 0;
            }
        }

        public override void GeneratorKill() {
            if (!VaultUtils.isClient) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            ThermalData.FuelItem.TurnToAir();

            if (!VaultUtils.isServer && GeneratorUI?.GeneratorTP == this
                    && UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive) {
                UIHandleLoader.GetUIHandleOfType<ThermalGeneratorUI>().IsActive = false;
            }
        }

        public override void RightClickByTile(bool newTP) {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                if (!ThermalData.FuelItem.IsAir && !VaultUtils.isClient) {
                    int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                    if (!VaultUtils.isSinglePlayer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
                ThermalData.FuelItem.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (item.IsAir || !FuelItems.FuelItemToCombustion.ContainsKey(item.type)) {
                return;
            }

            if (!ThermalData.FuelItem.IsAir && !VaultUtils.isClient) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, ThermalData.FuelItem.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
                ThermalData.FuelItem.TurnToAir();
            }

            if (FuelItems.FuelItemToCombustion.TryGetValue(item.type, out _)) {
                ThermalData.FuelItem = item.Clone();
                item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }
    }
}
