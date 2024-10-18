using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs
{
    internal class MuraChargeUI : UIHandle, ICWRLoader
    {
        internal MurasamaHeldProj murasamaHeld;
        internal Item murasama => Main.LocalPlayer.GetItem();
        private static Asset<Texture2D> SwordStanceTop;
        private static Asset<Texture2D> SwordStanceFull;
        private static Asset<Texture2D> SwordStanceBottom;
        private static Asset<Texture2D> Mura;
        private static int uiFrame;
        private static float uiAlape;
        private static float charge;
        private static float newForCharge;
        private static int Time;
        private static Color fullColor;
        public override bool Active {
            get {
                if (murasamaHeld == null || murasamaHeld.Type != ModContent.ProjectileType<MurasamaHeldProj>()) {
                    if (uiAlape > 0) {
                        uiAlape -= 0.05f;
                    }
                    return uiAlape > 0;
                }
                return murasamaHeld.Projectile.active || uiAlape > 0;
            }
        }
        internal Vector2 origMuraBarDrawPos => new Vector2(180, Main.screenHeight - 40);
        internal Vector2 muraBarDrawPos;
        internal Vector2 CartridgeUI_Offset {
            get {
                Vector2 offset = new Vector2(CWRServerConfig.Instance.CartridgeUI_Offset_X_Value
                , -CWRServerConfig.Instance.CartridgeUI_Offset_Y_Value);
                if (offset.X > 1400) {
                    offset.X = 1400;
                }
                return offset;
            }
        }
        void ICWRLoader.LoadAsset() {
            SwordStanceBottom = CWRUtils.GetT2DAsset(CWRConstant.UI + "SwordStanceBottom");
            SwordStanceTop = CWRUtils.GetT2DAsset(CWRConstant.UI + "SwordStanceTop");
            SwordStanceFull = CWRUtils.GetT2DAsset(CWRConstant.UI + "SwordStanceFull");
            Mura = CWRUtils.GetT2DAsset(CWRConstant.UI + "Mura");
        }
        void ICWRLoader.UnLoadData() {
            SwordStanceBottom = null;
            SwordStanceTop = null;
            SwordStanceFull = null;
            Mura = null;
        }
        public override void Update() {
            if (murasama.type == ItemID.None || murasama != null 
                && murasama.type != ModContent.ItemType<Murasama>() 
                && murasama.type != ModContent.ItemType<MurasamaEcType>()) {
                murasamaHeld = null;
                return;
            }
            if (uiAlape < 1) {
                uiAlape += 0.05f;
            }
            murasama.initialize();
            charge = murasama.CWR().ai[0];
            newForCharge = MathHelper.Lerp(newForCharge, charge, 0.2f);

            if (Math.Abs(charge - newForCharge) > 0.1f) {
                fullColor = Color.Lerp(Color.White, Color.Red, Math.Abs(MathF.Sin(Time * 0.2f)));
            }
            else {
                fullColor = Color.White;
            }

            if (murasamaHeld != null && murasamaHeld.Type == ModContent.ProjectileType<MurasamaHeldProj>()) {
                uiFrame = murasamaHeld.uiFrame2;
            }
            Time++;
        }
        public override void Draw(SpriteBatch spriteBatch) {
            float scale = 1;
            if (murasama.type == ItemID.None) {
                return;
            }
            if (uiAlape > 0) {
                Texture2D barBG = SwordStanceBottom.Value;
                Texture2D barFG = SwordStanceTop.Value;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                muraBarDrawPos = origMuraBarDrawPos + CartridgeUI_Offset;
                Color color = Color.White * uiAlape;

                Main.spriteBatch.Draw(Mura.Value, muraBarDrawPos + new Vector2(0, -138), null, color, 0f, barOrigin, scale, 0, 0f);

                if (charge < 9) {
                    Rectangle frameCrop = new Rectangle(0, 0, (int)(newForCharge / 9f * barFG.Width), barFG.Height);
                    Main.spriteBatch.Draw(barBG, muraBarDrawPos, null, color, 0f, barOrigin, scale, 0, 0f);
                    Main.spriteBatch.Draw(barFG, muraBarDrawPos + new Vector2(9, 6) * 2, frameCrop, fullColor * uiAlape, 0f, barOrigin, scale, 0, 0f);
                }
                else {
                    barBG = SwordStanceFull.Value;
                    Rectangle rectangle = CWRUtils.GetRec(barBG, uiFrame, 9);
                    Main.spriteBatch.Draw(barBG, muraBarDrawPos
                        , rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
                }
            }
        }
    }
}
