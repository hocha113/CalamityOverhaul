using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>设置可折叠分类，展开/滚动/开关</summary>
    internal abstract class SettingsCategory
    {
        protected const float CategoryHeight = 40f;
        protected const float ToggleRowHeight = 34f;
        protected const float ToggleBoxSize = 22f;

        public abstract string Title { get; }

        public bool Expanded;
        public float ExpandAnim;

        public float CategoryHoverAnim;
        public Rectangle CategoryHitBox;
        public bool HoveringCategory;

        public readonly List<SettingToggle> Toggles = [];
        protected bool Initialized;

        public float ScrollOffset;
        public float ScrollTarget;
        public float MaxScroll;
        private int oldScrollWheelValue;

        public bool IsDraggingScrollbar;
        public float DragStartY;
        public float DragStartScrollTarget;
        public Rectangle ScrollbarTrackRect;
        public Rectangle ScrollbarThumbRect;

        //悬浮提示，主UI读
        public string HoverTooltip;
        public Vector2 HoverTooltipPos;

        //展开裁剪矩形，主UI绘制时设
        public Rectangle ExpandClipRect;

        public string FooterHint;
        public bool ShowFooter;

        //操作按钮
        public readonly List<ActionButton> ActionButtons = [];


        public class ActionButton
        {
            public Func<string> Label;
            public Action OnClick;
            public Rectangle HitBox;
            public float HoverAnim;
            public bool Hovering;
        }

        /// <summary>子类 Init 开关列表</summary>
        public abstract void Initialize();

        /// <summary>子类开关切换回调</summary>
        public abstract void OnToggleChanged(SettingToggle toggle, bool newValue);

        /// <summary>开关显示标签</summary>
        public virtual string GetLabel(SettingToggle toggle) => toggle.ConfigPropertyName;

        /// <summary>开关悬浮提示</summary>
        public virtual string GetTooltip(SettingToggle toggle) => "";

        /// <summary>行左额外绘制(如物品图标)</summary>
        public virtual void DrawRowExtra(SpriteBatch spriteBatch, SettingToggle toggle,
            Rectangle rect, float alpha, float scale) { }

        /// <summary>标签起始X，给DrawRowExtra留空</summary>
        public virtual float GetLabelOffsetX(float scale) => 0f;

        public void EnsureInitialized() {
            if (!Initialized) {
                Initialized = true;
                Toggles.Clear();
                Initialize();
            }
        }

        protected void AddToggle(string propertyName, Func<bool> getter, Action<bool> setter, bool requiresReload) {
            Toggles.Add(new SettingToggle {
                ConfigPropertyName = propertyName,
                Getter = getter,
                Setter = setter,
                RequiresReload = requiresReload,
                HoverAnim = 0f,
                ToggleAnim = getter() ? 1f : 0f,
            });
        }


        public void Update(float contentFade, bool hoverInMainPage, Rectangle mouseHitBox,
            Vector2 mousePosition, Rectangle scrollAreaRect) {
            //展开缓动
            float expandTarget = Expanded ? 1f : 0f;
            float expandSpeed = Expanded ? 0.14f : 0.18f;
            ExpandAnim += (expandTarget - ExpandAnim) * expandSpeed;
            if (Math.Abs(ExpandAnim - expandTarget) < 0.005f) {
                ExpandAnim = expandTarget;
            }

            float hoverSpeed = 0.15f;
            CategoryHoverAnim += ((HoveringCategory ? 1f : 0f) - CategoryHoverAnim) * hoverSpeed;

            foreach (var toggle in Toggles) {
                float target = toggle.Getter() ? 1f : 0f;
                toggle.ToggleAnim += (target - toggle.ToggleAnim) * 0.15f;
                toggle.HoverAnim += ((toggle.Hovering ? 1f : 0f) - toggle.HoverAnim) * hoverSpeed;
            }

            HoverTooltip = null;

            //开关悬停，裁剪区且排除滚动条
            if (ExpandAnim > 0.5f && ExpandClipRect.Width > 0 && ExpandClipRect.Height > 0) {
                bool mouseInScrollbar = ScrollbarTrackRect.Width > 0 && ScrollbarTrackRect.Contains(mouseHitBox);
                foreach (var toggle in Toggles) {
                    toggle.Hovering = toggle.HitBox.Contains(mouseHitBox)
                        && ExpandClipRect.Contains(mouseHitBox)
                        && !mouseInScrollbar
                        && contentFade > 0.5f;
                    if (toggle.Hovering) {
                        string tip = GetTooltip(toggle);
                        if (!string.IsNullOrEmpty(tip)) {
                            HoverTooltip = tip;
                            HoverTooltipPos = mousePosition;
                        }
                    }
                }
            }

            if (IsDraggingScrollbar) {
                MouseState ms = Mouse.GetState();
                if (ms.LeftButton == ButtonState.Pressed) {
                    float trackHeight = ScrollbarTrackRect.Height;
                    float thumbHeight = ScrollbarThumbRect.Height;
                    float maxThumbY = trackHeight - thumbHeight;
                    if (maxThumbY > 0 && MaxScroll > 0) {
                        float deltaY = ms.Y - DragStartY;
                        float scrollRatio = deltaY / maxThumbY;
                        ScrollTarget = Math.Clamp(DragStartScrollTarget + scrollRatio * MaxScroll, 0f, MaxScroll);
                    }
                }
                else {
                    IsDraggingScrollbar = false;
                }
            }

            if (hoverInMainPage && Expanded && !IsDraggingScrollbar) {
                MouseState currentMouseState = Mouse.GetState();
                int scrollDelta = currentMouseState.ScrollWheelValue - oldScrollWheelValue;
                oldScrollWheelValue = currentMouseState.ScrollWheelValue;
                if (scrollDelta != 0) {
                    ScrollTarget -= scrollDelta * 0.3f;
                    ScrollTarget = Math.Clamp(ScrollTarget, 0f, Math.Max(0f, MaxScroll));
                }
            }
            else if (!IsDraggingScrollbar) {
                oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            }
            ScrollOffset += (ScrollTarget - ScrollOffset) * 0.2f;

            foreach (var btn in ActionButtons) {
                btn.HoverAnim += ((btn.Hovering ? 1f : 0f) - btn.HoverAnim) * hoverSpeed;
            }
        }

        /// <summary>点击，true=已消耗</summary>
        public virtual bool HandleClick(Rectangle mouseHitBox) {
            if (HoveringCategory) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.3f });
                Expanded = !Expanded;
                if (!Expanded) {
                    ScrollTarget = 0f;
                }
                return true;
            }

            if (ExpandAnim > 0.5f && MaxScroll > 0f) {
                if (ScrollbarThumbRect.Width > 0 && ScrollbarThumbRect.Contains(mouseHitBox)) {
                    IsDraggingScrollbar = true;
                    DragStartY = Mouse.GetState().Y;
                    DragStartScrollTarget = ScrollTarget;
                    return true;
                }
                if (ScrollbarTrackRect.Width > 0 && ScrollbarTrackRect.Contains(mouseHitBox)) {
                    float clickY = Mouse.GetState().Y;
                    float trackHeight = ScrollbarTrackRect.Height;
                    float relativeY = clickY - ScrollbarTrackRect.Y;
                    float ratio = relativeY / trackHeight;
                    ScrollTarget = Math.Clamp(ratio * MaxScroll, 0f, MaxScroll);
                    IsDraggingScrollbar = true;
                    DragStartY = Mouse.GetState().Y;
                    DragStartScrollTarget = ScrollTarget;
                    return true;
                }
            }

            if (ExpandAnim > 0.5f) {
                foreach (var btn in ActionButtons) {
                    if (btn.Hovering) {
                        btn.OnClick?.Invoke();
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.2f });
                        return true;
                    }
                }

                foreach (var toggle in GetVisibleToggles()) {
                    if (toggle.Hovering) {
                        bool newVal = !toggle.Getter();
                        toggle.Setter(newVal);
                        OnToggleChanged(toggle, newVal);
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = newVal ? 0.5f : -0.2f });
                        return true;
                    }
                }
            }

            return false;
        }


        public List<SettingToggle> GetVisibleToggles() => Toggles;

        /// <summary>展开占用高(不含分类钮)，限面板内，超出滚动</summary>
        public float GetExpandedHeight(float scale) {
            if (ExpandAnim <= 0.01f) return 0f;
            float totalContentH = GetVisibleToggles().Count * ToggleRowHeight * scale;
            if (ShowFooter) totalContentH += 30f * scale;
            if (ActionButtons.Count > 0) totalContentH += 36f * scale;
            //限最大展开高
            float maxVisualH = Main.screenHeight * 0.8f * 0.55f;
            float clampedH = Math.Min(totalContentH + 6f * scale, maxVisualH);
            float easedExpand = 1f - (1f - ExpandAnim) * (1f - ExpandAnim);
            return clampedH * easedExpand;
        }
    }
}
