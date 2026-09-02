using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace CalamityOverhaul.OtherMods.Calamity
{
    /// <summary>
    /// 灾厄特效文字黑屏修复。<br/>
    /// 燃金 / 异域彩虹稀有度、[ceffect/dog] 标签与疲惫尾巴的 <see cref="TextSnippet.UniqueDraw"/>
    /// 会租用一张全屏渲染目标并在提示框绘制期间切换 RenderTarget。提示框此时绘制在后备缓冲上，
    /// 而后备缓冲的 RenderTargetUsage 是 FNA 默认的 DiscardContents，重绑回后备缓冲时 FNA 会把它清成纯黑，
    /// 这一帧此前画好的全部画面随之丢失，表现为一悬停就黑屏。<br/>
    /// 这里接管这四个 UniqueDraw，用当前 SpriteBatch 直接叠画出等效效果（描边 / 高光 / 加色光晕），
    /// 全程不触碰渲染目标；顺带修正原实现在非 100% UI 缩放下位置被二次变换的错位，并省掉每帧全屏 RT 的分配释放
    /// </summary>
    internal sealed class CalamityTextEffectFix : CalamityPatchBase
    {
        private delegate bool UniqueDrawOrig(object self, bool justCheckingString, out Vector2 size,
            SpriteBatch spriteBatch, Vector2 position, Color color, float scale);
        private delegate bool UniqueDrawHook(UniqueDrawOrig orig, object self, bool justCheckingString, out Vector2 size,
            SpriteBatch spriteBatch, Vector2 position, Color color, float scale);

        private const BindingFlags DeclaredInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        //主构造函数捕获的 text 参数会被编译为私有字符串字段，按类型缓存
        private static FieldInfo auricTextField;
        private static FieldInfo exoTextField;
        private static FieldInfo dogTextField;
        private static FieldInfo tiredTailTextField;

        //BurnishedAuric.isFlashing / TextClr，ExoticRainbow.CustomTextSnippet.IsExpert，TiredTailTextEffects.expansionFactor
        private static FieldInfo auricFlashingField;
        private static FieldInfo exoExpertField;
        private static FieldInfo tiredTailExpansionField;

        private static Color auricTextColor = new(157, 110, 11, 255);
        private static readonly Color HotPinkTextColor = new(255, 0, 255);

        //异域彩虹调色板：Ares / Thanatos / Apollo；专家物品换为六色彩虹
        private static readonly Color[] ExoPalette = [
            new(255, 107, 107),
            new(125, 196, 225),
            new(211, 235, 108),
        ];
        private static readonly Color[] ExoExpertPalette = [
            new(255, 70, 70),
            new(255, 70, 255),
            new(70, 70, 255),
            new(70, 255, 255),
            new(70, 255, 90),
            new(255, 255, 70),
        ];

        private const int OutlineDirections = 8;
        private const int ExoGlowCopies = 16;

        private static bool Enabled => CWRClientConfig.Instance?.CalamityRarityTextFix ?? true;

        protected override bool Install(Mod calamity) {
            int hooked = 0;

            Type auricType = FindType(calamity, "CalamityMod.Rarities.BurnishedAuric");
            Type auricSnippet = FindType(calamity, "CalamityMod.Rarities.BurnishedAuric+CustomTextSnippet");
            if (auricSnippet != null) {
                auricFlashingField = FindField(auricType, "isFlashing", BindingFlags.NonPublic | BindingFlags.Static);
                FieldInfo textClr = auricType?.GetField("TextClr", BindingFlags.Public | BindingFlags.Static);
                if (textClr?.GetValue(null) is Color clr) {
                    auricTextColor = clr;
                }
                if (TryHookSnippet(auricSnippet, new UniqueDrawHook(OnBurnishedAuricDraw), out auricTextField)) {
                    hooked++;
                }
            }

            Type exoSnippet = FindType(calamity, "CalamityMod.Rarities.ExoticRainbow+CustomTextSnippet");
            if (exoSnippet != null) {
                exoExpertField = exoSnippet.GetField("IsExpert", BindingFlags.Public | BindingFlags.Instance);
                if (TryHookSnippet(exoSnippet, new UniqueDrawHook(OnExoticRainbowDraw), out exoTextField)) {
                    hooked++;
                }
            }

            Type dogSnippet = FindType(calamity, "CalamityMod.ChatTags.DoGTextSnippet");
            if (dogSnippet != null && TryHookSnippet(dogSnippet, new UniqueDrawHook(OnDoGDraw), out dogTextField)) {
                hooked++;
            }

            Type tiredTailSnippet = FindType(calamity, "CalamityMod.Items.Accessories.Wings.TiredTailTextEffects");
            if (tiredTailSnippet != null) {
                tiredTailExpansionField = tiredTailSnippet.GetField("expansionFactor", BindingFlags.Public | BindingFlags.Static);
                if (TryHookSnippet(tiredTailSnippet, new UniqueDrawHook(OnTiredTailDraw), out tiredTailTextField)) {
                    hooked++;
                }
            }

            return hooked > 0;
        }

        protected override void Cleanup() {
            auricTextField = exoTextField = dogTextField = tiredTailTextField = null;
            auricFlashingField = exoExpertField = tiredTailExpansionField = null;
        }

        private bool TryHookSnippet(Type snippetType, UniqueDrawHook hook, out FieldInfo textField) {
            textField = FindTextField(snippetType);
            if (textField == null) {
                CWRMod.Instance.Logger.Warn($"{LogName}: text field of {snippetType.Name} not found, this snippet is left untouched.");
                return false;
            }
            MethodInfo uniqueDraw = FindMethod(snippetType, nameof(TextSnippet.UniqueDraw), DeclaredInstance);
            try {
                return Hook(uniqueDraw, hook);
            } catch (Exception ex) {
                //单个片段挂钩失败不连坐其余片段
                CWRMod.Instance.Logger.Warn($"{LogName}: hooking {snippetType.Name}.UniqueDraw failed. {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static FieldInfo FindTextField(Type snippetType) {
            FieldInfo fallback = null;
            foreach (FieldInfo field in snippetType.GetFields(DeclaredInstance)) {
                if (field.FieldType != typeof(string)) {
                    continue;
                }
                if (field.Name.Contains("text", StringComparison.OrdinalIgnoreCase)) {
                    return field;
                }
                fallback ??= field;
            }
            return fallback;
        }

        #region 通用
        //主构造函数捕获字段优先；灾厄把基类 Text 传成空串，仅在捕获字段缺失时才退回基类字段
        private static bool TryGetText(object self, FieldInfo textField, out string text) {
            text = null;
            if (self == null) {
                return false;
            }
            if (textField != null) {
                text = textField.GetValue(self) as string;
            }
            if (string.IsNullOrEmpty(text) && self is TextSnippet snippet && !string.IsNullOrEmpty(snippet.Text)) {
                text = snippet.Text;
            }
            return text != null;
        }

        private static bool ReadStaticBool(FieldInfo field) => field?.GetValue(null) is bool value && value;

        private static float ReadStaticFloat(FieldInfo field) => field?.GetValue(null) is float value ? value : 0f;

        private static bool IsBlack(Color color) => color.R == 0 && color.G == 0 && color.B == 0;

        private static Vector2 MeasureLine(DynamicSpriteFont font, string text, float scale)
            => new Vector2(font.MeasureString(text).X, font.MeasureString(" ").Y) * scale;

        //逐字前缀宽度，沿用原实现的整段前缀测量以保留字距
        private static float PrefixWidth(DynamicSpriteFont font, string text, int index, float scale)
            => index <= 0 ? 0f : font.MeasureString(text[..index]).X * scale;

        private static void DrawOutline(SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 position, Color color, Vector2 scale) {
            for (int i = 0; i < OutlineDirections; i++) {
                Vector2 offset = new Vector2(2f, 0f).RotatedBy(MathHelper.TwoPi * i / OutlineDirections);
                spriteBatch.DrawString(font, text, position + offset, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
        #endregion

        #region 燃金
        private static bool OnBurnishedAuricDraw(UniqueDrawOrig orig, object self, bool justCheckingString, out Vector2 size,
            SpriteBatch spriteBatch, Vector2 position, Color color, float scale) {
            if (!Enabled || !TryGetText(self, auricTextField, out string text)) {
                return orig(self, justCheckingString, out size, spriteBatch, position, color, scale);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            size = MeasureLine(font, text, scale);
            if (color == default || color == Main.MouseTextColorReal) {
                color = Colors.AlphaDarken(auricTextColor);
            }
            if (justCheckingString || IsBlack(color) || spriteBatch == null) {
                return true;
            }

            Color borderColor = color * 2f;
            Color coreColor = new(77, 0, 33);
            Color shineColor = new(254, 231, 117);
            if (ReadStaticBool(auricFlashingField)) {
                shineColor = new Color(90, 207, 255);
                position += Main.rand.NextVector2Circular(8f, 4.8f);
            }

            Vector2 scaleVec = new(scale);
            DrawOutline(spriteBatch, font, text, position, borderColor, scaleVec);
            spriteBatch.DrawString(font, text, position, coreColor, 0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);

            //高光逐字扫过，pow(sin,120) 只在极窄区间可见，其余字符直接跳过
            float time = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < text.Length; i++) {
                float charX = position.X + PrefixWidth(font, text, i, scale);
                float sin = (MathF.Sin(charX * 0.02f + time * -1.5f) + 1f) * 0.5f;
                float strength = MathF.Pow(sin, 120f);
                if (strength < 1f / 255f) {
                    continue;
                }
                spriteBatch.DrawString(font, text[i].ToString(), new Vector2(charX, position.Y), shineColor * strength,
                    0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
            }
            return true;
        }
        #endregion

        #region 异域彩虹
        private static bool OnExoticRainbowDraw(UniqueDrawOrig orig, object self, bool justCheckingString, out Vector2 size,
            SpriteBatch spriteBatch, Vector2 position, Color color, float scale) {
            if (!Enabled || !TryGetText(self, exoTextField, out string text)) {
                return orig(self, justCheckingString, out size, spriteBatch, position, color, scale);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            size = MeasureLine(font, text, scale);
            if (justCheckingString || IsBlack(color) || spriteBatch == null || text.Length == 0) {
                return true;
            }

            bool expert = exoExpertField != null && exoExpertField.GetValue(self) is bool b && b;
            Color[] palette = expert ? ExoExpertPalette : ExoPalette;
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 scaleVec = new(scale);

            int count = text.Length;
            string[] glyphs = new string[count];
            Vector2[] positions = new Vector2[count];
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++) {
                float charX = position.X + PrefixWidth(font, text, i, scale);
                glyphs[i] = text[i].ToString();
                positions[i] = new Vector2(charX, position.Y);

                //色相按屏幕 X 与时间步进，并像原实现一样在半周期处跳变而非平滑过渡
                float rate = time * (expert ? 2f : 1f) + charX * (expert ? 0.01f : 0.005f);
                int index = (int)(rate / 2f % palette.Length);
                Color current = palette[index];
                Color next = palette[(index + 1) % palette.Length];
                colors[i] = Color.Lerp(current, next, rate % 2f > 1f ? 1f : MathF.Round(rate % 1f));
            }

            //环绕光晕：A=0 在预乘 AlphaBlend 下等价于加色混合，无需切换 BlendState
            float sine = MathF.Sin(time * 2f / MathHelper.Pi);
            sine = MathF.Pow(MathHelper.Lerp(sine, 0f, 0.35f), 5f);
            float radius = 4f + 16f * sine;
            for (int k = 0; k < ExoGlowCopies; k++) {
                Vector2 ring = (MathHelper.TwoPi * k / ExoGlowCopies + time * 1.7f).ToRotationVector2() * radius;
                for (int i = 0; i < count; i++) {
                    spriteBatch.DrawString(font, glyphs[i], positions[i] + ring, colors[i] with { A = 0 },
                        0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
                }
            }

            DrawOutline(spriteBatch, font, text, position, Color.Black, scaleVec);

            for (int i = 0; i < count; i++) {
                spriteBatch.DrawString(font, glyphs[i], positions[i], colors[i], 0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
            }
            return true;
        }
        #endregion

        #region [ceffect/dog]
        private static bool OnDoGDraw(UniqueDrawOrig orig, object self, bool justCheckingString, out Vector2 size,
            SpriteBatch spriteBatch, Vector2 position, Color color, float scale) {
            if (!Enabled || !TryGetText(self, dogTextField, out string text)) {
                return orig(self, justCheckingString, out size, spriteBatch, position, color, scale);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            size = MeasureLine(font, text, scale);
            if (justCheckingString || IsBlack(color) || spriteBatch == null || text.Length == 0) {
                return true;
            }

            float time = Main.GlobalTimeWrappedHourly;
            Vector2 scaleVec = new(scale);

            int count = text.Length;
            string[] glyphs = new string[count];
            Vector2[] positions = new Vector2[count];
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++) {
                float charX = position.X + PrefixWidth(font, text, i, scale);
                float sin = MathHelper.SmoothStep(0f, 1f, (MathF.Sin(charX * 0.02f + time * -1.5f) + 1f) * 0.5f);
                glyphs[i] = text[i].ToString();
                positions[i] = new Vector2(charX, position.Y - 2f + sin * 4f);
                colors[i] = Color.Lerp(Color.Cyan, Color.Fuchsia, sin);
            }

            foreach (Vector2 direction in ChatManager.ShadowDirections) {
                Vector2 offset = direction * 2f;
                for (int i = 0; i < count; i++) {
                    spriteBatch.DrawString(font, glyphs[i], positions[i] + offset, Color.Black, 0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
                }
            }
            for (int i = 0; i < count; i++) {
                spriteBatch.DrawString(font, glyphs[i], positions[i], colors[i], 0f, Vector2.Zero, scaleVec, SpriteEffects.None, 0f);
            }
            return true;
        }
        #endregion

        #region 疲惫尾巴
        private static bool OnTiredTailDraw(UniqueDrawOrig orig, object self, bool justCheckingString, out Vector2 size,
            SpriteBatch spriteBatch, Vector2 position, Color color, float scale) {
            if (!Enabled || !TryGetText(self, tiredTailTextField, out string text)) {
                return orig(self, justCheckingString, out size, spriteBatch, position, color, scale);
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            if (color == default || color == Main.MouseTextColorReal) {
                color = Colors.AlphaDarken(HotPinkTextColor);
            }

            //展开进度推进后前缀字符逐个换成符号，尺寸按替换后的文本计算
            float expansion = ReadStaticFloat(tiredTailExpansionField);
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                if (expansion - 10f > i) {
                    chars[i] = i == 0 ? 'ɔ' : '»';
                }
            }
            string textToDraw = new(chars);
            size = font.MeasureString(textToDraw) * scale;
            if (justCheckingString || IsBlack(color) || spriteBatch == null || chars.Length == 0) {
                return true;
            }

            float time = Main.GlobalTimeWrappedHourly;
            Color baseTint = Colors.AlphaDarken(new Color(0, 255, 200));
            float expandLerp = MathHelper.Clamp(expansion - 2f, 0f, 1f);
            float posMult = MathF.Max(MathHelper.Clamp((expansion - 10f) * 0.5f, 0f, 3f), MathHelper.Clamp((expansion - 2f) * 0.5f, 0f, 1f));
            Vector2 scaleVec = new(scale);

            int count = chars.Length;
            string[] glyphs = new string[count];
            Vector2[] positions = new Vector2[count];
            Vector2[] origins = new Vector2[count];
            float[] rotations = new float[count];
            Color[] colors = new Color[count];
            for (int i = 0; i < count; i++) {
                float charX = position.X + PrefixWidth(font, textToDraw, i, scale);
                float sin = (MathF.Sin(charX * 0.02f + time * -1.5f) + 1f) * 0.5f;
                float sin2 = (MathF.Sin(charX * 0.02f + time * -0.9f) + 1f) * 0.5f;
                float sin3 = MathF.Sin(charX * 0.02f + time * -1.5f + MathHelper.PiOver2);

                Color tint = new(171, 153, 204);
                if (i == 0 || i == count - 1) {
                    tint = Color.Cyan;
                }
                else if (i % 4 == 3) {
                    tint = Color.HotPink;
                }

                glyphs[i] = chars[i].ToString();
                origins[i] = font.MeasureString(glyphs[i]) * 0.5f;
                positions[i] = origins[i] + new Vector2(charX, position.Y)
                    + new Vector2(0f, chars[i] == 'ɔ' ? -1f : 0f)
                    + new Vector2((-2f + 4f * sin2) * posMult, (-2f + sin * 4f) * posMult);
                rotations[i] = sin3 * posMult * 0.1f;
                colors[i] = Color.Lerp(baseTint, tint, expandLerp);
            }

            foreach (Vector2 direction in ChatManager.ShadowDirections) {
                Vector2 offset = direction * 2f;
                for (int i = 0; i < count; i++) {
                    spriteBatch.DrawString(font, glyphs[i], positions[i] + offset, Color.Black, rotations[i], origins[i], scaleVec, SpriteEffects.None, 0f);
                }
            }
            for (int i = 0; i < count; i++) {
                spriteBatch.DrawString(font, glyphs[i], positions[i], colors[i], rotations[i], origins[i], scaleVec, SpriteEffects.None, 0f);
            }
            return true;
        }
        #endregion
    }
}
