using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using CalamityOverhaul.Content.UIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.UI
{
    /// <summary>
    /// 往生轮：黑暗虚空中一面缓旋的魂珠之轮。
    /// 珠位按进度弧排布，未解锁是暗槽轮廓、已解锁未燃是冷石珠、点燃即燃焰珠；
    /// 滚轮或拖拽旋轮，点珠入中心详情，点燃/熄灭在此取舍。
    /// 盘底走 <see cref="BlessingRenderer.DrawWheelBackground"/> 的 shader 程序化绘制
    /// </summary>
    internal class BlessingWheelUI : UIHandle, IFullScreenUIHandle
    {
        public static BlessingWheelUI Instance => UIHandleLoader.GetUIHandleOfType<BlessingWheelUI>();

        FullScreenUIDomain IFullScreenUIHandle.FullScreenDomain => FullScreenUIDomain.Asura;

        public override float RenderPriority => 2f;

        public override bool Active => IsOpen || OpenProgress.Current > 0.001f;

        //InnoVault 默认不接管 Esc，必须显式打开，否则界面只能靠键位合拢
        public override bool CloseOnEscape => true;

        public override SoundStyle? OpenSound => CWRSound.ButtonZero with { Pitch = -0.15f, Volume = 0.6f };

        public override SoundStyle? CloseSound => CWRSound.ButtonZero with { Pitch = -0.4f, Volume = 0.55f };

        private const string FreezeReason = "BlessingWheel";
        /// <summary>点燃演出时长（帧）</summary>
        private const int KindleAnimFrames = 34;

        private float wheelRot;
        private float wheelRotTarget;
        private int selected = -1;
        private int hoverSeat = -1;
        private float[] seatHover = [];
        private bool draggingWheel;
        private float dragStartRot;
        private float dragStartAngle;
        private int oldScrollWheelValue;
        private float buttonHover;
        private int rejectShake;
        private float kindleAnim;
        private int kindleSeat = -1;
        private readonly List<FlameCell> flameScratch = [];

#if DEBUG
        /// <summary>VisLab 视觉联排：真值时按席位模式伪装解锁/燃焰（仅影响本界面显示，不进玩法档案）</summary>
        public bool mockAll;
#endif

        /// <summary>界面视角的解锁判定（联排 mock 只在 DEBUG 生效）</summary>
        private bool ViewUnlocked(Blessing blessing) {
#if DEBUG
            if (mockAll) {
                return blessing.Seat % 3 != 2;
            }
#endif
            return BlessingWorld.IsUnlocked(blessing);
        }

        /// <summary>界面视角的燃焰判定（联排 mock 只在 DEBUG 生效）</summary>
        private bool ViewBurning(BlessingPlayer bp, Blessing blessing) {
#if DEBUG
            if (mockAll) {
                return blessing.Seat % 3 == 0;
            }
#endif
            return bp.IsBurning(blessing);
        }

        private static BlessingPlayer LocalBP => Main.LocalPlayer.GetModPlayer<BlessingPlayer>();

        //按键、HUD 灯、其余入口全走这里：别的全屏界面开着时不抢屏
        public override void Open() {
            if (!FullScreenUIHub.TryClaimScreen(this)) {
                return;
            }
            base.Open();
        }

        protected override void OnOpen() {
            Main.playerInventory = false;
            selected = -1;
            hoverSeat = -1;
            //开屏轻旋落座
            wheelRot = wheelRotTarget + 0.35f;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            draggingWheel = false;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
        }

        /// <summary>席位在屏上的位置：进度序沿环均布，顶端起步</summary>
        private Vector2 SeatPos(int seat, int count) {
            float ang = -MathHelper.PiOver2 + MathHelper.TwoPi * seat / Math.Max(1, count) + wheelRot;
            return BlessingTheme.WheelCenter + ang.ToRotationVector2() * BlessingTheme.WheelRadius;
        }

        /// <summary>滚轮基准每帧都要吃掉，合屏期间不读就会在开屏首帧收到一个巨量增量</summary>
        private int ReadScrollDelta() {
            int wheel = Mouse.GetState().ScrollWheelValue;
            int delta = wheel - oldScrollWheelValue;
            oldScrollWheelValue = wheel;
            return delta;
        }

        public override void Update() {
            int scrollDelta = ReadScrollDelta();
            IReadOnlyList<Blessing> all = BlessingRegistry.All;
            if (seatHover.Length != all.Count) {
                seatHover = new float[all.Count];
            }

            if (!IsOpen && OpenProgress.Current <= 0.01f) {
                draggingWheel = false;
                return;
            }

            wheelRot = MathHelper.Lerp(wheelRot, wheelRotTarget, 0.14f);
            if (rejectShake > 0) {
                rejectShake--;
            }
            if (kindleAnim > 0f) {
                kindleAnim = Math.Max(0f, kindleAnim - 1f / KindleAnimFrames);
            }

            UIHitBox = new Rectangle(0, 0, (int)BlessingTheme.UIScreenW, (int)BlessingTheme.UIScreenH);
            hoverInMainPage = IsOpen;

            if (IsOpen) {
                //整屏接管：指针、滚轮换武器、背包配方栏滚动，三者每帧常驻
                Main.playerInventory = false;
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/BlessingWheel");
            }

            if (!IsOpen || OpenProgress.Current < 0.9f) {
                return;
            }

            //右键随手合拢（右键在本界面无别的用途）
            if (Main.mouseRight && Main.mouseRightRelease && !draggingWheel) {
                Close();
                return;
            }

            Vector2 mouseV = Main.MouseScreen;
            Point mouse = mouseV.ToPoint();
            Vector2 center = BlessingTheme.WheelCenter;

            //滚轮旋轮：一格一席
            if (scrollDelta != 0 && !draggingWheel) {
                wheelRotTarget += (scrollDelta > 0 ? 1f : -1f) * (MathHelper.TwoPi / Math.Max(1, all.Count));
            }

            //拖拽旋轮
            if (draggingWheel) {
                if (!Main.mouseLeft) {
                    draggingWheel = false;
                }
                else {
                    float ang = (mouseV - center).ToRotation();
                    wheelRotTarget = dragStartRot + MathHelper.WrapAngle(ang - dragStartAngle);
                    wheelRot = wheelRotTarget;
                }
            }

            //珠位悬停
            int newHover = -1;
            if (!draggingWheel) {
                for (int i = 0; i < all.Count; i++) {
                    if (Vector2.Distance(mouseV, SeatPos(i, all.Count)) < BlessingTheme.BeadRadius + 6f) {
                        newHover = i;
                        break;
                    }
                }
            }
            if (newHover != hoverSeat && newHover != -1) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.45f });
            }
            hoverSeat = newHover;
            for (int i = 0; i < all.Count; i++) {
                bool up = i == hoverSeat || i == selected;
                seatHover[i] = MathHelper.Lerp(seatHover[i], up ? 1f : 0f, 0.2f);
            }

            //悬停即算见过（只记已解锁的，新焰苗随之熄）
            if (hoverSeat >= 0 && BlessingWorld.IsUnlocked(all[hoverSeat])) {
                LocalBP.MarkWitnessed(all[hoverSeat]);
            }

            Rectangle button = KindleButtonRect();
            bool buttonVisible = selected >= 0 && BlessingWorld.IsUnlocked(all[selected]);
            buttonHover = MathHelper.Lerp(buttonHover,
                buttonVisible && button.Contains(mouse) ? 1f : 0f, 0.2f);

            //点击
            if (keyLeftPressState != KeyPressState.Pressed) {
                return;
            }
            if (hoverSeat >= 0) {
                selected = hoverSeat;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f });
                return;
            }
            if (buttonVisible && button.Contains(mouse)) {
                HandleKindleClick(all[selected]);
                return;
            }
            //空场按下开始拖旋（避开中心详情盘）
            if (Vector2.Distance(mouseV, center) > BlessingTheme.CenterRadius) {
                draggingWheel = true;
                dragStartAngle = (mouseV - center).ToRotation();
                dragStartRot = wheelRotTarget;
            }
        }

        /// <summary>点燃/熄灭：槽满拒绝要横震 + 提示，成功点燃有火焰蔓延演出</summary>
        private void HandleKindleClick(Blessing blessing) {
            BlessingPlayer bp = LocalBP;
            if (bp.Kindled.Contains(blessing.ID)) {
                bp.Snuff(blessing);
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.45f, Volume = 0.7f });
                return;
            }
            if (bp.TryKindle(blessing)) {
                kindleAnim = 1f;
                kindleSeat = selected;
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Volume = 0.35f, Pitch = 0.4f });
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.1f });
                return;
            }
            rejectShake = 14;
            VaultUtils.Text(BlessingSystemText.SlotFullNotice.Value, BlessingTheme.Ember);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.6f, Volume = 0.7f });
        }

        private Rectangle KindleButtonRect() {
            Rectangle rect = BlessingTheme.KindleButton;
            if (rejectShake > 0) {
                rect.X += (int)(MathF.Sin(rejectShake * 1.35f) * (rejectShake / 14f) * 4f);
            }
            return rect;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float prog = OpenProgress.Current;
            float alpha = 1f - (1f - prog) * (1f - prog) * (1f - prog);
            if (alpha <= 0.01f) {
                return;
            }

            IReadOnlyList<Blessing> all = BlessingRegistry.All;
            BlessingPlayer bp = LocalBP;
            int cap = BlessingPlayer.SlotCap;
            int burningCount = bp.BurningCount;
            Vector2 center = BlessingTheme.WheelCenter;
            float radius = BlessingTheme.WheelRadius;

            //1 盘底
            BlessingRenderer.DrawWheelBackground(spriteBatch, alpha * 0.97f,
                cap > 0 ? burningCount / (float)cap : 0f);

            //2 标题与燃焰计数
            Utils.DrawBorderStringBig(spriteBatch, BlessingSystemText.WheelTitle.Value,
                new Vector2(center.X, center.Y - radius - 84f), BlessingTheme.Accent * alpha, 0.92f, 0.5f, 0.5f);
            bool full = burningCount >= cap;
            Utils.DrawBorderString(spriteBatch,
                BlessingSystemText.BurningCounter.Format(burningCount, cap),
                new Vector2(center.X, center.Y - radius - 46f),
                (full ? BlessingTheme.Ember : BlessingTheme.BoneDim) * alpha, 0.95f, 0.5f, 0.5f);

            //轮底关闭提示：动态键位文案
            string keyText = CWRKeySystem.GetKeybindText(CWRKeySystem.Blessing_Key, CWRKeySystem.Notbound.Value);
            Utils.DrawBorderString(spriteBatch,
                BlessingSystemText.CloseHint.Format(keyText),
                new Vector2(center.X, center.Y + radius + 52f),
                BlessingTheme.BoneDim * (alpha * 0.8f), 0.82f, 0.5f, 0.5f);

            //3 魂焰先落一批（画在珠圈之下）
            flameScratch.Clear();
            for (int i = 0; i < all.Count; i++) {
                if (!ViewBurning(bp, all[i])) {
                    continue;
                }
                float scale = 1f + seatHover[i] * 0.3f;
                Vector2 pos = SeatPos(i, all.Count);
                int w = (int)(70f * scale);
                int h = (int)(84f * scale);
                flameScratch.Add(new FlameCell {
                    //焰根在 quad 纵向 3/4 处，对齐珠心
                    Rect = new Rectangle((int)(pos.X - w / 2f), (int)(pos.Y - h * 0.75f), w, h),
                    Seed = i * 7.77f,
                    Lit = 1f,
                    Alpha = alpha * 0.95f,
                });
            }
            BlessingRenderer.DrawFlames(spriteBatch, flameScratch);

            //4 珠位：环 + 符纹 + 新焰苗
            for (int i = 0; i < all.Count; i++) {
                DrawBead(spriteBatch, all[i], i, bp, alpha);
            }

            //5 点燃演出：选中珠位冲出的扩散环
            if (kindleAnim > 0f && kindleSeat >= 0 && kindleSeat < all.Count) {
                Vector2 pos = SeatPos(kindleSeat, all.Count);
                float ease = 1f - kindleAnim;
                BlessingRenderer.DrawRingPasses(spriteBatch, pos,
                    MathHelper.Lerp(BlessingTheme.BeadRadius, BlessingTheme.BeadRadius * 3.4f, ease * ease),
                    Color.Lerp(BlessingTheme.Accent, BlessingTheme.Ember, 0.5f), kindleAnim * 0.85f * alpha);
                BlessingRenderer.DrawGlow(spriteBatch, pos, BlessingTheme.BeadRadius * 4.5f,
                    BlessingTheme.Ember, kindleAnim * 0.5f * alpha);
            }

            //6 中心详情
            DrawCenter(spriteBatch, all, bp, alpha);
        }

        private void DrawBead(SpriteBatch sb, Blessing blessing, int seat, BlessingPlayer bp, float alpha) {
            bool unlocked = ViewUnlocked(blessing);
            bool burning = ViewBurning(bp, blessing);
            float hoverA = seatHover[seat];
            float scale = 1f + hoverA * 0.3f;
            Vector2 pos = SeatPos(seat, BlessingRegistry.All.Count);
            float beadR = BlessingTheme.BeadRadius * scale;

            Color ringCol;
            Color sigilCol;
            Color? sigilCore = null;
            if (burning) {
                ringCol = BlessingTheme.Accent;
                sigilCol = BlessingTheme.Ember;
                sigilCore = Color.Lerp(BlessingTheme.Ember, Color.White, 0.35f) * 0.8f;
            }
            else if (unlocked) {
                ringCol = BlessingTheme.BoneDim;
                sigilCol = Color.Lerp(BlessingTheme.BoneDim, Color.White, 0.18f);
            }
            else {
                ringCol = BlessingTheme.BoneDim * 0.38f;
                sigilCol = BlessingTheme.BoneDim * 0.34f;
            }

            //珠环：燃焰珠双 pass 带辉，其余单线
            if (burning) {
                BlessingRenderer.DrawRingPasses(sb, pos, beadR, ringCol, alpha * (0.75f + hoverA * 0.25f));
            }
            else {
                BlessingRenderer.DrawRing(sb, pos, beadR, unlocked ? 1.8f : 1.2f,
                    ringCol * (alpha * (0.8f + hoverA * 0.2f)));
            }

            //符纹
            BlessingRenderer.DrawSigil(sb, blessing, pos, BlessingTheme.BeadSigilScale * scale,
                sigilCol * alpha, 1.4f, alpha * (unlocked ? 1f : 0.8f), sigilCore);

            //新焰苗：解锁未看过的珠位一粒呼吸余烬
            if (unlocked && !bp.Witnessed.Contains(blessing.ID)) {
                float breath = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.07f + seat);
                Vector2 sprout = pos + new Vector2(beadR * 0.8f, -beadR * 0.8f);
                BlessingRenderer.DrawGlow(sb, sprout, 14f + breath * 6f, BlessingTheme.Ember,
                    (0.35f + 0.4f * breath) * alpha);
            }

            //悬停浮名
            if (hoverA > 0.35f && seat == hoverSeat) {
                Utils.DrawBorderString(sb, blessing.DisplayName.Value,
                    pos + new Vector2(0f, beadR + 16f),
                    Color.Lerp(BlessingTheme.BoneDim, BlessingTheme.Ember, hoverA) * (alpha * hoverA),
                    0.85f, 0.5f, 0.5f);
            }
        }

        private void DrawCenter(SpriteBatch sb, IReadOnlyList<Blessing> all, BlessingPlayer bp, float alpha) {
            Vector2 center = BlessingTheme.WheelCenter;
            float radius = BlessingTheme.WheelRadius;

            if (selected < 0 || selected >= all.Count) {
                Utils.DrawBorderString(sb, BlessingSystemText.CenterHint.Value,
                    new Vector2(center.X, center.Y),
                    BlessingTheme.BoneDim * (alpha * (0.55f + 0.15f * MathF.Sin(Main.GameUpdateCount * 0.03f))),
                    0.95f, 0.5f, 0.5f);
                return;
            }

            Blessing blessing = all[selected];
            bool unlocked = ViewUnlocked(blessing);
            bool kindled = ViewBurning(bp, blessing) || (bp.Kindled.Contains(blessing.ID) && unlocked);

            //大符纹
            Color sigilCol = kindled ? BlessingTheme.Ember : unlocked ? BlessingTheme.BoneDim : BlessingTheme.BoneDim * 0.5f;
            BlessingRenderer.DrawSigil(sb, blessing, center - new Vector2(0f, radius * 0.18f), 34f,
                sigilCol * alpha, 1.8f, alpha, kindled ? Color.Lerp(BlessingTheme.Ember, Color.White, 0.3f) * 0.8f : null);

            //名与效果
            Utils.DrawBorderStringBig(sb, blessing.DisplayName.Value,
                center + new Vector2(0f, radius * 0.02f),
                (unlocked ? BlessingTheme.Accent : BlessingTheme.BoneDim) * alpha, 0.62f, 0.5f, 0.5f);
            string body = unlocked ? blessing.Description.Value : BlessingSystemText.LockedHint.Value;
            Utils.DrawBorderString(sb, body, center + new Vector2(0f, radius * 0.15f),
                (unlocked ? Color.Lerp(BlessingTheme.BoneDim, Color.White, 0.35f) : BlessingTheme.BoneDim) * alpha,
                0.92f, 0.5f, 0.5f);

            if (!unlocked) {
                return;
            }

            //点燃/熄灭按钮：漆底 + 边线，悬停染 accent
            Rectangle button = KindleButtonRect();
            Texture2D pixel = VaultAsset.placeholder2.Value;
            var one = new Rectangle(0, 0, 1, 1);
            sb.Draw(pixel, button, one, BlessingTheme.NightBase * (0.92f * alpha));
            Color border = Color.Lerp(kindled ? BlessingTheme.Ember : BlessingTheme.Accent,
                Color.White, buttonHover * 0.35f) * alpha;
            BlessingRenderer.DrawLine(sb, new Vector2(button.X, button.Y), new Vector2(button.Right, button.Y), 1.6f, border);
            BlessingRenderer.DrawLine(sb, new Vector2(button.X, button.Bottom), new Vector2(button.Right, button.Bottom), 1.6f, border * 0.7f);
            BlessingRenderer.DrawLine(sb, new Vector2(button.X, button.Y), new Vector2(button.X, button.Bottom), 1.2f, border * 0.8f);
            BlessingRenderer.DrawLine(sb, new Vector2(button.Right, button.Y), new Vector2(button.Right, button.Bottom), 1.2f, border * 0.8f);
            string label = kindled ? BlessingSystemText.SnuffLabel.Value : BlessingSystemText.KindleLabel.Value;
            Utils.DrawBorderString(sb, label, button.Center.ToVector2() + new Vector2(0f, 2f),
                Color.Lerp(border, Color.White, 0.2f), 1f, 0.5f, 0.5f);
        }
    }
}
