using CalamityOverhaul.Content.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GunCustomization.UI.AmmoView
{
    internal class AmmoViewUI : UIHandle
    {
        public static AmmoViewUI Instance => UIHandleLoader.GetUIHandleOfType<AmmoViewUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public bool uiActive => (CartridgeHolderUI.Instance.Active && CartridgeHolderUI.Instance.hoverInMainPage) || ItemVsActive;
        public override bool Active => uiActive || _sengs > 0;
        public List<AmmoItemElement> ammoItemElements = [];
        public float _sengs;
        public bool ItemVsActive;

        public void LoadAmmos(CWRItems cwrItem) {
            ammoItemElements.Clear();
            foreach (Item ammo in cwrItem.MagazineContents) {
                if (ammo == null) {
                    continue;
                }
                if (ammo.stack <= 0) {
                    continue;
                }

                if (ammo.type != ItemID.None && ammo.ammo != AmmoID.None) {
                    AmmoItemElement ammoItem = new AmmoItemElement();
                    ammoItem.Ammo = ammo;
                    ammoItemElements.Add(ammoItem);
                }
            }
        }

        public override void Update() {
            if (uiActive) {
                if (_sengs < 1) {
                    _sengs += 0.1f;
                }
            }
            else {
                if (_sengs > 0) {
                    _sengs -= 0.1f;
                }
            }

            _sengs = MathHelper.Clamp(_sengs, 0, 1f);

            if (ammoItemElements.Count <= 0 && CartridgeHolderUI.cwrWeapon != null) {
                LoadAmmos(CartridgeHolderUI.cwrWeapon);
            }

            ItemVsActive = false;
            for (int i = 0; i < ammoItemElements.Count; i++) {
                AmmoItemElement ammoItem = ammoItemElements[i];
                DrawPosition = CartridgeHolderUI.Instance.DrawPosition;
                ammoItem.DrawPosition = DrawPosition - new Vector2(0, AmmoItemElement.Height) * (1 + i) * _sengs;
                ammoItem.Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            foreach (AmmoItemElement ammoItem in ammoItemElements) {
                ammoItem.Draw(spriteBatch);
            }
        }

        public void PostDraw(SpriteBatch spriteBatch) {
            foreach (AmmoItemElement ammoItem in ammoItemElements) {
                ammoItem.PostDraw(spriteBatch);
            }
        }
    }
}
