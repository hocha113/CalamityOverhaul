using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs
{
    internal class CraftingSlotHighlighter : UIHandle, ICWRLoader
    {
        public static CraftingSlotHighlighter Instance => UIHandleLoader.GetUIHandleOfType<CraftingSlotHighlighter>();
        private SupertableUI mainUI => UIHandleLoader.GetUIHandleOfType<SupertableUI>();
        public override Texture2D Texture => CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/CallFull");
        public static Asset<Texture2D> eyeAsset;
        public override float RenderPriority => 2;
        public override bool Active {
            get {
                if (SupertableUI.Instance == null) {
                    return false;
                }
                return SupertableUI.Instance.Active;
            }
        }

        public bool eyEBool;

        void ICWRLoader.LoadAsset() => eyeAsset = CWRUtils.GetT2DAsset("CalamityOverhaul/Assets/UIs/SupertableUIs/Eye");
        void ICWRLoader.UnLoadData() => eyeAsset = null;

        public override void Update() {
            DrawPosition = mainUI.DrawPosition + new Vector2(460, 420);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 30, 30);
            hoverInMainPage = UIHitBox.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            if (hoverInMainPage) {
                //int mouseS = DownStartL();
                int mouseS = (int)keyLeftPressState;
                if (mouseS == 1) {
                    eyEBool = !eyEBool;
                    if (eyEBool) {
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.5f });
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.5f });
                    }
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(eyeAsset.Value, DrawPosition, CWRUtils.GetRec(eyeAsset.Value, eyEBool ? 1 : 0, 2)
                , Color.White * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (mainUI.items != null) {
                for (int i = 0; i < mainUI.items.Length; i++) {
                    if (mainUI.items[i] == null) {
                        mainUI.items[i] = new Item();
                    }
                    if (mainUI.previewItems[i] == null) {
                        mainUI.previewItems[i] = new Item();
                    }
                    if (mainUI.items[i].type != mainUI.previewItems[i].type && mainUI.items[i].type != ItemID.None && eyEBool) {
                        Vector2 pos = mainUI.ArcCellPos(i) + new Vector2(-1, 0);
                        spriteBatch.Draw(Texture, pos, null, Color.White * 0.6f * SupertableUI.Instance._sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                    }
                }
            }
            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                , eyEBool ? CWRLocText.GetTextValue("SupertableUI_Text4") : CWRLocText.GetTextValue("SupertableUI_Text5")
                , DrawPosition.X - 30, DrawPosition.Y + 30
                , Color.White * SupertableUI.Instance._sengs, Color.Black * SupertableUI.Instance._sengs, new Vector2(0.3f), 0.8f);
            }
        }
    }
}
