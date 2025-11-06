using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
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

        //图标动画状态
        private static int currentIconIndex = -1;
        private static int targetIconIndex = -1;
        private static float iconFadeProgress = 0f;
        private static float iconScaleProgress = 0f;
        private static float iconRotation = 0f;
        private static float iconGlowIntensity = 0f;

        //动画参数
        private const float FadeSpeed = 0.08f;
        private const float ScaleSpeed = 0.12f;
        private const float IconBaseScale = 2.2f;
        private const float IconMaxScale = 3.6f;

        //图标位置偏移（玩家头顶）
        private static Vector2 iconOffset = new Vector2(0, -120f);

        //科技光效粒子
        private static readonly List<TechParticle> techParticles = new();
        private static int particleSpawnTimer = 0;

        public string LocalizationCategory => "UI";

        public override void SetStaticDefaults() {
            
        }

        public override void UpdateBySystem(int index) {
            //更新图标动画
            UpdateIconAnimation();

            //更新科技粒子
            UpdateTechParticles();
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            //绘制机甲图标和特效
            if (currentIconIndex >= 0 && iconFadeProgress > 0.01f) {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                DrawMechIcon(spriteBatch, main);
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
                    }
                }
                else {
                    //直接切换到新图标
                    currentIconIndex = targetIconIndex;
                    iconFadeProgress = 0f;
                    iconScaleProgress = 0f;
                    iconRotation = 0f;
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
                0 => new Color(255, 80, 80),     // 阿瑞斯 - 红色
                1 => new Color(100, 255, 150),   // 塔纳托斯 - 绿色
                2 => new Color(80, 200, 255),    // 双子 - 蓝色
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
