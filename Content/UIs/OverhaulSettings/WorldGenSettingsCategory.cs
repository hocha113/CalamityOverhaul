using CalamityOverhaul.Common;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using SettingToggle = CalamityOverhaul.Content.UIs.OverhaulSettings.OverhaulSettingsUI.SettingToggle;

namespace CalamityOverhaul.Content.UIs.OverhaulSettings
{
    /// <summary>
    /// 结构密度等级枚举
    /// </summary>
    internal enum StructureDensity
    {
        /// <summary>灭绝（不生成）</summary>
        Extinction = 0,
        /// <summary>稀少</summary>
        Rare = 1,
        /// <summary>普通（默认）</summary>
        Normal = 2,
        /// <summary>常见</summary>
        Common = 3,
        /// <summary>泛滥</summary>
        Flood = 4,
        /// <summary>无处不在</summary>
        Everywhere = 5
    }

    /// <summary>
    /// 世界生成密度数据的持久化存储
    /// </summary>
    internal class WorldGenDensitySave : SaveMod
    {
        /// <summary>
        /// 各结构的密度等级，key为结构名称
        /// </summary>
        public static readonly Dictionary<string, StructureDensity> DensityByName = [];

        /// <summary>
        /// 所有可配置密度的结构名称列表
        /// </summary>
        internal static readonly string[] StructureNames = [
            "WindGrivenGenerator",
            "WGGCollector",
            "JunkmanBase",
            "RocketHut",
            "SylvanOutpost",
        ];

        /// <summary>
        /// 各结构的默认密度等级
        /// </summary>
        internal static StructureDensity GetDefaultDensity(string name) {
            return name switch {
                "JunkmanBase" or "RocketHut" or "SylvanOutpost" => StructureDensity.Rare,
                _ => StructureDensity.Normal
            };
        }

        public override void SetStaticDefaults() {
            foreach (string name in StructureNames) {
                DensityByName.TryAdd(name, GetDefaultDensity(name));
            }
            if (!HasSave) {
                DoSave<WorldGenDensitySave>();
            }
            DoLoad<WorldGenDensitySave>();
            SyncFromConfig();
        }

        public override void SaveData(TagCompound tag) {
            foreach (var pair in DensityByName) {
                tag[$"Density_{pair.Key}"] = (int)pair.Value;
            }
        }

        public override void LoadData(TagCompound tag) {
            foreach (string name in StructureNames) {
                if (tag.TryGet($"Density_{name}", out int level)) {
                    DensityByName[name] = (StructureDensity)Math.Clamp(level, 0, 5);
                }
                else {
                    DensityByName[name] = GetDefaultDensity(name);
                }
            }
        }

        public static void Save() => DoSave<WorldGenDensitySave>();

        public static StructureDensity GetDensity(string name) {
            return DensityByName.TryGetValue(name, out var d) ? d : StructureDensity.Normal;
        }

        public static void SetDensity(string name, StructureDensity density) {
            DensityByName[name] = density;
            Save();
            SyncToConfig(name, density);
        }

        /// <summary>
        /// 结构名称到配置属性名的映射
        /// </summary>
        internal static string GetConfigPropertyName(string structName) {
            return "Gen" + structName;
        }

        /// <summary>
        /// 防止双向同步时循环调用
        /// </summary>
        private static bool _syncing;

        /// <summary>
        /// 将密度等级同步到CWRServerConfig的布尔字段
        /// </summary>
        internal static void SyncToConfig(string structName, StructureDensity density) {
            if (_syncing) return;
            var config = CWRServerConfig.Instance;
            if (config == null) return;
            _syncing = true;
            try {
                bool enabled = density != StructureDensity.Extinction;
                switch (structName) {
                    case "WindGrivenGenerator": config.GenWindGrivenGenerator = enabled; break;
                    case "WGGCollector": config.GenWGGCollector = enabled; break;
                    case "JunkmanBase": config.GenJunkmanBase = enabled; break;
                    case "RocketHut": config.GenRocketHut = enabled; break;
                    case "SylvanOutpost": config.GenSylvanOutpost = enabled; break;
                }
                ContentSettingsCategory.SaveConfigStatic();
            } finally {
                _syncing = false;
            }
        }

