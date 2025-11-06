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

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons
{
    internal class ExoMechdusaSumRender : RenderHandle, ILocalizedModType
    {
        //用于记录上次悬停的选项，避免重复播放音效
        private static int lastHoveredChoice = -1;

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

        //图标动画状态
        private static int currentIconIndex = -1;
        private static int targetIconIndex = -1;
        private static float iconFadeProgress = 0f;
        private static float iconScaleProgress = 0f;
        private static float iconRotation = 0f;
        private static float iconGlowIntensity = 0f;

        //文本解码动画状态
        private static float textDecodeProgress = 0f;
        private static string decodedText = "";
        private static string targetText = "";
        private static float textFadeProgress = 0f;
        private static int visibleCharCount = 0;

        //动画参数
        private const float FadeSpeed = 0.08f;
        private const float ScaleSpeed = 0.12f;
        private const float TextDecodeSpeed = 0.05f;
        private const float TextFadeSpeed = 0.06f;
        private const float IconBaseScale = 2.2f;
        private const float IconMaxScale = 3.6f;

        //图标位置偏移（玩家头顶）
        private static Vector2 iconOffset = new Vector2(0, -120f);
        private static Vector2 textOffset = new Vector2(0, 60f);//文本相对图标的偏移

        //科技光效粒子
        private static readonly List<TechParticle> techParticles = new();
        private static int particleSpawnTimer = 0;

        //用于乱码生成的字符集
        private static readonly char[] glitchChars = "█▓▒░▄▀■□▪▫◘◙◚◛◜◝◞◟●○◎◯⊕⊗⊙⊛⊠⊡⌂▬▭▮▯┼┴┬┤├┌┐└┘╳╱╲╬╪╫╩╦╠╣╔╗╚╝║═╞╡╟╢╖╓╙╜╛╘╒╕╤╧╨╥╙╟╢".ToCharArray();

        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            ThanatosDescription = this.GetLocalization(nameof(ThanatosDescription), 
                () => "塔纳托斯，一条装备着厚重铠甲、搭载了无数机关炮的恐怖巨蟒。");
            AresDescription = this.GetLocalization(nameof(AresDescription), 
                () => "阿瑞斯，一个搭载着四台超级星流武器的庞然巨物。");
            ArtemisApolloDescription = this.GetLocalization(nameof(ArtemisApolloDescription), 
                () => "阿尔忒弥斯和阿波罗，一对能量储备十分不稳定的超耐久自动机器。");
        }

        public override void UpdateBySystem(int index) {
            //更新图标动画
            UpdateIconAnimation();

            //更新文本解码动画
            UpdateTextDecoding();

            //更新科技粒子
            UpdateTechParticles();
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            //绘制机甲图标和特效
            if (currentIconIndex >= 0 && iconFadeProgress > 0.01f) {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                DrawMechIcon(spriteBatch, main);
                DrawMechDescription(spriteBatch, main);
                Main.spriteBatch.End();
            }
        }

        /// <summary>
        /// 更新图标动画状态
        /// </summary>
        private static void UpdateIconAnimation() {
            iconRotation = 0f;
            //处理图标切换
            if (targetIconIndex != currentIconIndex) {
                //淡出当前图标
                if (currentIconIndex >= 0 && iconFadeProgress > 0f) {
                    iconFadeProgress -= FadeSpeed * 1.5f;
                    iconScaleProgress -= ScaleSpeed * 1.2f;

                    if (iconFadeProgress <= 0f) {
                        currentIconIndex = targetIconIndex;
                        iconFadeProgress = 0f;
                        iconScaleProgress = 0f;
                        iconRotation = 0f;
                        
                        //重置文本动画
                        ResetTextAnimation();
                    }
                }
                else {
                    //直接切换到新图标
                    currentIconIndex = targetIconIndex;
                    iconFadeProgress = 0f;
                    iconScaleProgress = 0f;
                    iconRotation = 0f;
                    
                    //重置文本动画
                    ResetTextAnimation();
                }
            }

            //淡入目标图标
            if (currentIconIndex >= 0 && currentIconIndex == targetIconIndex) {
                iconFadeProgress = Math.Min(iconFadeProgress + FadeSpeed, 1f);
                iconScaleProgress = Math.Min(iconScaleProgress + ScaleSpeed, 1f);
            }

            //持续旋转和光效脉冲
            if (currentIconIndex >= 0) {
                //光效脉冲
                iconGlowIntensity = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;
            }

            //生成科技粒子
            if (currentIconIndex >= 0 && iconFadeProgress > 0.5f) {
                particleSpawnTimer++;
                if (particleSpawnTimer >= 3) {
                    particleSpawnTimer = 0;
                    SpawnTechParticle();
                }
            }
        }

        /// <summary>
        /// 重置文本动画
        /// </summary>
        private static void ResetTextAnimation() {
            textDecodeProgress = 0f;
            textFadeProgress = 0f;
            visibleCharCount = 0;
            decodedText = "";
            targetText = GetDescriptionText(currentIconIndex);
        }

        /// <summary>
        /// 更新文本解码动画
        /// </summary>
        private static void UpdateTextDecoding() {
            if (currentIconIndex < 0 || iconFadeProgress < 0.5f) {
                return;
            }

            //文本淡入
            textFadeProgress = Math.Min(textFadeProgress + TextFadeSpeed, 1f);

            //解码进度
            if (textDecodeProgress < 1f) {
                textDecodeProgress = Math.Min(textDecodeProgress + TextDecodeSpeed, 1f);
                
                //计算可见字符数
                int targetCharCount = (int)(targetText.Length * textDecodeProgress);
                if (targetCharCount != visibleCharCount) {
                    visibleCharCount = targetCharCount;
                    UpdateDecodedText();
                }
            }
            else if (decodedText != targetText) {
                //完全解码后，直接显示完整文本
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
                if (i < visibleCharCount) {
                    //已解码的字符
                    sb.Append(targetText[i]);
                }
                else {
                    //未解码的字符显示为乱码
                    if (Main.rand.NextBool(3)) {
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
        /// 绘制机甲图标
        /// </summary>
        private static void DrawMechIcon(SpriteBatch spriteBatch, Main main) {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            //获取对应的图标纹理
            Texture2D iconTexture = GetIconTexture(currentIconIndex);
            if (iconTexture == null) return;

            //计算世界坐标位置
            Vector2 worldPos = player.Center + iconOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            //计算缩放（带缓动效果）
            float easedScale = CWRUtils.EaseOutBack(iconScaleProgress);
            float scale = MathHelper.Lerp(IconBaseScale, IconMaxScale, easedScale);

            //计算透明度
            float alpha = iconFadeProgress * 0.85f;

            //获取图标颜色
            Color iconColor = GetIconColor(currentIconIndex);

            //绘制外层光晕（多层）
            for (int i = 0; i < 3; i++) {
                float glowScale = scale * (1.3f + i * 0.15f);
                float glowAlpha = alpha * (0.3f - i * 0.08f) * iconGlowIntensity;
                spriteBatch.Draw(
                    iconTexture,
                    screenPos,
                    null,
                    iconColor * glowAlpha,
                    iconRotation * 0.5f,
                    iconTexture.Size() * 0.5f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //绘制科技扫描线效果
            DrawScanLines(spriteBatch, screenPos, iconTexture.Size() * scale, alpha, iconColor);

            //绘制主图标
            spriteBatch.Draw(
                iconTexture,
                screenPos,
                null,
                Color.White * alpha,
                iconRotation * 0.3f,
                iconTexture.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f
            );

            //绘制内层高光
            float highlightAlpha = alpha * iconGlowIntensity * 0.6f;
            spriteBatch.Draw(
                iconTexture,
                screenPos,
                null,
                Color.White * highlightAlpha,
                iconRotation * 0.3f,
                iconTexture.Size() * 0.5f,
                scale * 0.9f,
                SpriteEffects.None,
                0f
            );

            //绘制科技粒子
            DrawTechParticles(spriteBatch, screenPos);
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
            
            //计算文本位置
            Vector2 worldPos = player.Center + iconOffset + textOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            //计算文本尺寸
            Vector2 textSize = font.MeasureString(decodedText) * 0.75f;
            Vector2 textPos = screenPos - new Vector2(textSize.X * 0.5f, 0);

            float alpha = textFadeProgress * iconFadeProgress * 0.9f;
            Color iconColor = GetIconColor(currentIconIndex);

            //绘制背景面板
            DrawTextBackground(spriteBatch, textPos, textSize, alpha, iconColor);

            //绘制扫描线效果
            DrawTextScanLines(spriteBatch, textPos, textSize, alpha, iconColor);

            //绘制噪点效果
            DrawTextNoise(spriteBatch, textPos, textSize, alpha);

            //绘制文本光晕
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, decodedText, textPos + offset, 
                    iconColor * (alpha * 0.4f), 0.75f);
            }

            //绘制主文本
            Color textColor = Color.Lerp(Color.White, iconColor, 0.3f);
            Utils.DrawBorderString(spriteBatch, decodedText, textPos, textColor * alpha, 0.75f);
        }

        /// <summary>
        /// 绘制文本背景
        /// </summary>
        private static void DrawTextBackground(SpriteBatch sb, Vector2 pos, Vector2 size, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            Rectangle bgRect = new Rectangle(
                (int)(pos.X - 10),
                (int)(pos.Y - 6),
                (int)(size.X + 20),
                (int)(size.Y + 12)
            );

            //半透明深色背景
            sb.Draw(pixel, bgRect, new Rectangle(0, 0, 1, 1), new Color(10, 15, 25) * (alpha * 0.85f));

            //发光边框
            Color edgeColor = color * (alpha * 0.6f);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, 2), edgeColor);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Bottom - 2, bgRect.Width, 2), edgeColor * 0.7f);
            sb.Draw(pixel, new Rectangle(bgRect.X, bgRect.Y, 2, bgRect.Height), edgeColor * 0.85f);
            sb.Draw(pixel, new Rectangle(bgRect.Right - 2, bgRect.Y, 2, bgRect.Height), edgeColor * 0.85f);
        }

        /// <summary>
        /// 绘制文本扫描线
        /// </summary>
        private static void DrawTextScanLines(SpriteBatch sb, Vector2 pos, Vector2 size, float alpha, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            if (pixel == null) return;

            float scanSpeed = Main.GlobalTimeWrappedHourly * 3f;
            int lineCount = 3;

            for (int i = 0; i < lineCount; i++) {
                float lineProgress = (scanSpeed + i * 0.33f) % 1f;
                float lineY = pos.Y + size.Y * lineProgress;

                float lineAlpha = (float)Math.Sin(lineProgress * MathHelper.Pi) * alpha * 0.3f;
                Color lineColor = color * lineAlpha;

                sb.Draw(
                    pixel,
                    new Vector2(pos.X - 8, lineY),
                    new Rectangle(0, 0, 1, 1),
                    lineColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(size.X + 16, 1.5f),
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
            for (int i = 0; i < 8; i++) {
                if (Main.rand.NextBool(3)) {
                    Vector2 noisePos = pos + new Vector2(
                        Main.rand.NextFloat(size.X),
                        Main.rand.NextFloat(size.Y)
                    );

                    float noiseSize = Main.rand.NextFloat(0.5f, 1.5f);
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
                0 => new Color(255, 80, 80),     //阿瑞斯 - 红色
                1 => new Color(100, 255, 150),   //塔纳托斯 - 绿色
                2 => new Color(80, 200, 255),    //双子 - 蓝色
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

        private static void SpawnTechParticle() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            Vector2 worldPos = player.Center + iconOffset;
            Vector2 screenPos = worldPos - Main.screenPosition;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(20f, 40f);
            Vector2 spawnPos = screenPos + angle.ToRotationVector2() * distance;

            Color particleColor = GetIconColor(currentIconIndex);
            techParticles.Add(new TechParticle(spawnPos, particleColor));
        }

        private static void UpdateTechParticles() {
            for (int i = techParticles.Count - 1; i >= 0; i--) {
                if (techParticles[i].Update()) {
                    techParticles.RemoveAt(i);
                }
            }

            //限制粒子数量
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
        /// 注册悬停效果（音效和动画）
        /// </summary>
        internal static void RegisterHoverEffects() {
            //重置上次悬停状态
            lastHoveredChoice = -1;
            currentIconIndex = -1;
            targetIconIndex = -1;
            iconFadeProgress = 0f;
            iconScaleProgress = 0f;
            iconRotation = 0f;
            iconGlowIntensity = 0f;
            techParticles.Clear();
            particleSpawnTimer = 0;

            //订阅悬停变化事件
            ADVChoiceBox.OnHoverChanged += OnChoiceHoverChanged;
        }

        /// <summary>
        /// 处理选项悬停变化
        /// </summary>
        private static void OnChoiceHoverChanged(object sender, ChoiceHoverEventArgs e) {
            //如果悬停到新的启用选项上（避免重复触发）
            if (e.CurrentIndex >= 0 && e.CurrentChoice != null && e.CurrentChoice.Enabled && e.CurrentIndex != lastHoveredChoice) {
                lastHoveredChoice = e.CurrentIndex;

                //设置目标图标索引
                targetIconIndex = e.CurrentIndex;

                //播放对应机甲的悬停音效
                SoundStyle hoverSound = e.CurrentIndex switch {
                    0 => AresIconHover,           // 阿瑞斯
                    1 => ThanatosIconHover,       // 塔纳托斯
                    2 => ArtemisApolloIconHover,  // 双子
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

            //如果离开了选项（悬停到空白处）
            if (e.CurrentIndex < 0 && e.PreviousIndex >= 0) {
                lastHoveredChoice = -1;
                targetIconIndex = -1;

                //播放离开音效
                SoundEngine.PlaySound(SoundID.MenuClose with {
                    Volume = 0.3f,
                    Pitch = -0.2f
                });
            }
        }

        /// <summary>
        /// 清理资源（当场景结束时调用）
        /// </summary>
        internal static void Cleanup() {
            currentIconIndex = -1;
            targetIconIndex = -1;
            iconFadeProgress = 0f;
            iconScaleProgress = 0f;
            techParticles.Clear();
            ADVChoiceBox.OnHoverChanged -= OnChoiceHoverChanged;
        }
    }
}
