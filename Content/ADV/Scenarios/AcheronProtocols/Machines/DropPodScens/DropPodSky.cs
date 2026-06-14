using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>
    /// 空降仓子世界场景效果触发器
    /// </summary>
    internal class DropPodSkyData : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => DropPodWorld.Active;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(DropPodSky.Name, isActive);
    }

    /// <summary>

    /// 空降仓太空高空天空背景 漆黑的太空，稀疏的星点，机械残骸从下方高速掠过（模拟极速坠落）， 大气层边缘的蓝橙辉光从底部渗入

    /// </summary>
    internal class DropPodSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:DropPodSky";

        private bool active;
        private float intensity;

        //星星
        private StarEntity[] stars;
        //远景残骸（向上高速飞过，模拟坠落）
        private FallingDebris[] farDebris;
        //中景残骸
        private FallingDebris[] midDebris;
        //近景残骸
        private FallingDebris[] nearDebris;
        //大气层辉光
        private float atmospherePhase;
        //流星/碎片尾迹
        private readonly StreakParticle[] streaks = new StreakParticle[40];
        //脉冲计时
        private float pulseTimer;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer)
                return;

            SkyManager.Instance[Name] = this;

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.06f, 0.06f, 0.12f)
                .UseOpacity(0.4f), EffectPriority.VeryHigh);
        }

        void ICWRLoader.UnLoadData() { }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            atmospherePhase = 0f;
            pulseTimer = 0f;

            InitializeStars();
            InitializeDebrisLayers();
            InitializeStreaks();
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override bool IsActive() => active || intensity > 0;

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        #region 初始化

        private void InitializeStars() {
            stars = new StarEntity[120];
            for (int i = 0; i < stars.Length; i++) {
                stars[i] = StarEntity.CreateRandom();
            }
        }

        private void InitializeDebrisLayers() {
            //远景：小型残骸，向上高速移动
            farDebris = new FallingDebris[6];
            for (int i = 0; i < farDebris.Length; i++) {
                farDebris[i] = FallingDebris.CreateRandom(DebrisDepth.Far);
            }

            //中景
            midDebris = new FallingDebris[8];
            for (int i = 0; i < midDebris.Length; i++) {
                midDebris[i] = FallingDebris.CreateRandom(DebrisDepth.Mid);
            }

            //近景：大型残骸，极速掠过
            nearDebris = new FallingDebris[5];
            for (int i = 0; i < nearDebris.Length; i++) {
                nearDebris[i] = FallingDebris.CreateRandom(DebrisDepth.Near);
            }
        }

        private void InitializeStreaks() {
            for (int i = 0; i < streaks.Length; i++) {
                streaks[i] = StreakParticle.CreateRandom();
            }
        }

        #endregion

        #region 更新

        public override void Update(GameTime gameTime) {
            if (active) {
                if (intensity < 1f)
                    intensity += 0.02f;
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0)
                    Deactivate();
            }

            if (intensity <= 0.01f)
                return;

            pulseTimer += 0.016f;
            atmospherePhase += 0.006f;

            //更新星星闪烁
            if (stars != null) {
                foreach (ref var s in stars.AsSpan())
                    s.Update();
            }

            //更新残骸：全部向上移动（模拟玩家正在极速下坠）
            if (farDebris != null) {
                foreach (ref var d in farDebris.AsSpan())
                    d.Update(2.5f);
            }
            if (midDebris != null) {
                foreach (ref var d in midDebris.AsSpan())
                    d.Update(5.5f);
            }
            if (nearDebris != null) {
                foreach (ref var d in nearDebris.AsSpan())
                    d.Update(12f);
            }

            //更新流星尾迹
            foreach (ref var s in streaks.AsSpan())
                s.Update();
        }

        #endregion

        #region 绘制

        public override float GetCloudAlpha() => 1f - intensity;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || ExoGoreAssets.Assets == null)
                return;

            if (maxDepth >= 0 && minDepth < 0) {
                DrawSpaceBackground(spriteBatch);
                DrawStars(spriteBatch);
                DrawFarDebris(spriteBatch);
                DrawStreaks(spriteBatch);
                DrawMidDebris(spriteBatch);
                DrawNearDebris(spriteBatch);
                DrawAtmosphereGlow(spriteBatch);
            }
        }

        /// <summary>

        /// 漆黑的太空底色，极深的蓝黑

        /// </summary>
        private void DrawSpaceBackground(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color spaceVoid = new Color(2, 2, 6);
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                spaceVoid * intensity);

            //极微弱的深蓝星云辉光
            float nebulaPulse = MathF.Sin(pulseTimer * 0.2f) * 0.2f + 0.8f;
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.3f, Main.screenHeight * 0.25f),
                new Color(8, 12, 30) * (intensity * 0.15f * nebulaPulse), 600f);
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.7f, Main.screenHeight * 0.4f),
                new Color(10, 6, 20) * (intensity * 0.1f * nebulaPulse), 500f);
        }

        /// <summary>

        /// 星星：细小的白点/冷蓝点，微弱闪烁

        /// </summary>
        private void DrawStars(SpriteBatch sb) {
            if (stars == null) return;
            Texture2D px = VaultAsset.placeholder2.Value;

            foreach (ref var star in stars.AsSpan()) {
                float flicker = star.Alpha * intensity;
                Color c = star.Color * flicker;
                float size = star.Size;
                sb.Draw(px, star.Position, new Rectangle(0, 0, 1, 1), c, 0f,
                    new Vector2(0.5f), size, SpriteEffects.None, 0f);

                //较亮的星星添加辉光
                if (star.Size > 2f) {
                    DrawSoftGlow(sb, star.Position, star.Color * (flicker * 0.3f), star.Size * 4f);
                }
            }
        }

        /// <summary>

        /// 远景残骸：暗淡，小型，向上飞过

        /// </summary>
        private void DrawFarDebris(SpriteBatch sb) {
            if (farDebris == null) return;
            foreach (ref var d in farDebris.AsSpan()) {
                DrawFallingDebris(sb, ref d, intensity * 0.5f);
            }
        }

        /// <summary>

        /// 中景残骸

        /// </summary>
        private void DrawMidDebris(SpriteBatch sb) {
            if (midDebris == null) return;
            foreach (ref var d in midDebris.AsSpan()) {
                DrawFallingDebris(sb, ref d, intensity * 0.7f);
            }
        }

        /// <summary>

        /// 近景残骸：大型，极速掠过，带运动模糊感

        /// </summary>
        private void DrawNearDebris(SpriteBatch sb) {
            if (nearDebris == null) return;
            foreach (ref var d in nearDebris.AsSpan()) {
                DrawFallingDebris(sb, ref d, intensity * 0.9f);

                //近景残骸的运动模糊拖影
                Color trailColor = d.Tint * (intensity * 0.15f);
                for (int t = 1; t <= 4; t++) {
                    Vector2 trailPos = d.ScreenPosition + new Vector2(0, d.Speed * t * 3f);
                    float trailAlpha = 1f - t / 5f;
                    DrawFallingDebrisAt(sb, ref d, trailPos, trailColor * trailAlpha);
                }
            }
        }

        private static void DrawFallingDebris(SpriteBatch sb, ref FallingDebris d, float alpha) {
            DrawFallingDebrisAt(sb, ref d, d.ScreenPosition, d.Tint * alpha);
        }

        private static void DrawFallingDebrisAt(SpriteBatch sb, ref FallingDebris d, Vector2 pos, Color bodyColor) {
            Texture2D tex = ExoGoreAssets.GetTexture(d.TextureIndex);
            if (tex == null)
                return;

            Vector2 origin = tex.Size() * 0.5f;
            SpriteEffects fx = d.FlipH ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //冷色边缘光（rim light）
            Color rimColor = new Color(60, 120, 200) * (bodyColor.A / 255f * 0.5f);
            float rimOffset = 1.5f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.PiOver2 * i;
                Vector2 off = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * rimOffset;
                sb.Draw(tex, pos + off, null, rimColor, d.Rotation, origin, d.Scale, fx, 0f);
            }

            sb.Draw(tex, pos, null, bodyColor, d.Rotation, origin, d.Scale, fx, 0f);
        }

        /// <summary>

        /// 高速飞过的流星/碎片尾迹：细长的亮线，从下方向上飞过

        /// </summary>
        private void DrawStreaks(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;

            foreach (ref var s in streaks.AsSpan()) {
                float alpha = s.Alpha * intensity;
                Color c = s.Color * alpha;
                float length = s.Length;
                float rot = 0;//垂直向上

                sb.Draw(px, s.Position, new Rectangle(0, 0, 1, 1), c, rot,
                    new Vector2(0.5f, 0f), new Vector2(s.Width, length), SpriteEffects.None, 0f);

                //尾迹辉光
                if (s.Width > 1.5f) {
                    DrawSoftGlow(sb, s.Position, s.Color * (alpha * 0.4f), s.Width * 8f);
                }
            }
        }

        /// <summary>

        /// 大气层边缘辉光：从屏幕底部渗透的蓝橙色弧形辉光， 模拟从太空俯瞰大气层的边缘

        /// </summary>
        private void DrawAtmosphereGlow(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float glowPulse = MathF.Sin(atmospherePhase) * 0.15f + 0.85f;

            //底部大气弧光：蓝色主调
            int glowY = (int)(Main.screenHeight * 0.88f);
            Color atmosphereBlue = new Color(30, 80, 180) * (intensity * 0.35f * glowPulse);
            for (int dy = 0; dy < 80; dy++) {
                float grad = 1f - dy / 80f;
                sb.Draw(px, new Rectangle(0, glowY + dy, Main.screenWidth, 1),
                    atmosphereBlue * (grad * grad * grad));
            }

            //大气弧光上层：橙色大气散射
            Color atmosphereOrange = new Color(160, 80, 20) * (intensity * 0.18f * glowPulse);
            for (int dy = 0; dy < 40; dy++) {
                float grad = 1f - dy / 40f;
                sb.Draw(px, new Rectangle(0, glowY - 10 + dy, Main.screenWidth, 1),
                    atmosphereOrange * (grad * grad * grad));
            }

            //极薄的白色大气边缘线
            Color edgeLine = Color.White * (intensity * 0.12f * glowPulse);
            sb.Draw(px, new Rectangle(0, glowY - 2, Main.screenWidth, 2), edgeLine);

            //底部柔和辉光
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.5f, Main.screenHeight + 50),
                new Color(40, 100, 200) * (intensity * 0.25f * glowPulse), 800f);
        }

        /// <summary>

        /// 柔和光辉点

        /// </summary>
        private static void DrawSoftGlow(SpriteBatch sb, Vector2 center, Color color, float radius) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed)
                return;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float scale = radius / (glow.Width * 0.5f);
            sb.Draw(glow, center, null, color with { A = 0 }, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        #endregion

        public override Color OnTileColor(Color inColor) {
            if (intensity > 0.1f) {
                //太空冷色调但保持可见度
                Color tinted = new Color(
                    (int)(inColor.R * 0.45f),
                    (int)(inColor.G * 0.5f),
                    (int)(inColor.B * 0.7f),
                    inColor.A);
                return Color.Lerp(inColor, tinted, intensity * 0.6f);
            }
            return inColor;
        }

        #region 内部数据结构

        private struct StarEntity
        {
            public Vector2 Position;
            public float Size;
            public float Alpha;
            public Color Color;
            private float flickerPhase;
            private float flickerSpeed;

            public static StarEntity CreateRandom() {
                StarEntity s = new();
                s.Position = new Vector2(
                    Main.rand.NextFloat(Main.screenWidth),
                    Main.rand.NextFloat(Main.screenHeight * 0.85f));//星星只出现在上方85%区域
                s.Size = Main.rand.NextFloat(1f, 3.5f);
                s.Alpha = Main.rand.NextFloat(0.3f, 0.9f);
                s.flickerPhase = Main.rand.NextFloat(MathHelper.TwoPi);
                s.flickerSpeed = Main.rand.NextFloat(0.01f, 0.04f);

                //大多数白色/冷蓝，少数暖白
                float hue = Main.rand.NextFloat() < 0.8f
                    ? Main.rand.NextFloat(0.55f, 0.65f)//冷蓝
                    : Main.rand.NextFloat(0.08f, 0.15f);//暖白/淡黄
                s.Color = Main.hslToRgb(hue, Main.rand.NextFloat(0.1f, 0.3f), Main.rand.NextFloat(0.7f, 1f));
                return s;
            }

            public void Update() {
                flickerPhase += flickerSpeed;
                Alpha = MathF.Sin(flickerPhase) * 0.25f + 0.65f;

                //星星缓慢向上漂移（模拟极速下坠时的视差）
                Position.Y -= 0.15f;
                if (Position.Y < -10) {
                    Position.Y = Main.screenHeight * 0.85f + 10;
                    Position.X = Main.rand.NextFloat(Main.screenWidth);
                }
            }
        }

        private enum DebrisDepth { Far, Mid, Near }

        private struct FallingDebris
        {
            public Vector2 ScreenPosition;
            public float Speed;//向上移动的速度
            public float Rotation;
            public float RotationSpeed;
            public float Scale;
            public int TextureIndex;
            public bool FlipH;
            public Color Tint;

            public static FallingDebris CreateRandom(DebrisDepth depth) {
                FallingDebris d = new();
                //从屏幕底部以下随机位置开始
                d.ScreenPosition = new Vector2(
                    Main.rand.NextFloat(-100, Main.screenWidth + 100),
                    Main.rand.NextFloat(Main.screenHeight, Main.screenHeight + 400));

                d.Speed = depth switch {
                    DebrisDepth.Far => Main.rand.NextFloat(1.5f, 3f),
                    DebrisDepth.Mid => Main.rand.NextFloat(3f, 6f),
                    DebrisDepth.Near => Main.rand.NextFloat(8f, 14f),
                    _ => 3f
                };

                d.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                d.RotationSpeed = Main.rand.NextFloat(-0.01f, 0.01f) * (depth == DebrisDepth.Near ? 3f : 1f);

                d.Scale = depth switch {
                    DebrisDepth.Far => Main.rand.NextFloat(0.2f, 0.4f),
                    DebrisDepth.Mid => Main.rand.NextFloat(0.35f, 0.65f),
                    DebrisDepth.Near => Main.rand.NextFloat(0.5f, 0.9f),
                    _ => 0.4f
                };

                d.TextureIndex = ExoGoreAssets.Count > 0 ? Main.rand.Next(ExoGoreAssets.Count) : 0;
                d.FlipH = Main.rand.NextBool();

                float brightness = depth switch {
                    DebrisDepth.Far => Main.rand.NextFloat(0.25f, 0.4f),
                    DebrisDepth.Mid => Main.rand.NextFloat(0.35f, 0.55f),
                    DebrisDepth.Near => Main.rand.NextFloat(0.45f, 0.7f),
                    _ => 0.4f
                };
                d.Tint = new Color(brightness * 0.9f, brightness * 0.95f, brightness * 1.1f);

                return d;
            }

            public void Update(float speedMultiplier) {
                //向上移动（模拟极速坠落时残骸从下方掠过）
                ScreenPosition.Y -= Speed * speedMultiplier * 0.16f;
                //轻微水平漂移
                ScreenPosition.X += MathF.Sin(ScreenPosition.Y * 0.003f) * 0.3f;
                Rotation += RotationSpeed;

                //循环：飞出屏幕上方后从底部重新生成
                if (ScreenPosition.Y < -300) {
                    ScreenPosition.Y = Main.screenHeight + Main.rand.NextFloat(200, 500);
                    ScreenPosition.X = Main.rand.NextFloat(-100, Main.screenWidth + 100);
                    TextureIndex = ExoGoreAssets.Count > 0 ? Main.rand.Next(ExoGoreAssets.Count) : 0;
                    FlipH = Main.rand.NextBool();
                }
            }
        }

        private struct StreakParticle
        {
            public Vector2 Position;
            public float Speed;
            public float Length;
            public float Width;
            public float Alpha;
            public Color Color;
            private float life;
            private float maxLife;

            public static StreakParticle CreateRandom() {
                StreakParticle s = new();
                s.Position = new Vector2(
                    Main.rand.NextFloat(Main.screenWidth),
                    Main.rand.NextFloat(Main.screenHeight + 200));
                s.Speed = Main.rand.NextFloat(6f, 18f);
                s.Length = Main.rand.NextFloat(15f, 60f);
                s.Width = Main.rand.NextFloat(0.8f, 2.5f);
                s.Alpha = Main.rand.NextFloat(0.2f, 0.6f);
                s.life = Main.rand.NextFloat(0, 1f);
                s.maxLife = Main.rand.NextFloat(80, 200);

                //白色/冷蓝色的流星尾迹
                s.Color = Color.Lerp(new Color(180, 200, 255), new Color(255, 255, 255), Main.rand.NextFloat());
                return s;
            }

            public void Update() {
                //向上高速移动
                Position.Y -= Speed;
                life++;

                Alpha = MathF.Sin(life / maxLife * MathF.PI) * 0.5f + 0.1f;

                //飞出屏幕后重置
                if (Position.Y < -100 || life > maxLife) {
                    life = 0;
                    Position = new Vector2(
                        Main.rand.NextFloat(Main.screenWidth),
                        Main.screenHeight + Main.rand.NextFloat(50, 200));
                    Speed = Main.rand.NextFloat(6f, 18f);
                }
            }
        }

        #endregion
    }
}