        /// <summary>
        /// 从 CWRServerConfig 的布尔字段同步到密度等级（当Config被外部修改时调用）
        /// 如果配置为关闭但密度不是灭绝，则设为灭绝；如果配置为开启但密度是灭绝，则恢复默认密度
        /// </summary>
        internal static void SyncFromConfig() {
            if (_syncing) return;
            var config = CWRServerConfig.Instance;
            if (config == null) return;
            _syncing = true;
            try {
                SyncOneFromConfig("WindGrivenGenerator", config.GenWindGrivenGenerator);
                SyncOneFromConfig("WGGCollector", config.GenWGGCollector);
                SyncOneFromConfig("JunkmanBase", config.GenJunkmanBase);
                SyncOneFromConfig("RocketHut", config.GenRocketHut);
                SyncOneFromConfig("SylvanOutpost", config.GenSylvanOutpost);
                Save();
            } finally {
                _syncing = false;
            }
        }

        private static void SyncOneFromConfig(string structName, bool configEnabled) {
            var current = GetDensity(structName);
            if (!configEnabled && current != StructureDensity.Extinction) {
                DensityByName[structName] = StructureDensity.Extinction;
            }
            else if (configEnabled && current == StructureDensity.Extinction) {
                DensityByName[structName] = GetDefaultDensity(structName);
            }
        }

        /// <summary>
        /// 获取密度对应的数值乘数，用于世界生成时调节参数
        /// </summary>
        public static float GetMultiplier(string name) {
            return GetDensity(name) switch {
                StructureDensity.Extinction => 0f,
                StructureDensity.Rare => 0.3f,
                StructureDensity.Normal => 1f,
                StructureDensity.Common => 2f,
                StructureDensity.Flood => 4f,
                StructureDensity.Everywhere => 8f,
                _ => 1f
            };
        }

        /// <summary>
        /// 获取密度对应的最小距离系数（值越大距离越大，越稀疏）
        /// </summary>
        public static float GetDistanceFactor(string name) {
            return GetDensity(name) switch {
                StructureDensity.Extinction => 999f,
                StructureDensity.Rare => 2.5f,
                StructureDensity.Normal => 1f,
                StructureDensity.Common => 0.6f,
                StructureDensity.Flood => 0.35f,
                StructureDensity.Everywhere => 0.15f,
                _ => 1f
            };
        }
    }

    /// <summary>
    /// 世界生成设置分类：管理 CWRServerConfig 中的结构启用开关，
    /// 以及通过自管理数据控制结构的生成密度（饥荒风格左右箭头等级选择器）
    /// </summary>
    internal class WorldGenSettingsCategory : SettingsCategory
    {
        public override string Title => OverhaulSettingsUI.WorldGenSettingsText?.Value ?? "世界生成设置";

        private const string DensityPrefix = "Density_";
        private const int DensityLevelCount = 6;

        /// <summary>
        /// 获取密度等级对应的本地化显示名称
        /// </summary>
        private static string GetDensityLevelText(int level) {
            return level switch {
                0 => OverhaulSettingsUI.DensityExtinctionText?.Value ?? "灭绝",
                1 => OverhaulSettingsUI.DensityRareText?.Value ?? "稀少",
                2 => OverhaulSettingsUI.DensityNormalText?.Value ?? "普通",
                3 => OverhaulSettingsUI.DensityCommonText?.Value ?? "常见",
                4 => OverhaulSettingsUI.DensityFloodText?.Value ?? "泛滥",
                5 => OverhaulSettingsUI.DensityEverywhereText?.Value ?? "无处不在",
                _ => "???"
            };
        }

