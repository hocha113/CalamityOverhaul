using CalamityOverhaul.Content.ADV;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// 至尊女主立绘UI - 主菜单显示
    /// </summary>
    internal class SupCalPortraitUI : UIHandle, ICWRLoader
    {
        #region 数据字段
        public static SupCalPortraitUI Instance => UIHandleLoader.GetUIHandleOfType<SupCalPortraitUI>();

        private bool _showFullPortrait = false; // 是否显示全身立绘
        private float _iconAlpha = 0f; // 头像框透明度
        private float _portraitAlpha = 0f; // 立绘透明度
        private float _transitionProgress = 0f; // 过渡进度

        // 动画计时器
        private float _flameTimer = 0f;
        private float _glowTimer = 0f;
        private float _pulseTimer = 0f;

        // 粒子系统
        private readonly List<EmberParticle> _embers = new();
        private readonly List<FlameWisp> _flameWisps = new();
        private int _emberSpawnTimer = 0;
        private int _wispSpawnTimer = 0;

        // UI位置和尺寸
        private const float IconSize = 80f;
        private const float IconBottomMargin = 20f;
        private const float PortraitScale = 0.8f;
        private Vector2 IconPosition => new Vector2(
            Main.screenWidth / 6 * 1 - IconSize / 2,
            Main.screenHeight - IconSize - IconBottomMargin
        );

        private Rectangle IconHitBox => new Rectangle(
            (int)IconPosition.X,
            (int)IconPosition.Y,
            (int)IconSize,
            (int)IconSize
        );

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

                // 边界检查
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
        }

        public override void UnLoad() {
            _embers?.Clear();
            _flameWisps?.Clear();
        }
        #endregion

        #region 更新逻辑
        public override void Update() {
            if (!Main.gameMenu) {
                _iconAlpha = 0f;
                _portraitAlpha = 0f;
                return;
            }

            // 渐入效果
            if (_iconAlpha < 1f) {
                _iconAlpha += 0.02f;
            }

            // 立绘过渡
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

            // 动画计时器
            _flameTimer += 0.045f;
            _glowTimer += 0.038f;
            _pulseTimer += 0.025f;

            if (_flameTimer > MathHelper.TwoPi) _flameTimer -= MathHelper.TwoPi;
            if (_glowTimer > MathHelper.TwoPi) _glowTimer -= MathHelper.TwoPi;
            if (_pulseTimer > MathHelper.TwoPi) _pulseTimer -= MathHelper.TwoPi;

            // 更新粒子
            UpdateParticles();

            // 检测点击
            bool hoverIcon = IconHitBox.Contains(MousePosition.ToPoint());
            if (hoverIcon && keyLeftPressState == KeyPressState.Pressed && CanInteract()) {
                _showFullPortrait = !_showFullPortrait;
                SoundEngine.PlaySound(_showFullPortrait ? SoundID.MenuOpen : SoundID.MenuClose);
            }
        }

        private void UpdateParticles() {
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);

            // 生成余烬粒子
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

            // 生成火焰精灵
            if (_showFullPortrait) {
                _wispSpawnTimer++;
                if (_wispSpawnTimer >= 30 && _flameWisps.Count < 12) {
                    _wispSpawnTimer = 0;
                    Vector2 portraitCenter = new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);
                    Vector2 spawnPos = portraitCenter + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(200f, 400f);
                    _flameWisps.Add(new FlameWisp(spawnPos));
                }

                for (int i = _flameWisps.Count - 1; i >= 0; i--) {
                    if (_flameWisps[i].Update(new Vector2(Main.screenWidth / 2, Main.screenHeight / 2), 500f)) {
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

            // 绘制全身立绘
            if (_portraitAlpha > 0.01f) {
                DrawFullPortrait(spriteBatch);
            }

            // 绘制头像框
            DrawIconFrame(spriteBatch);
        }

        private void DrawFullPortrait(SpriteBatch spriteBatch) {
            if (ADVAsset.SupCalADV == null) {
                return;
            }

            Texture2D portraitTex = ADVAsset.SupCalADV;
            Vector2 portraitCenter = new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);

            // 背景暗化
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float bgAlpha = _portraitAlpha * 0.7f;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Rectangle(0, 0, 1, 1), Color.Black * bgAlpha);

            // 绘制火焰精灵
            foreach (var wisp in _flameWisps) {
                wisp.Draw(spriteBatch, _portraitAlpha * 0.8f);
            }

            // 计算立绘尺寸和位置
            float scale = PortraitScale * (0.9f + _transitionProgress * 0.1f) * 2;
            Vector2 portraitSize = portraitTex.Size() * scale;
            Vector2 portraitPos = portraitCenter - portraitSize / 2;

            // 立绘阴影
            float shadowOffset = 10f;
            spriteBatch.Draw(portraitTex, portraitPos + new Vector2(shadowOffset, shadowOffset),
                null, new Color(20, 0, 0) * (_portraitAlpha * 0.5f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // 火焰光晕
            float glowPulse = (float)Math.Sin(_glowTimer * 1.5f) * 0.5f + 0.5f;
            Color glowColor = new Color(255, 120, 60) * (_portraitAlpha * 0.15f * glowPulse);
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + _flameTimer;
                Vector2 offset = angle.ToRotationVector2() * 8f;
                spriteBatch.Draw(portraitTex, portraitPos + offset,
                    null, glowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            // 主体绘制
            spriteBatch.Draw(portraitTex, portraitPos,
                null, Color.White * _portraitAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // 边框装饰
            DrawPortraitFrame(spriteBatch, new Rectangle((int)portraitPos.X, (int)portraitPos.Y,
                (int)portraitSize.X, (int)portraitSize.Y), _portraitAlpha);
        }

        private void DrawIconFrame(SpriteBatch spriteBatch) {
            if (ADVAsset.SupCalsADV == null || ADVAsset.SupCalsADV.Count == 0) {
                return;
            }

            Texture2D iconTex = ADVAsset.SupCalsADV[0];
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);
            bool hoverIcon = IconHitBox.Contains(MousePosition.ToPoint()) && CanInteract();

            // 绘制余烬粒子
            foreach (var ember in _embers) {
                ember.Draw(spriteBatch, _iconAlpha * 0.9f);
            }

            // 背景框
            Rectangle bgRect = new Rectangle((int)IconPosition.X - 5, (int)IconPosition.Y - 5,
                (int)IconSize + 10, (int)IconSize + 10);
            Color bgColor = new Color(25, 5, 5) * (_iconAlpha * 0.85f);

            // 悬停光效
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

            // 火焰脉冲背景
            float pulse = (float)Math.Sin(_pulseTimer * 1.8f) * 0.5f + 0.5f;
            Color pulseColor = new Color(120, 25, 15) * (_iconAlpha * 0.2f * pulse);
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);

            // 头像
            float iconScale = IconSize / Math.Max(iconTex.Width, iconTex.Height);
            if (hoverIcon) {
                iconScale *= 1.1f + (float)Math.Sin(_flameTimer * 2f) * 0.05f;
            }

            Vector2 iconDrawPos = iconCenter;
            spriteBatch.Draw(iconTex, iconDrawPos, null, Color.White * _iconAlpha,
                0f, iconTex.Size() / 2, iconScale, SpriteEffects.None, 0f);

            // 火焰边框
            DrawBrimstoneFrame(spriteBatch, bgRect, _iconAlpha, pulse);
        }

        private void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);

            // 外框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            // 内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            // 角落火焰标记
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

        private void DrawPortraitFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(_pulseTimer * 1.5f) * 0.5f + 0.5f;

            // 火焰边框
            Color edge = Color.Lerp(new Color(200, 80, 40), new Color(255, 140, 70), pulse) * (alpha * 0.8f);
            int thickness = 4;

            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.9f);

            // 角落装饰
            int cornerSize = 20;
            Color cornerColor = new Color(255, 180, 80) * (alpha * 0.7f);

            // 左上角
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, cornerSize, thickness), new Rectangle(0, 0, 1, 1), cornerColor);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, cornerSize), new Rectangle(0, 0, 1, 1), cornerColor);

            // 右上角
            sb.Draw(pixel, new Rectangle(rect.Right - cornerSize, rect.Y, cornerSize, thickness), new Rectangle(0, 0, 1, 1), cornerColor);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, cornerSize), new Rectangle(0, 0, 1, 1), cornerColor);

            // 左下角
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, cornerSize, thickness), new Rectangle(0, 0, 1, 1), cornerColor * 0.8f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - cornerSize, thickness, cornerSize), new Rectangle(0, 0, 1, 1), cornerColor * 0.8f);

            // 右下角
            sb.Draw(pixel, new Rectangle(rect.Right - cornerSize, rect.Bottom - thickness, cornerSize, thickness), new Rectangle(0, 0, 1, 1), cornerColor * 0.8f);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Bottom - cornerSize, thickness, cornerSize), new Rectangle(0, 0, 1, 1), cornerColor * 0.8f);
        }
        #endregion
    }
}
