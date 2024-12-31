using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.OverhaulTheBible
{
    internal class ItemVidous : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public BaseRItem BaseRItem { get; set; }
        public static int Width => 66;
        public static int Height => 66;
        public static Vector2 handerOffsetTopL => new Vector2(6, 6);
        public override void Update() {
            Item item = new Item(BaseRItem.TargetID);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Width, Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            if (UIHitBox.Intersects(mouseHit)) {
                Main.HoverItem = item.Clone();
                Main.hoverItemName = item.Name;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value
                , 4, DrawPosition, Width, Height, Color.White * OverhaulTheBibleUI.Instance._sengs
                , Color.GhostWhite * OverhaulTheBibleUI.Instance._sengs, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRUtils.GetT2DValue(CWRConstant.UI + "JAR")
                , 4, DrawPosition, Width, Height, Color.White * OverhaulTheBibleUI.Instance._sengs, Color.White * 0, 1);

            Item item = new Item(BaseRItem.TargetID);
            if (BaseRItem.IsVanilla) {
                Main.instance.LoadItem(BaseRItem.TargetID);
            }
            float size = VaultUtils.GetDrawItemSize(item, Width);
            VaultUtils.SimpleDrawItem(spriteBatch, BaseRItem.TargetID, DrawPosition + new Vector2(Width, Height) / 2
                , size, 0, Color.White * OverhaulTheBibleUI.Instance._sengs, Vector2.Zero);
        }
    }
}
