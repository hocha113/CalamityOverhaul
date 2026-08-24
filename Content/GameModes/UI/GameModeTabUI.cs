using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.UI
{
    /// <summary>
    /// 背包右缘的游戏模式标签行：残酷模式常驻，修罗模式在残酷开启后从其背后滑出。
    /// 标签动画由旗标真值差分点火（本地点击与联机回执走同一条表现路径）；
    /// 切换演出大字也由本 UI 绘制，背包合上时靠 <see cref="GameModeCeremony.LineActive"/> 维持活跃
    /// </summary>
    internal class GameModeTabUI : UIHandle
    {
        public static GameModeTabUI Instance => UIHandleLoader.GetUIHandleOfType<GameModeTabUI>();

        /// <summary>切换爆发动画时长（帧）</summary>
        private const float BurstStep = 1f / 42f;
        /// <summary>拒绝横震时长（帧）</summary>
        private const int ShakeDuration = 14;
        /// <summary>活跃间隔超过该帧数视为新会话，旗标差分静默重播（进档同步不该放爆发）</summary>
        private const uint SessionGapTicks = 30;

        private float brutalLit;
        private float asuraLit;
        private float asuraReveal;
        private float brutalHover;
        private float asuraHover;
        private float disabledDim;
        private float brutalBurst = 1f;
        private float asuraBurst = 1f;
        private bool brutalBurstOn;
        private bool asuraBurstOn;
        private int shakeTimer;
        private int shakeTab;
        private bool prevBrutal;
        private bool prevAsura;
        private int hoverKind = -1;
        private uint lastUpdateTick;

        private static bool TabsVisible => Main.playerInventory && !Main.gameMenu;

        public override bool Active =>
            TabsVisible
            || GameModeCeremony.LineActive
            || brutalBurst < 1f || asuraBurst < 1f
            || asuraReveal is > 0.01f and < 0.99f;

        public override void Update() {
            GameModeCeremony.UpdateLine();

            uint tick = Main.GameUpdateCount;
            bool freshSession = tick - lastUpdateTick > SessionGapTicks;
            lastUpdateTick = tick;
            if (freshSession) {
                //长间隔后的第一帧：静默对齐旗标（进档/换世界不该播切换爆发）
                prevBrutal = GameModeSystem.BrutalActive;
                prevAsura = GameModeSystem.AsuraActive;
                shakeTimer = 0;
            }

            //真值差分点火切换爆发
            if (GameModeSystem.BrutalActive != prevBrutal) {
                prevBrutal = GameModeSystem.BrutalActive;
                brutalBurst = 0f;
                brutalBurstOn = prevBrutal;
            }
            if (GameModeSystem.AsuraActive != prevAsura) {
                prevAsura = GameModeSystem.AsuraActive;
                asuraBurst = 0f;
                asuraBurstOn = prevAsura;
            }

            brutalBurst = Math.Min(1f, brutalBurst + BurstStep);
            asuraBurst = Math.Min(1f, asuraBurst + BurstStep);

            brutalLit = Ease(brutalLit, GameModeSystem.BrutalActive ? 1f : 0f, 0.12f);
            asuraLit = Ease(asuraLit, GameModeSystem.AsuraActive ? 1f : 0f, 0.12f);
            asuraReveal = Ease(asuraReveal, GameModeSystem.BrutalActive ? 1f : 0f, 0.13f);
            disabledDim = Ease(disabledDim, GameModeSystem.CanToggleNow() ? 0f : 1f, 0.15f);

            if (shakeTimer > 0) {
                shakeTimer--;
            }

            UpdateInteraction();
        }

        private void UpdateInteraction() {
            //先算后比，悬停 tick 只在进入新目标时响一次
            int next = -1;
            if (TabsVisible) {
                Point mouse = Main.MouseScreen.ToPoint();
                if (GameModeTheme.BrutalTab.Contains(mouse)) {
                    next = 0;
                }
                else if (asuraReveal > 0.6f && GameModeTheme.AsuraTab(EasedReveal()).Contains(mouse)) {
                    next = 1;
                }
            }
            if (next != hoverKind && next != -1) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f });
            }
            hoverKind = next;
            brutalHover = Ease(brutalHover, hoverKind == 0 ? 1f : 0f, 0.2f);
            asuraHover = Ease(asuraHover, hoverKind == 1 ? 1f : 0f, 0.2f);

            if (hoverKind < 0) {
                return;
            }

            player.mouseInterface = true;
            GameModeKind kind = hoverKind == 0 ? GameModeKind.Brutal : GameModeKind.Asura;
            Main.hoverItemName = ComposeTip(kind);

            if (keyLeftPressState != KeyPressState.Pressed) {
                return;
            }

            if (GameModeSystem.RequestToggle(kind)) {
                //单人当帧生效由差分点火接管；联机等服务端回执，这里只给受理音
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else {
                //拒绝必有回应：横震 + 闷响（Boss 在场或修罗依赖未满足）
                shakeTimer = ShakeDuration;
                shakeTab = hoverKind;
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.6f, Volume = 0.7f });
            }
        }

        private static string ComposeTip(GameModeKind kind) {
            bool on = kind == GameModeKind.Brutal ? GameModeSystem.BrutalActive : GameModeSystem.AsuraActive;
            GameModeFace face = GameModeSystem.FaceOf(kind);
            string state = on ? GameModeText.StateOn.Value : GameModeText.StateOff.Value;
            string hint = !GameModeSystem.CanToggleNow() ? GameModeText.BossRefuse.Value
                : on ? GameModeText.HintDisable.Value : GameModeText.HintEnable.Value;
            return GameModeText.Name(face).Value + " · " + state
                + "\n" + GameModeText.Desc(face).Value
                + "\n" + hint;
        }

        private float EasedReveal() => MathHelper.SmoothStep(0f, 1f, asuraReveal);

        public override void Draw(SpriteBatch spriteBatch) {
            if (TabsVisible) {
                //修罗先画（从残酷背后滑出），残酷压在上层；天顶世界里修罗恒以毁灭脸示人
                float reveal = EasedReveal();
                if (reveal > 0.01f) {
                    Rectangle asuraRect = GameModeTheme.AsuraTab(reveal);
                    if (shakeTimer > 0 && shakeTab == 1) {
                        asuraRect.X += ShakeOffset();
                    }
                    GameModeRenderer.DrawTab(spriteBatch, asuraRect, GameModeSystem.FaceOf(GameModeKind.Asura),
                        asuraLit, asuraHover, asuraBurst, asuraBurstOn, disabledDim, reveal);
                }

                Rectangle brutalRect = GameModeTheme.BrutalTab;
                if (shakeTimer > 0 && shakeTab == 0) {
                    brutalRect.X += ShakeOffset();
                }
                GameModeRenderer.DrawTab(spriteBatch, brutalRect, GameModeFace.Brutal,
                    brutalLit, brutalHover, brutalBurst, brutalBurstOn, disabledDim, 1f);
            }

            GameModeRenderer.DrawCeremonyLine(spriteBatch);
        }

        private int ShakeOffset()
            => (int)(MathF.Sin(shakeTimer * 1.35f) * (shakeTimer / (float)ShakeDuration) * 4f);

        private static float Ease(float cur, float target, float rate) {
            float next = MathHelper.Lerp(cur, target, rate);
            return Math.Abs(next - target) < 0.004f ? target : next;
        }
    }
}
