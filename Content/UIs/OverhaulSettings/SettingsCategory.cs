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
    /// <summary>
    /// 设置面板中的一个可折叠分类区域，管理自身的展开/折叠、滚动、悬停和开关项
    /// </summary>
    internal abstract class SettingsCategory
    {
        //布局常量（与主UI共享）
        protected const float CategoryHeight = 40f;
        protected const float ToggleRowHeight = 34f;
        protected const float ToggleBoxSize = 22f;

        //分类标题
        public abstract string Title { get; }

        //展开/折叠状态
        public bool Expanded;
        public float ExpandAnim;

        //悬停
        public float CategoryHoverAnim;
        public Rectangle CategoryHitBox;
        public bool HoveringCategory;

        //设置项
        public readonly List<SettingToggle> Toggles = [];
        protected bool Initialized;

        //滚动
        public float ScrollOffset;
        public float ScrollTarget;
        public float MaxScroll;
        private int oldScrollWheelValue;

        //滚动条拖动
        public bool IsDraggingScrollbar;
        public float DragStartY;
        public float DragStartScrollTarget;
        public Rectangle ScrollbarTrackRect;
        public Rectangle ScrollbarThumbRect;

        //悬浮提示(由主UI读取)
        public string HoverTooltip;
        public Vector2 HoverTooltipPos;

        //展开区域的实际裁剪矩形(由主UI在绘制时设置)
        public Rectangle ExpandClipRect;

        //底部额外提示
        public string FooterHint;
        public bool ShowFooter;

        //操作按钮(如"全部启用"/"全部禁用")
        public readonly List<ActionButton> ActionButtons = [];

        /// <summary>
        /// 操作按钮数据
        /// </summary>
        public class ActionButton
        {
            public Func<string> Label;
            public Action OnClick;
            public Rectangle HitBox;
            public float HoverAnim;
            public bool Hovering;
        }

        /// <summary>
        /// 子类实现：初始化开关项列表
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// 子类实现：当某个开关被切换时的回调
        /// </summary>
        public abstract void OnToggleChanged(SettingToggle toggle, bool newValue);

        /// <summary>
        /// 子类可覆写：获取开关项的显示标签
        /// </summary>
        public virtual string GetLabel(SettingToggle toggle) => toggle.ConfigPropertyName;

        /// <summary>
        /// 子类可覆写：获取开关项的悬浮提示
        /// </summary>
        public virtual string GetTooltip(SettingToggle toggle) => "";

        /// <summary>
        /// 子类可覆写：在开关行左侧额外绘制内容(例如物品图标)
        /// </summary>
        public virtual void DrawRowExtra(SpriteBatch spriteBatch, SettingToggle toggle,
            Rectangle rect, float alpha, float scale) { }

        /// <summary>
        /// 子类可覆写：行内标签的起始X偏移(给DrawRowExtra留出空间)
        /// </summary>
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

        /// <summary>
        /// 更新展开动画、开关动画、悬停检测、滚动
        /// </summary>
        public void Update(float contentFade, bool hoverInMainPage, Rectangle mouseHitBox,
            Vector2 mousePosition, Rectangle scrollAreaRect) {
            //展开动画：使用更平滑的缓动曲线
            float expandTarget = Expanded ? 1f : 0f;
            float expandSpeed = Expanded ? 0.14f : 0.18f;
            ExpandAnim += (expandTarget - ExpandAnim) * expandSpeed;
            if (Math.Abs(ExpandAnim - expandTarget) < 0.005f) {
                ExpandAnim = expandTarget;
            }

            //分类按钮悬停
            float hoverSpeed = 0.15f;
            CategoryHoverAnim += ((HoveringCategory ? 1f : 0f) - CategoryHoverAnim) * hoverSpeed;

            //更新开关动画
            foreach (var toggle in Toggles) {
                float target = toggle.Getter() ? 1f : 0f;
                toggle.ToggleAnim += (target - toggle.ToggleAnim) * 0.15f;
                toggle.HoverAnim += ((toggle.Hovering ? 1f : 0f) - toggle.HoverAnim) * hoverSpeed;
            }

            //悬浮提示清除
            HoverTooltip = null;

            //更新开关项悬停：使用实际裁剪区域判定，排除滚动条区域
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

            //滚动条拖动处理
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

            //滚动处理
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

            //更新操作按钮悬停动画
            foreach (var btn in ActionButtons) {
                btn.HoverAnim += ((btn.Hovering ? 1f : 0f) - btn.HoverAnim) * hoverSpeed;
            }
        }

        /// <summary>
        /// 处理点击事件，返回true表示消耗了点击
        /// </summary>
        public virtual bool HandleClick(Rectangle mouseHitBox) {
            if (HoveringCategory) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.3f });
                Expanded = !Expanded;
                if (!Expanded) {
                    ScrollTarget = 0f;
                }
                return true;
            }

            //滚动条拖动检测
            if (ExpandAnim > 0.5f && MaxScroll > 0f) {
                if (ScrollbarThumbRect.Width > 0 && ScrollbarThumbRect.Contains(mouseHitBox)) {
                    IsDraggingScrollbar = true;
                    DragStartY = Mouse.GetState().Y;
                    DragStartScrollTarget = ScrollTarget;
                    return true;
                }
                //点击轨道跳转
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
                //操作按钮点击
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

        /// <summary>
        /// 获取当前可见的开关列表
        /// </summary>
        public List<SettingToggle> GetVisibleToggles() => Toggles;

        /// <summary>
        /// 计算展开后占用的总高度(不含分类按钮本身)
        /// 高度会被限制在可用面板空间内，超出部分通过滚动访问
        /// </summary>
        public float GetExpandedHeight(float scale) {
            if (ExpandAnim <= 0.01f) return 0f;
            float totalContentH = GetVisibleToggles().Count * ToggleRowHeight * scale;
            if (ShowFooter) totalContentH += 30f * scale;
            if (ActionButtons.Count > 0) totalContentH += 36f * scale;
            //限制最大展开高度，避免大量项目撑破面板
            float maxVisualH = Main.screenHeight * 0.8f * 0.55f;
            float clampedH = Math.Min(totalContentH + 6f * scale, maxVisualH);
            //使用缓动曲线让高度变化更自然
            float easedExpand = 1f - (1f - ExpandAnim) * (1f - ExpandAnim);
            return clampedH * easedExpand;
        }
    }
}
