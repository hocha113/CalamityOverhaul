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
        internal enum MuraStyleEnum
        {
            delicacy_HasMK_overhead = 1,
            delicacy_NoMK_overhead,
            delicacy_HasMK_compact,
            delicacy_NoMK_compact,
            classical_overhead,
            classical_compact,
        }
        internal static MuraChargeUI Instance;
        internal MurasamaHeldProj murasamaHeld;
        internal Item murasama => Main.LocalPlayer.GetItem();
        private bool compact => MuraStyle == MuraStyleEnum.delicacy_HasMK_compact
                || MuraStyle == MuraStyleEnum.delicacy_NoMK_compact
                || MuraStyle == MuraStyleEnum.classical_compact;
        private static Asset<Texture2D> classical_SwordStanceTop;
        private static Asset<Texture2D> classical_SwordStanceFull;
        private static Asset<Texture2D> classical_SwordStanceBottom;
        private static Asset<Texture2D> SwordStanceTop;
        private static Asset<Texture2D> SwordStanceFull;
        private static Asset<Texture2D> SwordStanceBottom;
        private static Asset<Texture2D> MuraBarBottom;
        private static Asset<Texture2D> MuraBarTop;
        private static Asset<Texture2D> MuraBarFull;
        private static Asset<Texture2D> Mura;
        private static Asset<Texture2D> Num;
        private static int uiFrame;
        private static int uiFrame2;
        private static float uiAlape;
        private static float charge;
        private static float newForCharge;
        private static int Time;
        private static Color fullColor;
        internal MuraStyleEnum MuraStyle => (MuraStyleEnum)CWRServerConfig.Instance.MuraStyleType;
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
        void ICWRLoader.LoadAsset() {
            classical_SwordStanceBottom = CWRUtils.GetT2DAsset(CWRConstant.UI + "classical_SwordStanceBottom");
            classical_SwordStanceTop = CWRUtils.GetT2DAsset(CWRConstant.UI + "classical_SwordStanceTop");
            classical_SwordStanceFull = CWRUtils.GetT2DAsset(CWRConstant.UI + "classical_SwordStanceFull");
            SwordStanceBottom = CWRUtils.GetT2DAsset(CWRConstant.UI + "SwordStanceBottom");
            SwordStanceTop = CWRUtils.GetT2DAsset(CWRConstant.UI + "SwordStanceTop");
            SwordStanceFull = CWRUtils.GetT2DAsset(CWRConstant.UI + "SwordStanceFull");
            MuraBarBottom = CWRUtils.GetT2DAsset(CWRConstant.UI + "MuraBarBottom");
            MuraBarTop = CWRUtils.GetT2DAsset(CWRConstant.UI + "MuraBarTop");
            MuraBarFull = CWRUtils.GetT2DAsset(CWRConstant.UI + "MuraBarFull");
            Mura = CWRUtils.GetT2DAsset(CWRConstant.UI + "Mura");
            Num = CWRUtils.GetT2DAsset(CWRConstant.UI + "NumList");
        }
        void ICWRLoader.UnLoadData() {
            classical_SwordStanceBottom = null;
            classical_SwordStanceTop = null;
            classical_SwordStanceFull = null;
            SwordStanceBottom = null;
            SwordStanceTop = null;
            SwordStanceFull = null;
            MuraBarBottom = null;
            MuraBarTop = null;
            MuraBarFull = null;
            Mura = null;
            Num = null;
            Instance = null;
        }

        public override void Load() => Instance = this;

        public override void Update() {
            if (murasama.type == ItemID.None
                || (murasama != null && murasama.type != ModContent.ItemType<Murasama>()
                && murasama.type != ModContent.ItemType<MurasamaEcType>())) {
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
                uiFrame = murasamaHeld.uiFrame;
                uiFrame2 = murasamaHeld.uiFrame2;
            }

            Time++;
        }

        internal void DrawOverheadSorwdBar(Player Owner, float risingDragon, int uiFrame, int maxFrame) {
            if (compact) {
                return;
            }
            float scale = 1;
            if (!(risingDragon <= 0f) || uiAlape > 0) {//这是一个通用的进度条绘制，用于判断进度
                Texture2D barBG = MuraBarBottom.Value;
                Texture2D barFG = MuraBarTop.Value;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                Vector2 drawPos = Owner.GetPlayerStabilityCenter() + new Vector2(0, -90) - Main.screenPosition;
                Color color = Color.White * uiAlape;
                if (risingDragon < MurasamaEcType.GetOnRDCD) {
                    Rectangle frameCrop = new Rectangle(0, 0, (int)(risingDragon / MurasamaEcType.GetOnRDCD * barFG.Width), barFG.Height);
                    Main.spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale, 0, 0f);
                    Main.spriteBatch.Draw(barFG, drawPos + new Vector2(12, 42), frameCrop, color * 0.8f, 0f, barOrigin, scale, 0, 0f);
                }
                else {
                    barBG = MuraBarFull.Value;
                    Rectangle rectangle = CWRUtils.GetRec(barBG, uiFrame, maxFrame);
                    Main.spriteBatch.Draw(barBG, drawPos
                        , rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float scale = 1;
            if (uiAlape <= 0) {
                return;
            }

            muraBarDrawPos = origMuraBarDrawPos + CartridgeUI_Offset;
            Color color = Color.White * uiAlape;
            Vector2 topBarOffset = new Vector2(18, 12);
            Texture2D barBG = SwordStanceBottom.Value;
            Texture2D barFG = SwordStanceTop.Value;
            Texture2D fullFG = SwordStanceFull.Value;
            if (MuraStyle == MuraStyleEnum.classical_overhead || MuraStyle == MuraStyleEnum.classical_compact) {
                topBarOffset = new Vector2(46, 6);
                barBG = classical_SwordStanceBottom.Value;
                barFG = classical_SwordStanceTop.Value;
                fullFG = classical_SwordStanceFull.Value;
            }

            Vector2 barOrigin = barBG.Size() * 0.5f;

            if (MuraStyle == MuraStyleEnum.delicacy_HasMK_overhead || MuraStyle == MuraStyleEnum.delicacy_HasMK_compact) {
                Main.spriteBatch.Draw(Mura.Value, muraBarDrawPos + new Vector2(0, -138), null, color, 0f, barOrigin, scale, 0, 0f);
            }

            if (charge < 9) {
                Rectangle frameCrop = new Rectangle(0, 0, (int)(newForCharge / 9f * barFG.Width), barFG.Height);
                Main.spriteBatch.Draw(barBG, muraBarDrawPos, null, color, 0f, barOrigin, scale, 0, 0f);
                Main.spriteBatch.Draw(barFG, muraBarDrawPos + topBarOffset, frameCrop, fullColor * uiAlape, 0f, barOrigin, scale, 0, 0f);
            }
            else {
                Rectangle rectangle = CWRUtils.GetRec(fullFG, uiFrame2, 9);
                Main.spriteBatch.Draw(fullFG, muraBarDrawPos, rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
            }

            //Rectangle numRec = CWRUtils.GetRec(Num.Value, InWorldBossPhase.Instance.Mura_Level(), 15);
            //Vector2 numOrig = numRec.Size() / 2;
            //Main.spriteBatch.Draw(Num.Value, muraBarDrawPos + new Vector2(160, 0), numRec, color, 0f, numOrig, scale, 0, 0f);

            if (compact) {
                float risingDragon = Main.LocalPlayer.CWR().RisingDragonCharged;
                if (!(risingDragon <= 0f) || uiAlape > 0) {//这是一个通用的进度条绘制，用于判断进度
                    Texture2D muraBarBottom = MuraBarBottom.Value;
                    Texture2D muraBarTop = MuraBarTop.Value;
                    Vector2 barOrigin2 = muraBarBottom.Size() * 0.5f;
                    Vector2 drawPos = muraBarDrawPos + new Vector2(-20, -50);
                    if (risingDragon < MurasamaEcType.GetOnRDCD) {
                        Rectangle frameCrop = new Rectangle(0, 0, (int)(risingDragon / MurasamaEcType.GetOnRDCD * muraBarTop.Width), muraBarTop.Height);
                        Main.spriteBatch.Draw(muraBarBottom, drawPos, null, color, 0f, barOrigin2, scale, 0, 0f);
                        Main.spriteBatch.Draw(muraBarTop, drawPos + new Vector2(12, 42), frameCrop, color * 0.8f, 0f, barOrigin2, scale, 0, 0f);
                    }
                    else {
                        muraBarBottom = MuraBarFull.Value;
                        Rectangle rectangle = CWRUtils.GetRec(muraBarBottom, uiFrame, 6);
                        Main.spriteBatch.Draw(muraBarBottom, drawPos
                            , rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
                    }
                }
            }
        }
    }
}
