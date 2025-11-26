using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.MainMenuOvers
{
    /// <summary>
    /// 比目鱼小姐立绘UI,主菜单显示
    /// </summary>
    internal class HelenPortraitUI : BasePortraitUI
    {
        #region 数据字段
        public static HelenPortraitUI Instance => UIHandleLoader.GetUIHandleOfType<HelenPortraitUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;

        private bool _unlocked = false; //是否已解锁
        private float _unlockProgress = 0f; //解锁进度动画

        //动画计时器
        private float _waveTimer = 0f;
        private float _bubbleTimer = 0f;

        //粒子系统
        private readonly List<BubbleParticle> _bubbles = [];
        private int _bubbleSpawnTimer = 0;
        private readonly List<WaterRipple> _ripples = [];
        private int _rippleSpawnTimer = 0;

        //重写基类属性
        protected override Vector2 GetIconBasePosition() => new Vector2(
            Main.screenWidth / 2 - IconSize / 2 + IconSpacing / 2,
            Main.screenHeight - IconSize - IconBottomMargin
        );

        protected override bool IsResourceLoaded() {
            return ADVAsset.HelenADV != null && !ADVAsset.HelenADV.IsDisposed;
        }

        protected override Color GetHoverGlowColor() => new Color(70, 180, 230);
        protected override Color GetPulseColor() => new Color(30, 120, 150);
        #endregion

        #region 粒子内部类
        private class BubbleParticle
        {
            public Vector2 Pos;
            public float Radius;
            public float RiseSpeed;
            public float Drift;
            public float Life;
            public float MaxLife;
            public float Seed;

            public BubbleParticle(Vector2 start) {
                Pos = start;
                Radius = Main.rand.NextFloat(2f, 5f);
                RiseSpeed = Main.rand.NextFloat(0.3f, 0.8f);
                Drift = Main.rand.NextFloat(-0.15f, 0.15f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 120f);
                Seed = Main.rand.NextFloat(10f);
            }

            public bool Update() {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (0.9f + (float)Math.Sin(t * Math.PI) * 0.2f);
                Pos.X += (float)Math.Sin(Life * 0.05f + Seed) * Drift;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Radius * (0.9f + (float)Math.Sin((Life + Seed * 12f) * 0.12f) * 0.15f);

                Color core = new Color(140, 230, 255) * (alpha * 0.6f * fade);
                Color rim = new Color(30, 100, 150) * (alpha * 0.35f * fade);

                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), rim, 0f,
                    new Vector2(0.5f, 0.5f), new Vector2(scale * 1.5f, scale * 0.5f), SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), core, 0f,
                    new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }

        private class WaterRipple
        {
            public Vector2 Center;
            public float Radius;
            public float MaxRadius;
            public float Life;
            public float MaxLife;

            public WaterRipple(Vector2 center) {
                Center = center;
                Radius = 0f;
                MaxRadius = Main.rand.NextFloat(25f, 45f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(40f, 70f);
            }

            public bool Update() {
                Life++;
                float t = Life / MaxLife;
                Radius = MaxRadius * t;
                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);

                Color rippleColor = new Color(70, 180, 230) * (alpha * 0.4f * fade);

                int segments = 24;
                for (int i = 0; i < segments; i++) {
                    float angle1 = MathHelper.TwoPi * i / segments;
                    float angle2 = MathHelper.TwoPi * (i + 1) / segments;

                    Vector2 p1 = Center + angle1.ToRotationVector2() * Radius;
                    Vector2 p2 = Center + angle2.ToRotationVector2() * Radius;

                    Vector2 diff = p2 - p1;
                    float length = diff.Length();
                    float rotation = diff.ToRotation();

                    sb.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), rippleColor, rotation,
                        Vector2.Zero, new Vector2(length, 1.5f), SpriteEffects.None, 0f);
                }
            }
        }
        #endregion

        #region 生命周期
        protected override void OnSetStaticDefaults() {
            _unlocked = false;
            _unlockProgress = 0f;
            _waveTimer = 0f;
            _bubbleTimer = 0f;
            _bubbleSpawnTimer = 0;
            _rippleSpawnTimer = 0;
        }

        protected override void OnUnLoad() {
            _bubbles?.Clear();
            _ripples?.Clear();

            _unlocked = false;
            _unlockProgress = 0f;
            _waveTimer = 0f;
            _bubbleTimer = 0f;
        }

        public override void LoadSavedState() {
            _portraitOffset = MenuSave.Helen_PortraitOffset;
            _needsSave = false;
        }

        public override void SaveCurrentState() {
            if (!_needsSave) {
                return;
            }

            MenuSave.SaveHelenPortraitState(_portraitOffset);
            _needsSave = false;
        }
        #endregion

        #region 解锁管理
        public void Unlock() {
            if (!_unlocked) {
                _unlocked = true;
                SoundEngine.PlaySound(SoundID.Item4);
            }
        }

        public void Lock() {
            _unlocked = false;
            _unlockProgress = 0f;
        }
        #endregion

        #region 更新逻辑
        public override void Update() {
            UpdateIconAlpha();

            if (!Main.gameMenu || !IsResourceLoaded()) {
                return;
            }

            HandleAutoSave();

            if (!ShouldShowIcon()) {
                return;
            }

            //解锁动画
            if (_unlocked && _unlockProgress < 1f) {
                _unlockProgress += 0.03f;
            }
            else if (!_unlocked && _unlockProgress > 0f) {
                _unlockProgress -= 0.03f;
            }

            //动画计时器
            _waveTimer += 0.035f;
            _bubbleTimer += 0.025f;
            UpdatePulseTimer();

            if (_waveTimer > MathHelper.TwoPi) _waveTimer -= MathHelper.TwoPi;
            if (_bubbleTimer > MathHelper.TwoPi) _bubbleTimer -= MathHelper.TwoPi;

            UpdateParticles();

            if (CanInteract() && IconHitBox.Contains(MousePosition.ToPoint())) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    OnIconClicked();
                }
            }
        }

        private void UpdateParticles() {
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);

            _bubbleSpawnTimer++;
            if (_bubbleSpawnTimer >= 15 && _bubbles.Count < 15) {
                _bubbleSpawnTimer = 0;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPos = iconCenter + angle.ToRotationVector2() * IconSize * 0.35f;
                _bubbles.Add(new BubbleParticle(spawnPos));
            }

            for (int i = _bubbles.Count - 1; i >= 0; i--) {
                if (_bubbles[i].Update()) {
                    _bubbles.RemoveAt(i);
                }
            }

            if (_unlocked) {
                _rippleSpawnTimer++;
                if (_rippleSpawnTimer >= 80 && _ripples.Count < 3) {
                    _rippleSpawnTimer = 0;
                    _ripples.Add(new WaterRipple(iconCenter));
                }

                for (int i = _ripples.Count - 1; i >= 0; i--) {
                    if (_ripples[i].Update()) {
                        _ripples.RemoveAt(i);
                    }
                }
            }
        }

        private void OnIconClicked() {
            if (!_unlocked) {
                SoundEngine.PlaySound(SoundID.Unlock);
            }
            else {
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }
        #endregion

        #region 绘制
        public override void Draw(SpriteBatch spriteBatch) {
            if (_iconAlpha <= 0.01f || !IsResourceLoaded()) {
                return;
            }

            DrawIconFrame(spriteBatch);
        }

        private void DrawIconFrame(SpriteBatch spriteBatch) {
            if (!IsResourceLoaded()) {
                return;
            }

            Texture2D iconTex = ADVAsset.HelenADV;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);
            bool hoverIcon = IconHitBox.Contains(MousePosition.ToPoint()) && CanInteract();

            //绘制水波纹
            foreach (var ripple in _ripples) {
                ripple.Draw(spriteBatch, _iconAlpha * _unlockProgress);
            }

            //绘制气泡粒子
            foreach (var bubble in _bubbles) {
                bubble.Draw(spriteBatch, _iconAlpha * 0.8f);
            }

            Rectangle bgRect = new Rectangle(
                (int)IconPosition.X - 5,
                (int)IconPosition.Y - 5,
                (int)IconSize + 10,
                (int)IconSize + 10
            );

            Color bgColor = new Color(5, 20, 28) * (_iconAlpha * 0.9f);
            DrawBaseBackground(spriteBatch, bgRect, _iconAlpha, hoverIcon, bgColor);

            //头像绘制
            float iconScale = IconSize / Math.Max(iconTex.Width, iconTex.Height);
            if (hoverIcon) {
                iconScale *= 1.08f + (float)Math.Sin(_waveTimer * 1.8f) * 0.04f;
            }

            Vector2 iconDrawPos = iconCenter;

            Color iconColor = _unlocked ?
                Color.White * _iconAlpha :
                new Color(30, 50, 70) * (_iconAlpha * 0.4f);

            spriteBatch.Draw(iconTex, iconDrawPos, null, iconColor,
                0f, iconTex.Size() / 2, iconScale, SpriteEffects.None, 0f);

            //深海边框
            DrawOceanFrame(spriteBatch, bgRect, _iconAlpha, (float)Math.Sin(_pulseTimer * 1.5f) * 0.5f + 0.5f);

            //解锁动画特效
            if (_unlockProgress > 0f && _unlockProgress < 1f) {
                DrawUnlockEffect(spriteBatch, iconCenter, _unlockProgress);
            }
        }

        private void DrawOceanFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(30, 140, 190), new Color(90, 210, 255), pulse) * (alpha * 0.8f);

            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);

            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerGlow = new Color(120, 220, 255) * (alpha * 0.18f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.65f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.6f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.6f);
        }

        private void DrawCornerStar(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 5f;
            Color starColor = new Color(150, 230, 255) * alpha;

            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), starColor, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), starColor * 0.8f, MathHelper.PiOver2,
                new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }

        private void DrawUnlockEffect(SpriteBatch sb, Vector2 center, float progress) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            int rings = 3;
            for (int i = 0; i < rings; i++) {
                float t = (progress + i * 0.3f) % 1f;
                float radius = 20f + t * 60f;
                float alpha = (float)Math.Sin(t * Math.PI) * 0.6f;

                Color ringColor = new Color(140, 230, 255) * alpha;

                int segments = 24;
                for (int s = 0; s < segments; s++) {
                    float angle1 = MathHelper.TwoPi * s / segments;
                    float angle2 = MathHelper.TwoPi * (s + 1) / segments;

                    Vector2 p1 = center + angle1.ToRotationVector2() * radius;
                    Vector2 p2 = center + angle2.ToRotationVector2() * radius;

                    Vector2 diff = p2 - p1;
                    float length = diff.Length();
                    float rotation = diff.ToRotation();

                    sb.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), ringColor, rotation,
                        Vector2.Zero, new Vector2(length, 2f), SpriteEffects.None, 0f);
                }
            }
        }
        #endregion
    }
}
