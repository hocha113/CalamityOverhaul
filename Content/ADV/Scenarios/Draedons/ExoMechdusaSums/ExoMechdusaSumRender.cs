using CalamityOverhaul.Content.ADV.ADVChoices;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Text;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums
{
    internal class ExoMechdusaSumRender : RenderHandle, ILocalizedModType
    {
        //用于记录上次悬停的选项，避免重复播放音效
        private static int lastHoveredChoice = -1;

        //三个机甲图标的悬停音效
        public static readonly SoundStyle ThanatosIconHover = new("CalamityMod/Sounds/Custom/Codebreaker/ThanatosIconHover");
        public static readonly SoundStyle AresIconHover = new("CalamityMod/Sounds/Custom/Codebreaker/AresIconHover");
        public static readonly SoundStyle ArtemisApolloIconHover = new("CalamityMod/Sounds/Custom/Codebreaker/ArtemisApolloIconHover");

        //反射加载纹理，对应三种机甲图标
        [VaultLoaden("@CalamityMod/UI/DraedonSummoning/")]
        public static Texture2D HeadIcon_THanos = null;
        [VaultLoaden("@CalamityMod/UI/DraedonSummoning/")]
        public static Texture2D HeadIcon_Ares = null;
        [VaultLoaden("@CalamityMod/UI/DraedonSummoning/")]
        public static Texture2D HeadIcon_ArtemisApollo = null;

        //本地化文本
        public static LocalizedText ThanatosDescription { get; private set; }
        public static LocalizedText AresDescription { get; private set; }
        public static LocalizedText ArtemisApolloDescription { get; private set; }

        //主图标动画状态（顶部）
        private static int currentMainIcon = -1;
        private static int targetMainIcon = -1;
        private static float mainIconFade = 0f;
        private static float mainIconScale = 0f;
        private static float mainIconRotation = 0f;
        private static float mainIconGlow = 0f;

        //侧边图标动画状态（左下和右下）
        private static float[] sideIconFade = new float[2];
        private static float[] sideIconScale = new float[2];
        private static float[] sideIconRotation = new float[2];

        //文本解码动画状态
        private static float textDecodeProgress = 0f;
        private static string decodedText = "";
        private static string targetText = "";
        private static float textFadeProgress = 0f;
        private static float[] charDecodeProgress;
        private static int decodeUpdateTimer = 0;

        //动画参数
        private const float FadeSpeed = 0.08f;
        private const float ScaleSpeed = 0.12f;
        private const float TextDecodeSpeed = 0.03f;
        private const float TextFadeSpeed = 0.04f;
        private const float MainIconBaseScale = 2.2f;
        private const float MainIconMaxScale = 3.6f;
        private const float SideIconBaseScale = 1.2f;
        private const float SideIconMaxScale = 1.8f;
        private const int GlitchUpdateInterval = 2;

        //图标位置偏移
        private readonly static Vector2 mainIconOffset = new Vector2(0, -120f);
        private readonly static Vector2 textOffset = new Vector2(0, -100f);
        private readonly static Vector2 leftSideIconOffset = new Vector2(-180f, 0f);
        private readonly static Vector2 rightSideIconOffset = new Vector2(180f, 0f);

        //科技光效粒子
        private static readonly List<TechParticle> techParticles = new();
        private static int particleSpawnTimer = 0;

        //用于乱码生成的字符集
        private static readonly char[] glitchChars = "█▓▒░▄▀■□▪▫◘◙◚◛◜◝◞◟●○◎◯⊕⊗⊙⊛⊠⊡⌂▬▭▮▯┼┴┬┤├┌┐└┘╳╱╲╬╪╫╩╦╠╣╔╗╚╝║═╞╡╟╢╖╓╙╜╛╘╒╕╤╧╨╥╙╟╢01ABCDEFX".ToCharArray();

        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            ThanatosDescription = this.GetLocalization(nameof(ThanatosDescription),
                () => "塔纳托斯，一条装备着厚重铠甲、搭载了无数机关炮的恐怖巨蟒");
            AresDescription = this.GetLocalization(nameof(AresDescription),
                () => "阿瑞斯，一个搭载着四台超级星流武器的庞然巨物");
            ArtemisApolloDescription = this.GetLocalization(nameof(ArtemisApolloDescription),
                () => "阿尔忒弥斯和阿波罗，一对能量储备十分不稳定的超耐久自动机器");
        }

        public override void UpdateBySystem(int index) {
            UpdateMainIconAnimation();
            UpdateSideIconsAnimation();
            UpdateTextDecoding();
            UpdateTechParticles();
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (currentMainIcon >= 0 || sideIconFade[0] > 0.01f || sideIconFade[1] > 0.01f) {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                DrawSideIcons(spriteBatch, main);
                DrawMainIcon(spriteBatch, main);
                DrawMechDescription(spriteBatch, main);

                Main.spriteBatch.End();
            }
        }

        /// <summary>
        /// 更新主图标动画状态
        /// </summary>
        private static void UpdateMainIconAnimation() {
            mainIconRotation = 0f;

            if (targetMainIcon != currentMainIcon) {
                if (currentMainIcon >= 0 && mainIconFade > 0f) {
                    mainIconFade -= FadeSpeed * 1.5f;
                    mainIconScale -= ScaleSpeed * 1.2f;

                    if (mainIconFade <= 0f) {
                        currentMainIcon = targetMainIcon;
                        mainIconFade = 0f;
                        mainIconScale = 0f;
                        mainIconRotation = 0f;
                        ResetTextAnimation();
                    }
                }
                else {
                    currentMainIcon = targetMainIcon;
                    mainIconFade = 0f;
                    mainIconScale = 0f;
                    mainIconRotation = 0f;
                    ResetTextAnimation();
                }
            }

            if (currentMainIcon >= 0 && currentMainIcon == targetMainIcon) {
                mainIconFade = Math.Min(mainIconFade + FadeSpeed, 1f);
                mainIconScale = Math.Min(mainIconScale + ScaleSpeed, 1f);
            }

            if (currentMainIcon >= 0) {
                mainIconGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;
            }

            if (currentMainIcon >= 0 && mainIconFade > 0.5f) {
                particleSpawnTimer++;
                if (particleSpawnTimer >= 3) {
                    particleSpawnTimer = 0;
                    SpawnTechParticle(mainIconOffset);
                }
            }
        }

        /// <summary>
        /// 更新侧边图标动画状态
        /// </summary>
        private static void UpdateSideIconsAnimation() {
            if (currentMainIcon < 0) {
                //没有选中任何选项，淡出所有侧边图标
                for (int i = 0; i < 2; i++) {
                    sideIconFade[i] = Math.Max(sideIconFade[i] - FadeSpeed * 1.5f, 0f);
                    sideIconScale[i] = Math.Max(sideIconScale[i] - ScaleSpeed * 1.2f, 0f);
                }
                return;
            }

            for (int i = 0; i < 2; i++) {
                //淡入侧边图标
                sideIconFade[i] = Math.Min(sideIconFade[i] + FadeSpeed * 0.8f, 0.7f);
                sideIconScale[i] = Math.Min(sideIconScale[i] + ScaleSpeed * 0.8f, 1f);
                sideIconRotation[i] = 0f;//别转
            }
        }

        /// <summary>
        /// 获取其他两个图标的索引
        /// </summary>
        private static int[] GetOtherIconIndices(int mainIndex) {
            List<int> indices = new List<int>();
            for (int i = 0; i < 3; i++) {
                if (i != mainIndex) {
                    indices.Add(i);
                }
            }
            return indices.ToArray();
        }

        /// <summary>
        /// 重置文本动画
        /// </summary>
        private static void ResetTextAnimation() {
            textDecodeProgress = 0f;
            textFadeProgress = 0f;
            decodedText = "";
            targetText = GetDescriptionText(currentMainIcon);
            decodeUpdateTimer = 0;

            if (!string.IsNullOrEmpty(targetText)) {
                charDecodeProgress = new float[targetText.Length];
                for (int i = 0; i < charDecodeProgress.Length; i++) {
                    charDecodeProgress[i] = 0f;
                }
            }
        }

        /// <summary>
        /// 更新文本解码动画
        /// </summary>
        private static void UpdateTextDecoding() {
            if (currentMainIcon < 0 || mainIconFade < 0.5f) {
                return;
            }

            textFadeProgress = Math.Min(textFadeProgress + TextFadeSpeed, 1f);

            if (textDecodeProgress < 1f) {
                textDecodeProgress = Math.Min(textDecodeProgress + TextDecodeSpeed, 1f);

                if (charDecodeProgress != null) {
                    for (int i = 0; i < charDecodeProgress.Length; i++) {
                        float charStartProgress = (float)i / charDecodeProgress.Length;
                        if (textDecodeProgress > charStartProgress) {
                            float localProgress = (textDecodeProgress - charStartProgress) / (1f - charStartProgress);
                            charDecodeProgress[i] = Math.Min(localProgress, 1f);
                        }
                    }
                }

                decodeUpdateTimer++;
                if (decodeUpdateTimer >= GlitchUpdateInterval) {
                    decodeUpdateTimer = 0;
                    UpdateDecodedText();
                }
            }
            else if (decodedText != targetText) {
                decodedText = targetText;
            }
        }

        /// <summary>
        /// 更新解码文本（带乱码效果）
        /// </summary>
        private static void UpdateDecodedText() {
            if (string.IsNullOrEmpty(targetText)) {
                decodedText = "";
                return;
            }

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < targetText.Length; i++) {
                if (charDecodeProgress == null || i >= charDecodeProgress.Length) {
                    continue;
                }

                float charProgress = charDecodeProgress[i];

                if (charProgress >= 0.9f) {
                    //字符已完全解码，哈哈哈哈哈哈我看懂啦道爷我成啦哈哈哈哈哈哈哈哈哈
                    sb.Append(targetText[i]);
                }
                else if (charProgress > 0.1f) {
                    //解码中，显示多层乱码效果
                    if (charProgress > 0.7f) {
                        //接近完成，偶尔显示真实字符，嘿你看得懂了吗，嘉登牌翻译器，用了都骂逼养的
                        if (Main.rand.NextBool(3)) {
                            sb.Append(targetText[i]);
                        }
                        else {
                            //使用相似度更高的乱码
                            sb.Append(glitchChars[Main.rand.Next(glitchChars.Length / 2)]);
                        }
                    }
                    else if (charProgress > 0.4f) {
                        //中期，使用中等密度乱码，偶尔显示真实字符，初具人形这块
                        if (Main.rand.NextBool(4)) {
                            sb.Append(targetText[i]);
                        }
                        else {
                            sb.Append(glitchChars[Main.rand.Next(glitchChars.Length)]);
                        }
                    }
                    else {
                        //初期，完全随机乱码，偶尔空格，but，肯定还是看不懂滴
                        if (Main.rand.NextBool(2)) {
                            sb.Append(glitchChars[Main.rand.Next(glitchChars.Length)]);
                        }
                        else {
                            sb.Append(' ');
                        }
                    }
                }
                else {
                    //尚未开始解码，全是火星文噜噜噜噜
                    if (Main.rand.NextBool(4)) {
                        sb.Append(glitchChars[Main.rand.Next(glitchChars.Length)]);
                    }
                    else {
                        sb.Append(' ');
                    }
                }
            }

            decodedText = sb.ToString();
        }

        /// <summary>
        /// 获取描述文本
        /// </summary>
        private static string GetDescriptionText(int index) {
            return index switch {
                0 => AresDescription?.Value ?? "",
                1 => ThanatosDescription?.Value ?? "",
                2 => ArtemisApolloDescription?.Value ?? "",
                _ => ""
            };
        }

        /// <summary>
        /// 绘制主图标（顶部）
        /// </summary>
        private static void DrawMainIcon(SpriteBatch spriteBatch, Main main) {
            if (currentMainIcon < 0 || mainIconFade < 0.01f) return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            Texture2D iconTexture = GetIconTexture(currentMainIcon);
            if (iconTexture == null) return;

            Vector2 worldPos = player.Center + mainIconOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            float easedScale = CWRUtils.EaseOutBack(mainIconScale);
            float scale = MathHelper.Lerp(MainIconBaseScale, MainIconMaxScale, easedScale);
            float alpha = mainIconFade * 0.85f;
            Color iconColor = GetIconColor(currentMainIcon);

            //绘制外层光晕
            for (int i = 0; i < 3; i++) {
                float glowScale = scale * (1.3f + i * 0.15f);
                float glowAlpha = alpha * (0.3f - i * 0.08f) * mainIconGlow;
                spriteBatch.Draw(
                    iconTexture,
                    screenPos,
                    null,
                    iconColor * glowAlpha,
                    mainIconRotation * 0.5f,
                    iconTexture.Size() * 0.5f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            DrawScanLines(spriteBatch, screenPos, iconTexture.Size() * scale, alpha, iconColor);

            //绘制主图标
            spriteBatch.Draw(
                iconTexture,
                screenPos,
                null,
                Color.White * alpha,
                mainIconRotation * 0.3f,
                iconTexture.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f
            );

            //绘制内层高光
            float highlightAlpha = alpha * mainIconGlow * 0.6f;
            spriteBatch.Draw(
                iconTexture,
                screenPos,
                null,
                Color.White * highlightAlpha,
                mainIconRotation * 0.3f,
                iconTexture.Size() * 0.5f,
                scale * 0.9f,
                SpriteEffects.None,
                0f
            );

            DrawTechParticles(spriteBatch, screenPos);
        }

        /// <summary>
        /// 绘制侧边图标（左下和右下）
        /// </summary>
        private static void DrawSideIcons(SpriteBatch spriteBatch, Main main) {
            if (currentMainIcon < 0) return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            int[] otherIndices = GetOtherIconIndices(currentMainIcon);
            Vector2[] sideOffsets = [leftSideIconOffset, rightSideIconOffset];

            for (int i = 0; i < 2; i++) {
                if (sideIconFade[i] < 0.01f) continue;

                int iconIndex = otherIndices[i];
                Texture2D iconTexture = GetIconTexture(iconIndex);
                if (iconTexture == null) continue;

                Vector2 worldPos = player.Center + sideOffsets[i];
                Vector2 screenPos = worldPos - Main.screenPosition;

                float easedScale = CWRUtils.EaseOutBack(sideIconScale[i]);
                float scale = MathHelper.Lerp(SideIconBaseScale, SideIconMaxScale, easedScale);
                float alpha = sideIconFade[i];
                Color iconColor = GetIconColor(iconIndex);

                //绘制光晕
                for (int j = 0; j < 2; j++) {
                    float glowScale = scale * (1.2f + j * 0.15f);
                    float glowAlpha = alpha * (0.25f - j * 0.1f);
                    spriteBatch.Draw(
                        iconTexture,
                        screenPos,
                        null,
                        iconColor * glowAlpha,
                        sideIconRotation[i],
                        iconTexture.Size() * 0.5f,
                        glowScale,
                        SpriteEffects.None,
                        0f
                    );
                }

                //绘制主体
                spriteBatch.Draw(
                    iconTexture,
                    screenPos,
                    null,
                    Color.White * alpha,
                    sideIconRotation[i],
                    iconTexture.Size() * 0.5f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制机甲描述文本
        /// </summary>
        private static void DrawMechDescription(SpriteBatch spriteBatch, Main main) {
            if (string.IsNullOrEmpty(decodedText) || textFadeProgress < 0.01f) {
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            Vector2 worldPos = player.Center + mainIconOffset + textOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            float textScale = 1.5f;
            Vector2 textSize = font.MeasureString(decodedText) * textScale;
            Vector2 textPos = screenPos - new Vector2(textSize.X * 0.5f, textSize.Y);

            float alpha = textFadeProgress * mainIconFade * 0.9f;
            Color iconColor = GetIconColor(currentMainIcon);

            DrawTextBackground(spriteBatch, textPos, textSize, alpha, iconColor);
            DrawTextScanLines(spriteBatch, textPos, textSize, alpha, iconColor);
            DrawTextNoise(spriteBatch, textPos, textSize, alpha);

            //绘制文本光晕
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * (2f * textScale);
                Utils.DrawBorderString(spriteBatch, decodedText, textPos + offset,
                    iconColor * (alpha * 0.4f), textScale);
            }

            Color textColor = Color.Lerp(Color.White, iconColor, 0.3f);
            Utils.DrawBorderString(spriteBatch, decodedText, textPos, textColor * alpha, textScale);
        }

        /// <summary>
        /// 绘制文本背景
        /// </summary>
        private static void DrawTextBackground(SpriteBatch sb, Vector2 pos, Vector2 size, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Rectangle bgRect = new Rectangle(
                (int)(pos.X - 15),
                (int)(pos.Y - 10),
                (int)(size.X + 30),
                (int)(size.Y + 20)
            );

            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), new Color(10, 15, 25) * (alpha * 0.85f));

            Color edgeColor = color * (alpha * 0.6f);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, 3), edgeColor);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Bottom - 3, bgRect.Width, 3), edgeColor * 0.7f);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, 3, bgRect.Height), edgeColor * 0.85f);
            sb.Draw(pixel, new Rectangle(bgRect.Right - 3, bgRect.Y, 3, bgRect.Height), edgeColor * 0.85f);
        }

        /// <summary>
        /// 绘制文本扫描线
        /// </summary>
        private static void DrawTextScanLines(SpriteBatch sb, Vector2 pos, Vector2 size, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float scanSpeed = Main.GlobalTimeWrappedHourly * 3f;
            int lineCount = 4;

            for (int i = 0; i < lineCount; i++) {
                float lineProgress = (scanSpeed + i * 0.25f) % 1f;
                float lineY = pos.Y + size.Y * lineProgress;

                float lineAlpha = (float)Math.Sin(lineProgress * MathHelper.Pi) * alpha * 0.3f;
                Color lineColor = color * lineAlpha;

                sb.Draw(
                    pixel,
                    new Vector2(pos.X - 12, lineY),
                    new Rectangle(0, 0, 1, 1),
                    lineColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(size.X + 24, 2f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制文本噪点效果
        /// </summary>
        private static void DrawTextNoise(SpriteBatch sb, Vector2 pos, Vector2 size, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            //随机噪点
            for (int i = 0; i < 12; i++) {
                if (Main.rand.NextBool(3)) {
                    Vector2 noisePos = pos + new Vector2(
                        Main.rand.NextFloat(size.X),
                        Main.rand.NextFloat(size.Y)
                    );

                    float noiseSize = Main.rand.NextFloat(1f, 2.5f);
                    Color noiseColor = new Color(100, 200, 255) * (alpha * 0.3f);

                    sb.Draw(
                        pixel,
                        noisePos,
                        new Rectangle(0, 0, 1, 1),
                        noiseColor,
                        0f,
                        new Vector2(0.5f),
                        noiseSize,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        /// <summary>
        /// 绘制扫描线效果
        /// </summary>
        private static void DrawScanLines(SpriteBatch spriteBatch, Vector2 center, Vector2 size, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float scanSpeed = Main.GlobalTimeWrappedHourly * 2f;
            int lineCount = 5;

            for (int i = 0; i < lineCount; i++) {
                float lineProgress = (scanSpeed + i * 0.2f) % 1f;
                float lineY = center.Y - size.Y * 0.5f + size.Y * lineProgress;

                float lineAlpha = (float)Math.Sin(lineProgress * MathHelper.Pi) * alpha * 0.4f;
                Color lineColor = color * lineAlpha;

                spriteBatch.Draw(
                    pixel,
                    new Vector2(center.X - size.X * 0.5f, lineY),
                    new Rectangle(0, 0, 1, 1),
                    lineColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(size.X, 2f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 获取图标纹理
        /// </summary>
        private static Texture2D GetIconTexture(int index) {
            return index switch {
                0 => HeadIcon_Ares,
                1 => HeadIcon_THanos,
                2 => HeadIcon_ArtemisApollo,
                _ => null
            };
        }

        /// <summary>
        /// 获取图标颜色
        /// </summary>
        private static Color GetIconColor(int index) {
            return index switch {
                0 => new Color(255, 80, 80),
                1 => new Color(100, 255, 150),
                2 => new Color(80, 200, 255),
                _ => Color.White
            };
        }

        #region 科技粒子系统
        private class TechParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public Color Color;

            public TechParticle(Vector2 pos, Color color) {
                Position = pos;
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(0.5f, 2f);
                Velocity = angle.ToRotationVector2() * speed;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(30f, 60f);
                Size = Main.rand.NextFloat(2f, 4f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                Color = color;
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.95f;
                Rotation += 0.05f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                if (pixel == null) return;

                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                float alpha = fade * 0.8f;

                sb.Draw(
                    pixel,
                    Position,
                    new Rectangle(0, 0, 1, 1),
                    Color * alpha,
                    Rotation,
                    new Vector2(0.5f),
                    new Vector2(Size, Size * 0.3f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private static void SpawnTechParticle(Vector2 iconOffset) {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            Vector2 worldPos = player.Center + iconOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(20f, 40f);
            Vector2 spawnPos = screenPos + angle.ToRotationVector2() * distance;

            Color particleColor = GetIconColor(currentMainIcon);
            techParticles.Add(new TechParticle(spawnPos, particleColor));
        }

        private static void UpdateTechParticles() {
            for (int i = techParticles.Count - 1; i >= 0; i--) {
                if (techParticles[i].Update()) {
                    techParticles.RemoveAt(i);
                }
            }

            while (techParticles.Count > 30) {
                techParticles.RemoveAt(0);
            }
        }

        private static void DrawTechParticles(SpriteBatch spriteBatch, Vector2 center) {
            foreach (var particle in techParticles) {
                particle.Draw(spriteBatch);
            }
        }
        #endregion

        /// <summary>
        /// 注册悬停效果，管理音效和动画
        /// </summary>
        internal static void RegisterHoverEffects() {
            lastHoveredChoice = -1;
            currentMainIcon = -1;
            targetMainIcon = -1;
            mainIconFade = 0f;
            mainIconScale = 0f;
            mainIconRotation = 0f;
            mainIconGlow = 0f;
            sideIconFade = new float[2];
            sideIconScale = new float[2];
            sideIconRotation = new float[2];
            techParticles.Clear();
            particleSpawnTimer = 0;

            ADVChoiceBox.OnHoverChanged += OnChoiceHoverChanged;
        }

        /// <summary>
        /// 处理选项悬停变化
        /// </summary>
        private static void OnChoiceHoverChanged(object sender, ChoiceHoverEventArgs e) {
            //如果悬停到新的启用选项上，避免重复触发
            if (e.CurrentIndex >= 0 && e.CurrentChoice != null && e.CurrentChoice.Enabled && e.CurrentIndex != lastHoveredChoice) {
                lastHoveredChoice = e.CurrentIndex;
                targetMainIcon = e.CurrentIndex;

                //播放对应机甲的悬停音效
                SoundStyle hoverSound = e.CurrentIndex switch {
                    0 => AresIconHover,//阿瑞斯
                    1 => ThanatosIconHover,//塔纳托斯
                    2 => ArtemisApolloIconHover,//双子
                    _ => SoundID.MenuTick
                };

                //播放音效
                if (hoverSound != SoundID.MenuTick) {
                    SoundEngine.PlaySound(hoverSound with {
                        Volume = 0.7f,
                        MaxInstances = 2
                    });
                }
                else {
                    //备用音效
                    SoundEngine.PlaySound(SoundID.MenuTick with {
                        Volume = 0.5f,
                        Pitch = 0.3f + e.CurrentIndex * 0.15f,
                        MaxInstances = 3
                    });
                }

                //播放额外的科技感音效
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.3f,
                    Pitch = 0.5f,
                    MaxInstances = 2
                });
            }

            //如果离开了选项，悬停到空白处
            if (e.CurrentIndex < 0 && e.PreviousIndex >= 0) {
                lastHoveredChoice = -1;
                targetMainIcon = -1;

                //播放离开音效
                SoundEngine.PlaySound(SoundID.MenuClose with {
                    Volume = 0.3f,
                    Pitch = -0.2f
                });
            }
        }

        /// <summary>
        /// 清理资源，当场景结束时调用
        /// </summary>
        internal static void Cleanup() {
            currentMainIcon = -1;
            targetMainIcon = -1;
            mainIconFade = 0f;
            mainIconScale = 0f;
            sideIconFade = new float[2];
            sideIconScale = new float[2];
            techParticles.Clear();
            ADVChoiceBox.OnHoverChanged -= OnChoiceHoverChanged;
        }
    }
}
