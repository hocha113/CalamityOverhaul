using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Events;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;

namespace CalamityOverhaul.Content.UIs
{
    internal class TungstenRiotUI : CWRUIPanel
    {
        public static TungstenRiotUI Instance;
        public static Asset<Texture2D> icon;
        private float sengs;
        public bool Active {
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
        
        public override void Load() {
            Instance = this;
            if (!Main.dedServ) {
                icon = CWRUtils.GetT2DAsset(CWRConstant.Asset + "Events/TungstenRiotIcon");
            }
        }

        public override void Update(GameTime gameTime) {
            DrawPos = new Vector2(Main.screenWidth - 60, Main.screenHeight - 60);
        }

        public override void Draw(SpriteBatch spriteBatch) 
            => CWRUtils.DrawEventProgressBar(spriteBatch, DrawPos, icon, 1 - TungstenRiot.Instance.EventKillRatio
                , sengs, 200, 45, CWRLocText.GetTextValue("Event_TungstenRiot_Name"), TungstenRiot.Instance.MainColor);
    }
}
