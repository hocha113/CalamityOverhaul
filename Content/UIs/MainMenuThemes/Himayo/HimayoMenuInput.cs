using CalamityOverhaul.Content.UIs.MainMenuCharacters;
using CalamityOverhaul.Content.UIs.MainMenuOverUIs;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.UIs.MainMenuThemes.Himayo
{
    internal static class HimayoMenuInput
    {
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo IconAlphaField = typeof(BasePortraitUI)
            .GetField("_iconAlpha", InstanceMembers);
        private static readonly PropertyInfo IconHitBoxProperty = typeof(BasePortraitUI)
            .GetProperty("IconHitBox", InstanceMembers);
        private static readonly FieldInfo PortraitAlphaField = typeof(SupCalPortraitUI)
            .GetField("_portraitAlpha", InstanceMembers);
        private static readonly FieldInfo ExpressionAlphaField = typeof(SupCalPortraitUI)
            .GetField("_expressionButtonAlpha", InstanceMembers);
        private static readonly PropertyInfo LeftPortraitHitBoxProperty = typeof(SupCalPortraitUI)
            .GetProperty("LeftPortraitHitBox", InstanceMembers);
        private static readonly PropertyInfo ExpressionHitBoxProperty = typeof(SupCalPortraitUI)
            .GetProperty("ExpressionButtonHitBox", InstanceMembers);

        internal static int PhysicalScreenWidth => PlayerInput.RealScreenWidth > 0
            ? PlayerInput.RealScreenWidth
            : Math.Max(1, (int)(Main.screenWidth * Math.Max(Main.UIScale, 0.01f)));

        internal static int PhysicalScreenHeight => PlayerInput.RealScreenHeight > 0
            ? PlayerInput.RealScreenHeight
            : Math.Max(1, (int)(Main.screenHeight * Math.Max(Main.UIScale, 0.01f)));

        internal static float UIScreenWidth => PhysicalScreenWidth / Math.Max(Main.UIScale, 0.01f);

        internal static float UIScreenHeight => PhysicalScreenHeight / Math.Max(Main.UIScale, 0.01f);

        internal static Vector2 UIPointer => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY)
            / Math.Max(Main.UIScale, 0.01f);

        internal static Vector2 PhysicalPointer => new(PlayerInput.MouseX, PlayerInput.MouseY);

        internal static bool IsModalActive() {
            return OverhaulSettingsUI.OnActive()
                || FeedbackUI.Instance?.OnActive() == true
                || AcknowledgmentUI.OnActive()
                || Main.MenuUI?.IsVisible == true;
        }

        internal static bool IsOverlayCapturing(Vector2 uiPointer) {
            if (IsModalActive()) {
                return true;
            }

            Point point = uiPointer.ToPoint();
            if (SupCalCapturesInput(SupCalPortraitUI.Instance, point)
                || HelenPortraitUI.Instance?.CapturesMenuInput(point) == true) {
                return true;
            }

            BulletinBoardUI board = BulletinBoardUI.Instance;
            if (board != null && board.UIHitBox.Contains(point)) {
                return true;
            }

            if (BulletinBoardUI.bulletinBoardElements != null) {
                for (int i = 0; i < BulletinBoardUI.bulletinBoardElements.Count; i++) {
                    if (BulletinBoardUI.bulletinBoardElements[i].UIHitBox.Contains(point)) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool SupCalCapturesInput(SupCalPortraitUI portrait, Point point) {
            if (portrait == null || !portrait.Active || Main.menuMode != 0) {
                return false;
            }

            if (!TryRead(IconAlphaField, portrait, out float iconAlpha)
                || !TryRead(IconHitBoxProperty, portrait, out Rectangle iconHitBox)
                || !TryRead(PortraitAlphaField, portrait, out float portraitAlpha)
                || !TryRead(ExpressionAlphaField, portrait, out float expressionAlpha)
                || !TryRead(LeftPortraitHitBoxProperty, portrait, out Rectangle portraitHitBox)
                || !TryRead(ExpressionHitBoxProperty, portrait, out Rectangle expressionHitBox)) {
                return portrait.CapturesMenuInput(point);
            }

            if (iconAlpha > 0.01f) {
                iconHitBox.Inflate(5, 5);
                if (iconHitBox.Contains(point)) {
                    return true;
                }
            }

            if (portraitAlpha > 0.01f && !portraitHitBox.IsEmpty) {
                portraitHitBox.Width = (int)MathF.Ceiling(portraitHitBox.Width * (1.6f / 1.8f));
                portraitHitBox.Height = (int)MathF.Ceiling(portraitHitBox.Height * (1.6f / 1.8f));
                portraitHitBox.Inflate(8, 8);
                if (portraitHitBox.Contains(point)) {
                    return true;
                }
            }

            if (iconAlpha > 0.01f && expressionAlpha > 0.01f) {
                expressionHitBox.Inflate(3, 3);
                return expressionHitBox.Contains(point);
            }
            return false;
        }

        private static bool TryRead<T>(MemberInfo member, object instance, out T value) {
            try {
                object raw = member switch {
                    FieldInfo field => field.GetValue(instance),
                    PropertyInfo property => property.GetValue(instance),
                    _ => null
                };
                if (raw is T typed) {
                    value = typed;
                    return true;
                }
            }
            catch {
            }

            value = default;
            return false;
        }
    }
}
