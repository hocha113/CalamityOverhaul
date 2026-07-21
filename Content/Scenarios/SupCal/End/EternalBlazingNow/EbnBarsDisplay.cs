using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    [VaultLoaden(CWRConstant.ADV)]
    internal class EbnBarsDisplay : ModResourceOverlay
    {
        //VaultLoaden心脏纹理
        public static Asset<Texture2D> EbnLife = null!;//填充22*22
        public static Asset<Texture2D> EbnLifeBack = null!;//背景30*30，边框宽4
        //VaultLoaden魔力星纹理
        public static Asset<Texture2D> EbnMagicStar = null!;//填充22*24
        public static Asset<Texture2D> EbnMagicStarBack = null!;//背景32*30

        //多排血条
        private const int MaxHeartsPerRow = 10;//每行最多心数
        private const int HeartSpacing = 0;//心间距
        private const int RowSpacing = 2;//行间距

        //魔力星列
        private const int MaxStarsPerColumn = 20;//每列最多星数
        private const int StarSpacing = 0;//星间距

        //血条状态
        private int _totalHearts;
        private int _currentLife;
        private int _maxLife;

        //魔力状态
        private int _totalStars;
        private int _currentMana;
        private int _maxMana;
        private static int _lastMana = -1;

        //悬停碰撞箱
        private static readonly List<Rectangle> _heartHitboxes = new();
        private static readonly List<Rectangle> _starHitboxes = new();
        private static bool _isHoveringLifeBar = false;
        private static bool _isHoveringManaBar = false;

        //受伤抖动
        private static int _lastLife = -1;
        private static int _shakeTimer = 0;
        private const int ShakeDuration = 15;//抖动帧数
        private static readonly Dictionary<int, float> _heartScales = new();//心缩放
        private static readonly Dictionary<int, int> _heartDamageTimers = new();//心受伤计时

        //魔力消耗动效
        private static readonly Dictionary<int, float> _starScales = new();//星缩放
        private static readonly Dictionary<int, int> _starConsumeTimers = new();//星消耗计时
        private static readonly Dictionary<int, float> _starGlowIntensity = new();//星泛光

        //魔力整体闪
        private static int _manaFlashTimer = 0;
        private const int ManaFlashDuration = 20;//闪烁帧数
        private static float _manaFlashIntensity = 0f;

        //心跳相位
        private static float _heartbeatPhase = 0f;
        //星闪相位
        private static float _starTwinklePhase = 0f;

        public override bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot
            , IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText) {
            if (EbnState.OnEbn(Main.LocalPlayer)) {
                drawText = true;
                PreDrawResources(snapshot);
                DrawLife(Main.spriteBatch);
                DrawMana(Main.spriteBatch);
                return false;
            }
            return base.PreDrawResourceDisplay(snapshot, displaySet, drawingLife, ref textColor, out drawText);
        }

        public override bool DisplayHoverText(PlayerStatsSnapshot snapshot, IPlayerResourcesDisplaySet displaySet, bool drawingLife) {
            if (EbnState.OnEbn(Main.LocalPlayer)) {
                return false;
            }
            return base.DisplayHoverText(snapshot, displaySet, drawingLife);
        }

        public void PreDrawResources(PlayerStatsSnapshot snapshot) {
            _currentLife = snapshot.Life;
            _maxLife = snapshot.LifeMax;

            _totalHearts = (int)MathHelper.Clamp(snapshot.AmountOfLifeHearts, 1, 20);

            _totalStars = snapshot.AmountOfManaStars;
            _currentMana = snapshot.Mana;
            _maxMana = snapshot.ManaMax;

            if (_lastLife > 0 && _currentLife < _lastLife) {
                _shakeTimer = ShakeDuration;

                int lifePerHeart = _maxLife / _totalHearts;
                int damagedHeartStart = _currentLife / lifePerHeart;
                int damagedHeartEnd = _lastLife / lifePerHeart;

                for (int i = damagedHeartStart; i <= Math.Min(damagedHeartEnd, _totalHearts - 1); i++) {
                    _heartDamageTimers[i] = 30;//受伤动效帧
                }
            }
            _lastLife = _currentLife;

            if (_lastMana > 0 && _currentMana < _lastMana) {
                _manaFlashTimer = ManaFlashDuration;
                _manaFlashIntensity = 1f;

                if (_totalStars > 0 && _maxMana > 0) {
                    int manaPerStar = _maxMana / _totalStars;
                    if (manaPerStar > 0) {
                        int consumedStarStart = _currentMana / manaPerStar;
                        int consumedStarEnd = _lastMana / manaPerStar;

                        for (int i = consumedStarStart; i <= Math.Min(consumedStarEnd, _totalStars - 1); i++) {
                            _starConsumeTimers[i] = 45;//消耗动效帧
                            _starGlowIntensity[i] = 1f;//泛光初值
                        }
                    }
                }
            }
            _lastMana = _currentMana;

            if (_shakeTimer > 0) {
                _shakeTimer--;
            }

            if (_manaFlashTimer > 0) {
                _manaFlashTimer--;
                _manaFlashIntensity = _manaFlashTimer / (float)ManaFlashDuration;
            }
            else {
                _manaFlashIntensity = 0f;
            }

            List<int> keysToRemove = new();
            foreach (var key in _heartDamageTimers.Keys) {
                _heartDamageTimers[key]--;
                if (_heartDamageTimers[key] <= 0) {
                    keysToRemove.Add(key);
                }
            }
            foreach (var key in keysToRemove) {
                _heartDamageTimers.Remove(key);
            }

            keysToRemove.Clear();
            foreach (var key in _starConsumeTimers.Keys) {
                _starConsumeTimers[key]--;
                if (_starConsumeTimers[key] <= 0) {
                    keysToRemove.Add(key);
                    _starGlowIntensity.Remove(key);
                }
                else {
                    float progress = _starConsumeTimers[key] / 45f;
                    _starGlowIntensity[key] = progress;
                }
            }
            foreach (var key in keysToRemove) {
                _starConsumeTimers.Remove(key);
            }

            _heartbeatPhase += 0.08f;
            if (_heartbeatPhase > MathHelper.TwoPi) {
                _heartbeatPhase -= MathHelper.TwoPi;
            }

            _starTwinklePhase += 0.06f;
            if (_starTwinklePhase > MathHelper.TwoPi) {
                _starTwinklePhase -= MathHelper.TwoPi;
            }
        }

        public void DrawLife(SpriteBatch spriteBatch) {
            if (Main.dedServ || EbnLifeBack == null || EbnLife == null || EbnLife.IsDisposed || EbnLifeBack.IsDisposed)
                return;

            _heartHitboxes.Clear();
            _isHoveringLifeBar = false;

            int heartBackWidth = EbnLifeBack.Width();
            int heartBackHeight = EbnLifeBack.Height();
            int heartFillWidth = EbnLife.Width();
            int heartFillHeight = EbnLife.Height();

            int totalRows = (_totalHearts + MaxHeartsPerRow - 1) / MaxHeartsPerRow;

            totalRows = Math.Min(totalRows, 2);

            int maxHeartsInAnyRow = Math.Min(MaxHeartsPerRow, _totalHearts);
            int totalWidth = maxHeartsInAnyRow * (heartBackWidth + HeartSpacing) - HeartSpacing;

            int startX = Main.screenWidth - totalWidth - 44;
            int startY = 18;

            Vector2 globalShakeOffset = Vector2.Zero;
            if (_shakeTimer > 0) {
                float shakeIntensity = _shakeTimer / (float)ShakeDuration * 3f;
                globalShakeOffset = new Vector2(
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity),
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity)
                );
            }

            for (int row = 0; row < totalRows; row++) {
                int heartsInThisRow = Math.Min(MaxHeartsPerRow, _totalHearts - row * MaxHeartsPerRow);

                int rowY = startY + row * (heartBackHeight + RowSpacing);

                for (int i = 0; i < heartsInThisRow; i++) {
                    int heartIndex = row * MaxHeartsPerRow + i;

                    int heartX = startX + i * (heartBackWidth + HeartSpacing);

                    int lifePerHeart = _maxLife / _totalHearts;
                    int heartStartLife = heartIndex * lifePerHeart;
                    int heartEndLife = (heartIndex + 1) * lifePerHeart;

                    //末心余数
                    if (heartIndex == _totalHearts - 1) {
                        heartEndLife = _maxLife;
                        lifePerHeart = heartEndLife - heartStartLife;
                    }

                    float fillPercent = 0f;
                    if (_currentLife > heartStartLife) {
                        int lifeInThisHeart = Math.Min(_currentLife - heartStartLife, lifePerHeart);
                        fillPercent = (float)lifeInThisHeart / lifePerHeart;
                    }

                    float targetScale = 0.5f + fillPercent * 0.5f;//0.5~1

                    if (fillPercent > 0 && fillPercent < 1f) {
                        float heartbeat = (float)Math.Sin(_heartbeatPhase * 2f) * 0.05f;
                        targetScale += heartbeat;
                    }

                    if (!_heartScales.ContainsKey(heartIndex)) {
                        _heartScales[heartIndex] = targetScale;
                    }
                    else {
                        _heartScales[heartIndex] = MathHelper.Lerp(_heartScales[heartIndex], targetScale, 0.15f);
                    }
                    float currentScale = _heartScales[heartIndex];

                    Vector2 individualShakeOffset = Vector2.Zero;
                    if (_heartDamageTimers.TryGetValue(heartIndex, out int damageTimer)) {
                        float damageShakeIntensity = damageTimer / 30f * 4f;
                        individualShakeOffset = new Vector2(
                            (float)Math.Sin(damageTimer * 0.5f) * damageShakeIntensity,
                            (float)Math.Cos(damageTimer * 0.7f) * damageShakeIntensity
                        );
                    }

                    Vector2 heartCenter = new Vector2(
                        heartX + heartBackWidth / 2f,
                        rowY + heartBackHeight / 2f
                    ) + globalShakeOffset + individualShakeOffset;

                    int scaledWidth = (int)(heartBackWidth * currentScale);
                    int scaledHeight = (int)(heartBackHeight * currentScale);
                    Rectangle heartHitbox = new Rectangle(
                        (int)(heartCenter.X - scaledWidth / 2f),
                        (int)(heartCenter.Y - scaledHeight / 2f),
                        scaledWidth,
                        scaledHeight
                    );
                    _heartHitboxes.Add(heartHitbox);

                    if (heartHitbox.Intersects((Main.MouseScreen - new Vector2(2, 2)).GetRectangle(4))) {
                        _isHoveringLifeBar = true;
                    }

                    Vector2 backOrigin = new Vector2(heartBackWidth / 2f, heartBackHeight / 2f);

                    spriteBatch.Draw(
                        EbnLifeBack.Value,
                        heartCenter,
                        null,
                        Color.White,
                        0f,
                        backOrigin,
                        currentScale,
                        SpriteEffects.None,
                        0f
                    );

                    if (fillPercent > 0f) {
                        Vector2 fillOrigin = new Vector2(heartFillWidth / 2f, heartFillHeight / 2f);

                        Color fillColor = Color.White;
                        if (fillPercent < 0.3f) {
                            float pulseIntensity = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.3f + 0.7f;
                            fillColor = Color.Lerp(Color.Red, Color.White, fillPercent / 0.3f) * pulseIntensity;
                        }

                        spriteBatch.Draw(
                            EbnLife.Value,
                            heartCenter,
                            null,
                            fillColor,
                            0f,
                            fillOrigin,
                            currentScale,
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            if (_isHoveringLifeBar) {
                DrawLifeTooltip(spriteBatch);
            }
        }

        public void DrawMana(SpriteBatch spriteBatch) {
            if (Main.dedServ || EbnMagicStarBack == null || EbnMagicStar == null ||
                EbnMagicStar.IsDisposed || EbnMagicStarBack.IsDisposed)
                return;

            _starHitboxes.Clear();
            _isHoveringManaBar = false;

            int starBackWidth = EbnMagicStarBack.Width();
            int starBackHeight = EbnMagicStarBack.Height();
            int starFillWidth = EbnMagicStar.Width();
            int starFillHeight = EbnMagicStar.Height();

            int displayStars = _totalStars;
            if (displayStars <= 0) {
                displayStars = Math.Min((_maxMana + 19) / 20, MaxStarsPerColumn);
            }
            displayStars = Math.Min(displayStars, MaxStarsPerColumn);

            int startX = Main.screenWidth - starBackWidth - 8;
            int startY = 28;//血条下方

            Color flashColor = Color.White;
            if (_manaFlashIntensity > 0f) {
                float flashPulse = (float)Math.Sin(_manaFlashIntensity * MathHelper.Pi * 4f) * 0.5f + 0.5f;
                flashColor = Color.Lerp(Color.White, new Color(150, 200, 255), _manaFlashIntensity * flashPulse);
            }

            for (int i = 0; i < displayStars; i++) {
                int starIndex = i;

                int starY = startY + i * (starBackHeight + StarSpacing);

                float fillPercent = 0f;
                if (displayStars > 0 && _maxMana > 0) {
                    int manaPerStar = _maxMana / displayStars;
                    if (manaPerStar > 0) {
                        int starStartMana = starIndex * manaPerStar;
                        int starEndMana = (starIndex + 1) * manaPerStar;

                        //末星余数
                        if (starIndex == displayStars - 1) {
                            starEndMana = _maxMana;
                            manaPerStar = starEndMana - starStartMana;
                        }

                        if (_currentMana > starStartMana) {
                            int manaInThisStar = Math.Min(_currentMana - starStartMana, manaPerStar);
                            fillPercent = (float)manaInThisStar / manaPerStar;
                        }
                    }
                }

                float targetScale = 0.5f + fillPercent * 0.5f;//0.5~1

                if (fillPercent > 0 && fillPercent < 1f) {
                    float twinkle = (float)Math.Sin(_starTwinklePhase + starIndex * 0.3f) * 0.08f;
                    targetScale += twinkle;
                }

                if (!_starScales.ContainsKey(starIndex)) {
                    _starScales[starIndex] = targetScale;
                }
                else {
                    _starScales[starIndex] = MathHelper.Lerp(_starScales[starIndex], targetScale, 0.15f);
                }
                float currentScale = _starScales[starIndex];

                Vector2 starCenter = new Vector2(
                    startX + starBackWidth / 2f,
                    starY + starBackHeight / 2f
                );

                int scaledWidth = (int)(starBackWidth * currentScale);
                int scaledHeight = (int)(starBackHeight * currentScale);
                Rectangle starHitbox = new Rectangle(
                    (int)(starCenter.X - scaledWidth / 2f),
                    (int)(starCenter.Y - scaledHeight / 2f),
                    scaledWidth,
                    scaledHeight
                );
                _starHitboxes.Add(starHitbox);

                if (starHitbox.Intersects((Main.MouseScreen - new Vector2(2, 2)).GetRectangle(4))) {
                    _isHoveringManaBar = true;
                }

                Vector2 backOrigin = new Vector2(starBackWidth / 2f, starBackHeight / 2f);

                spriteBatch.Draw(
                    EbnMagicStarBack.Value,
                    starCenter,
                    null,
                    flashColor,
                    0f,
                    backOrigin,
                    1f,
                    SpriteEffects.None,
                    0f
                );

                if (fillPercent > 0f) {
                    Vector2 fillOrigin = new Vector2(starFillWidth / 2f, starFillHeight / 2f);

                    Color fillColor = flashColor;

                    spriteBatch.Draw(
                        EbnMagicStar.Value,
                        starCenter,
                        null,
                        fillColor,
                        0f,
                        fillOrigin,
                        currentScale,
                        SpriteEffects.None,
                        0f
                    );

                    if (_starGlowIntensity.TryGetValue(starIndex, out float glowIntensity) && glowIntensity > 0f) {
                        for (int layer = 0; layer < 3; layer++) {
                            float layerScale = currentScale * (1.2f + layer * 0.3f);
                            float layerAlpha = glowIntensity * (0.6f - layer * 0.15f);
                            Color glowColor = new Color(100, 150, 255, 0) * layerAlpha;

                            spriteBatch.Draw(
                                EbnMagicStar.Value,
                                starCenter,
                                null,
                                glowColor,
                                Main.GlobalTimeWrappedHourly * (1f + layer * 0.2f),
                                fillOrigin,
                                layerScale,
                                SpriteEffects.None,
                                0f
                            );
                        }
                    }
                }
            }

            if (_isHoveringManaBar) {
                DrawManaTooltip(spriteBatch);
            }
        }

        private void DrawLifeTooltip(SpriteBatch spriteBatch) {
            string lifeText = $"{_currentLife}/{_maxLife}";

            float lifePercent = _currentLife / (float)_maxLife * 100f;
            string percentText = $"({lifePercent:F1}%)";

            float textScale = 1f;
            Vector2 lifeTextSize = FontAssets.MouseText.Value.MeasureString(lifeText) * textScale;
            Vector2 percentTextSize = FontAssets.MouseText.Value.MeasureString(percentText) * textScale * 0.8f;

            Vector2 totalSize = new Vector2(
                Math.Max(lifeTextSize.X, percentTextSize.X),
                lifeTextSize.Y + percentTextSize.Y + 4
            );

            Vector2 drawPos = new Vector2(Main.mouseX + 16, Main.mouseY + 16);

            //clamp勿出屏
            if (drawPos.X + totalSize.X > Main.screenWidth) {
                drawPos.X = Main.mouseX - totalSize.X - 16;
            }
            if (drawPos.Y + totalSize.Y > Main.screenHeight) {
                drawPos.Y = Main.mouseY - totalSize.Y - 16;
            }

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                lifeText,
                drawPos.X,
                drawPos.Y,
                Color.White,
                Color.Black,
                Vector2.Zero,
                textScale
            );

            Vector2 percentPos = drawPos + new Vector2(0, lifeTextSize.Y + 4);
            Color percentColor = lifePercent < 30f ? Color.Red :
                                lifePercent < 50f ? Color.Yellow :
                                Color.LimeGreen;

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                percentText,
                percentPos.X,
                percentPos.Y,
                percentColor,
                Color.Black,
                Vector2.Zero,
                textScale * 0.8f
            );
        }

        private void DrawManaTooltip(SpriteBatch spriteBatch) {
            string manaText = $"{_currentMana}/{_maxMana}";

            float manaPercent = _currentMana / (float)_maxMana * 100f;
            string percentText = $"({manaPercent:F1}%)";

            float textScale = 1f;
            Vector2 manaTextSize = FontAssets.MouseText.Value.MeasureString(manaText) * textScale;
            Vector2 percentTextSize = FontAssets.MouseText.Value.MeasureString(percentText) * textScale * 0.8f;

            Vector2 totalSize = new Vector2(
                Math.Max(manaTextSize.X, percentTextSize.X),
                manaTextSize.Y + percentTextSize.Y + 4
            );

            Vector2 drawPos = new Vector2(Main.mouseX + 16, Main.mouseY + 16);

            //clamp勿出屏
            if (drawPos.X + totalSize.X > Main.screenWidth) {
                drawPos.X = Main.mouseX - totalSize.X - 16;
            }
            if (drawPos.Y + totalSize.Y > Main.screenHeight) {
                drawPos.Y = Main.mouseY - totalSize.Y - 16;
            }

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                manaText,
                drawPos.X,
                drawPos.Y,
                new Color(100, 150, 255),
                Color.Black,
                Vector2.Zero,
                textScale
            );

            Vector2 percentPos = drawPos + new Vector2(0, manaTextSize.Y + 4);
            Color percentColor = manaPercent < 30f ? Color.Red :
                                manaPercent < 50f ? Color.Yellow :
                                Color.Cyan;

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                percentText,
                percentPos.X,
                percentPos.Y,
                percentColor,
                Color.Black,
                Vector2.Zero,
                textScale * 0.8f
            );
        }
    }
}
