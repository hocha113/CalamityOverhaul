using CalamityOverhaul.Content.UIs.HudStack;
using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.WeaponSkills
{
    /// <summary>
    /// 手持技能按钮 HUD:手持实现 <see cref="IWeaponSkillProvider"/> 的武器时,
    /// 屏幕左下角亮出两枚可点击的技能按钮(接入 <see cref="BottomLeftHudStack"/> 避让)
    /// <br/>共享机枢(外框/冷却弧/悬停面板/点击反馈)在此,图标与色板由武器提供
    /// </summary>
    internal sealed class WeaponSkillHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "UI";
        public static WeaponSkillHud Instance => UIHandleLoader.GetUIHandleOfType<WeaponSkillHud>();

        public static LocalizedText ReadyHint { get; private set; }
        public static LocalizedText AimHint { get; private set; }
        public static LocalizedText ReleaseHint { get; private set; }
        public static LocalizedText CooldownFormat { get; private set; }
        public static LocalizedText ActiveLine { get; private set; }
        public static LocalizedText ManaCostFormat { get; private set; }

        /// <summary>本地正在按住拖放落点技能,供 SetControls 锁普攻</summary>
        public static bool IsAiming { get; private set; }

        public override void SetStaticDefaults() {
            ReadyHint = this.GetLocalization(nameof(ReadyHint), () => "就绪，点击施放");
            AimHint = this.GetLocalization(nameof(AimHint), () => "按住拖到落点，松开施放");
            ReleaseHint = this.GetLocalization(nameof(ReleaseHint), () => "松开施放");
            CooldownFormat = this.GetLocalization(nameof(CooldownFormat), () => "冷却剩余 {0} 秒");
            ActiveLine = this.GetLocalization(nameof(ActiveLine), () => "施放中");
            ManaCostFormat = this.GetLocalization(nameof(ManaCostFormat), () => "魔力消耗 {0}");
        }

        #region 几何
        private const int SlotCount = 2;
        /// <summary>按钮盘半径</summary>
        private const float ButtonR = 25f;
        /// <summary>命中半径</summary>
        private const float HitR = 29f;
        /// <summary>图标内容半径</summary>
        private const float IconR = 14f;
        /// <summary>双钮心距</summary>
        private const float Spacing = 66f;
        /// <summary>簇心距屏左</summary>
        private const float AnchorX = 104f;
        /// <summary>簇心距屏底</summary>
        private const float AnchorUp = 86f;

        private static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;
        private static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

        /// <summary>簇心自然锚点(未避让)</summary>
        public static Vector2 NaturalAnchor => new(AnchorX, BottomLeftHudStack.UIScreenH - AnchorUp);
        #endregion

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        float IBottomLeftHud.HudStackTopExtent => ButtonR + 16f;
        float IBottomLeftHud.HudStackBottomExtent => ButtonR + 10f;
        #endregion

        private IWeaponSkillProvider provider;
        private float appear;
        //全屏界面让位:交互当帧断电,绘制随此值淡出
        private float fsFade = 1f;
        private float time;
        private int hoverSlot = -1;
        private int dragSlot = -1;
        private bool draggedOff;
        private readonly float[] hoverAnim = new float[SlotCount];
        private readonly float[] pressAnim = new float[SlotCount];
        private readonly float[] denyAnim = new float[SlotCount];
        private readonly Vector2[] centers = new Vector2[SlotCount];
        private readonly WeaponSkillView[] views = new WeaponSkillView[SlotCount];

        public override Vector2 MousePosition => UIMouse;

        private static IWeaponSkillProvider ResolveProvider() {
            if (Main.gameMenu || Main.dedServ) {
                return null;
            }
            Item item = Main.LocalPlayer?.HeldItem;
            if (item == null || !item.Alives()) {
                return null;
            }
            return item.ModItem as IWeaponSkillProvider;
        }

        public override bool Active => ResolveProvider() != null || appear > 0.01f;

        public override void LogicUpdate() {
            provider = ResolveProvider();
            time += 1f / 60f;
            if (time > 3600f) {
                time -= 3600f;
            }
            fsFade = MathHelper.Clamp(fsFade + (FullScreenUIHub.AnyOpen ? -0.12f : 0.12f), 0f, 1f);

            Player lp = Main.LocalPlayer;
            bool forceCancel = provider == null
                || lp == null || !lp.active || lp.dead
                || FullScreenUIHub.AnyOpen || RadialWheelHub.AnyOpen;
            if (forceCancel) {
                CancelAim(false);
            }

            if (provider == null) {
                //切离武器立即收起,视觉状态一并清零
                appear = 0f;
                hoverSlot = -1;
                for (int i = 0; i < SlotCount; i++) {
                    hoverAnim[i] = pressAnim[i] = denyAnim[i] = 0f;
                }
                return;
            }
            appear = MathHelper.Clamp(appear + 0.08f, 0f, 1f);
            if (dragSlot >= 0) {
                UIInputGuard.SuppressWeaponSwitch();
            }
        }

        public override void Update() {
            //Update 跑在绘制阶段,首帧可能先于 LogicUpdate
            provider ??= ResolveProvider();
            if (provider == null) {
                CancelAim(false);
                return;
            }

            Vector2 anchor = BottomLeftHudStack.ResolveAnchor(this);
            Player lp = Main.LocalPlayer;
            for (int i = 0; i < SlotCount; i++) {
                centers[i] = anchor + new Vector2((i - (SlotCount - 1) * 0.5f) * Spacing, 0f);
                views[i] = provider.GetWeaponSkill(i, lp);
            }

            bool inputOk = appear > 0.9f && fsFade > 0.9f
                && !FullScreenUIHub.AnyOpen && !RadialWheelHub.AnyOpen;
            Vector2 mouse = UIMouse;

            if (dragSlot >= 0) {
                HoldAimLock();
                if (mouse.Distance(centers[dragSlot]) > HitR) {
                    draggedOff = true;
                }
                hoverSlot = dragSlot;
                TickButtonAnims();
                if (keyRightPressState == KeyPressState.Pressed) {
                    CancelAim(true);
                    return;
                }
                if (keyLeftPressState == KeyPressState.Released) {
                    if (draggedOff) {
                        TryRelease(dragSlot, lp);
                    }
                    else {
                        CancelAim(true);
                    }
                }
                return;
            }

            int nextHover = -1;
            if (inputOk) {
                for (int i = 0; i < SlotCount; i++) {
                    if (mouse.Distance(centers[i]) <= HitR) {
                        nextHover = i;
                        break;
                    }
                }
            }
            //先算目标再比对,进入真目标才响一声
            if (nextHover >= 0 && nextHover != hoverSlot) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f });
            }
            hoverSlot = nextHover;
            TickButtonAnims();

            if (hoverSlot >= 0) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    TryPress(hoverSlot, lp);
                }
            }
        }

        private void TickButtonAnims() {
            for (int i = 0; i < SlotCount; i++) {
                hoverAnim[i] = MathHelper.Lerp(hoverAnim[i], i == hoverSlot ? 1f : 0f, 0.22f);
                if (i == dragSlot) {
                    pressAnim[i] = 1f;
                }
                else {
                    pressAnim[i] *= 0.82f;
                }
                denyAnim[i] = MathF.Max(denyAnim[i] - 0.075f, 0f);
            }
        }

        private void HoldAimLock() {
            player.mouseInterface = true;
            UIInputGuard.SuppressWeaponSwitch();
        }

        private void BeginAim(int slot) {
            dragSlot = slot;
            draggedOff = false;
            IsAiming = true;
            pressAnim[slot] = 1f;
        }

        private void CancelAim(bool softSound) {
            if (dragSlot < 0) {
                IsAiming = false;
                draggedOff = false;
                return;
            }
            if (softSound) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f });
            }
            pressAnim[dragSlot] = 0f;
            dragSlot = -1;
            draggedOff = false;
            IsAiming = false;
        }

        private void TryPress(int slot, Player lp) {
            WeaponSkillView view = views[slot];
            if (!view.Ready) {
                Deny(slot);
                return;
            }
            if (view.NeedsAim) {
                BeginAim(slot);
                return;
            }
            Fire(slot, lp, Main.MouseWorld);
        }

        private void TryRelease(int slot, Player lp) {
            int held = slot;
            CancelAim(false);
            if (!views[held].Ready) {
                Deny(held);
                return;
            }
            Fire(held, lp, Main.MouseWorld);
        }

        private void Fire(int slot, Player lp, Vector2 aimWorld) {
            bool ok = provider.TriggerWeaponSkill(slot, lp, aimWorld);
            if (ok) {
                pressAnim[slot] = 1f;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.65f });
                views[slot] = provider.GetWeaponSkill(slot, lp);
            }
            else {
                Deny(slot);
            }
        }

        private void Deny(int slot) {
            denyAnim[slot] = 1f;
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.7f, Volume = 0.75f });
        }

        public override void Draw(SpriteBatch spriteBatch) {
            float a = appear * fsFade;
            if (provider == null || a <= 0.02f) {
                return;
            }
            float ease = 1f - (1f - appear) * (1f - appear);
            float rise = (1f - ease) * 14f;
            for (int i = 0; i < SlotCount; i++) {
                DrawButton(spriteBatch, i, centers[i] + new Vector2(0f, rise), a, ease);
            }
            int panelSlot = dragSlot >= 0 ? dragSlot : hoverSlot;
            if (panelSlot >= 0 && a > 0.85f) {
                DrawHoverPanel(spriteBatch, panelSlot, a);
            }
            if (dragSlot >= 0 && a > 0.85f) {
                DrawAimPreview(spriteBatch, views[dragSlot], a);
            }
        }

        private void DrawButton(SpriteBatch sb, int slot, Vector2 center, float a, float ease) {
            WeaponSkillView view = views[slot];
            Color accent = view.Accent;
            float hover = hoverAnim[slot];
            float deny = denyAnim[slot];
            float press = pressAnim[slot];

            //拒绝横震只震这一枚
            center.X += MathF.Sin(deny * 26f) * 3.2f * deny;
            float scale = (0.72f + 0.28f * ease) * (1f - 0.09f * press) * (1f + 0.05f * hover);
            float r = ButtonR * scale;

            float cd01 = view.CooldownTotal > 0
                ? MathHelper.Clamp(view.CooldownLeft / (float)view.CooldownTotal, 0f, 1f)
                : 0f;
            bool coolingDown = view.CooldownLeft > 0;
            float lit = view.Ready ? 1f : view.Alive ? 0.9f : 0.35f + 0.3f * (1f - cd01);

            float breath = 0.5f + 0.5f * MathF.Sin(time * MathHelper.TwoPi * 0.5f + slot * 1.7f);
            float alivePulse = 0.5f + 0.5f * MathF.Sin(time * MathHelper.TwoPi * 1.6f);

            //底光:就绪呼吸,施放中快脉,冷却近熄
            float glowA = view.Ready ? 0.24f + 0.12f * breath
                : view.Alive ? 0.28f + 0.18f * alivePulse
                : 0.06f;
            WeaponSkillBrush.DrawGlow(sb, center, r * 2.1f, accent, glowA * a);

            //贴身投影(小偏移,非同心羽化)
            WeaponSkillBrush.DrawFilledCircle(sb, center + new Vector2(2f, 3f), r, Color.Black * (0.4f * a));

            //盘底与内缘厚度
            Color baseCol = Color.Lerp(new Color(11, 11, 16), accent, 0.1f);
            WeaponSkillBrush.DrawFilledCircle(sb, center, r, baseCol * (0.94f * a));
            WeaponSkillBrush.DrawRing(sb, center, r - 1.6f, 2.2f, Color.Black * (0.32f * a), 40);

            //图标交给武器
            provider.DrawWeaponSkillIcon(sb, slot, center, IconR * scale, lit, time, a);

            //冷却:暗幕压图标+恢复弧自顶顺时针长回来
            if (coolingDown) {
                WeaponSkillBrush.DrawFilledCircle(sb, center, r - 2.5f, Color.Black * (0.38f * a));
                float arcR = r - 4f;
                WeaponSkillBrush.DrawRing(sb, center, arcR, 1f, new Color(120, 120, 130) * (0.25f * a), 40);
                float sweep = MathHelper.TwoPi * (1f - cd01);
                if (sweep > 0.02f) {
                    WeaponSkillBrush.DrawArc(sb, center, arcR, 2.6f, accent * (0.9f * a),
                        -MathHelper.PiOver2, -MathHelper.PiOver2 + sweep, 44);
                }
            }

            //缘环按状态归色:施放中白脉冲,就绪亮呼吸,其余沉暗
            Color rimCol;
            float rimTh = 1.8f;
            if (view.Alive) {
                rimCol = Color.Lerp(accent, Color.White, 0.35f * alivePulse);
                rimTh += 0.8f * alivePulse;
            }
            else if (view.Ready) {
                rimCol = accent * (0.85f + 0.15f * breath);
            }
            else {
                rimCol = accent * 0.32f;
            }
            WeaponSkillBrush.DrawRing(sb, center, r, rimTh, rimCol * a, 44);

            if (hover > 0.02f) {
                WeaponSkillBrush.DrawRing(sb, center, r + 3.5f, 1.1f, accent * (0.55f * hover * a), 44);
            }

            if (deny > 0.02f) {
                Color denyCol = new(255, 70, 60);
                WeaponSkillBrush.DrawRing(sb, center, r + 1.5f, 2f, denyCol * (0.8f * deny * a), 44);
                WeaponSkillBrush.DrawFilledCircle(sb, center, r - 2f, denyCol * (0.16f * deny * a));
            }
        }

        private void DrawHoverPanel(SpriteBatch sb, int slot, float a) {
            WeaponSkillView view = views[slot];
            var font = FontAssets.MouseText.Value;

            string status;
            Color statusCol;
            if (dragSlot == slot) {
                status = ReleaseHint.Value;
                statusCol = Color.Lerp(view.Accent, Color.White, 0.35f);
            }
            else if (view.Alive) {
                status = ActiveLine.Value;
                statusCol = Color.Lerp(view.Accent, Color.White, 0.4f);
            }
            else if (view.CooldownLeft > 0) {
                status = string.Format(CooldownFormat.Value, (view.CooldownLeft / 60f).ToString("0.0"));
                statusCol = new Color(168, 168, 176);
            }
            else if (view.NeedsAim) {
                status = AimHint.Value;
                statusCol = view.Accent;
            }
            else {
                status = ReadyHint.Value;
                statusCol = view.Accent;
            }

            //行集:题行/说明(可多行)/消耗/状态
            var rowBuf = new (string text, float scale, Color color)[6];
            int rows = 0;
            rowBuf[rows++] = (view.Name, 1f, Color.Lerp(Color.White, view.Accent, 0.35f));
            if (!string.IsNullOrEmpty(view.Desc)) {
                foreach (string line in view.Desc.Split('\n')) {
                    if (rows < 4 && !string.IsNullOrWhiteSpace(line)) {
                        rowBuf[rows++] = (line, 0.85f, new Color(206, 206, 216));
                    }
                }
            }
            if (!string.IsNullOrEmpty(view.CostLine)) {
                rowBuf[rows++] = (view.CostLine, 0.75f, new Color(152, 152, 164));
            }
            rowBuf[rows++] = (status, 0.78f, statusCol);

            const float PadX = 12f;
            const float PadY = 9f;
            const float RowGap = 4f;
            float w = 0f;
            float h = PadY * 2f;
            Span<float> rowH = stackalloc float[6];
            rowH.Clear();
            for (int i = 0; i < rows; i++) {
                Vector2 size = font.MeasureString(rowBuf[i].text) * rowBuf[i].scale;
                w = MathF.Max(w, size.X);
                rowH[i] = size.Y;
                h += size.Y + (i > 0 ? RowGap : 0f);
            }
            w += PadX * 2f;

            Vector2 c = centers[slot];
            float px = MathHelper.Clamp(c.X - w * 0.5f, 8f, MathF.Max(8f, UIScreenW - w - 8f));
            float py = c.Y - ButtonR - 12f - h;
            Rectangle panel = new((int)px, (int)py, (int)w, (int)h);

            //实底+1px 边+顶缘受光线,无暗羽化
            WeaponSkillBrush.FillRect(sb, panel, new Color(10, 10, 15) * (0.92f * a));
            WeaponSkillBrush.StrokeRect(sb, panel, 1, view.Accent * (0.5f * a));
            WeaponSkillBrush.FillRect(sb, new Rectangle(panel.X + 1, panel.Y + 1, panel.Width - 2, 1),
                view.Accent * (0.85f * a));

            float curY = panel.Y + PadY;
            for (int i = 0; i < rows; i++) {
                Utils.DrawBorderString(sb, rowBuf[i].text,
                    new Vector2(panel.X + PadX, curY), rowBuf[i].color * a, rowBuf[i].scale);
                curY += rowH[i] + RowGap;
            }
        }

        private static void DrawAimPreview(SpriteBatch sb, WeaponSkillView view, float a) {
            Vector2 c = UIMouse;
            Color accent = view.Accent;
            WeaponSkillBrush.DrawGlow(sb, c, 18f, accent, 0.28f * a);
            WeaponSkillBrush.DrawRing(sb, c, 11f, 1.4f, accent * (0.9f * a), 28);
            WeaponSkillBrush.DrawRing(sb, c, 4f, 1f,
                Color.Lerp(accent, Color.White, 0.45f) * a, 16);
            for (int i = 0; i < 4; i++) {
                Vector2 dir = (MathHelper.PiOver2 * i).ToRotationVector2();
                WeaponSkillBrush.Line(sb, c + dir * 7f, c + dir * 15f, accent * (0.85f * a), 1.3f);
            }

            if (view.AimPreviewRadius <= 8f) {
                return;
            }
            float zoom = Main.GameViewMatrix.Zoom.X;
            float uiR = view.AimPreviewRadius * zoom / Main.UIScale;
            if (uiR < 12f) {
                return;
            }
            int segs = Math.Clamp((int)(uiR * 0.35f), 36, 80);
            WeaponSkillBrush.DrawRing(sb, c, uiR, 1.6f, accent * (0.55f * a), segs);
        }
    }
}
