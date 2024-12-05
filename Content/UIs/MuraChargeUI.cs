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
        #region Data
        internal enum MuraUIStyleEnum
        {
            delicacy_overhead = 1,
            delicacy_compact,
            classical_overhead,
            classical_compact,
        }
        internal enum MuraPosStyleEnum
        {
            right = 1,
            left,
            high,
        }
        internal static MuraChargeUI Instance;
        internal MurasamaHeld murasamaHeld;
        internal Item murasama => Main.LocalPlayer.GetItem();
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
        private static float invasionSetOffsetValue;
        private static Vector2 setBossHealthBarOffsetValue;
        private static Color fullColor;
        internal MuraUIStyleEnum MuraUIStyle => (MuraUIStyleEnum)CWRServerConfig.Instance.MuraUIStyleType;
        internal MuraPosStyleEnum MuraPosStyle => (MuraPosStyleEnum)CWRServerConfig.Instance.MuraPosStyleType;
        internal Vector2 origMuraBarDrawPos => new Vector2(180, Main.screenHeight - 40);
        private bool compact => MuraUIStyle == MuraUIStyleEnum.delicacy_compact || MuraUIStyle == MuraUIStyleEnum.classical_compact;
        internal bool dontAddUIAlape =>
            murasamaHeld == null || murasamaHeld.Type != ModContent.ProjectileType<MurasamaHeld>() || Main.playerInventory;
        public override bool Active {
            get {
                if (dontAddUIAlape) {
                    if (uiAlape > 0) {
                        uiAlape -= 0.05f;
                    }
                    return uiAlape > 0;
                }
                return murasamaHeld.Projectile.active || uiAlape > 0;
            }
        }
        #endregion
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

            if (uiAlape < 1 && !dontAddUIAlape) {
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

            if (murasamaHeld != null && murasamaHeld.Type == ModContent.ProjectileType<MurasamaHeld>()) {
                uiFrame = murasamaHeld.uiFrame;
                uiFrame2 = murasamaHeld.uiFrame2;
            }

            Time++;
        }

        internal Vector2 ModifyBossHealthBarManagerPositon(int x, int y) {
            Vector2 position = new Vector2(x, y);
            Vector2 targetBossHealthBar = Vector2.Zero;
            if (Active && MuraPosStyle == MuraPosStyleEnum.right) {
                targetBossHealthBar = new Vector2(0, -100);
            }
            setBossHealthBarOffsetValue = Vector2.Lerp(setBossHealthBarOffsetValue, targetBossHealthBar, 0.1f);
            return position + setBossHealthBarOffsetValue;
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
                    Main.spriteBatch.Draw(barFG, drawPos + new Vector2(4, 6), frameCrop, color * 0.8f, 0f, barOrigin, scale, 0, 0f);
                }
                else {
                    barBG = MuraBarFull.Value;
                    Rectangle rectangle = CWRUtils.GetRec(barBG, uiFrame, maxFrame);
                    Main.spriteBatch.Draw(barBG, drawPos + new Vector2(0, -3)
                        , rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float scale = 1;
            if (uiAlape <= 0) {
                return;
            }

            Vector2 otherOffset;
            if (MuraPosStyle == MuraPosStyleEnum.left) {
                otherOffset = new Vector2(80, 0);
            }
            else if (MuraPosStyle == MuraPosStyleEnum.high) {
                otherOffset = new Vector2(Main.screenWidth / 3 * 2, -Main.screenHeight + 180);
            }
            else {
                otherOffset = new Vector2(Main.screenWidth - SwordStanceBottom.Width() - 40 + invasionSetOffsetValue, 0);
                int targetInvasion = 0;
                if (CWRUtils.Invasion) {
                    targetInvasion = -250;
                }
                invasionSetOffsetValue = MathHelper.Lerp(invasionSetOffsetValue, targetInvasion, 0.1f);
            }
            DrawPosition = origMuraBarDrawPos + otherOffset;

            Color color = Color.White * uiAlape;
            Vector2 topBarOffset = new Vector2(18, 12);
            Texture2D barBG = SwordStanceBottom.Value;
            Texture2D barFG = SwordStanceTop.Value;
            Texture2D fullFG = SwordStanceFull.Value;
            if (MuraUIStyle == MuraUIStyleEnum.classical_overhead || MuraUIStyle == MuraUIStyleEnum.classical_compact) {
                topBarOffset = new Vector2(46, 6);
                barBG = classical_SwordStanceBottom.Value;
                barFG = classical_SwordStanceTop.Value;
                fullFG = classical_SwordStanceFull.Value;
            }

            Vector2 barOrigin = barBG.Size() * 0.5f;

            if (InWorldBossPhase.Instance.Mura_Level() == 14 || MurasamaEcType.NameIsSam(Main.LocalPlayer)) {
                Main.spriteBatch.Draw(Mura.Value, DrawPosition + new Vector2(-110, -88), null, color, 0f, barOrigin, scale, 0, 0f);
            }

            if (charge <= 9 || !MurasamaEcType.UnlockSkill3) {
                Rectangle frameCrop = new Rectangle(0, 0, (int)(newForCharge / 10f * barFG.Width), barFG.Height);
                Main.spriteBatch.Draw(barBG, DrawPosition, null, color, 0f, barOrigin, scale, 0, 0f);
                Main.spriteBatch.Draw(barFG, DrawPosition + topBarOffset, frameCrop, fullColor * uiAlape, 0f, barOrigin, scale, 0, 0f);
            }
            else {
                Rectangle rectangle = CWRUtils.GetRec(fullFG, uiFrame2, 9);
                Main.spriteBatch.Draw(fullFG, DrawPosition, rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
            }

            if (compact) {
                float risingDragon = Main.LocalPlayer.CWR().RisingDragonCharged;
                if (!(risingDragon <= 0f) || uiAlape > 0) {//这是一个通用的进度条绘制，用于判断进度
                    Texture2D muraBarBottom = MuraBarBottom.Value;
                    Texture2D muraBarTop = MuraBarTop.Value;
                    Vector2 barOrigin2 = muraBarBottom.Size() * 0.5f;
                    Vector2 drawPos = DrawPosition + new Vector2(-20, -40);
                    if (risingDragon < MurasamaEcType.GetOnRDCD) {
                        Rectangle frameCrop = new Rectangle(0, 0, (int)(risingDragon / MurasamaEcType.GetOnRDCD * muraBarTop.Width), muraBarTop.Height);
                        Main.spriteBatch.Draw(muraBarBottom, drawPos, null, color, 0f, barOrigin2, scale, 0, 0f);
                        Main.spriteBatch.Draw(muraBarTop, drawPos + new Vector2(4, 6), frameCrop, color * 0.8f, 0f, barOrigin2, scale, 0, 0f);
                    }
                    else {
                        muraBarBottom = MuraBarFull.Value;
                        Rectangle rectangle = CWRUtils.GetRec(muraBarBottom, uiFrame, 6);
                        Main.spriteBatch.Draw(muraBarBottom, drawPos + new Vector2(0, -3)
                            , rectangle, color, 0f, rectangle.Size() / 2, scale, 0, 0f);
                    }
                }
            }
        }
    }
}
