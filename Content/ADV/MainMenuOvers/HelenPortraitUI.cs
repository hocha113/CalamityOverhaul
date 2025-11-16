using CalamityOverhaul.Content.UIs.MainMenuOverUIs;
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
    /// 比目鱼小姐立绘UI，深海风格，主菜单显示
    /// </summary>
    internal class HelenPortraitUI : UIHandle, ICWRLoader
    {
        #region 数据字段
        public static HelenPortraitUI Instance => UIHandleLoader.GetUIHandleOfType<HelenPortraitUI>();

        private bool _unlocked = false; //是否已解锁
        private float _iconAlpha = 0f; //头像框透明度
        private float _unlockProgress = 0f; //解锁进度动画

        //动画计时器
        private float _waveTimer = 0f;
        private float _bubbleTimer = 0f;
        private float _pulseTimer = 0f;

        //粒子系统
        private readonly List<BubbleParticle> _bubbles = [];
        private int _bubbleSpawnTimer = 0;
        private readonly List<WaterRipple> _ripples = [];
        private int _rippleSpawnTimer = 0;

        //UI位置和尺寸
        private const float IconSize = 80f;
        private const float IconBottomMargin = 46f;
        private const float IconSpacing = 95f; //与女巫头像的间距

        //立绘偏移（用于拖动功能的预留）
        private Vector2 _portraitOffset = Vector2.Zero;

        //自动保存计时器
        private int _autoSaveTimer = 0;
        private const int AutoSaveInterval = 300; // 5秒自动保存一次
        private bool _needsSave = false;

        private Vector2 IconPosition => new Vector2(
            Main.screenWidth / 2 - IconSize / 2 + IconSpacing / 2,
            Main.screenHeight - IconSize - IconBottomMargin
        ) + _portraitOffset;

        private Rectangle IconHitBox => new Rectangle(
            (int)IconPosition.X,
            (int)IconPosition.Y,
            (int)IconSize,
            (int)IconSize
        );

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;

        //确保资源已加载
        public override bool Active => MenuSave.IsPortraitUnlocked() && CWRLoad.OnLoadContentBool && Main.gameMenu && IsResourceLoaded();

        //检查资源是否已正确加载
        private static bool IsResourceLoaded() {
            return ADVAsset.HelenADV != null && !ADVAsset.HelenADV.IsDisposed;
        }

        /// <summary>
        /// 检查玩家是否在主菜单（menuMode == 0），而不是在子菜单中
        /// </summary>
        private static bool IsInMainMenu() {
            return Main.menuMode == 0;
        }

        /// <summary>
        /// 检查图标是否应该可见（仅在主菜单显示，进入子菜单时隐藏）
        /// </summary>
        private static bool ShouldShowIcon() {
            return IsInMainMenu();
        }

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

                //绘制圆环
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
        void ICWRLoader.SetupData() { }

        public override void SetStaticDefaults() {
            _iconAlpha = 0f;
            _unlocked = false;
            _unlockProgress = 0f;
            _waveTimer = 0f;
            _bubbleTimer = 0f;
            _pulseTimer = 0f;
            _bubbleSpawnTimer = 0;
            _rippleSpawnTimer = 0;
            _portraitOffset = Vector2.Zero;
            _autoSaveTimer = 0;
            _needsSave = false;

            //加载保存的状态
            LoadSavedState();
        }

        public override void UnLoad() {
            // 卸载前保存当前状态
            SaveCurrentState();

            _bubbles?.Clear();
            _ripples?.Clear();

            // 重置所有状态
            _iconAlpha = 0f;
            _unlocked = false;
            _unlockProgress = 0f;
            _waveTimer = 0f;
            _bubbleTimer = 0f;
            _pulseTimer = 0f;
        }

        /// <summary>
        /// 从MenuSave加载保存的UI状态
        /// </summary>
        public void LoadSavedState() {
            // 加载立绘位置
            _portraitOffset = MenuSave.Helen_PortraitOffset;
            _needsSave = false;
        }

        /// <summary>
        /// 保存当前UI状态到MenuSave
        /// </summary>
        public void SaveCurrentState() {
            if (!_needsSave) {
                return;
            }

            MenuSave.SaveHelenPortraitState(_portraitOffset);
            _needsSave = false;
        }
        #endregion

        #region 解锁管理
        /// <summary>
        /// 解锁比目鱼小姐头像
        /// </summary>
        public void Unlock() {
            if (!_unlocked) {
                _unlocked = true;
                SoundEngine.PlaySound(SoundID.Item4);
            }
        }

        /// <summary>
        /// 锁定比目鱼小姐头像（用于测试）
        /// </summary>
        public void Lock() {
            _unlocked = false;
            _unlockProgress = 0f;
        }
        #endregion

        #region 更新逻辑
        public override void Update() {
            if (!Main.gameMenu || !IsResourceLoaded()) {
                _iconAlpha = 0f;
                return;
            }

            // 自动保存逻辑
            if (_needsSave) {
                _autoSaveTimer++;
                if (_autoSaveTimer >= AutoSaveInterval) {
                    SaveCurrentState();
                    _autoSaveTimer = 0;
                }
            }

            // 进入子菜单时快速淡出图标
            if (!ShouldShowIcon()) {
                if (_iconAlpha > 0f) {
                    _iconAlpha -= 0.1f; //快速淡出
                    if (_iconAlpha < 0f) _iconAlpha = 0f;
                }
                return; //在子菜单中不更新其他逻辑
            }

            //渐入效果
            if (_iconAlpha < 1f) {
                _iconAlpha += 0.02f;
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
            _pulseTimer += 0.02f;

            if (_waveTimer > MathHelper.TwoPi) _waveTimer -= MathHelper.TwoPi;
            if (_bubbleTimer > MathHelper.TwoPi) _bubbleTimer -= MathHelper.TwoPi;
            if (_pulseTimer > MathHelper.TwoPi) _pulseTimer -= MathHelper.TwoPi;

            //更新粒子
            UpdateParticles();

            //检测点击
            if (CanInteract() && IconHitBox.Contains(MousePosition.ToPoint())) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    OnIconClicked();
                }
            }
        }

        private void UpdateParticles() {
            Vector2 iconCenter = IconPosition + new Vector2(IconSize / 2);

            //生成气泡粒子
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

            //生成水波纹
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
                //未解锁时播放锁定音效
                SoundEngine.PlaySound(SoundID.Unlock);
            }
            else {
                //已解锁时的行为（暂时留空，后续可以添加显示立绘等功能）
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }

        private static bool CanInteract() {
            // 必须在主菜单才能交互
            return IsInMainMenu() && !FeedbackUI.Instance.OnActive() && !AcknowledgmentUI.OnActive();
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
            // 双重检查资源有效性
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

            //背景框
            Rectangle bgRect = new Rectangle(
                (int)IconPosition.X - 5,
                (int)IconPosition.Y - 5,
                (int)IconSize + 10,
                (int)IconSize + 10
            );

            Color bgColor = new Color(5, 20, 28) * (_iconAlpha * 0.9f);

            //悬停光效（深海蓝色）
            if (hoverIcon) {
                Color hoverGlow = new Color(70, 180, 230) * (_iconAlpha * 0.35f);
                for (int i = 0; i < 6; i++) {
                    spriteBatch.Draw(pixel, bgRect.Location.ToVector2(),
                        new Rectangle(0, 0, bgRect.Width, bgRect.Height), hoverGlow);
                }
            }

            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), bgColor);

            //水波脉冲背景
            float pulse = (float)Math.Sin(_pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color pulseColor = new Color(30, 120, 150) * (_iconAlpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), pulseColor);

            //头像绘制
            float iconScale = IconSize / Math.Max(iconTex.Width, iconTex.Height);
            if (hoverIcon) {
                iconScale *= 1.08f + (float)Math.Sin(_waveTimer * 1.8f) * 0.04f;
            }

            Vector2 iconDrawPos = iconCenter;

            //未解锁时的暗化效果
            Color iconColor;
            if (_unlocked) {
                iconColor = Color.White * _iconAlpha;
            }
            else {
                //暗化并添加蓝色调
                iconColor = new Color(30, 50, 70) * (_iconAlpha * 0.4f);
            }

            spriteBatch.Draw(iconTex, iconDrawPos, null, iconColor,
                0f, iconTex.Size() / 2, iconScale, SpriteEffects.None, 0f);

            //深海边框
            DrawOceanFrame(spriteBatch, bgRect, _iconAlpha, pulse);

            //解锁动画特效
            if (_unlockProgress > 0f && _unlockProgress < 1f) {
                DrawUnlockEffect(spriteBatch, iconCenter, _unlockProgress);
            }
        }

        private void DrawOceanFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(30, 140, 190), new Color(90, 210, 255), pulse) * (alpha * 0.8f);

            //外框
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerGlow = new Color(120, 220, 255) * (alpha * 0.18f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.65f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            //角落星标
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

            //从中心向外扩散的光环
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
