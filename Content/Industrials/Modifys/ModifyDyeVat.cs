using CalamityOverhaul.Content.Industrials.ElectricPowers.Spectrometers;
using InnoVault.GameSystem;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class DyeVatItem : ItemOverride
    {
        public override int TargetID => ItemID.DyeVat;
        public override bool DrawingInfo => false;
        public override void SetDefaults(Item item) {
            item.rare = ItemRarityID.Green;
        }
    }

    internal class ModifyDyeVat : TileOverride
    {
        public override int TargetID => TileID.DyeVat;
        public override bool? RightClick(int i, int j, Tile tile) {
            if (TPUtils.TryGetTPAt(i, j, out DyeVatTP tp)) {
                tp.RightClick(Main.LocalPlayer);
            }
            return base.RightClick(i, j, tile);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ItemID.DyeVat);
    }

    internal class DyeVatTP : BaseDyeTP
    {
        public override int TargetTileID => TileID.DyeVat;
        public override bool CanDrop => false;
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 0;
        public override BaseDyeMachineUI DyeMachineUI => DyeVatUI.Instance;
        public override void PreTileDraw(SpriteBatch spriteBatch) {
            Item item = null;
            if (BeDyedItem.type > ItemID.None) {
                item = BeDyedItem;
            }
            if (ResultDyedItem.type > ItemID.None) {
                item = ResultDyedItem;
            }

            //染槽有物则搅动绘制
            if (item == null) {
                return;
            }

            float time = Main.GlobalTimeWrappedHourly;
            Vector2 drawPosition = CenterInWorld - Main.screenPosition;
            Color drawColor = Lighting.GetColor(PosInWorld.ToTileCoordinates());

            //椭圆晃动 + 轻旋
            float animOffsetX = (float)Math.Cos(time * 2f) * 4f;
            float animOffsetY = (float)Math.Sin(time * 2f) * 2f;
            float rotation = (float)Math.Sin(time * 1.5f) * 0.2f;

            Vector2 itemDrawPos = drawPosition + new Vector2(animOffsetX, animOffsetY);

            if (DyeSlotItem.type > ItemID.None) {
                item.BeginDyeEffectForWorld(DyeSlotItem.type);
            }
            VaultUtils.SimpleDrawItem(spriteBatch, item.type, itemDrawPos, Width / 2, 1f, rotation, drawColor);
            if (DyeSlotItem.type > ItemID.None) {
                item.EndDyeEffectForWorld();
            }
        }
    }
}