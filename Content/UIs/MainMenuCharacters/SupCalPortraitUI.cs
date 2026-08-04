using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuCharacters
{
    /// <summary>女巫主菜单立绘 UI</summary>
    internal class SupCalPortraitUI : BasePortraitUI
    {
        #region 数据字段
        public static SupCalPortraitUI Instance => UIHandleLoader.GetUIHandleOfType<SupCalPortraitUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;

        private enum PortraitExpression
        {
            Default,
            CloseEyes,
            Smile
        }

        private PortraitExpression _currentExpression = PortraitExpression.Default;
        private bool _showFullPortrait = false;
        private float _portraitAlpha = 0f;
        private float _transitionProgress = 0f;

        private float _flameTimer = 0f;
        private float _glowTimer = 0f;

        private readonly List<EmberPRT> _embers = new();
        private readonly List<FlameWispPRT> _flameWisps = new();
        private int _emberSpawnTimer = 0;
        private int _wispSpawnTimer = 0;

        //左侧半身大图，腰部起裁
        private const float LeftPortraitXRatio = 0.14f;
        private const float LeftPortraitCropBottom = 0.5f;

        //右侧全身小图
        private const float RightPortraitXRatio = 0.96f;

        private float _leftPortraitScale = 2.0f;
        private float _rightPortraitScale = 0.85f;
        private const float MinScale = 0.3f;
        private const float MaxScale = 5.0f;
        private const float ScaleStep = 0.05f;
        private int _scaleKeyTimer = 0;
        private const int ScaleKeyDelay = 3;//按键缩放延迟帧

        private bool _draggingLeftPortrait = false;
        private bool _draggingRightPortrait = false;
        private Vector2 _leftPortraitOffset = Vector2.Zero;
        private Vector2 _rightPortraitOffset = Vector2.Zero;
        private Vector2 _dragStartMousePos = Vector2.Zero;
        private Vector2 _dragStartOffset = Vector2.Zero;
        private const float MinVisibleSize = 80f;//最小可见px

        private const float ExpressionButtonSize = 30f;
        private float _expressionButtonAlpha = 0f;

        protected override Vector2 GetIconBasePosition() => new Vector2(
            Main.screenWidth / 2 - IconSize / 2 - IconSpacing / 2,
            Main.screenHeight - IconSize - IconBottomMargin
        );

        protected override bool IsResourceLoaded() {
            if (ADVAsset.SupCalsADV == null || ADVAsset.SupCalsADV.Count == 0) {
                return false;
            }

            if (ADVAsset.SupCalADV == null || ADVAsset.SupCalADV.IsDisposed) {
                return false;
            }

            if (ADVAsset.SupCalsADV[0] == null || ADVAsset.SupCalsADV[0].IsDisposed) {
                return false;
            }

            return true;
        }

        protected override Color GetHoverGlowColor() => new Color(255, 180, 80);
        protected override Color GetPulseColor() => new Color(120, 25, 15);

        private Vector2 ExpressionButtonPosition => new Vector2(
            IconPosition.X + ExpressionButtonSize / 2,
            IconPosition.Y - ExpressionButtonSize
        );

        private Rectangle ExpressionButtonHitBox => new Rectangle(
            (int)ExpressionButtonPosition.X,
            (int)ExpressionButtonPosition.Y,
            (int)ExpressionButtonSize,
            (int)ExpressionButtonSize
        );

        private Rectangle LeftPortraitHitBox {
            get {
                Texture2D portraitTex = GetCurrentPortraitTexture();
                if (portraitTex == null) return Rectangle.Empty;

                Vector2 leftPos = GetLeftPortraitPosition(portraitTex);
                float scale = _leftPortraitScale * (0.95f + _transitionProgress * 0.05f) * 1.8f;
                Vector2 size = new Vector2(portraitTex.Width, portraitTex.Height) * scale;

                return new Rectangle((int)leftPos.X, (int)leftPos.Y, (int)size.X, (int)size.Y);
            }
        }

        private Rectangle RightPortraitHitBox {
            get {
                Texture2D portraitTex = GetCurrentPortraitTexture();
                if (portraitTex == null) return Rectangle.Empty;

                Vector2 rightPos = GetRightPortraitPosition(portraitTex);
                float scale = _rightPortraitScale * (0.95f + _transitionProgress * 0.05f) * 2f;
                Vector2 size = portraitTex.Size() * scale;

                return new Rectangle((int)rightPos.X, (int)rightPos.Y, (int)size.X, (int)size.Y);
            }
        }

        internal override bool CapturesMenuInput(Point point) {
            if (!Active || !ShouldShowIcon() || _iconAlpha <= 0.01f) {
                return false;
            }

            if (_draggingLeftPortrait || _draggingRightPortrait) {
                return true;
            }

            if (IconHitBox.Contains(point)) {
                return true;
            }

            return _showFullPortrait && (_expressionButtonAlpha > 0.01f
                && ExpressionButtonHitBox.Contains(point)
                || _portraitAlpha > 0.01f
                && (LeftPortraitHitBox.Contains(point) || RightPortraitHitBox.Contains(point)));
        }
        #endregion

        #region 生命周期
        protected override void OnSetStaticDefaults() {
            _portraitAlpha = 0f;
            _showFullPortrait = false;
            _currentExpression = PortraitExpression.Default;
            _leftPortraitOffset = Vector2.Zero;
            _rightPortraitOffset = Vector2.Zero;
            _leftPortraitScale = 2.0f;
            _rightPortraitScale = 0.85f;
            _expressionButtonAlpha = 0f;
            _flameTimer = 0f;
            _glowTimer = 0f;
            _emberSpawnTimer = 0;
            _wispSpawnTimer = 0;
            _scaleKeyTimer = 0;
            _draggingLeftPortrait = false;
            _draggingRightPortrait = false;
        }

        protected override void OnUnLoad() {
            _embers?.Clear();
            _flameWisps?.Clear();

            _portraitAlpha = 0f;
            _showFullPortrait = false;
            _currentExpression = PortraitExpression.Default;
            _leftPortraitOffset = Vector2.Zero;
            _rightPortraitOffset = Vector2.Zero;
            _leftPortraitScale = 2.0f;
            _rightPortraitScale = 0.85f;
            _expressionButtonAlpha = 0f;
            _scaleKeyTimer = 0;
            _draggingLeftPortrait = false;
            _draggingRightPortrait = false;
        }

        public override void LoadSavedState() {
            int savedExpression = MenuSave.SupCal_Expression;
            _currentExpression = savedExpression switch {
                1 => PortraitExpression.CloseEyes,
                2 => PortraitExpression.Smile,
                _ => PortraitExpression.Default
            };

            _leftPortraitOffset = MenuSave.SupCal_LeftPortraitOffset;
            _rightPortraitOffset = MenuSave.SupCal_RightPortraitOffset;
            _showFullPortrait = MenuSave.SupCal_ShowFullPortrait;
            _leftPortraitScale = MenuSave.SupCal_LeftPortraitScale;
            _rightPortraitScale = MenuSave.SupCal_RightPortraitScale;

            _leftPortraitScale = Math.Clamp(_leftPortraitScale, MinScale, MaxScale);
            _rightPortraitScale = Math.Clamp(_rightPortraitScale, MinScale, MaxScale);

            _needsSave = false;
        }

        public override void SaveCurrentState() {
            if (!_needsSave) {
                return;
            }

            int expressionValue = _currentExpression switch {
                PortraitExpression.CloseEyes => 1,
                PortraitExpression.Smile => 2,
                _ => 0
            };

            MenuSave.SaveSupCalPortraitState(expressionValue, _leftPortraitOffset, _rightPortraitOffset, _showFullPortrait, _leftPortraitScale, _rightPortraitScale);
            _needsSave = false;
        }
        #endregion

        #region 立绘管理
        private Texture2D GetCurrentPortraitTexture() {
            if (!IsResourceLoaded()) {
                return null;
            }

            return _currentExpression switch {
                PortraitExpression.CloseEyes => ADVAsset.SupCal_closeEyesADV ?? ADVAsset.SupCalADV,
                PortraitExpression.Smile => ADVAsset.SupCal_smileADV ?? ADVAsset.SupCalADV,
                _ => ADVAsset.SupCalADV
            };
        }

        private void CycleExpression() {
            _currentExpression = _currentExpression switch {
                PortraitExpression.Default => PortraitExpression.CloseEyes,
                PortraitExpression.CloseEyes => PortraitExpression.Smile,
                PortraitExpression.Smile => PortraitExpression.Default,
                _ => PortraitExpression.Default
            };
            SoundEngine.PlaySound(SoundID.MenuTick);
            MarkNeedsSave();
        }

        private Vector2 GetLeftPortraitPosition(Texture2D tex) {
            float scale = _leftPortraitScale * (0.95f + _transitionProgress * 0.05f);
            float displayHeight = tex.Height * (1f - LeftPortraitCropBottom) * scale;

            Vector2 basePos = new Vector2(
                Main.screenWidth * LeftPortraitXRatio - tex.Width * scale / 2,
                Main.screenHeight - displayHeight - 140
            );

            return basePos + _leftPortraitOffset;
        }

        private Vector2 GetRightPortraitPosition(Texture2D tex) {
            float scale = _rightPortraitScale * (0.95f + _transitionProgress * 0.05f);

            Vector2 basePos = new Vector2(
                Main.screenWidth * RightPortraitXRatio - tex.Width * scale / 2 - 300,
                Main.screenHeight - tex.Height * scale - 220
            );

            return basePos + _rightPortraitOffset;
        }

        private void AdjustLeftPortraitScale(float delta) {
            _leftPortraitScale = Math.Clamp(_leftPortraitScale + delta, MinScale, MaxScale);
            MarkNeedsSave();
        }

        private void AdjustRightPortraitScale(float delta) {
            _rightPortraitScale = Math.Clamp(_rightPortraitScale + delta, MinScale, MaxScale);
            MarkNeedsSave();
        }

        private void ClampPortraitOffsets() {
            Texture2D tex = GetCurrentPortraitTexture();
            if (tex == null || Main.screenWidth <= 0 || Main.screenHeight <= 0) return;

            float baseScaleLeft = _leftPortraitScale * (0.95f + _transitionProgress * 0.05f);
            float hitboxScaleLeft = baseScaleLeft * 1.8f;
            Vector2 sizeLeft = new Vector2(tex.Width, tex.Height) * hitboxScaleLeft;
            Vector2 basePosLeft = new Vector2(
                Main.screenWidth * LeftPortraitXRatio - tex.Width * baseScaleLeft / 2,
                Main.screenHeight - tex.Height * (1f - LeftPortraitCropBottom) * baseScaleLeft - 140
            );
            _leftPortraitOffset.X = Math.Clamp(_leftPortraitOffset.X,
                MinVisibleSize - basePosLeft.X - sizeLeft.X,
                Main.screenWidth - MinVisibleSize - basePosLeft.X);
            _leftPortraitOffset.Y = Math.Clamp(_leftPortraitOffset.Y,
                MinVisibleSize - basePosLeft.Y - sizeLeft.Y,
                Main.screenHeight - MinVisibleSize - basePosLeft.Y);

            float baseScaleRight = _rightPortraitScale * (0.95f + _transitionProgress * 0.05f);
            float hitboxScaleRight = baseScaleRight * 2f;
            Vector2 sizeRight = tex.Size() * hitboxScaleRight;
            Vector2 basePosRight = new Vector2(
                Main.screenWidth * RightPortraitXRatio - tex.Width * baseScaleRight / 2 - 300,
                Main.screenHeight - tex.Height * baseScaleRight - 220
            );
            _rightPortraitOffset.X = Math.Clamp(_rightPortraitOffset.X,
                MinVisibleSize - basePosRight.X - sizeRight.X,
                Main.screenWidth - MinVisibleSize - basePosRight.X);
            _rightPortraitOffset.Y = Math.Clamp(_rightPortraitOffset.Y,
                MinVisibleSize - basePosRight.Y - sizeRight.Y,
                Main.screenHeight - MinVisibleSize - basePosRight.Y);
        }
        #endregion

        #region 更新逻辑
        public override void Update() {
            if (!Main.gameMenu || !IsResourceLoaded()) {
                _iconAlpha = 0f;
                _portraitAlpha = 0f;
                _expressionButtonAlpha = 0f;
                _draggingLeftPortrait = false;
                _draggingRightPortrait = false;
                return;
            }

            HandleAutoSave();
            UpdateIconAlpha();

            if (_showFullPortrait && _expressionButtonAlpha < 1f) {
                _expressionButtonAlpha += 0.05f;
            }
            else if (!_showFullPortrait && _expressionButtonAlpha > 0f) {
                _expressionButtonAlpha -= 0.05f;
            }

            if (_showFullPortrait) {
                if (_transitionProgress < 1f) {
                    _transitionProgress += 0.04f;
                }
                if (_portraitAlpha < 1f) {
                    _portraitAlpha += 0.05f;
                }
            }
            else {
                if (_portraitAlpha > 0f) {
                    _portraitAlpha -= 0.05f;
                }
                if (_transitionProgress > 0f) {
                    _transitionProgress -= 0.04f;
                }
            }


            _flameTimer += 0.045f;
            _glowTimer += 0.038f;
            UpdatePulseTimer();

            if (_flameTimer > MathHelper.TwoPi) _flameTimer -= MathHelper.TwoPi;
            if (_glowTimer > MathHelper.TwoPi) _glowTimer -= MathHelper.TwoPi;

            UpdateParticles();
            UpdateInteraction();
            UpdateScaleInput();
            if (_showFullPortrait) {
                ClampPortraitOffsets();
            }
        }

        private void UpdateInteraction() {
            bool hoverIcon = ShouldShowIcon() && IconHitBox.Contains(MousePosition.ToPoint());
            bool hoverExpressionButton = ShouldShowIcon() && _showFullPortrait && ExpressionButtonHitBox.Contains(MousePosition.ToPoint()) && !hoverIcon;
            bool hoverLeftPortrait = ShouldShowIcon() && _showFullPortrait && LeftPortraitHitBox.Contains(MousePosition.ToPoint()) && !hoverIcon && !hoverExpressionButton;
            bool hoverRightPortrait = ShouldShowIcon() && _showFullPortrait && RightPortraitHitBox.Contains(MousePosition.ToPoint()) && !hoverIcon && !hoverExpressionButton;

            if (!CanInteract()) {
                if (_draggingLeftPortrait || _draggingRightPortrait) {
                    MarkNeedsSave();
                }
                _draggingLeftPortrait = false;
                _draggingRightPortrait = false;
                return;
            }

            if (keyLeftPressState == KeyPressState.Pressed) {
                if (hoverLeftPortrait && !_draggingRightPortrait) {
                    _draggingLeftPortrait = true;
                    _dragStartMousePos = MousePosition;
                    _dragStartOffset = _leftPortraitOffset;
                }
                else if (hoverRightPortrait && !_draggingLeftPortrait) {
                    _draggingRightPortrait = true;
                    _dragStartMousePos = MousePosition;
                    _dragStartOffset = _rightPortraitOffset;
                }
                else if (hoverIcon && !_draggingLeftPortrait && !_draggingRightPortrait) {
                    _showFullPortrait = !_showFullPortrait;
                    MarkNeedsSave();
                }
                else if (hoverExpressionButton && !_draggingLeftPortrait && !_draggingRightPortrait) {
                    CycleExpression();
                }
            }

            if (keyLeftPressState == KeyPressState.Released) {
                if (_draggingLeftPortrait || _draggingRightPortrait) {
                    MarkNeedsSave();
                }
                _draggingLeftPortrait = false;
                _draggingRightPortrait = false;
            }

            if (_draggingLeftPortrait) {
                _leftPortraitOffset = _dragStartOffset + (MousePosition - _dragStartMousePos);
            }
            else if (_draggingRightPortrait) {
                _rightPortraitOffset = _dragStartOffset + (MousePosition - _dragStartMousePos);
            }

            if (_showFullPortrait && CanInteract()) {
                int scrollDelta = PlayerInput.ScrollWheelDeltaForUI;
                if (scrollDelta != 0) {
                    float scaleDelta = scrollDelta > 0 ? ScaleStep : -ScaleStep;

                    if (hoverLeftPortrait) {
                        AdjustLeftPortraitScale(scaleDelta);
                    }
                    else if (hoverRightPortrait) {
                        AdjustRightPortraitScale(scaleDelta);
                    }
                }
            }
        }

        private void UpdateScaleInput() {
            if (!_showFullPortrait || !CanInteract()) {
                _scaleKeyTimer = 0;
                return;
            }

            if (_scaleKeyTimer > 0) {
                _scaleKeyTimer--;
                return;
            }

            bool hoverLeftPortrait = LeftPortraitHitBox.Contains(MousePosition.ToPoint());
            bool hoverRightPortrait = RightPortraitHitBox.Contains(MousePosition.ToPoint());

            //+/=增大
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemPlus) ||
                Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Add)) {

                if (hoverLeftPortrait) {
                    AdjustLeftPortraitScale(ScaleStep);
                    _scaleKeyTimer = ScaleKeyDelay;
                }
                else if (hoverRightPortrait) {
                    AdjustRightPortraitScale(ScaleStep);
                    _scaleKeyTimer = ScaleKeyDelay;
                }
            }
            //-减小
            else if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemMinus) ||
                     Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Subtract)) {

                if (hoverLeftPortrait) {
                    AdjustLeftPortraitScale(-ScaleStep);
                    _scaleKeyTimer = ScaleKeyDelay;
                }
                else if (hoverRightPortrait) {
                    AdjustRightPortraitScale(-ScaleStep);
                    _scaleKeyTimer = ScaleKeyDelay;
                }
            }
        }

        private void UpdateParticles() {
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);

            _emberSpawnTimer++;
            if (_emberSpawnTimer >= 10 && _embers.Count < 25) {
                _emberSpawnTimer = 0;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = iconCenter + angle.ToRotationVector2() * IconSize * 0.4f;
                _embers.Add(new EmberPRT(spawnPos
                    , Main.rand.NextFloat(2f, 4.5f)
                    , Main.rand.NextFloat(0.4f, 1.0f)
                    , Main.rand.NextFloat(-0.3f, 0.3f)
                    , 0
                    , Main.rand.NextFloat(60f, 100f)));
            }

            for (int i = _embers.Count - 1; i >= 0; i--) {
                if (_embers[i].Update()) {
                    _embers.RemoveAt(i);
                }
            }

            if (_showFullPortrait) {
                _wispSpawnTimer++;
                if (_wispSpawnTimer >= 30 && _flameWisps.Count < 20) {
                    _wispSpawnTimer = 0;

                    Vector2 center = new Vector2(Main.screenWidth * LeftPortraitXRatio, Main.screenHeight * 0.5f) + _leftPortraitOffset;

                    Vector2 spawnPos = center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(200f, 350f);
                    var wisp = new FlameWispPRT(spawnPos);
                    wisp.Size /= 2;
                    _flameWisps.Add(wisp);
                }

                for (int i = _flameWisps.Count - 1; i >= 0; i--) {
                    Vector2 targetCenter = _flameWisps[i].Pos.X < Main.screenWidth * 0.5f
                        ? new Vector2(Main.screenWidth * LeftPortraitXRatio, Main.screenHeight * 0.5f) + _leftPortraitOffset
                        : new Vector2(Main.screenWidth * RightPortraitXRatio, Main.screenHeight * 0.5f) + _rightPortraitOffset;

                    if (_flameWisps[i].Update(targetCenter, 400f)) {
                        _flameWisps.RemoveAt(i);
                    }
                }
            }
        }
        #endregion

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (!IsResourceLoaded()) {
                return;
            }

            if (_portraitAlpha > 0.01f) {
                DrawPortraits(spriteBatch);
            }

            if (_iconAlpha > 0.01f) {
                DrawIconFrame(spriteBatch);

                if (_expressionButtonAlpha > 0.01f) {
                    DrawExpressionButton(spriteBatch);
                }
            }
        }

        private void DrawPortraits(SpriteBatch spriteBatch) {
            Texture2D portraitTex = GetCurrentPortraitTexture();
            if (portraitTex == null || portraitTex.IsDisposed) {
                return;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            foreach (var wisp in _flameWisps) {
                wisp.Draw(spriteBatch, _portraitAlpha * 0.5f);
            }

            DrawLeftPortrait(spriteBatch, portraitTex);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private void DrawLeftPortrait(SpriteBatch sb, Texture2D tex) {
            float scale = _leftPortraitScale * (0.95f + _transitionProgress * 0.05f) * 1.6f;
            Vector2 drawPos = GetLeftPortraitPosition(tex);
            Rectangle sourceRect = new Rectangle(0, 0, tex.Width, tex.Height);
            float dragHighlight = _draggingLeftPortrait ? 1.1f : 1f;

            float shadowOffset = 6f;
            sb.Draw(tex, drawPos + new Vector2(shadowOffset, shadowOffset),
                sourceRect, new Color(10, 5, 5) * (_portraitAlpha * 0.25f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            float glowPulse = (float)Math.Sin(_glowTimer * 1.2f) * 0.5f + 0.5f;
            Color glowColor = new Color(255, 120, 60) * (_portraitAlpha * 0.08f * glowPulse * dragHighlight);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + _flameTimer;
                Vector2 offset = angle.ToRotationVector2() * 4f;
                sb.Draw(tex, drawPos + offset, sourceRect, glowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            sb.Draw(tex, drawPos, sourceRect, Color.White * _portraitAlpha * dragHighlight, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawExpressionButton(SpriteBatch sb) {
            if (_expressionButtonAlpha <= 0.01f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 buttonPos = ExpressionButtonPosition;
            bool hoverButton = ExpressionButtonHitBox.Contains(MousePosition.ToPoint()) && CanInteract();

            Rectangle bgRect = new Rectangle(
                (int)buttonPos.X - 3,
                (int)buttonPos.Y - 3,
                (int)ExpressionButtonSize + 6,
                (int)ExpressionButtonSize + 6
            );
            Color bgColor = new Color(25, 5, 5) * (_expressionButtonAlpha * _iconAlpha * 0.85f);
            float pulse = (float)Math.Sin(_pulseTimer * 1.8f) * 0.5f + 0.5f;

            DrawBaseBackground(sb, bgRect, _expressionButtonAlpha * _iconAlpha, hoverButton, bgColor);

            Color pulseColor = new Color(120, 25, 15) * (_expressionButtonAlpha * _iconAlpha * 0.2f * pulse);
            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);

            string expressionIcon = _currentExpression switch {
                PortraitExpression.Default => "◆",
                PortraitExpression.CloseEyes => "◇",
                PortraitExpression.Smile => "◈",
                _ => "◆"
            };

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 textSize = font.MeasureString(expressionIcon);
            Vector2 textPos = buttonPos + new Vector2(ExpressionButtonSize / 2 - textSize.X / 2, ExpressionButtonSize / 2 - textSize.Y / 2 + 4);

            float iconScale = hoverButton ? 1.15f : 1f;
            Utils.DrawBorderString(sb, expressionIcon, textPos, Color.White * _expressionButtonAlpha * _iconAlpha, iconScale);

            DrawBrimstoneFrame(sb, bgRect, _expressionButtonAlpha * _iconAlpha, pulse);
        }

        private void DrawIconFrame(SpriteBatch spriteBatch) {
            if (!IsResourceLoaded()) {
                return;
            }

            Texture2D iconTex = ADVAsset.SupCalsADV[0];
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);
            bool hoverIcon = IconHitBox.Contains(MousePosition.ToPoint()) && CanInteract();

            foreach (var ember in _embers) {
                ember.Draw(spriteBatch, _iconAlpha * 0.9f);
            }

            Rectangle bgRect = new Rectangle((int)IconPosition.X - 5, (int)IconPosition.Y - 5,
                (int)IconSize + 10, (int)IconSize + 10);
            Color bgColor = new Color(25, 5, 5) * (_iconAlpha * 0.85f);

            DrawBaseBackground(spriteBatch, bgRect, _iconAlpha, hoverIcon, bgColor);

            float iconScale = IconSize / Math.Max(iconTex.Width, iconTex.Height);
            if (hoverIcon) {
                iconScale *= 1.1f + (float)Math.Sin(_flameTimer * 2f) * 0.05f;
            }

            Vector2 iconDrawPos = iconCenter;
            spriteBatch.Draw(iconTex, iconDrawPos, null, Color.White * _iconAlpha,
                0f, iconTex.Size() / 2, iconScale, SpriteEffects.None, 0f);

            DrawBrimstoneFrame(spriteBatch, bgRect, _iconAlpha, (float)Math.Sin(_pulseTimer * 1.8f) * 0.5f + 0.5f);
        }

        private void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);

            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            DrawFlameMark(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawFlameMark(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
            DrawFlameMark(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.65f);
            DrawFlameMark(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.65f);
        }

        private void DrawFlameMark(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color flameColor = new Color(255, 150, 70) * alpha;

            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.85f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size * 1.2f, size * 0.3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, MathHelper.PiOver4,
                new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), flameColor * 0.7f, -MathHelper.PiOver4,
                new Vector2(0.5f, 0.5f), new Vector2(size * 0.9f, size * 0.25f), SpriteEffects.None, 0f);
        }
        #endregion
    }
}

