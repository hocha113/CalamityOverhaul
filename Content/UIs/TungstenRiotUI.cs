using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Events;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.UIs
{
    internal class TungstenRiotUI : UIHandle, ICWRLoader
    {
        public static TungstenRiotUI Instance;
        public static Asset<Texture2D> icon;
        private float sengs;
        public override LayersModeEnum LayersMode => LayersModeEnum.Vanilla_Diagnose_Net;
        public override bool Active {
            get {
                bool value = TungstenRiot.Instance.TungstenRiotIsOngoing;
                if (value) {
                    if (sengs < 1) {
                        sengs += 0.01f;
                    }
                    return true;
                }
                else {
                    if (sengs > 0) {
                        sengs -= 0.01f;
                    }
                    else {
                        return false;
                    }
                    return true;
                }
            }
        }

        public override void Load() => Instance = this;
        void ICWRLoader.LoadAsset() => icon = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Events/TungstenRiotIcon");

        public override void Update() => DrawPosition = new Vector2(Main.screenWidth - 60, Main.screenHeight - 60);

        public override void Draw(SpriteBatch spriteBatch) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float ratio = 1 - TungstenRiot.Instance.EventKillRatio;
            string name = CWRLocText.GetTextValue("Event_TungstenRiot_Name");
            VaultUtils.DrawEventProgressBar(spriteBatch, pixel, DrawPosition, icon, ratio, sengs, 200, 45, name, TungstenRiot.Instance.MainColor);
        }
    }
}