        private static readonly Color[] DensityLevelColors = [
            new(100, 40, 40),    // Extinction - 暗红
            new(90, 130, 180),   // Rare - 冷蓝
            new(160, 180, 160),  // Normal - 灰绿
            new(200, 180, 70),   // Common - 暖黄
            new(200, 110, 45),   // Flood - 深橙
            new(200, 55, 55),    // Everywhere - 亮红
        ];

        /// <summary>
        /// 左右箭头点击区域缓存，key为toggle的ConfigPropertyName
        /// </summary>
        private readonly Dictionary<string, Rectangle> _leftArrowRects = [];
        private readonly Dictionary<string, Rectangle> _rightArrowRects = [];
        private readonly Dictionary<string, float> _leftArrowHover = [];
        private readonly Dictionary<string, float> _rightArrowHover = [];

        public override void Initialize() {
            if (CWRServerConfig.Instance == null) return;

            foreach (string structName in WorldGenDensitySave.StructureNames) {
                string toggleName = DensityPrefix + structName;
                AddToggle(toggleName,
                    () => WorldGenDensitySave.GetDensity(structName) != StructureDensity.Extinction,
                    _ => { },
                    false);
            }

            ActionButtons.Add(new ActionButton {
                Label = () => OverhaulSettingsUI.ResetDefaultText?.Value ?? "重置为默认",
                OnClick = ResetAllToDefault
            });

            ShowFooter = true;
            FooterHint = OverhaulSettingsUI.WorldGenFooterHintText?.Value ?? "";
        }

        private static bool IsDensityToggle(SettingToggle toggle)
            => toggle.ConfigPropertyName.StartsWith(DensityPrefix);

        private static string GetStructureName(SettingToggle toggle)
            => toggle.ConfigPropertyName[DensityPrefix.Length..];

        public override string GetLabel(SettingToggle toggle) {
            if (IsDensityToggle(toggle)) {
                string structName = GetStructureName(toggle);
                string configProp = WorldGenDensitySave.GetConfigPropertyName(structName);
                string configKey = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{configProp}.Label";
                string configValue = Language.GetTextValue(configKey);
                return configValue == configKey ? structName : configValue;
            }
            string key2 = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{toggle.ConfigPropertyName}.Label";
            string value2 = Language.GetTextValue(key2);
            return value2 == key2 ? toggle.ConfigPropertyName : value2;
        }

        public override string GetTooltip(SettingToggle toggle) {
            if (IsDensityToggle(toggle)) {
                string structName = GetStructureName(toggle);
                var density = WorldGenDensitySave.GetDensity(structName);
                int level = (int)density;
                string levelText = GetDensityLevelText(level);
                string densityLabel = OverhaulSettingsUI.DensityTooltipText?.Value ?? "结构生成密度";
                string tip = $"{densityLabel}: [c/{DensityLevelColors[level].Hex3()}:{levelText}]";
                string configProp = WorldGenDensitySave.GetConfigPropertyName(structName);
                string tooltipKey = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{configProp}.Tooltip";
                string configDesc = Language.GetTextValue(tooltipKey);
                if (configDesc != tooltipKey) {
                    tip += "\n" + configDesc;
                }
                return tip;
            }
            string key = $"Mods.CalamityOverhaul.Configs.CWRServerConfig.{toggle.ConfigPropertyName}.Tooltip";
            string value = Language.GetTextValue(key);
            return value == key ? "" : value;
        }

        public override void OnToggleChanged(SettingToggle toggle, bool newValue) {
            if (!IsDensityToggle(toggle)) {
                SaveConfig();
            }
        }

        private static void ResetAllToDefault() {
            foreach (string name in WorldGenDensitySave.StructureNames) {
                WorldGenDensitySave.SetDensity(name, WorldGenDensitySave.GetDefaultDensity(name));
            }
        }

