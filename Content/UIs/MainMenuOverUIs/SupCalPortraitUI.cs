using CalamityOverhaul.Content.ADV;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// 女巫立绘UI，主菜单显示
    /// </summary>
    internal class SupCalPortraitUI : UIHandle, ICWRLoader
    {
        #region 数据字段
        public static SupCalPortraitUI Instance => UIHandleLoader.GetUIHandleOfType<SupCalPortraitUI>();

        /// <summary>
        /// 立绘表情类型枚举
        /// </summary>
        private enum PortraitExpression
        {
            Default,    // 默认表情
            CloseEyes,  // 闭眼
            Smile       // 微笑
        }

        private PortraitExpression _currentExpression = PortraitExpression.Default;
        private bool _showFullPortrait = false; //是否显示全身立绘
        private float _iconAlpha = 0f; //头像框透明度
        private float _portraitAlpha = 0f; //立绘透明度
        private float _transitionProgress = 0f; //过渡进度

        //动画计时器
        private float _flameTimer = 0f;
        private float _glowTimer = 0f;
        private float _pulseTimer = 0f;

        //粒子系统
        private readonly List<EmberParticle> _embers = new();
        private readonly List<FlameWisp> _flameWisps = new();
        private int _emberSpawnTimer = 0;
        private int _wispSpawnTimer = 0;

        //UI位置和尺寸
        private const float IconSize = 80f;
        private const float IconBottomMargin = 46f;
        
        //左侧立绘参数（半身大图，从腰部开始裁剪）
        private const float LeftPortraitXRatio = 0.18f;
        private const float LeftPortraitScale = 2.0f;
        private const float LeftPortraitCropBottom = 0.45f;
        
        //右侧立绘参数（全身小图）
        private const float RightPortraitXRatio = 0.82f;
        private const float RightPortraitScale = 0.85f;

        //立绘拖动相关
        private bool _draggingLeftPortrait = false;
        private bool _draggingRightPortrait = false;
        private Vector2 _leftPortraitOffset = Vector2.Zero; //左侧立绘偏移
        private Vector2 _rightPortraitOffset = Vector2.Zero; //右侧立绘偏移
        private Vector2 _dragStartMousePos = Vector2.Zero;
        private Vector2 _dragStartOffset = Vector2.Zero;

        //表情切换按钮相关
        private const float ExpressionButtonSize = 40f;
        private float _expressionButtonAlpha = 0f;
        
        private Vector2 IconPosition => new Vector2(
            Main.screenWidth / 2 - IconSize / 2,
            Main.screenHeight - IconSize - IconBottomMargin
        );

        private Rectangle IconHitBox => new Rectangle(
            (int)IconPosition.X,
            (int)IconPosition.Y,
            (int)IconSize,
            (int)IconSize
        );

        // 表情切换按钮位置（在头像框右侧）
        private Vector2 ExpressionButtonPosition => new Vector2(
            IconPosition.X + IconSize + 15,
            IconPosition.Y + (IconSize - ExpressionButtonSize) / 2
        );

        private Rectangle ExpressionButtonHitBox => new Rectangle(
            (int)ExpressionButtonPosition.X,
            (int)ExpressionButtonPosition.Y,
            (int)ExpressionButtonSize,
            (int)ExpressionButtonSize
        );

        // 左侧立绘点击区域
        private Rectangle LeftPortraitHitBox {
            get {
                Texture2D portraitTex = GetCurrentPortraitTexture();
                if (portraitTex == null) return Rectangle.Empty;
                
                Vector2 leftPos = GetLeftPortraitPosition(portraitTex);
                float scale = LeftPortraitScale * (0.95f + _transitionProgress * 0.05f) * 1.6f;
                Vector2 size = new Vector2(portraitTex.Width, portraitTex.Height * (1f - LeftPortraitCropBottom)) * scale;
                
                return new Rectangle((int)leftPos.X, (int)leftPos.Y, (int)size.X, (int)size.Y);
            }
        }

        // 右侧立绘点击区域
        private Rectangle RightPortraitHitBox {
            get {
                Texture2D portraitTex = GetCurrentPortraitTexture();
                if (portraitTex == null) return Rectangle.Empty;
                
                Vector2 rightPos = GetRightPortraitPosition(portraitTex);
                float scale = RightPortraitScale * (0.95f + _transitionProgress * 0.05f) * 2f;
                Vector2 size = portraitTex.Size() * scale;
                
                return new Rectangle((int)rightPos.X, (int)rightPos.Y, (int)size.X, (int)size.Y);
            }
        }

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool && Main.gameMenu;

        #endregion

        #region 粒子内部类
        private class EmberParticle
        {
            public Vector2 Pos;
            public float Size;
            public float RiseSpeed;
            public float Drift;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float Rotation;
            public float RotationSpeed;

            public EmberParticle(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(2f, 4.5f);
                RiseSpeed = Main.rand.NextFloat(0.4f, 1.0f);
                Drift = Main.rand.NextFloat(-0.3f, 0.3f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 100f);
                Seed = Main.rand.NextFloat(10f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.04f, 0.04f);
            }

            public bool Update() {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (1f - t * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
                Rotation += RotationSpeed;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.15f);

                Color emberCore = Color.Lerp(new Color(255, 180, 80), new Color(255, 80, 40), t) * (alpha * 0.85f * fade);
                Color emberGlow = Color.Lerp(new Color(255, 140, 60), new Color(180, 40, 20), t) * (alpha * 0.5f * fade);

                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberGlow, 0f, new Vector2(0.5f, 0.5f), scale * 2.2f, SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberCore, Rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }

        private class FlameWisp
        {
            public Vector2 Pos;
            public Vector2 Velocity;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float Phase;

            public FlameWisp(Vector2 start) {
                Pos = start;
                Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.2f, 0.6f);
                Size = Main.rand.NextFloat(6f, 12f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(80f, 140f);
                Seed = Main.rand.NextFloat(10f);
                Phase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update(Vector2 center, float radius) {
                Life++;
                Phase += 0.08f;
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.5f,
                    (float)Math.Cos(Phase * 1.3f + Seed * 1.5f) * 0.3f
                );
                Pos += Velocity + drift;

                //边界检查
                Vector2 toCenter = center - Pos;
                if (toCenter.Length() > radius) {
                    Velocity = toCenter * 0.01f;
                }

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float pulse = (float)Math.Sin(Life * 0.15f + Seed) * 0.5f + 0.5f;
                float scale = Size * (0.8f + pulse * 0.4f);

                Color wispCore = new Color(255, 200, 120) * (alpha * 0.6f * fade);
                Color wispGlow = new Color(255, 120, 60) * (alpha * 0.3f * fade);

                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), wispGlow, 0f, new Vector2(0.5f, 0.5f), scale * 3f, SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), wispCore, 0f, new Vector2(0.5f, 0.5f), scale * 1.2f, SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 生命周期
        void ICWRLoader.SetupData() { }

        public override void SetStaticDefaults() {
            _iconAlpha = 0f;
            _portraitAlpha = 0f;
            _showFullPortrait = false;
            _currentExpression = PortraitExpression.Default;
            _leftPortraitOffset = Vector2.Zero;
            _rightPortraitOffset = Vector2.Zero;
            _expressionButtonAlpha = 0f;
        }

        public override void UnLoad() {
            _embers?.Clear();
            _flameWisps?.Clear();
        }
        #endregion

        #region 立绘管理
        /// <summary>
        /// 获取当前表情对应的立绘纹理
        /// </summary>
        private Texture2D GetCurrentPortraitTexture() {
            return _currentExpression switch {
                PortraitExpression.CloseEyes => ADVAsset.SupCal_closeEyesADV ?? ADVAsset.SupCalADV,
                PortraitExpression.Smile => ADVAsset.SupCal_smileADV ?? ADVAsset.SupCalADV,
                _ => ADVAsset.SupCalADV
            };
        }

        /// <summary>
        /// 切换到下一个表情
        /// </summary>
        private void CycleExpression() {
            _currentExpression = _currentExpression switch {
                PortraitExpression.Default => PortraitExpression.CloseEyes,
                PortraitExpression.CloseEyes => PortraitExpression.Smile,
                PortraitExpression.Smile => PortraitExpression.Default,
                _ => PortraitExpression.Default
            };
            SoundEngine.PlaySound(SoundID.MenuTick);
        }

        /// <summary>
        /// 获取左侧立绘绘制位置（上半身大图）
        /// </summary>
        private Vector2 GetLeftPortraitPosition(Texture2D tex) {
            float scale = LeftPortraitScale * (0.95f + _transitionProgress * 0.05f);
            float displayHeight = tex.Height * (1f - LeftPortraitCropBottom) * scale;
            
            Vector2 basePos = new Vector2(
                Main.screenWidth * LeftPortraitXRatio - (tex.Width * scale) / 2,
                Main.screenHeight - displayHeight - 140
            );
            
            return basePos + _leftPortraitOffset;
        }

        /// <summary>
        /// 获取右侧立绘绘制位置（全身小图）
        /// </summary>
        private Vector2 GetRightPortraitPosition(Texture2D tex) {
            float scale = RightPortraitScale * (0.95f + _transitionProgress * 0.05f);
            
            Vector2 basePos = new Vector2(
                Main.screenWidth * RightPortraitXRatio - (tex.Width * scale) / 2 - 300,
                Main.screenHeight - tex.Height * scale - 220
            );
            
            return basePos + _rightPortraitOffset;
        }
        #endregion

        #region 更新逻辑
        public override void Update() {
            if (!Main.gameMenu) {
                _iconAlpha = 0f;
                _portraitAlpha = 0f;
                _expressionButtonAlpha = 0f;
                return;
            }

            //渐入效果
            if (_iconAlpha < 1f) {
                _iconAlpha += 0.02f;
            }

            //表情按钮渐入效果
            if (_showFullPortrait && _expressionButtonAlpha < 1f) {
                _expressionButtonAlpha += 0.05f;
            }
            else if (!_showFullPortrait && _expressionButtonAlpha > 0f) {
                _expressionButtonAlpha -= 0.05f;
            }

            //立绘过渡
            if (_showFullPortrait) {
                if (_portraitAlpha < 1f) {
                    _portraitAlpha += 0.05f;
                }
                if (_transitionProgress < 1f) {
                    _transitionProgress += 0.04f;
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

            //动画计时器
            _flameTimer += 0.045f;
            _glowTimer += 0.038f;
            _pulseTimer += 0.025f;

            if (_flameTimer > MathHelper.TwoPi) _flameTimer -= MathHelper.TwoPi;
            if (_glowTimer > MathHelper.TwoPi) _glowTimer -= MathHelper.TwoPi;
            if (_pulseTimer > MathHelper.TwoPi) _pulseTimer -= MathHelper.TwoPi;

            //更新粒子
            UpdateParticles();

            //检测交互
            UpdateInteraction();
        }

        private void UpdateInteraction() {
            bool hoverIcon = IconHitBox.Contains(MousePosition.ToPoint());
            bool hoverExpressionButton = _showFullPortrait && ExpressionButtonHitBox.Contains(MousePosition.ToPoint());
            bool hoverLeftPortrait = _showFullPortrait && LeftPortraitHitBox.Contains(MousePosition.ToPoint());
            bool hoverRightPortrait = _showFullPortrait && RightPortraitHitBox.Contains(MousePosition.ToPoint());

            if (!CanInteract()) {
                _draggingLeftPortrait = false;
                _draggingRightPortrait = false;
                return;
            }

            //处理拖动
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
                    //点击头像框：切换立绘显示/隐藏
                    _showFullPortrait = !_showFullPortrait;
                }
                else if (hoverExpressionButton && !_draggingLeftPortrait && !_draggingRightPortrait) {
                    //点击表情按钮：切换表情
                    CycleExpression();
                }
            }

            if (keyLeftPressState == KeyPressState.Released) {
                _draggingLeftPortrait = false;
                _draggingRightPortrait = false;
            }

            //更新拖动偏移
            if (_draggingLeftPortrait) {
                _leftPortraitOffset = _dragStartOffset + (MousePosition - _dragStartMousePos);
            }
            else if (_draggingRightPortrait) {
                _rightPortraitOffset = _dragStartOffset + (MousePosition - _dragStartMousePos);
            }
        }

        private void UpdateParticles() {
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);

            //生成余烬粒子
            _emberSpawnTimer++;
            if (_emberSpawnTimer >= 10 && _embers.Count < 25) {
                _emberSpawnTimer = 0;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = iconCenter + angle.ToRotationVector2() * IconSize * 0.4f;
                _embers.Add(new EmberParticle(spawnPos));
            }

            for (int i = _embers.Count - 1; i >= 0; i--) {
                if (_embers[i].Update()) {
                    _embers.RemoveAt(i);
                }
            }

            //生成火焰精灵（分布在两侧立绘周围）
            if (_showFullPortrait) {
                _wispSpawnTimer++;
                if (_wispSpawnTimer >= 30 && _flameWisps.Count < 20) {
                    _wispSpawnTimer = 0;
                    
                    //随机在左侧或右侧生成
                    bool spawnLeft = Main.rand.NextBool();
                    Vector2 center = spawnLeft 
                        ? new Vector2(Main.screenWidth * LeftPortraitXRatio, Main.screenHeight * 0.5f) + _leftPortraitOffset
                        : new Vector2(Main.screenWidth * RightPortraitXRatio, Main.screenHeight * 0.5f) + _rightPortraitOffset;
                    
                    Vector2 spawnPos = center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(200f, 350f);
                    _flameWisps.Add(new FlameWisp(spawnPos));
                }

                for (int i = _flameWisps.Count - 1; i >= 0; i--) {
                    //让火焰精灵分别围绕左右两侧
                    Vector2 targetCenter = _flameWisps[i].Pos.X < Main.screenWidth * 0.5f
                        ? new Vector2(Main.screenWidth * LeftPortraitXRatio, Main.screenHeight * 0.5f) + _leftPortraitOffset
                        : new Vector2(Main.screenWidth * RightPortraitXRatio, Main.screenHeight * 0.5f) + _rightPortraitOffset;
                    
                    if (_flameWisps[i].Update(targetCenter, 400f)) {
                        _flameWisps.RemoveAt(i);
                    }
                }
            }
        }

        private bool CanInteract() {
            return !FeedbackUI.Instance.OnActive() && !AcknowledgmentUI.OnActive();
        }
        #endregion

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (_iconAlpha <= 0.01f) {
                return;
            }

            //绘制立绘（无背景暗化，无边框）
            if (_portraitAlpha > 0.01f) {
                DrawPortraits(spriteBatch);
            }

            //绘制头像框
            DrawIconFrame(spriteBatch);

            //绘制表情切换按钮
            if (_expressionButtonAlpha > 0.01f) {
                DrawExpressionButton(spriteBatch);
            }
        }

        private void DrawPortraits(SpriteBatch spriteBatch) {
            Texture2D portraitTex = GetCurrentPortraitTexture();
            if (portraitTex == null) {
                return;
            }

            //绘制火焰精灵（在立绘后面）
            foreach (var wisp in _flameWisps) {
                wisp.Draw(spriteBatch, _portraitAlpha * 0.5f);
            }

            //左侧立绘（上半身大图）
            DrawLeftPortrait(spriteBatch, portraitTex);

            //右侧立绘（全身小图）
            DrawRightPortrait(spriteBatch, portraitTex);
        }

        /// <summary>
        /// 绘制左侧上半身大图
        /// </summary>
        private void DrawLeftPortrait(SpriteBatch sb, Texture2D tex) {
            float scale = LeftPortraitScale * (0.95f + _transitionProgress * 0.05f) * 1.6f;
            Vector2 drawPos = GetLeftPortraitPosition(tex);

            //计算裁剪区域（只显示上半身，裁剪腿部）
            int displayHeight = (int)(tex.Height * (1f - LeftPortraitCropBottom));
            Rectangle sourceRect = new Rectangle(0, 0, tex.Width, displayHeight);

            //拖动时的高亮效果
            float dragHighlight = _draggingLeftPortrait ? 1.1f : 1f;

            //轻微阴影（更淡，融入背景）
            float shadowOffset = 6f;
            sb.Draw(tex, drawPos + new Vector2(shadowOffset, shadowOffset),
                sourceRect, new Color(10, 5, 5) * (_portraitAlpha * 0.25f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            //火焰光晕（更柔和）
            float glowPulse = (float)Math.Sin(_glowTimer * 1.2f) * 0.5f + 0.5f;
            Color glowColor = new Color(255, 120, 60) * (_portraitAlpha * 0.08f * glowPulse * dragHighlight);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + _flameTimer;
                Vector2 offset = angle.ToRotationVector2() * 4f;
                sb.Draw(tex, drawPos + offset, sourceRect, glowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            //主体绘制
            sb.Draw(tex, drawPos, sourceRect, Color.White * _portraitAlpha * dragHighlight, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制右侧全身小图
        /// </summary>
        private void DrawRightPortrait(SpriteBatch sb, Texture2D tex) {
            float scale = RightPortraitScale * (0.95f + _transitionProgress * 0.05f) * 2;
            Vector2 drawPos = GetRightPortraitPosition(tex);

            //拖动时的高亮效果
            float dragHighlight = _draggingRightPortrait ? 1.1f : 1f;

            //轻微阴影
            float shadowOffset = 5f;
            sb.Draw(tex, drawPos + new Vector2(shadowOffset, shadowOffset),
                null, new Color(10, 5, 5) * (_portraitAlpha * 0.2f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            //火焰光晕（更柔和）
            float glowPulse = (float)Math.Sin(_glowTimer * 1.1f) * 0.5f + 0.5f;
            Color glowColor = new Color(255, 120, 60) * (_portraitAlpha * 0.05f * glowPulse * dragHighlight);
            for (int i = 0; i < 3; i++) {
                float angle = MathHelper.TwoPi * i / 3f + _flameTimer * 0.7f;
                Vector2 offset = angle.ToRotationVector2() * 3f;
                sb.Draw(tex, drawPos + offset, null, glowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            //主体绘制
            sb.Draw(tex, drawPos, null, Color.White * _portraitAlpha * dragHighlight, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 绘制表情切换按钮
        /// </summary>
        private void DrawExpressionButton(SpriteBatch sb) {
            if (_expressionButtonAlpha <= 0.01f) return;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 buttonPos = ExpressionButtonPosition;
            bool hoverButton = ExpressionButtonHitBox.Contains(MousePosition.ToPoint()) && CanInteract();

            //背景框
            Rectangle bgRect = new Rectangle(
                (int)buttonPos.X - 3,
                (int)buttonPos.Y - 3,
                (int)ExpressionButtonSize + 6,
                (int)ExpressionButtonSize + 6
            );
            Color bgColor = new Color(25, 5, 5) * (_expressionButtonAlpha * 0.85f);

            //悬停光效
            if (hoverButton) {
                Color hoverGlow = new Color(255, 180, 80) * (_expressionButtonAlpha * 0.4f);
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f + _flameTimer * 1.5f;
                    Vector2 offset = angle.ToRotationVector2() * 5f;
                    sb.Draw(pixel, bgRect.Location.ToVector2() + offset,
                        new Rectangle(0, 0, bgRect.Width, bgRect.Height), hoverGlow);
                }
            }

            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), bgColor);

            //火焰脉冲背景
            float pulse = (float)Math.Sin(_pulseTimer * 1.8f) * 0.5f + 0.5f;
            Color pulseColor = new Color(120, 25, 15) * (_expressionButtonAlpha * 0.2f * pulse);
            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);

            //绘制表情图标（文字）
            string expressionIcon = _currentExpression switch {
                PortraitExpression.Default => "◆",
                PortraitExpression.CloseEyes => "◇",
                PortraitExpression.Smile => "◈",
                _ => "◆"
            };

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 textSize = font.MeasureString(expressionIcon);
            Vector2 textPos = buttonPos + new Vector2(ExpressionButtonSize / 2 - textSize.X / 2, ExpressionButtonSize / 2 - textSize.Y / 2);

            float iconScale = hoverButton ? 1.15f : 1f;
            Utils.DrawBorderString(sb, expressionIcon, textPos, Color.White * _expressionButtonAlpha, iconScale);

            //火焰边框
            DrawBrimstoneFrame(sb, bgRect, _expressionButtonAlpha, pulse);
        }

        private void DrawIconFrame(SpriteBatch spriteBatch) {
            if (ADVAsset.SupCalsADV == null || ADVAsset.SupCalsADV.Count == 0) {
                return;
            }

            Texture2D iconTex = ADVAsset.SupCalsADV[0];
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);
            bool hoverIcon = IconHitBox.Contains(MousePosition.ToPoint()) && CanInteract();

            //绘制余烬粒子
            foreach (var ember in _embers) {
                ember.Draw(spriteBatch, _iconAlpha * 0.9f);
            }

            //背景框
            Rectangle bgRect = new Rectangle((int)IconPosition.X - 5, (int)IconPosition.Y - 5,
                (int)IconSize + 10, (int)IconSize + 10);
            Color bgColor = new Color(25, 5, 5) * (_iconAlpha * 0.85f);

            //悬停光效
            if (hoverIcon) {
                Color hoverGlow = new Color(255, 180, 80) * (_iconAlpha * 0.4f);
                for (int i = 0; i < 6; i++) {
                    float angle = MathHelper.TwoPi * i / 6f + _flameTimer * 1.5f;
                    Vector2 offset = angle.ToRotationVector2() * 6f;
                    spriteBatch.Draw(pixel, bgRect.Location.ToVector2() + offset,
                        new Rectangle(0, 0, bgRect.Width, bgRect.Height), hoverGlow);
                }
            }

            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), bgColor);

            //火焰脉冲背景
            float pulse = (float)Math.Sin(_pulseTimer * 1.8f) * 0.5f + 0.5f;
            Color pulseColor = new Color(120, 25, 15) * (_iconAlpha * 0.2f * pulse);
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);

            //头像
            float iconScale = IconSize / Math.Max(iconTex.Width, iconTex.Height);
            if (hoverIcon) {
                iconScale *= 1.1f + (float)Math.Sin(_flameTimer * 2f) * 0.05f;
            }

            Vector2 iconDrawPos = iconCenter;
            spriteBatch.Draw(iconTex, iconDrawPos, null, Color.White * _iconAlpha,
                0f, iconTex.Size() / 2, iconScale, SpriteEffects.None, 0f);

            //火焰边框
            DrawBrimstoneFrame(spriteBatch, bgRect, _iconAlpha, pulse);
        }

        private void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);

            //外框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            //角落火焰标记
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
