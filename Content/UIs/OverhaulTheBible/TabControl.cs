using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.OverhaulTheBible
{
    internal class TabControl : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public Vector2 DrawPositionOffset { get; set; }
        public DamageClass DamageClass { get; set; }
        public bool Tab { get; set; } = true;
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, 64, 64);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            if (UIHitBox.Intersects(mouseHit)) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (DamageClass != DamageClass.Default) {
                        Tab = !Tab;
                        OverhaulTheBibleUI.Instance.SetItemVidousList();
                    }
                    else {
                        OverhaulTheBibleUI.Instance.Active = false;
                    }
                    SoundEngine.PlaySound(SoundID.MenuClose);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.Placeholder_White.Value
                , 4, DrawPosition, 64, 64, Color.White * OverhaulTheBibleUI.Instance._sengs
                , (Tab ? Color.Red : Color.GhostWhite) * OverhaulTheBibleUI.Instance._sengs, 1);
            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRUtils.GetT2DValue(CWRConstant.UI + "JAR")
                , 4, DrawPosition, 64, 64, Color.White * OverhaulTheBibleUI.Instance._sengs, Color.White * 0, 1);

            if (DamageClass != DamageClass.Default) {
                int targetID = 4;
                if (DamageClass == DamageClass.Ranged) {
                    targetID = 99;
                }
                if (DamageClass == DamageClass.Magic) {
                    targetID = 114;
                }
                if (DamageClass == DamageClass.Summon) {
                    targetID = 4281;
                }
                if (DamageClass == CWRLoad.RogueDamageClass) {
                    targetID = 55;
                }

                Main.instance.LoadItem(targetID);

                Item item = new Item(targetID);
                float size = VaultUtils.GetDrawItemSize(item, 60);
                VaultUtils.SimpleDrawItem(spriteBatch, targetID, DrawPosition + new Vector2(64, 64) / 2
                    , size, 0, Color.White * OverhaulTheBibleUI.Instance._sengs, Vector2.Zero);
            }
            else {
                Texture2D value = CWRAsset.Placeholder_ERROR.Value;
                spriteBatch.Draw(value, DrawPosition + new Vector2(64, 64) / 2, null, Color.White * OverhaulTheBibleUI.Instance._sengs
                    , MathHelper.PiOver4 + MathHelper.PiOver2, value.Size() / 2, 1, SpriteEffects.None, 0);
            }
        }
    }
}
