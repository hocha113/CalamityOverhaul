using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    internal class OverhaulSettingsUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static OverhaulSettingsUI Instance => UIHandleLoader.GetUIHandleOfType<OverhaulSettingsUI>();

        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText CloseText { get; private set; }
        public static LocalizedText ContentSettingsText { get; private set; }
        public static LocalizedText ReloadHintText { get; private set; }
        public static LocalizedText WeaponOverrideText { get; private set; }
        public static LocalizedText EnableAllText { get; private set; }
        public static LocalizedText DisableAllText { get; private set; }
        public static LocalizedText WorldGenSettingsText { get; private set; }
        public static LocalizedText WorldGenFooterHintText { get; private set; }

        //UI控制
        internal bool _active;
        private float _sengs;
        private float contentFade;
        private bool closing;
        private float hideProgress;

        //动画时间轴
        private float globalTime;
        private float panelSlideOffset;
        private float panelScaleAnim;
        private float breatheAnim;
        private float shimmerPhase;

        //关闭按钮动画
        private float closeHoverAnim;
        private float closePressAnim;

        //粒子系统
        private readonly List<SettingsParticle> particles = [];
        private float particleSpawnTimer;

        //面板尺寸
        private static float PanelWidth => Main.screenWidth * 0.8f;
        private static float PanelHeight => Main.screenHeight * 0.8f;
        //布局常量
        private const float Padding = 24f;
        private const float ButtonHeight = 42f;
        private const float ButtonWidth = 130f;
        private const float CornerRadius = 10f;
        private const float TitleBarHeight = 50f;
        private const float CategoryHeight = 40f;
        private const float ToggleRowHeight = 34f;
        private const float ToggleBoxSize = 22f;

        //按钮
        private Rectangle closeButtonRect;
        private bool hoveringClose;

        //分类列表
        private readonly List<SettingsCategory> categories = [];
        private bool categoriesInitialized;
        private Rectangle scrollAreaRect;

        //当前展开的分类索引(-1表示无)
        private int expandedCategoryIndex = -1;

        //悬浮提示
        private string hoverTooltip;
        private Vector2 hoverTooltipPos;

        //粒子结构
        private struct SettingsParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public Color BaseColor;
        }

        //设置项开关
        internal class SettingToggle
        {
            public string ConfigPropertyName;
            public Func<bool> Getter;
            public Action<bool> Setter;
            public bool RequiresReload;
            public float HoverAnim;
            public float ToggleAnim;
            public Rectangle HitBox;
            public bool Hovering;
            /// <summary>
            /// 可选：关联的物品类型ID，用于绘制物品图标
            /// </summary>
            public int ItemType;
        }

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool;

        public static bool OnActive() {
            if (Instance == null) {
                return false;
            }
            return Instance._active || Instance._sengs > 0;
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "大修设置");
            CloseText = this.GetLocalization(nameof(CloseText), () => "关闭");
            ContentSettingsText = this.GetLocalization(nameof(ContentSettingsText), () => "内容设置");
            ReloadHintText = this.GetLocalization(nameof(ReloadHintText), () => "[c/FF6666:* 带此标记的选项需要重新加载模组才能生效]");
            WeaponOverrideText = this.GetLocalization(nameof(WeaponOverrideText), () => "武器修改管理");
            EnableAllText = this.GetLocalization(nameof(EnableAllText), () => "启用全部");
            DisableAllText = this.GetLocalization(nameof(DisableAllText), () => "禁用全部");
            WorldGenSettingsText = this.GetLocalization(nameof(WorldGenSettingsText), () => "世界生成设置");
            WorldGenFooterHintText = this.GetLocalization(nameof(WorldGenFooterHintText), () => "这些设置将在下次创建世界时生效");

            ContentSettingsCategory.LoadReflection();
        }

        public override void UnLoad() {
            _sengs = 0;
            _active = false;
            ContentSettingsCategory.UnloadReflection();
        }

        private void ResetAnimations() {
            _sengs = 0f;
            contentFade = 0f;
            panelSlideOffset = 60f;
            panelScaleAnim = 0.85f;
            closeHoverAnim = 0f;
            closePressAnim = 0f;
            closing = false;
            hideProgress = 0f;
            particles.Clear();
            hoverTooltip = null;
            foreach (var cat in categories) {
                cat.ScrollOffset = 0f;
                cat.ScrollTarget = 0f;
            }
        }

        private void InitializeCategories() {
            if (categoriesInitialized) return;
            categoriesInitialized = true;
            categories.Clear();

            var contentCat = new ContentSettingsCategory();
            contentCat.EnsureInitialized();
            categories.Add(contentCat);

            var weaponCat = new WeaponOverrideCategory();
            weaponCat.EnsureInitialized();
            categories.Add(weaponCat);

            var worldGenCat = new WorldGenSettingsCategory();
            worldGenCat.EnsureInitialized();
            categories.Add(worldGenCat);
        }

        public override void Update() {
            globalTime += 0.016f;

            //淡入淡出
            if (_active && !closing) {
                if (_sengs < 1f) {
                    _sengs += 0.1f;
                }
                if (_sengs > 1f) {
                    _sengs = 1f;
                }
            }
            else if (!_active || closing) {
                if (closing) {
                    hideProgress += 0.06f;
                    if (hideProgress >= 1f) {
                        hideProgress = 1f;
                        closing = false;
                        _active = false;
                        ResetAnimations();
                        return;
                    }
                    _sengs = 1f - EaseOutQuad(hideProgress);
                }
                else {
                    if (_sengs > 0f) {
                        _sengs -= 0.1f;
                    }
                    if (_sengs <= 0f) {
                        _sengs = 0f;
                        return;
                    }
                }
            }

            if (_sengs <= 0.001f) {
                particles.Clear();
                return;
            }

            InitializeCategories();

            //面板滑入动画
            float targetSlide = _active && !closing ? 0f : 60f;
            panelSlideOffset += (targetSlide - panelSlideOffset) * 0.15f;

            //面板缩放动画
            float targetScale = _active && !closing ? 1f : 0.85f;
            panelScaleAnim += (targetScale - panelScaleAnim) * 0.1f;

            //内容淡入
            if (_sengs > 0.5f && !closing) {
                contentFade += (1f - contentFade) * 0.12f;
            }
            else {
                contentFade *= 0.85f;
            }
            contentFade = Math.Clamp(contentFade, 0f, 1f);

            //呼吸动画
            breatheAnim = MathF.Sin(globalTime * 1.5f) * 0.5f + 0.5f;
            shimmerPhase = globalTime * 2f;

            //按钮动画
            float hoverSpeed = 0.15f;
            closeHoverAnim += ((hoveringClose ? 1f : 0f) - closeHoverAnim) * hoverSpeed;
            closePressAnim *= 0.85f;

            //更新粒子
            UpdateParticles();

            //生成新粒子
            if (_sengs > 0.3f && !closing) {
                particleSpawnTimer += 1f;
                if (particleSpawnTimer > 4f) {
                    SpawnParticle();
                    particleSpawnTimer = 0f;
                }
            }

            //计算面板位置(居中)
            float scaledWidth = PanelWidth * panelScaleAnim;
            float scaledHeight = PanelHeight * panelScaleAnim;
            Vector2 panelCenter = new(Main.screenWidth / 2f, Main.screenHeight / 2f + panelSlideOffset);
            DrawPosition = panelCenter - new Vector2(scaledWidth, scaledHeight) / 2f;
            Size = new Vector2(scaledWidth, scaledHeight);
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)scaledWidth, (int)scaledHeight);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            //悬停检测
            hoveringClose = closeButtonRect.Contains(MouseHitBox) && contentFade > 0.5f;

            //清除悬浮提示
            hoverTooltip = null;

            //判定鼠标是否在某个展开分类的内容区域内(用于遮挡层级)
            int expandedContentOwner = -1;
            for (int i = 0; i < categories.Count; i++) {
                var cat = categories[i];
                if (cat.ExpandAnim > 0.5f && cat.ExpandClipRect.Width > 0
                    && cat.ExpandClipRect.Contains(MouseHitBox)) {
                    expandedContentOwner = i;
                    break;
                }
            }

            //更新所有分类
            bool anyExpanded = false;
            for (int i = 0; i < categories.Count; i++) {
                var cat = categories[i];
                //分类按钮悬停：如果鼠标被另一个分类的展开内容遮挡，则不判定悬停
                bool blocked = expandedContentOwner >= 0 && expandedContentOwner != i;
                cat.HoveringCategory = !blocked && cat.CategoryHitBox.Contains(MouseHitBox) && contentFade > 0.5f;
                cat.Update(contentFade, hoverInMainPage, MouseHitBox, MousePosition, scrollAreaRect);
                if (cat.Expanded) anyExpanded = true;
                //收集悬浮提示
                if (cat.HoverTooltip != null && hoverTooltip == null) {
                    hoverTooltip = cat.HoverTooltip;
                    hoverTooltipPos = cat.HoverTooltipPos;
                }
            }

            //确保同时只有一个分类展开
            for (int i = 0; i < categories.Count; i++) {
                if (categories[i].Expanded && i != expandedCategoryIndex) {
                    //新的分类展开了，折叠旧的
                    if (expandedCategoryIndex >= 0 && expandedCategoryIndex < categories.Count) {
                        categories[expandedCategoryIndex].Expanded = false;
                        categories[expandedCategoryIndex].ScrollTarget = 0f;
                    }
                    expandedCategoryIndex = i;
                    break;
                }
            }
            if (!anyExpanded) expandedCategoryIndex = -1;

            //点击处理
            if (keyLeftPressState == KeyPressState.Pressed && contentFade > 0.8f) {
                if (hoveringClose) {
                    closePressAnim = 1f;
                    OnClose();
                }
                else {
                    foreach (var cat in categories) {
                        if (cat.HandleClick(MouseHitBox)) break;
                    }
                }
            }

            //ESC关闭
            if (OnActive()) {
                KeyboardState currentKeyState = Main.keyState;
                KeyboardState previousKeyState = Main.oldKeyState;
                if (currentKeyState.IsKeyDown(Keys.Escape) && !previousKeyState.IsKeyDown(Keys.Escape)) {
                    OnClose();
                }
            }
        }

        private void OnClose() {
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            Main.menuMode = 0;
            if (!closing) {
                closing = true;
                hideProgress = 0f;
            }
        }

        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                var p = particles[i];
                p.Life -= 0.016f;
                p.Position += p.Velocity;
                p.Velocity *= 0.98f;
                p.Velocity.Y -= 0.015f;
                p.Rotation += p.RotationSpeed;
                particles[i] = p;

                if (p.Life <= 0f) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void SpawnParticle() {
            if (particles.Count > 25) return;

            var p = new SettingsParticle {
                Position = new Vector2(
                    DrawPosition.X + Main.rand.NextFloat(Size.X),
                    DrawPosition.Y + Size.Y
                ),
                Velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.2f, -0.4f)),
                Life = Main.rand.NextFloat(1.5f, 3f),
                MaxLife = 0f,
                Size = Main.rand.NextFloat(1.5f, 4f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.04f, 0.04f),
                BaseColor = Color.Lerp(new Color(180, 40, 40), new Color(120, 20, 20), Main.rand.NextFloat())
            };
            p.MaxLife = p.Life;
            particles.Add(p);
        }

        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        public override void Draw(SpriteBatch spriteBatch) {
            if (_sengs <= 0.001f) return;

            if (CWRAsset.Placeholder_White == null || CWRAsset.Placeholder_White.IsDisposed) {
                _active = false;
                return;
            }

            float alpha = _sengs;

            //绘制粒子(在面板后面)
            DrawParticles(spriteBatch, alpha);

            //绘制主面板
            DrawPanel(spriteBatch, alpha);

            //绘制内容
            if (contentFade > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFade);
            }

            //绘制悬浮提示(最上层)
            if (hoverTooltip != null && contentFade > 0.5f) {
                DrawTooltip(spriteBatch, hoverTooltip, hoverTooltipPos, alpha * contentFade);
            }
        }

        private void DrawParticles(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            foreach (var p in particles) {
                float lifeRatio = p.Life / p.MaxLife;
                float particleAlpha = lifeRatio * alpha;
                float size = p.Size * (0.5f + lifeRatio * 0.5f);

                Color color = p.BaseColor * particleAlpha;
                spriteBatch.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), color, p.Rotation,
                    new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);

                Color glowColor = color * 0.25f;
                spriteBatch.Draw(pixel, p.Position, new Rectangle(0, 0, 1, 1), glowColor, p.Rotation,
                    new Vector2(0.5f), new Vector2(size * 2f), SpriteEffects.None, 0f);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Rectangle panelRect = UIHitBox;

            //多层阴影
            for (int i = 4; i >= 1; i--) {
                Rectangle shadowRect = panelRect;
                shadowRect.Offset(i * 2, i * 3);
                float shadowAlpha = alpha * 0.15f * (5 - i) / 4f;
                DrawRoundedRect(spriteBatch, shadowRect, Color.Black * shadowAlpha, CornerRadius + i);
            }

            //背景渐变(深红色严肃质感)
            Color bgTop = new Color(30, 12, 12);
            Color bgBottom = new Color(50, 18, 18);
            DrawGradientRoundedRect(spriteBatch, panelRect, bgTop * (alpha * 0.97f), bgBottom * (alpha * 0.97f), CornerRadius);

            //内发光效果
            float innerGlowIntensity = 0.12f + breatheAnim * 0.08f;
            DrawInnerGlow(spriteBatch, panelRect, new Color(160, 50, 50) * (alpha * innerGlowIntensity), CornerRadius, 18);

            //流光边框
            DrawAnimatedBorder(spriteBatch, panelRect, alpha);

            //顶部标题栏分割
            int titleBarBottom = panelRect.Y + (int)(TitleBarHeight * panelScaleAnim);
            float highlightAlpha = 0.5f + breatheAnim * 0.2f;
            Rectangle highlightBar = new(panelRect.X + 15, titleBarBottom, panelRect.Width - 30, 2);
            DrawHorizontalGradient(spriteBatch, highlightBar,
                Color.Transparent, new Color(200, 60, 60) * (alpha * highlightAlpha), Color.Transparent);

            //角落装饰
            DrawCornerOrnaments(spriteBatch, panelRect, alpha);
        }

        private void DrawAnimatedBorder(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Color baseColor = new Color(100, 35, 35) * (alpha * 0.8f);
            DrawRoundedRectBorder(spriteBatch, rect, baseColor, CornerRadius, 2);

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float shimmerPos = shimmerPhase % 4f / 4f;

            for (int i = 0; i < 3; i++) {
                float offset = (shimmerPos + i * 0.33f) % 1f;
                Vector2 pos = GetPointOnRectPerimeter(rect, offset);
                float intensity = MathF.Sin(offset * MathHelper.Pi) * 0.7f;
                Color shimmerColor = new Color(220, 80, 80) * (alpha * intensity);

                spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), shimmerColor,
                    0f, new Vector2(0.5f), new Vector2(7f, 3f), SpriteEffects.None, 0f);

                for (int j = 1; j <= 4; j++) {
                    float trailOffset = (offset - j * 0.01f + 1f) % 1f;
                    Vector2 trailPos = GetPointOnRectPerimeter(rect, trailOffset);
                    float trailIntensity = intensity * (1f - j / 5f);
                    spriteBatch.Draw(pixel, trailPos, new Rectangle(0, 0, 1, 1),
                        shimmerColor * trailIntensity * 0.5f, 0f, new Vector2(0.5f),
                        new Vector2(5f - j, 2f - j * 0.3f), SpriteEffects.None, 0f);
                }
            }
        }

        private static Vector2 GetPointOnRectPerimeter(Rectangle rect, float t) {
            float perimeter = (rect.Width + rect.Height) * 2f;
            float dist = t * perimeter;

            if (dist < rect.Width) {
                return new Vector2(rect.X + dist, rect.Y);
            }
            dist -= rect.Width;
            if (dist < rect.Height) {
                return new Vector2(rect.Right, rect.Y + dist);
            }
            dist -= rect.Height;
            if (dist < rect.Width) {
                return new Vector2(rect.Right - dist, rect.Bottom);
            }
            dist -= rect.Width;
            return new Vector2(rect.X, rect.Bottom - dist);
        }

        private void DrawCornerOrnaments(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float ornamentAlpha = alpha * (0.5f + breatheAnim * 0.3f);
            Color ornamentColor = new Color(200, 70, 70) * ornamentAlpha;

            Vector2[] corners = [
                new(rect.X + 8, rect.Y + 8),
                new(rect.Right - 8, rect.Y + 8),
                new(rect.X + 8, rect.Bottom - 8),
                new(rect.Right - 8, rect.Bottom - 8)
            ];

            for (int i = 0; i < 4; i++) {
                spriteBatch.Draw(pixel, corners[i], new Rectangle(0, 0, 1, 1), ornamentColor,
                    MathHelper.PiOver4, new Vector2(0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);

                for (int j = 0; j < 4; j++) {
                    float rayRot = j * MathHelper.PiOver2 + globalTime * 0.3f;
                    Vector2 rayDir = rayRot.ToRotationVector2();
                    spriteBatch.Draw(pixel, corners[i] + rayDir * 3f, new Rectangle(0, 0, 1, 1),
                        ornamentColor * 0.4f, rayRot, new Vector2(0f, 0.5f), new Vector2(7f, 1.2f), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float scale = panelScaleAnim;

            //标题(居中)
            string title = TitleText.Value;
            Vector2 titleMeasure = FontAssets.MouseText.Value.MeasureString(title) * scale * 1.1f;
            Vector2 titlePos = new Vector2(
                DrawPosition.X + (Size.X - titleMeasure.X) / 2f,
                DrawPosition.Y + Padding * scale * 0.6f);

            float titleGlow = 0.4f + breatheAnim * 0.4f;
            Color titleGlowColor = new Color(200, 60, 60) * (alpha * titleGlow * 0.35f);
            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + globalTime * 0.4f;
                Vector2 offset = angle.ToRotationVector2() * (2.5f + breatheAnim * 1.5f);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlowColor, scale * 1.1f);
            }

            Color titleColor = Color.Lerp(new Color(240, 200, 200), new Color(220, 100, 100), breatheAnim * 0.3f);
            Utils.DrawBorderString(spriteBatch, title, titlePos, titleColor * alpha, scale * 1.1f);

            //内容区域
            float contentTop = DrawPosition.Y + TitleBarHeight * scale + 10f * scale;
            float contentLeft = DrawPosition.X + Padding * scale;
            float contentWidth = Size.X - Padding * scale * 2;
            float contentBottom = DrawPosition.Y + Size.Y - Padding * scale - ButtonHeight * scale - 15f * scale;
            float contentHeight = contentBottom - contentTop;

            scrollAreaRect = new Rectangle((int)contentLeft, (int)contentTop, (int)contentWidth, (int)contentHeight);

            //绘制所有分类
            float catY = contentTop;
            foreach (var cat in categories) {
                Rectangle catRect = new((int)contentLeft, (int)catY, (int)contentWidth, (int)(CategoryHeight * scale));
                cat.CategoryHitBox = catRect;
                DrawCategoryButton(spriteBatch, catRect, cat.Title, cat.Expanded,
                    cat.CategoryHoverAnim, alpha, scale, cat.ExpandAnim);

                //未展开时清除裁剪区域，避免残留的ExpandClipRect影响层级判定
                if (cat.ExpandAnim <= 0.01f) {
                    cat.ExpandClipRect = Rectangle.Empty;
                }

                //展开的设置项列表
                if (cat.ExpandAnim > 0.01f) {
                    float easedExpand = EaseOutQuad(cat.ExpandAnim);

                    //展开高度由动画驱动，统一控制裁剪和布局
                    float expandedH = cat.GetExpandedHeight(scale);
                    float actionBarHeight = cat.ActionButtons.Count > 0 ? 36f * scale : 0f;

                    //整个展开区域的裁剪范围(包含操作按钮和列表)
                    float expandAreaTop = catY + CategoryHeight * scale + 4f * scale;
                    float expandAreaHeight = Math.Min(expandedH, Math.Max(0f, contentBottom - expandAreaTop));

                    //展开区域的容器背景(在裁剪前绘制，但尺寸受动画控制)
                    float containerAlpha = alpha * easedExpand;
                    int containerPad = (int)(4f * scale);
                    Rectangle expandClipRect = new(
                        (int)contentLeft, (int)expandAreaTop,
                        (int)contentWidth, (int)expandAreaHeight);
                    cat.ExpandClipRect = expandClipRect;
                    Rectangle containerRect = new(
                        expandClipRect.X - containerPad,
                        expandClipRect.Y - containerPad,
                        expandClipRect.Width + containerPad * 2,
                        expandClipRect.Height + containerPad * 2);

                    DrawRoundedRect(spriteBatch, containerRect,
                        new Color(22, 9, 9) * (containerAlpha * 0.6f), 5f);

                    Color containerBorderColor = Color.Lerp(
                        new Color(90, 38, 38), new Color(120, 50, 50), breatheAnim * 0.3f);
                    DrawRoundedRectBorder(spriteBatch, containerRect,
                        containerBorderColor * (containerAlpha * 0.65f), 5f, 1);

                    DrawInnerGlow(spriteBatch, containerRect,
                        new Color(140, 45, 45) * (containerAlpha * 0.06f), 5f, 4);

                    //使用RasterizerState对整个展开区域进行裁剪
                    spriteBatch.End();
                    Rectangle prevScissor = spriteBatch.GraphicsDevice.ScissorRectangle;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        DepthStencilState.None, new RasterizerState { ScissorTestEnable = true }, null, Main.UIScaleMatrix);
                    spriteBatch.GraphicsDevice.ScissorRectangle = VaultUtils.GetClippingRectangle(spriteBatch, expandClipRect);

                    //绘制操作按钮栏(在裁剪区域内)
                    if (cat.ActionButtons.Count > 0) {
                        float barY = expandAreaTop;
                        float barAlpha = alpha * easedExpand;
                        float btnWidth = 100f * scale;
                        float btnHeight = 28f * scale;
                        float gap = 8f * scale;
                        float totalBtnWidth = cat.ActionButtons.Count * btnWidth + (cat.ActionButtons.Count - 1) * gap;
                        float startX = contentLeft + (contentWidth - totalBtnWidth) / 2f;

                        for (int bi = 0; bi < cat.ActionButtons.Count; bi++) {
                            var btn = cat.ActionButtons[bi];
                            float bx = startX + bi * (btnWidth + gap);
                            Rectangle btnRect = new((int)bx, (int)(barY + 2f * scale), (int)btnWidth, (int)btnHeight);
                            btn.HitBox = btnRect;
                            btn.Hovering = btnRect.Contains(MouseHitBox) && contentFade > 0.5f && cat.ExpandAnim > 0.5f;
                            DrawSmallButton(spriteBatch, btnRect, btn.Label, btn.HoverAnim, barAlpha, scale);
                        }
                    }

                    //列表区域
                    float listTop = expandAreaTop + actionBarHeight + 2f * scale;
                    float listHeight = Math.Max(0f, expandAreaTop + expandAreaHeight - listTop);

                    //获取可见的开关列表
                    var visibleToggles = cat.GetVisibleToggles();

                    //计算总内容高度
                    float totalContentH = visibleToggles.Count * ToggleRowHeight * scale;
                    if (cat.ShowFooter) {
                        totalContentH += 30f * scale;
                    }
                    cat.MaxScroll = Math.Max(0f, totalContentH - listHeight);

                    float itemAlpha = alpha * easedExpand;
                    //展开/收起时内容向上滑入/滑出
                    float slideOffset = (1f - easedExpand) * 20f * scale;
                    float yPos = listTop - cat.ScrollOffset + slideOffset;

                    for (int i = 0; i < visibleToggles.Count; i++) {
                        var toggle = visibleToggles[i];
                        float rowY = yPos + i * ToggleRowHeight * scale;

                        Rectangle rowRect = new((int)contentLeft, (int)rowY, (int)contentWidth, (int)(ToggleRowHeight * scale));
                        toggle.HitBox = rowRect;

                        if (rowRect.Bottom >= listTop && rowRect.Y <= listTop + listHeight) {
                            DrawToggleRow(spriteBatch, toggle, rowRect, itemAlpha, scale, cat);
                        }
                    }

                    //底部提示
                    if (cat.ShowFooter && !string.IsNullOrEmpty(cat.FooterHint)) {
                        float hintY = yPos + visibleToggles.Count * ToggleRowHeight * scale + 8f * scale;
                        if (hintY < listTop + listHeight) {
                            Utils.DrawBorderString(spriteBatch, cat.FooterHint,
                                new Vector2(contentLeft + 10f * scale, hintY),
                                new Color(255, 100, 100) * (itemAlpha * 0.8f), 0.7f * scale);
                        }
                    }

                    spriteBatch.End();
                    spriteBatch.GraphicsDevice.ScissorRectangle = prevScissor;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        DepthStencilState.None, new RasterizerState { ScissorTestEnable = false }, null, Main.UIScaleMatrix);

                    //滚动条绘制在裁剪外(始终可见)
                    Rectangle scrollClipRect = new((int)contentLeft, (int)listTop, (int)contentWidth, (int)listHeight);
                    if (cat.MaxScroll > 0f) {
                        DrawScrollBar(spriteBatch, scrollClipRect, alpha * cat.ExpandAnim, cat);
                    }
                }

                //下一个分类的Y位置
                catY += CategoryHeight * scale + 4f * scale;
                catY += cat.GetExpandedHeight(scale);
            }

            //关闭按钮
            float buttonY = DrawPosition.Y + Size.Y - Padding * scale - ButtonHeight * scale;
            float buttonCenterX = DrawPosition.X + Size.X / 2f;
            float scaledButtonWidth = ButtonWidth * scale;
            float scaledButtonHeight = ButtonHeight * scale;

            closeButtonRect = new Rectangle(
                (int)(buttonCenterX - scaledButtonWidth / 2f),
                (int)buttonY,
                (int)scaledButtonWidth,
                (int)scaledButtonHeight
            );

            DrawButton(spriteBatch, closeButtonRect, CloseText.Value, closeHoverAnim, closePressAnim, alpha, scale);
        }

        private void DrawCategoryButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            bool expanded, float hoverAnim, float alpha, float scale, float expandAnim = 0f) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //背景
            Color bgColor = Color.Lerp(new Color(45, 18, 18), new Color(65, 25, 25), hoverAnim);
            DrawRoundedRect(spriteBatch, rect, bgColor * (alpha * 0.9f), 6f);

            //边框
            Color borderColor = Color.Lerp(new Color(120, 50, 50), new Color(180, 70, 70), hoverAnim);
            DrawRoundedRectBorder(spriteBatch, rect, borderColor * (alpha * 0.7f), 6f, 1);

            //展开指示箭头
            float animArrowRot = MathHelper.Lerp(0f, MathHelper.PiOver2, expandAnim);
            Vector2 arrowPos = new(rect.X + 18f * scale, rect.Y + rect.Height / 2f);
            Color arrowColor = Color.Lerp(new Color(180, 120, 120), new Color(240, 160, 160), hoverAnim) * alpha;

            //三角箭头
            float arrowSize = 5f * scale;
            Vector2 p1 = arrowPos + (animArrowRot - MathHelper.PiOver4).ToRotationVector2() * arrowSize;
            Vector2 p2 = arrowPos + (animArrowRot + MathHelper.PiOver4).ToRotationVector2() * arrowSize;
            spriteBatch.Draw(pixel, arrowPos, new Rectangle(0, 0, 1, 1), arrowColor, animArrowRot,
                new Vector2(0f, 0.5f), new Vector2(arrowSize * 1.5f, 2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, arrowPos, new Rectangle(0, 0, 1, 1), arrowColor, animArrowRot + MathHelper.PiOver2,
                new Vector2(0f, 0.5f), new Vector2(arrowSize, 2f), SpriteEffects.None, 0f);

            //文字(居中显示)
            Vector2 textMeasure = FontAssets.MouseText.Value.MeasureString(text) * 0.9f * scale;
            Vector2 textPos = new(
                rect.X + (rect.Width - textMeasure.X) / 2f,
                rect.Y + (rect.Height - textMeasure.Y) / 2f);
            Color textColor = Color.Lerp(new Color(220, 180, 180), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.9f * scale);

            //悬停时的内发光
            if (hoverAnim > 0.01f) {
                DrawInnerGlow(spriteBatch, rect, new Color(180, 60, 60) * (alpha * hoverAnim * 0.1f), 6f, 6);
            }
        }

        private void DrawToggleRow(SpriteBatch spriteBatch, SettingToggle toggle, Rectangle rect,
            float alpha, float scale, SettingsCategory category = null) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //行背景(悬停时高亮)
            if (toggle.HoverAnim > 0.01f) {
                Color rowBg = new Color(60, 25, 25) * (alpha * toggle.HoverAnim * 0.5f);
                spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), rowBg);
            }

            //开关盒子
            float boxSize = ToggleBoxSize * scale;
            float boxX = rect.X + 12f * scale;
            float boxY = rect.Y + (rect.Height - boxSize) / 2f;
            Rectangle boxRect = new((int)boxX, (int)boxY, (int)boxSize, (int)boxSize);

            //盒子背景
            Color boxBg = Color.Lerp(new Color(35, 14, 14), new Color(50, 20, 20), toggle.HoverAnim);
            spriteBatch.Draw(pixel, boxRect, new Rectangle(0, 0, 1, 1), boxBg * alpha);

            //盒子边框
            Color boxBorder = Color.Lerp(new Color(100, 45, 45), new Color(160, 65, 65), toggle.HoverAnim);
            DrawSimpleBorder(spriteBatch, boxRect, boxBorder * alpha, 1);

            //勾选状态填充
            if (toggle.ToggleAnim > 0.01f) {
                int inset = (int)(3f * scale);
                Rectangle fillRect = new(boxRect.X + inset, boxRect.Y + inset,
                    boxRect.Width - inset * 2, boxRect.Height - inset * 2);

                Color fillColor = Color.Lerp(new Color(160, 50, 50), new Color(200, 70, 70), toggle.HoverAnim) * (alpha * toggle.ToggleAnim);
                spriteBatch.Draw(pixel, fillRect, new Rectangle(0, 0, 1, 1), fillColor);

                //对勾
                if (toggle.ToggleAnim > 0.5f) {
                    float checkAlpha = (toggle.ToggleAnim - 0.5f) * 2f;
                    Color checkColor = new Color(255, 220, 220) * (alpha * checkAlpha);
                    Vector2 checkCenter = boxRect.Center.ToVector2();
                    float cs = boxSize * 0.2f;
                    spriteBatch.Draw(pixel, checkCenter + new Vector2(-cs, 0),
                        new Rectangle(0, 0, 1, 1), checkColor, MathHelper.PiOver4,
                        new Vector2(0f, 0.5f), new Vector2(cs * 1.2f, 2f * scale), SpriteEffects.None, 0f);
                    spriteBatch.Draw(pixel, checkCenter + new Vector2(-cs * 0.1f, cs * 0.3f),
                        new Rectangle(0, 0, 1, 1), checkColor, -MathHelper.PiOver4,
                        new Vector2(0f, 0.5f), new Vector2(cs * 2.2f, 2f * scale), SpriteEffects.None, 0f);
                }
            }

            //子类额外绘制(如物品图标)
            category?.DrawRowExtra(spriteBatch, toggle, rect, alpha, scale);

            //标签文字
            string label = category?.GetLabel(toggle) ?? toggle.ConfigPropertyName;
            float labelOffset = category?.GetLabelOffsetX(scale) ?? 0f;
            Vector2 textPos = new(boxX + boxSize + 10f * scale + labelOffset, rect.Y + rect.Height / 2f - 9f * scale);
            Color textColor = Color.Lerp(new Color(200, 175, 170), new Color(240, 210, 210), toggle.HoverAnim);
            Utils.DrawBorderString(spriteBatch, label, textPos, textColor * alpha, 0.78f * scale);

            //底部分隔线
            Color lineColor = new Color(80, 35, 35) * (alpha * 0.4f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + (int)(20f * scale), rect.Bottom - 1,
                rect.Width - (int)(40f * scale), 1), new Rectangle(0, 0, 1, 1), lineColor);
        }

        private void DrawScrollBar(SpriteBatch spriteBatch, Rectangle clipRect, float alpha, SettingsCategory category = null) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = 8f;
            float trackX = clipRect.Right - barWidth - 2f;
            float trackHeight = clipRect.Height;

            //轨道
            Rectangle trackRect = new((int)trackX, clipRect.Y, (int)barWidth, (int)trackHeight);
            spriteBatch.Draw(pixel, trackRect, new Rectangle(0, 0, 1, 1), new Color(40, 18, 18) * (alpha * 0.6f));

            //滑块
            var visibleToggles = category?.GetVisibleToggles();
            int toggleCount = visibleToggles?.Count ?? 0;
            bool hasFooter = category?.ShowFooter ?? false;
            float totalContent = toggleCount * ToggleRowHeight * panelScaleAnim;
            if (hasFooter) totalContent += 30f * panelScaleAnim;
            float viewRatio = Math.Min(1f, trackHeight / Math.Max(1f, totalContent));
            float thumbHeight = Math.Max(30f, trackHeight * viewRatio);
            float catMaxScroll = category?.MaxScroll ?? 0f;
            float catScrollOffset = category?.ScrollOffset ?? 0f;
            float scrollRatio = catMaxScroll > 0 ? catScrollOffset / catMaxScroll : 0f;
            float thumbY = clipRect.Y + scrollRatio * (trackHeight - thumbHeight);

            Rectangle thumbRect = new((int)trackX, (int)thumbY, (int)barWidth, (int)thumbHeight);

            //检测悬停
            bool hoveringThumb = thumbRect.Contains(MouseHitBox);
            bool hoveringTrack = trackRect.Contains(MouseHitBox);
            bool isDragging = category?.IsDraggingScrollbar ?? false;

            Color trackBorderColor = new Color(80, 35, 35) * (alpha * 0.4f);
            DrawSimpleBorder(spriteBatch, trackRect, trackBorderColor, 1);

            Color thumbColor;
            if (isDragging) {
                thumbColor = new Color(220, 80, 80) * (alpha * 0.95f);
            }
            else if (hoveringThumb) {
                thumbColor = new Color(200, 75, 75) * (alpha * 0.9f);
            }
            else {
                thumbColor = new Color(160, 60, 60) * (alpha * 0.8f);
            }
            spriteBatch.Draw(pixel, thumbRect, new Rectangle(0, 0, 1, 1), thumbColor);

            //滑块边框
            Color thumbBorderColor = isDragging ? new Color(255, 100, 100) * (alpha * 0.7f) : new Color(120, 50, 50) * (alpha * 0.5f);
            DrawSimpleBorder(spriteBatch, thumbRect, thumbBorderColor, 1);

            //存储轨道和滑块的矩形供拖动检测使用
            if (category != null) {
                category.ScrollbarTrackRect = trackRect;
                category.ScrollbarThumbRect = thumbRect;
            }
        }

        private static void DrawTooltip(SpriteBatch spriteBatch, string text, Vector2 mousePos, float alpha) {
            if (string.IsNullOrEmpty(text)) return;

            //清理掉配色标记用于测量
            string cleanText = text.Replace("\n", "\n");
            string[] lines = cleanText.Split('\n');

            float tipScale = 0.75f;
            float maxWidth = 0;
            float totalHeight = 0;
            var font = FontAssets.MouseText.Value;

            foreach (string line in lines) {
                //去掉颜色标记来估算宽度
                string measureLine = System.Text.RegularExpressions.Regex.Replace(line, @"\[c/[0-9a-fA-F]+:", "");
                measureLine = measureLine.Replace("]", "");
                Vector2 lineSize = font.MeasureString(measureLine) * tipScale;
                maxWidth = Math.Max(maxWidth, lineSize.X);
                totalHeight += lineSize.Y;
            }

            float padX = 12f;
            float padY = 8f;
            float tipX = mousePos.X + 16f;
            float tipY = mousePos.Y + 16f;

            //防止溢出屏幕
            if (tipX + maxWidth + padX * 2 > Main.screenWidth) {
                tipX = mousePos.X - maxWidth - padX * 2 - 8f;
            }
            if (tipY + totalHeight + padY * 2 > Main.screenHeight) {
                tipY = mousePos.Y - totalHeight - padY * 2 - 8f;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle bgRect = new((int)(tipX - padX), (int)(tipY - padY),
                (int)(maxWidth + padX * 2), (int)(totalHeight + padY * 2));

            //背景
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), new Color(25, 10, 10) * (alpha * 0.95f));
            //边框
            DrawSimpleBorder(spriteBatch, bgRect, new Color(120, 50, 50) * (alpha * 0.7f), 1);

            //绘制文字
            float lineY = tipY;
            Color textColor = new Color(220, 200, 190) * alpha;
            foreach (string line in lines) {
                Utils.DrawBorderString(spriteBatch, line, new Vector2(tipX, lineY), textColor, tipScale);
                string measureLine = System.Text.RegularExpressions.Regex.Replace(line, @"\[c/[0-9a-fA-F]+:", "");
                measureLine = measureLine.Replace("]", "");
                lineY += font.MeasureString(measureLine).Y * tipScale;
            }
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float pressAnim, float alpha, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Rectangle drawRect = rect;
            if (pressAnim > 0.01f) {
                int pressOffset = (int)(pressAnim * 2f);
                drawRect.Y += pressOffset;
            }

            int expandAmount = (int)(hoverAnim * 3f);
            drawRect.Inflate(expandAmount, expandAmount / 2);

            Color bgTop = Color.Lerp(new Color(55, 20, 20), new Color(80, 30, 30), hoverAnim);
            Color bgBottom = Color.Lerp(new Color(40, 15, 15), new Color(60, 22, 22), hoverAnim);
            DrawGradientRoundedRect(spriteBatch, drawRect, bgTop * (alpha * 0.95f), bgBottom * (alpha * 0.95f), 6f);

            Color borderColor = Color.Lerp(new Color(140, 60, 60), new Color(220, 90, 90), hoverAnim);
            DrawRoundedRectBorder(spriteBatch, drawRect, borderColor * alpha, 6f, 1 + (int)hoverAnim);

            if (hoverAnim > 0.01f) {
                Color innerGlow = new Color(200, 80, 80) * (alpha * hoverAnim * 0.15f);
                DrawInnerGlow(spriteBatch, drawRect, innerGlow, 6f, 8);
            }

            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.85f * scale;
            Vector2 textPos = drawRect.Center.ToVector2() - textSize / 2f + new Vector2(0, 2);

            Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(1, 2),
                Color.Black * (alpha * 0.5f), 0.85f * scale);

            Color textColor = Color.Lerp(new Color(200, 180, 170), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.85f * scale);

            if (hoverAnim > 0.3f) {
                Color textGlow = new Color(220, 100, 100) * (alpha * (hoverAnim - 0.3f) * 0.4f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textGlow, 0.85f * scale);
            }
        }

        #region 绘制辅助方法

        private void DrawSmallButton(SpriteBatch spriteBatch, Rectangle rect, string text,
            float hoverAnim, float alpha, float scale) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color bgColor = Color.Lerp(new Color(45, 18, 18), new Color(70, 28, 28), hoverAnim);
            spriteBatch.Draw(pixel, rect, new Rectangle(0, 0, 1, 1), bgColor * (alpha * 0.9f));

            Color borderColor = Color.Lerp(new Color(110, 45, 45), new Color(180, 70, 70), hoverAnim);
            DrawSimpleBorder(spriteBatch, rect, borderColor * (alpha * 0.7f), 1);

            Vector2 textMeasure = FontAssets.MouseText.Value.MeasureString(text) * 0.72f * scale;
            Vector2 textPos = rect.Center.ToVector2() - textMeasure / 2f + new Vector2(0, 1);
            Color textColor = Color.Lerp(new Color(200, 170, 170), Color.White, hoverAnim);
            Utils.DrawBorderString(spriteBatch, text, textPos, textColor * alpha, 0.72f * scale);

            if (hoverAnim > 0.01f) {
                DrawInnerGlow(spriteBatch, rect, new Color(180, 60, 60) * (alpha * hoverAnim * 0.1f), 3f, 3);
            }
        }

        private static void DrawSimpleBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawRoundedRect(SpriteBatch sb, Rectangle rect, Color color, float radius) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);

            Rectangle center = new(rect.X + r, rect.Y, rect.Width - r * 2, rect.Height);
            sb.Draw(pixel, center, new Rectangle(0, 0, 1, 1), color);

            Rectangle left = new(rect.X, rect.Y + r, r, rect.Height - r * 2);
            Rectangle right = new(rect.Right - r, rect.Y + r, r, rect.Height - r * 2);
            sb.Draw(pixel, left, new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, right, new Rectangle(0, 0, 1, 1), color);

            for (int i = 0; i < r; i++) {
                float t = i / (float)r;
                int cornerWidth = (int)(r * MathF.Sqrt(1f - (1f - t) * (1f - t)));
                sb.Draw(pixel, new Rectangle(rect.X + r - cornerWidth, rect.Y + i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Y + i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.X + r - cornerWidth, rect.Bottom - 1 - i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
                sb.Draw(pixel, new Rectangle(rect.Right - r, rect.Bottom - 1 - i, cornerWidth, 1), new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawGradientRoundedRect(SpriteBatch sb, Rectangle rect, Color topColor, Color bottomColor, float radius) {
            int segments = rect.Height;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Color color = Color.Lerp(topColor, bottomColor, t);
                int y = rect.Y + i;
                int inset = 0;
                int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
                if (i < r) {
                    float cornerT = i / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - cornerT) * (1f - cornerT))));
                }
                else if (i > rect.Height - r) {
                    float cornerT = (rect.Height - i) / (float)r;
                    inset = (int)(r * (1f - MathF.Sqrt(1f - (1f - cornerT) * (1f - cornerT))));
                }
                Rectangle line = new(rect.X + inset, y, rect.Width - inset * 2, 1);
                sb.Draw(VaultAsset.placeholder2.Value, line, new Rectangle(0, 0, 1, 1), color);
            }
        }

        private static void DrawRoundedRectBorder(SpriteBatch sb, Rectangle rect, Color color, float radius, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int r = (int)Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Y, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X + r, rect.Bottom - thickness, rect.Width - r * 2, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y + r, thickness, rect.Height - r * 2), new Rectangle(0, 0, 1, 1), color);
            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Y + r), r, MathHelper.Pi, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Y + r), r, -MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.X + r, rect.Bottom - r), r, MathHelper.PiOver2, MathHelper.PiOver2, color, thickness);
            DrawCornerArc(sb, new Vector2(rect.Right - r, rect.Bottom - r), r, 0, MathHelper.PiOver2, color, thickness);
        }

        private static void DrawCornerArc(SpriteBatch sb, Vector2 center, float radius, float startAngle, float sweep, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = Math.Max(4, (int)(radius * sweep / 2f));
            for (int i = 0; i <= segments; i++) {
                float angle = startAngle + sweep * i / segments;
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), thickness, SpriteEffects.None, 0f);
            }
        }

        private static void DrawInnerGlow(SpriteBatch sb, Rectangle rect, Color color, float radius, int glowSize) {
            for (int i = 0; i < glowSize; i++) {
                float t = i / (float)glowSize;
                float glowAlpha = (1f - t) * (1f - t);
                Color glowColor = color * glowAlpha;
                Rectangle glowRect = rect;
                glowRect.Inflate(-i, -i);
                if (glowRect.Width > 0 && glowRect.Height > 0) {
                    DrawRoundedRectBorder(sb, glowRect, glowColor, Math.Max(0, radius - i), 1);
                }
            }
        }

        private static void DrawHorizontalGradient(SpriteBatch sb, Rectangle rect, Color left, Color center, Color right) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            for (int i = 0; i < rect.Width; i++) {
                float t = i / (float)rect.Width;
                Color color;
                if (t < 0.5f) {
                    color = Color.Lerp(left, center, t * 2f);
                }
                else {
                    color = Color.Lerp(center, right, (t - 0.5f) * 2f);
                }
                sb.Draw(pixel, new Rectangle(rect.X + i, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), color);
            }
        }

        #endregion
    }
}
