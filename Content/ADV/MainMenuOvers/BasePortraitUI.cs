using CalamityOverhaul.Content.UIs.MainMenuOverUIs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.MainMenuOvers
{
    /// <summary>
    /// 主菜单立绘UI基类
    /// </summary>
    internal abstract class BasePortraitUI : UIHandle, ICWRLoader
    {
        #region 通用字段
        protected float _iconAlpha = 0f; //头像框透明度
        protected Vector2 _portraitOffset = Vector2.Zero; //立绘偏移
        protected int _autoSaveTimer = 0; //自动保存计时器
        protected const int AutoSaveInterval = 300; //5秒自动保存一次(60帧*5秒)
        protected bool _needsSave = false; //标记是否需要保存

        //动画计时器
        protected float _pulseTimer = 0f;

        //UI位置和尺寸
        protected const float IconSize = 80f;
        protected const float IconBottomMargin = 46f;
        protected const float IconSpacing = 95f; //与另一个头像的间距

        //图标位置(由子类实现具体偏移)
        protected abstract Vector2 GetIconBasePosition();

        protected Vector2 IconPosition => GetIconBasePosition() + _portraitOffset;

        protected Rectangle IconHitBox => new Rectangle(
            (int)IconPosition.X,
            (int)IconPosition.Y,
            (int)IconSize,
            (int)IconSize
        );

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;

        //确保资源已加载
        public override bool Active => MenuSave.IsPortraitUnlocked() && 
                                      CWRLoad.OnLoadContentBool && 
                                      Main.gameMenu && 
                                      IsResourceLoaded();

        //检查资源是否已正确加载(由子类实现)
        protected abstract bool IsResourceLoaded();
        #endregion

        #region 通用方法
        /// <summary>
        /// 检查玩家是否在主菜单(menuMode == 0),而不是在子菜单中
        /// </summary>
        protected static bool IsInMainMenu() {
            return Main.menuMode == 0;
        }

        /// <summary>
        /// 检查图标是否应该可见(仅在主菜单显示,进入子菜单时隐藏)
        /// </summary>
        protected static bool ShouldShowIcon() {
            return IsInMainMenu();
        }

        /// <summary>
        /// 检查是否可以交互
        /// </summary>
        protected static bool CanInteract() {
            return IsInMainMenu() && 
                   !FeedbackUI.Instance.OnActive() && 
                   !AcknowledgmentUI.OnActive();
        }

        /// <summary>
        /// 标记需要保存
        /// </summary>
        protected void MarkNeedsSave() {
            _needsSave = true;
            _autoSaveTimer = 0; //重置自动保存计时器
        }

        /// <summary>
        /// 处理自动保存逻辑
        /// </summary>
        protected void HandleAutoSave() {
            if (_needsSave) {
                _autoSaveTimer++;
                if (_autoSaveTimer >= AutoSaveInterval) {
                    SaveCurrentState();
                    _autoSaveTimer = 0;
                }
            }
        }

        /// <summary>
        /// 更新图标透明度
        /// </summary>
        protected void UpdateIconAlpha() {
            if (!Main.gameMenu || !IsResourceLoaded()) {
                _iconAlpha = 0f;
                return;
            }

            //进入子菜单时快速淡出图标
            if (!ShouldShowIcon()) {
                if (_iconAlpha > 0f) {
                    _iconAlpha -= 0.1f; //快速淡出
                    if (_iconAlpha < 0f) _iconAlpha = 0f;
                }
            }
            else {
                //仅在主菜单时渐入图标
                if (_iconAlpha < 1f) {
                    _iconAlpha += 0.02f;
                }
            }
        }

        /// <summary>
        /// 更新脉冲计时器
        /// </summary>
        protected void UpdatePulseTimer() {
            _pulseTimer += 0.02f;
            if (_pulseTimer > MathHelper.TwoPi) {
                _pulseTimer -= MathHelper.TwoPi;
            }
        }

        /// <summary>
        /// 绘制基础背景框
        /// </summary>
        protected void DrawBaseBackground(SpriteBatch sb, Rectangle bgRect, float alpha, bool hoverIcon, Color bgColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //悬停光效(由子类提供颜色)
            if (hoverIcon) {
                Color hoverGlow = GetHoverGlowColor() * (alpha * 0.35f);
                for (int i = 0; i < 6; i++) {
                    sb.Draw(pixel, bgRect.Location.ToVector2(),
                        new Rectangle(0, 0, bgRect.Width, bgRect.Height), hoverGlow);
                }
            }

            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), bgColor);

            //脉冲背景
            float pulse = (float)Math.Sin(_pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = GetPulseColor() * (alpha * 0.15f * pulse);
            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);
        }

        /// <summary>
        /// 获取悬停光晕颜色(由子类实现)
        /// </summary>
        protected abstract Color GetHoverGlowColor();

        /// <summary>
        /// 获取脉冲颜色(由子类实现)
        /// </summary>
        protected abstract Color GetPulseColor();

        /// <summary>
        /// 从MenuSave加载保存的UI状态(由子类实现)
        /// </summary>
        public abstract void LoadSavedState();

        /// <summary>
        /// 保存当前UI状态到MenuSave(由子类实现)
        /// </summary>
        public abstract void SaveCurrentState();
        #endregion

        #region 生命周期
        void ICWRLoader.SetupData() { }

        public override void SetStaticDefaults() {
            _iconAlpha = 0f;
            _portraitOffset = Vector2.Zero;
            _autoSaveTimer = 0;
            _needsSave = false;
            _pulseTimer = 0f;

            OnSetStaticDefaults();
            LoadSavedState();
        }

        public override void UnLoad() {
            SaveCurrentState();
            OnUnLoad();

            //重置基础状态
            _iconAlpha = 0f;
            _portraitOffset = Vector2.Zero;
        }

        /// <summary>
        /// 子类的SetStaticDefaults逻辑
        /// </summary>
        protected virtual void OnSetStaticDefaults() { }

        /// <summary>
        /// 子类的UnLoad逻辑
        /// </summary>
        protected virtual void OnUnLoad() { }
        #endregion
    }
}