        /// <summary>
        /// 覆写点击处理：密度选项通过左右箭头切换等级
        /// </summary>
        public override bool HandleClick(Rectangle mouseHitBox) {
            if (HoveringCategory) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.3f });
                Expanded = !Expanded;
                if (!Expanded) {
                    ScrollTarget = 0f;
                }
                return true;
            }

            if (ExpandAnim > 0.5f && MaxScroll > 0f) {
                if (ScrollbarThumbRect.Width > 0 && ScrollbarThumbRect.Contains(mouseHitBox)) {
                    IsDraggingScrollbar = true;
                    DragStartY = Microsoft.Xna.Framework.Input.Mouse.GetState().Y;
                    DragStartScrollTarget = ScrollTarget;
                    return true;
                }
                if (ScrollbarTrackRect.Width > 0 && ScrollbarTrackRect.Contains(mouseHitBox)) {
                    float clickY = Microsoft.Xna.Framework.Input.Mouse.GetState().Y;
                    float trackHeight = ScrollbarTrackRect.Height;
                    float relativeY = clickY - ScrollbarTrackRect.Y;
                    float ratio = relativeY / trackHeight;
                    ScrollTarget = Math.Clamp(ratio * MaxScroll, 0f, MaxScroll);
                    IsDraggingScrollbar = true;
                    DragStartY = Microsoft.Xna.Framework.Input.Mouse.GetState().Y;
                    DragStartScrollTarget = ScrollTarget;
                    return true;
                }
            }

            if (ExpandAnim > 0.5f) {
                foreach (var btn in ActionButtons) {
                    if (btn.Hovering) {
                        btn.OnClick?.Invoke();
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.2f });
                        return true;
                    }
                }

                foreach (var toggle in GetVisibleToggles()) {
                    if (!toggle.Hovering) continue;

                    if (IsDensityToggle(toggle)) {
                        string name = toggle.ConfigPropertyName;
                        string structName = GetStructureName(toggle);
                        var current = WorldGenDensitySave.GetDensity(structName);
                        int curLevel = (int)current;

                        //检查左箭头点击
                        if (_leftArrowRects.TryGetValue(name, out var leftRect) && leftRect.Contains(mouseHitBox)) {
                            if (curLevel > 0) {
                                var next = (StructureDensity)(curLevel - 1);
                                WorldGenDensitySave.SetDensity(structName, next);
                                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.3f + (int)next * 0.12f });
                            }
                            else {
                                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f, Pitch = -0.5f });
                            }
                            return true;
                        }
                        //检查右箭头点击
                        if (_rightArrowRects.TryGetValue(name, out var rightRect) && rightRect.Contains(mouseHitBox)) {
                            if (curLevel < DensityLevelCount - 1) {
                                var next = (StructureDensity)(curLevel + 1);
                                WorldGenDensitySave.SetDensity(structName, next);
                                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.3f + (int)next * 0.12f });
                            }
                            else {
                                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f, Pitch = -0.5f });
                            }
                            return true;
                        }
                        //点击中间区域也可循环切换
                        var next2 = (StructureDensity)(((int)current + 1) % DensityLevelCount);
                        WorldGenDensitySave.SetDensity(structName, next2);
                        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.3f + (int)next2 * 0.12f });
                        return true;
                    }
                    else {
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

        public override float GetLabelOffsetX(float scale) => 0f;

        public override void DrawRowExtra(SpriteBatch spriteBatch, SettingToggle toggle,
            Rectangle rect, float alpha, float scale) {
            if (!IsDensityToggle(toggle)) return;

            string name = toggle.ConfigPropertyName;
            string structName = GetStructureName(toggle);
            var density = WorldGenDensitySave.GetDensity(structName);
            int level = (int)density;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            var font = FontAssets.MouseText.Value;
            Point mousePoint = new(Microsoft.Xna.Framework.Input.Mouse.GetState().X, Microsoft.Xna.Framework.Input.Mouse.GetState().Y);

            //=== 布局参数 ===
            float selectorWidth = 160f * scale;
            float selectorHeight = 24f * scale;
            float selectorX = rect.Right - selectorWidth - 12f * scale;
            float selectorY = rect.Y + (rect.Height - selectorHeight) / 2f;

            float arrowWidth = 22f * scale;
            float centerWidth = selectorWidth - arrowWidth * 2;

            Rectangle leftArrowRect = new((int)selectorX, (int)selectorY, (int)arrowWidth, (int)selectorHeight);
            Rectangle centerRect = new((int)(selectorX + arrowWidth), (int)selectorY, (int)centerWidth, (int)selectorHeight);
            Rectangle rightArrowRect = new((int)(selectorX + arrowWidth + centerWidth), (int)selectorY, (int)arrowWidth, (int)selectorHeight);

            //缓存箭头矩形供点击检测使用
            _leftArrowRects[name] = leftArrowRect;
            _rightArrowRects[name] = rightArrowRect;

            //检测箭头悬停
            bool hoverLeft = toggle.Hovering && leftArrowRect.Contains(mousePoint) && ExpandClipRect.Contains(mousePoint);
            bool hoverRight = toggle.Hovering && rightArrowRect.Contains(mousePoint) && ExpandClipRect.Contains(mousePoint);
            bool canDecrease = level > 0;
            bool canIncrease = level < DensityLevelCount - 1;

            //更新箭头悬停动画
            _leftArrowHover.TryAdd(name, 0f);
            _rightArrowHover.TryAdd(name, 0f);
            _leftArrowHover[name] += ((hoverLeft ? 1f : 0f) - _leftArrowHover[name]) * 0.18f;
            _rightArrowHover[name] += ((hoverRight ? 1f : 0f) - _rightArrowHover[name]) * 0.18f;
            float leftHover = _leftArrowHover[name];
            float rightHover = _rightArrowHover[name];

            Color levelColor = DensityLevelColors[level];

            //=== 绘制整体选择器背景 ===
            Rectangle bgRect = new((int)selectorX - 1, (int)selectorY - 1, (int)selectorWidth + 2, (int)selectorHeight + 2);
            spriteBatch.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), new Color(18, 8, 8) * (alpha * 0.7f));

            //选择器边框
            Color borderColor = Color.Lerp(new Color(80, 35, 35), levelColor, 0.25f) * (alpha * 0.6f);
            DrawSimpleBorder(spriteBatch, bgRect, borderColor, 1);

            //=== 绘制左箭头 ◀ ===
            {
                Color arrowBg = hoverLeft && canDecrease
                    ? Color.Lerp(new Color(50, 20, 20), new Color(80, 30, 30), leftHover)
                    : new Color(35, 14, 14);
                spriteBatch.Draw(pixel, leftArrowRect, new Rectangle(0, 0, 1, 1), arrowBg * (alpha * 0.85f));

                float arrowAlpha = canDecrease ? (0.6f + leftHover * 0.4f) : 0.2f;
                Color arrowColor = canDecrease
                    ? Color.Lerp(new Color(180, 100, 100), new Color(240, 160, 160), leftHover) * (alpha * arrowAlpha)
                    : new Color(80, 40, 40) * (alpha * arrowAlpha);

                //绘制 ◀ 三角箭头
                Vector2 arrowCenter = leftArrowRect.Center.ToVector2();
                float arrowSize = 5f * scale;
                float pushOffset = canDecrease ? leftHover * -1.5f * scale : 0f;
                Vector2 tip = arrowCenter + new Vector2(-arrowSize * 0.7f + pushOffset, 0);
                spriteBatch.Draw(pixel, tip, new Rectangle(0, 0, 1, 1), arrowColor, MathHelper.Pi + MathHelper.PiOver4,
                    new Vector2(0f, 0.5f), new Vector2(arrowSize * 1.1f, 2f * scale), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, tip, new Rectangle(0, 0, 1, 1), arrowColor, MathHelper.Pi - MathHelper.PiOver4,
                    new Vector2(0f, 0.5f), new Vector2(arrowSize * 1.1f, 2f * scale), SpriteEffects.None, 0f);
            }

            //=== 绘制右箭头 ▶ ===
            {
                Color arrowBg = hoverRight && canIncrease
                    ? Color.Lerp(new Color(50, 20, 20), new Color(80, 30, 30), rightHover)
                    : new Color(35, 14, 14);
                spriteBatch.Draw(pixel, rightArrowRect, new Rectangle(0, 0, 1, 1), arrowBg * (alpha * 0.85f));

                float arrowAlpha = canIncrease ? (0.6f + rightHover * 0.4f) : 0.2f;
                Color arrowColor = canIncrease
                    ? Color.Lerp(new Color(180, 100, 100), new Color(240, 160, 160), rightHover) * (alpha * arrowAlpha)
                    : new Color(80, 40, 40) * (alpha * arrowAlpha);

                Vector2 arrowCenter = rightArrowRect.Center.ToVector2();
                float arrowSize = 5f * scale;
                float pushOffset = canIncrease ? rightHover * 1.5f * scale : 0f;
                Vector2 tip = arrowCenter + new Vector2(arrowSize * 0.7f + pushOffset, 0);
                spriteBatch.Draw(pixel, tip, new Rectangle(0, 0, 1, 1), arrowColor, MathHelper.PiOver4,
                    new Vector2(0f, 0.5f), new Vector2(arrowSize * 1.1f, 2f * scale), SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, tip, new Rectangle(0, 0, 1, 1), arrowColor, -MathHelper.PiOver4,
                    new Vector2(0f, 0.5f), new Vector2(arrowSize * 1.1f, 2f * scale), SpriteEffects.None, 0f);
            }

            //=== 绘制中间等级名称区域 ===
            {
                //中间背景微微比两侧亮，并随等级变色
                Color centerBg = Color.Lerp(new Color(30, 12, 12), levelColor, 0.1f) * (alpha * 0.65f);
                spriteBatch.Draw(pixel, centerRect, new Rectangle(0, 0, 1, 1), centerBg);

                //等级名称（居中）
                string levelText = GetDensityLevelText(level);
                float textScale = 0.72f * scale;
                Vector2 textSize = font.MeasureString(levelText) * textScale;
                Vector2 textPos = new(
                    centerRect.X + (centerRect.Width - textSize.X) / 2f,
                    centerRect.Y + (centerRect.Height - textSize.Y) / 2f);

                //等级文字颜色
                Color textColor = Color.Lerp(levelColor, Color.White, 0.35f) * alpha;
                Utils.DrawBorderString(spriteBatch, levelText, textPos, textColor, textScale);

                //底部等级指示条（细长色条，当前等级高亮）
                float indicatorY = centerRect.Bottom - 3f * scale;
                float indicatorHeight = 2f * scale;
                float totalIndicatorWidth = centerRect.Width - 8f * scale;
                float segWidth = totalIndicatorWidth / DensityLevelCount;
                float segStartX = centerRect.X + 4f * scale;

                for (int i = 0; i < DensityLevelCount; i++) {
                    Rectangle segRect = new(
                        (int)(segStartX + i * segWidth + 1),
                        (int)indicatorY,
                        (int)(segWidth - 2),
                        (int)indicatorHeight);

                    Color segColor = i <= level
                        ? DensityLevelColors[i] * (alpha * 0.8f)
                        : new Color(40, 18, 18) * (alpha * 0.4f);
                    spriteBatch.Draw(pixel, segRect, new Rectangle(0, 0, 1, 1), segColor);
                }
            }
        }

        private static void DrawSimpleBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), new Rectangle(0, 0, 1, 1), color);
        }

        private static void SaveConfig() {
            if (CWRServerConfig.Instance == null) return;
            CWRServerConfig.Instance.OnChanged();
            ContentSettingsCategory.SaveConfigStatic();
        }
    }
}


