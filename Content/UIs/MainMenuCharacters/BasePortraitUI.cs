using CalamityOverhaul.Content.UIs.MainMenuOverUIs;
using CalamityOverhaul.Content.UIs.OverhaulSettings;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.UIs.MainMenuCharacters
{
    /// <summary>主菜单立绘UI基类</summary>
    internal abstract class BasePortraitUI : UIHandle, ICWRLoader
    {
        #region 通用字段
        protected float _iconAlpha = 0f;
        protected Vector2 _portraitOffset = Vector2.Zero;
        protected int _autoSaveTimer = 0;
        protected const int AutoSaveInterval = 300;//5s=300帧
        protected bool _needsSave = false;

        protected float _pulseTimer = 0f;

        protected const float IconSize = 60f;
        protected const float IconBottomMargin = 46f;
        protected const float IconSpacing = 80f;//与另一头像间距

        protected abstract Vector2 GetIconBasePosition();

        protected Vector2 IconPosition => GetIconBasePosition() + _portraitOffset;

        protected Rectangle IconHitBox => new Rectangle(
            (int)IconPosition.X,
            (int)IconPosition.Y,
            (int)IconSize,
            (int)IconSize
        );

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;

        public override bool Active => MenuSave.IsPortraitUnlocked() &&
                                      VaultLoad.LoadenContent &&
                                      Main.gameMenu &&
                                      IsResourceLoaded();

        protected abstract bool IsResourceLoaded();
        #endregion

        #region 通用方法
        /// <summary>menuMode==0</summary>
        protected static bool IsInMainMenu() {
            return Main.menuMode == 0;
        }

        protected static bool ShouldShowIcon() {
            return IsInMainMenu();
        }

        protected static bool CanInteract() {
            return IsInMainMenu() &&
                   !OverhaulSettingsUI.OnActive() &&
                   !FeedbackUI.Instance.OnActive() &&
                   !AcknowledgmentUI.OnActive();
        }

        protected void MarkNeedsSave() {
            _needsSave = true;
            _autoSaveTimer = 0;
        }

        protected void HandleAutoSave() {
            if (_needsSave) {
                _autoSaveTimer++;
                if (_autoSaveTimer >= AutoSaveInterval) {
                    SaveCurrentState();
                    _autoSaveTimer = 0;
                }
            }
        }

        protected void UpdateIconAlpha() {
            if (!Main.gameMenu || !IsResourceLoaded()) {
                _iconAlpha = 0f;
                return;
            }

            //进子菜单快淡出
            if (!ShouldShowIcon()) {
                if (_iconAlpha > 0f) {
                    _iconAlpha -= 0.1f;
                    if (_iconAlpha < 0f) _iconAlpha = 0f;
                }
            }
            else {

                if (_iconAlpha < 1f) {
                    _iconAlpha += 0.02f;
                }
            }
        }

        protected void UpdatePulseTimer() {
            _pulseTimer += 0.02f;
            if (_pulseTimer > MathHelper.TwoPi) {
                _pulseTimer -= MathHelper.TwoPi;
            }
        }

        protected void DrawBaseBackground(SpriteBatch sb, Rectangle bgRect, float alpha, bool hoverIcon, Color bgColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            if (hoverIcon) {
                Color hoverGlow = GetHoverGlowColor() * (alpha * 0.35f);
                for (int i = 0; i < 6; i++) {
                    sb.Draw(pixel, bgRect.Location.ToVector2(),
                        new Rectangle(0, 0, bgRect.Width, bgRect.Height), hoverGlow);
                }
            }

            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), bgColor);

            float pulse = (float)Math.Sin(_pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = GetPulseColor() * (alpha * 0.15f * pulse);
            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);
        }

        /// <summary>悬停光晕色</summary>
        protected abstract Color GetHoverGlowColor();

        /// <summary>脉冲色</summary>
        protected abstract Color GetPulseColor();

        /// <summary>从 MenuSave 加载</summary>
        public abstract void LoadSavedState();

        /// <summary>保存到 MenuSave</summary>
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

            _iconAlpha = 0f;
            _portraitOffset = Vector2.Zero;
        }

        protected virtual void OnSetStaticDefaults() { }

        protected virtual void OnUnLoad() { }
        #endregion
    }
}

