using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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

        //悬浮提示(由主UI读取)
        public string HoverTooltip;
        public Vector2 HoverTooltipPos;

        //底部额外提示
        public string FooterHint;
        public bool ShowFooter;

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
            //展开动画
            float expandTarget = Expanded ? 1f : 0f;
            ExpandAnim += (expandTarget - ExpandAnim) * 0.12f;
            if (Math.Abs(ExpandAnim - expandTarget) < 0.001f) {
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

            //更新开关项悬停
            if (ExpandAnim > 0.5f) {
                foreach (var toggle in Toggles) {
                    toggle.Hovering = toggle.HitBox.Contains(mouseHitBox)
                        && scrollAreaRect.Contains(mouseHitBox) && contentFade > 0.5f;
                    if (toggle.Hovering) {
                        string tip = GetTooltip(toggle);
                        if (!string.IsNullOrEmpty(tip)) {
                            HoverTooltip = tip;
                            HoverTooltipPos = mousePosition;
                        }
                    }
                }
            }

            //滚动处理
            if (hoverInMainPage && Expanded) {
                MouseState currentMouseState = Mouse.GetState();
                int scrollDelta = currentMouseState.ScrollWheelValue - oldScrollWheelValue;
                oldScrollWheelValue = currentMouseState.ScrollWheelValue;
                if (scrollDelta != 0) {
                    ScrollTarget -= scrollDelta * 0.3f;
                    ScrollTarget = Math.Clamp(ScrollTarget, 0f, Math.Max(0f, MaxScroll));
                }
            }
            else {
                oldScrollWheelValue = Mouse.GetState().ScrollWheelValue;
            }
            ScrollOffset += (ScrollTarget - ScrollOffset) * 0.2f;
        }

        /// <summary>
        /// 处理点击事件，返回true表示消耗了点击
        /// </summary>
        public bool HandleClick(Rectangle mouseHitBox) {
            if (HoveringCategory) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.3f });
                Expanded = !Expanded;
                if (!Expanded) {
                    ScrollTarget = 0f;
                }
                return true;
            }

            if (ExpandAnim > 0.5f) {
                foreach (var toggle in Toggles) {
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
        /// 计算展开后占用的总高度(不含分类按钮本身)
        /// </summary>
        public float GetExpandedHeight(float scale) {
            if (ExpandAnim <= 0.01f) return 0f;
            float totalContentH = Toggles.Count * ToggleRowHeight * scale;
            if (ShowFooter) totalContentH += 30f * scale;
            return (totalContentH + 6f * scale) * ExpandAnim;
        }
    }
}
