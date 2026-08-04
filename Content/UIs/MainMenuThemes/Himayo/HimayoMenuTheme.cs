using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuThemes.Himayo
{
    internal static class HimayoMenuTheme
    {
        private static readonly HimayoMenuState State = new();
        private static readonly HimayoMenuRenderer Renderer = new();
        private static bool subscribed;

        internal static Asset<DynamicSpriteFont> Font => FontAssets.MouseText;

        internal static bool ShouldDrawNativeLogo
            => !HimayoMenuVanillaBridge.BridgeOperational || !State.IsTitlePage;

        internal static void LoadAssets(Mod mod) {
            Renderer.LoadAssets(mod);
            if (!subscribed) {
                HimayoMenuVanillaBridge.FrameUpdate += State.AdvanceFrame;
                subscribed = true;
            }
        }

        internal static void UnloadAssets() {
            if (subscribed) {
                HimayoMenuVanillaBridge.FrameUpdate -= State.AdvanceFrame;
                subscribed = false;
            }
            State.Deselect();
            Renderer.UnloadAssets();
        }

        internal static void OnSelected() {
            HimayoMenuVanillaBridge.SetThemeActive(true);
            State.Select();
        }

        internal static void OnDeselected() {
            State.Deselect();
            HimayoMenuVanillaBridge.SetThemeActive(false);
        }

        internal static void DrawBackground(SpriteBatch spriteBatch)
            => Renderer.DrawBackground(spriteBatch, State);

        internal static void DrawForeground(SpriteBatch spriteBatch)
            => Renderer.DrawForeground(spriteBatch, State);
    }

    internal sealed class HimayoMenuState
    {
        private const float FixedStep = 1f / 60f;
        private const int MaxCatchUpSteps = 4;
        private const float CameraSpringFrequency = 6.5f;
        private const float TitleYawLimit = MathHelper.Pi / 10f;
        private const float TitlePitchLimit = MathHelper.Pi / 18f;
        private const float ChildYawLimit = MathHelper.Pi / 36f;
        private const float ChildPitchLimit = MathHelper.Pi / 60f;

        private readonly float[] previousHover = new float[HimayoMenuLayout.ButtonCount];
        private readonly float[] currentHover = new float[HimayoMenuLayout.ButtonCount];
        private readonly float[] previousEntry = new float[HimayoMenuLayout.ButtonCount];
        private readonly float[] currentEntry = new float[HimayoMenuLayout.ButtonCount];

        private CameraState previousCamera;
        private CameraState currentCamera;
        private double accumulator;
        private float interpolation;
        private float elapsed;
        private bool active;
        private bool previousTitlePage;
        private bool renderLeftDown;
        private bool renderRightDown;
        private bool previousInputBlocked;
        private int hoveredButton = -1;

        internal HimayoMenuLayout Layout { get; } = new();

        internal HimayoPetalField Petals { get; } = new();

        internal float Interpolation => interpolation;

        internal float CameraYaw => MathHelper.Lerp(previousCamera.Yaw, currentCamera.Yaw, interpolation);

        internal float CameraPitch => MathHelper.Lerp(previousCamera.Pitch, currentCamera.Pitch, interpolation);

        internal float Elapsed => elapsed;

        internal bool IsTitlePage => active && Main.gameMenu
            && (HimayoMenuVanillaBridge.TitleFrameActive
                || MenuLoader.CurrentMenu is HimayoMainMenu && Main.menuMode == 0);

        internal float GetHover(int index)
            => MathHelper.Lerp(previousHover[index], currentHover[index], interpolation);

        internal float GetEntry(int index)
            => MathHelper.Lerp(previousEntry[index], currentEntry[index], interpolation);

        internal void Select() {
            active = true;
            accumulator = 0d;
            interpolation = 1f;
            elapsed = 0f;
            previousCamera = default;
            currentCamera = default;
            Array.Clear(previousHover);
            Array.Clear(currentHover);
            Array.Clear(previousEntry);
            Array.Clear(currentEntry);
            Layout.Rebuild(HimayoMenuTheme.Font.Value);
            Petals.Initialize(HimayoMenuInput.PhysicalPointer);
            renderLeftDown = Main.mouseLeft;
            renderRightDown = Main.mouseRight;
            previousInputBlocked = HimayoMenuInput.IsOverlayCapturing(HimayoMenuInput.UIPointer);
            hoveredButton = -1;
            previousTitlePage = true;
        }

        internal void Deselect() {
            if (!active) {
                return;
            }

            active = false;
            accumulator = 0d;
            interpolation = 1f;
            Petals.ReleaseCapture();
            Petals.ResetMouseTrail();
            HimayoMenuVanillaBridge.SetCustomSwitchRect(Rectangle.Empty);
            previousInputBlocked = false;
            hoveredButton = -1;
        }

        internal void AdvanceFrame(GameTime gameTime) {
            if (!active || MenuLoader.CurrentMenu is not HimayoMainMenu) {
                return;
            }

            double frameTime = Math.Clamp(gameTime.ElapsedGameTime.TotalSeconds, 0d,
                FixedStep * MaxCatchUpSteps);
            accumulator += frameTime;
            int steps = 0;
            while (accumulator >= FixedStep && steps < MaxCatchUpSteps) {
                FixedUpdate();
                accumulator -= FixedStep;
                steps++;
            }

            if (steps == MaxCatchUpSteps && accumulator >= FixedStep) {
                accumulator = FixedStep - 0.000001d;
            }
            interpolation = MathHelper.Clamp((float)(accumulator / FixedStep), 0f, 1f);
        }

        private void FixedUpdate() {
            previousCamera = currentCamera;
            Array.Copy(currentHover, previousHover, currentHover.Length);
            Array.Copy(currentEntry, previousEntry, currentEntry.Length);

            bool titlePage = IsTitlePage;
            bool focused = Main.instance?.IsActive == true;
            Vector2 pointer = HimayoMenuInput.UIPointer;
            Vector2 physicalPointer = HimayoMenuInput.PhysicalPointer;
            Vector2 normalized = new(
                MathHelper.Clamp(pointer.X / Math.Max(1f, HimayoMenuInput.UIScreenWidth) * 2f - 1f, -1f, 1f),
                MathHelper.Clamp(pointer.Y / Math.Max(1f, HimayoMenuInput.UIScreenHeight) * 2f - 1f, -1f, 1f));

            float yawLimit = titlePage ? TitleYawLimit : ChildYawLimit;
            float pitchLimit = titlePage ? TitlePitchLimit : ChildPitchLimit;
            float targetYaw = focused ? normalized.X * yawLimit : 0f;
            float targetPitch = focused ? -normalized.Y * pitchLimit : 0f;
            StepSpring(ref currentCamera.Yaw, ref currentCamera.YawVelocity, targetYaw);
            StepSpring(ref currentCamera.Pitch, ref currentCamera.PitchVelocity, targetPitch);
            currentCamera.Pitch = MathHelper.Clamp(currentCamera.Pitch,
                -MathHelper.PiOver2 + 0.02f, MathHelper.PiOver2 - 0.02f);

            elapsed += FixedStep;
            Layout.Rebuild(HimayoMenuTheme.Font.Value);
            bool blocked = HimayoMenuInput.IsOverlayCapturing(pointer);
            int nextHovered = -1;
            ReadOnlySpan<HimayoMenuButtonLayout> buttons = Layout.Buttons;
            for (int i = 0; i < buttons.Length; i++) {
                float delay = i * 0.055f;
                currentEntry[i] = SmoothStep(MathHelper.Clamp((elapsed - delay) / 0.38f, 0f, 1f));
                bool hovering = HimayoMenuVanillaBridge.BridgeOperational && titlePage && focused
                    && !blocked && currentEntry[i] > 0.62f && buttons[i].HitBox.Contains(pointer.ToPoint());
                currentHover[i] = ExpApproach(currentHover[i], hovering ? 1f : 0f, 16f);
                if (hovering) {
                    nextHovered = i;
                }
            }

            if (nextHovered >= 0 && nextHovered != hoveredButton) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.65f, Pitch = 0.08f });
            }
            hoveredButton = nextHovered;

            bool allowNearInteraction = HimayoMenuVanillaBridge.BridgeOperational && titlePage && focused
                && !blocked && !Layout.ContainsMenuControl(pointer.ToPoint());
            if (!focused || titlePage != previousTitlePage || blocked != previousInputBlocked) {
                Petals.ReleaseCapture();
                Petals.ResetMouseTrail();
            }
            Petals.Update(physicalPointer, Main.mouseLeft, titlePage, allowNearInteraction);
            previousTitlePage = titlePage;
            previousInputBlocked = blocked;
        }

        internal void ProcessRenderInput() {
            Layout.Rebuild(HimayoMenuTheme.Font.Value);
            bool leftPressed = Main.mouseLeft && !renderLeftDown;
            bool rightPressed = Main.mouseRight && !renderRightDown;
            renderLeftDown = Main.mouseLeft;
            renderRightDown = Main.mouseRight;

            bool focused = Main.instance?.IsActive == true;
            if (!HimayoMenuVanillaBridge.BridgeOperational || !IsTitlePage || !focused) {
                HimayoMenuVanillaBridge.SetCustomSwitchRect(Rectangle.Empty);
                Petals.ReleaseCapture();
                Petals.ResetMouseTrail();
                return;
            }

            HimayoMenuVanillaBridge.SetCustomSwitchRect(Layout.ThemeSwitchRect);
            Vector2 pointer = HimayoMenuInput.UIPointer;
            Point point = pointer.ToPoint();
            if (HimayoMenuInput.IsOverlayCapturing(pointer)) {
                Petals.ReleaseCapture();
                Petals.ResetMouseTrail();
                return;
            }

            if (leftPressed) {
                ReadOnlySpan<HimayoMenuButtonLayout> buttons = Layout.Buttons;
                for (int i = 0; i < buttons.Length; i++) {
                    if (GetEntry(i) > 0.62f && buttons[i].HitBox.Contains(point)) {
                        if (HimayoMenuVanillaBridge.TryEnqueueAction(buttons[i].Action)) {
                            Petals.ReleaseCapture();
                            Petals.ResetMouseTrail();
                        }
                        return;
                    }
                }
            }

            bool switched = false;
            if (leftPressed && Layout.PreviousThemeRect.Contains(point)) {
                switched = HimayoMenuVanillaBridge.RequestPreviousTheme();
            }
            else if (leftPressed && Layout.ThemeSwitchRect.Contains(point)) {
                switched = HimayoMenuVanillaBridge.RequestNextTheme();
            }
            else if (rightPressed && Layout.ThemeSwitchRect.Contains(point)) {
                switched = HimayoMenuVanillaBridge.RequestPreviousTheme();
            }

            if (switched) {
                Petals.ReleaseCapture();
                Petals.ResetMouseTrail();
            }
        }

        private static void StepSpring(ref float position, ref float velocity, float target) {
            float acceleration = (target - position) * CameraSpringFrequency * CameraSpringFrequency
                - velocity * (2f * CameraSpringFrequency);
            velocity += acceleration * FixedStep;
            position += velocity * FixedStep;
        }

        private static float ExpApproach(float value, float target, float speed) {
            float amount = 1f - MathF.Exp(-speed * FixedStep);
            return MathHelper.Lerp(value, target, amount);
        }

        private static float SmoothStep(float value) => value * value * (3f - 2f * value);

        private struct CameraState
        {
            internal float Yaw;
            internal float Pitch;
            internal float YawVelocity;
            internal float PitchVelocity;
        }
    }
}
