using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines
{
    internal class MachineSkyData : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => MachineWorld.Active;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(MachineWorldSky.Name, isActive);
    }

    /// <summary>

    /// 科尔托三号星寂灭天空背景 绝对零度下的机械墓场，被虫群吞噬的星流余晖

    /// </summary>
    internal class MachineWorldSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:MachineWorldSky";

        private bool active;
        private float intensity;

        //闪电闪光系统
        private static float lightningFlashIntensity;
        private static float lightningFlashDecay;
        private static Vector2 lightningFlashScreenPos;

        //远景残骸层
        private DebrisEntity[] farDebris;
        //中景残骸层
        private DebrisEntity[] midDebris;
        //近景残骸层
        private DebrisEntity[] nearDebris;

        //细小粒子
        private readonly MicroParticle[] metalShards = new MicroParticle[80];
        private readonly MicroParticle[] sporeParticles = new MicroParticle[50];

        //能量脉冲计时
        private float pulseTimer;
        //亚空间裂隙
        private SubspaceRift currentRift;
        private int riftCooldown;
        //地平线光效
        private float horizonGlowPhase;

        /// <summary>

        /// 触发闪电闪光效果，由MachineTesla在OnStrike时调用

        /// </summary>
        /// <param name="worldPosition">闪电击中的世界坐标</param>
        /// <param name="flashStrength">闪光强度(0-1)</param>
        internal static void TriggerLightningFlash(Vector2 worldPosition, float flashStrength) {
            lightningFlashIntensity = MathHelper.Clamp(flashStrength, 0f, 1f);
            lightningFlashDecay = 0f;
            lightningFlashScreenPos = worldPosition - Main.screenPosition;
        }

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer)
                return;

            SkyManager.Instance[Name] = this;

            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.08f, 0.07f, 0.12f)
                .UseOpacity(0.35f), EffectPriority.VeryHigh);
        }

        void ICWRLoader.UnLoadData() { }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
            pulseTimer = 0f;
            horizonGlowPhase = 0f;
            riftCooldown = Main.rand.Next(600, 1200);

            InitializeDebrisLayers();
            InitializeMicroParticles();
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

        private void InitializeDebrisLayers() {
            //远景层：大型残骸，移动极慢，数量较少
            farDebris = new DebrisEntity[8];
            for (int i = 0; i < farDebris.Length; i++) {
                farDebris[i] = DebrisEntity.CreateRandom(DebrisLayer.Far);
            }

            //中景层
            midDebris = new DebrisEntity[12];
            for (int i = 0; i < midDebris.Length; i++) {
                midDebris[i] = DebrisEntity.CreateRandom(DebrisLayer.Mid);
            }

            //近景层：较小的碎片，偶有电弧
            nearDebris = new DebrisEntity[10];
            for (int i = 0; i < nearDebris.Length; i++) {
                nearDebris[i] = DebrisEntity.CreateRandom(DebrisLayer.Near);
            }
        }

        private void InitializeMicroParticles() {
            for (int i = 0; i < metalShards.Length; i++) {
                metalShards[i] = MicroParticle.CreateMetalShard();
            }
            for (int i = 0; i < sporeParticles.Length; i++) {
                sporeParticles[i] = MicroParticle.CreateSpore();
            }
        }

        #endregion

        #region 更新

        public override void Update(GameTime gameTime) {
            if (active) {
                if (intensity < 1f)
                    intensity += 0.015f;
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0)
                    Deactivate();
            }

            if (intensity <= 0.01f)
                return;

            pulseTimer += 0.016f;
            horizonGlowPhase += 0.008f;

            //更新闪电闪光衰减
            if (lightningFlashIntensity > 0.01f) {
                lightningFlashDecay += 0.04f;
                lightningFlashIntensity *= 0.92f;
                if (lightningFlashIntensity < 0.01f)
                    lightningFlashIntensity = 0f;
            }

            //更新残骸
            if (farDebris != null) {
                foreach (ref var d in farDebris.AsSpan())
                    d.Update(0.08f);
            }
            if (midDebris != null) {
                foreach (ref var d in midDebris.AsSpan())
                    d.Update(0.25f);
            }
            if (nearDebris != null) {
                foreach (ref var d in nearDebris.AsSpan())
                    d.Update(0.6f);
            }

            //更新微粒子
            foreach (ref var p in metalShards.AsSpan())
                p.Update();
            foreach (ref var p in sporeParticles.AsSpan())
                p.Update();

            //亚空间裂隙
            if (currentRift != null) {
                currentRift.Update();
                if (currentRift.IsDead)
                    currentRift = null;
            }
            else {
                riftCooldown--;
                if (riftCooldown <= 0) {
                    currentRift = SubspaceRift.CreateRandom();
                    riftCooldown = Main.rand.Next(90, 240);
                }
            }
        }

        #endregion

        #region 绘制

        public override float GetCloudAlpha() => 1f - intensity;

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f || ExoGoreAssets.Assets == null)
                return;

            //在最远景深度绘制完整天空替换
            if (maxDepth >= 0 && minDepth < 0) {
                DrawVoidBackground(spriteBatch);
                DrawDustClouds(spriteBatch);
                DrawFarDebris(spriteBatch);
                DrawHorizonGlow(spriteBatch);
                DrawMidDebris(spriteBatch);
                DrawMetalShards(spriteBatch);
                DrawSporeParticles(spriteBatch);
                DrawNearDebris(spriteBatch);
                DrawSubspaceRift(spriteBatch);
                DrawLightningFlash(spriteBatch);
            }
        }

        /// <summary>

        /// 底层虚无：窒息黑 + 暗紫红尘埃云

        /// </summary>
        private void DrawVoidBackground(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            //深邃暗底色，略带深蓝紫调，闪电时提亮
            float flashLift = lightningFlashIntensity * 0.15f;
            Color baseVoid = new Color(
                (int)(8 + 30 * flashLift),
                (int)(6 + 35 * flashLift),
                (int)(14 + 50 * flashLift));
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                baseVoid * intensity);

            //暗紫红孢子云，在边缘和角落更浓
            float cloudPulse = MathF.Sin(pulseTimer * 0.3f) * 0.3f + 0.7f;

            //左下角尘埃
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.1f, Main.screenHeight * 0.85f),
                new Color(40, 10, 25) * (intensity * 0.35f * cloudPulse), 450f);

            //右上角尘埃
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.9f, Main.screenHeight * 0.15f),
                new Color(40, 10, 25) * (intensity * 0.25f * cloudPulse), 400f);

            //中心偏右的微弱尘埃
            float drift = MathF.Sin(pulseTimer * 0.15f) * 50f;
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.65f + drift, Main.screenHeight * 0.5f),
                new Color(30, 8, 20) * (intensity * 0.2f), 550f);

            //上方冷色星云辉光
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.4f, Main.screenHeight * 0.12f),
                new Color(15, 20, 45) * (intensity * 0.2f * cloudPulse), 500f);

            //中心左侧深蓝辉光
            float drift2 = MathF.Sin(pulseTimer * 0.1f + 1f) * 40f;
            DrawSoftGlow(sb, new Vector2(Main.screenWidth * 0.25f + drift2, Main.screenHeight * 0.45f),
                new Color(12, 18, 40) * (intensity * 0.15f), 400f);
        }

        /// <summary>

        /// 暗淡的尘埃云层

        /// </summary>
        private void DrawDustClouds(SpriteBatch sb) {
            if (CWRAsset.Fog == null || CWRAsset.Fog.IsDisposed)
                return;

            Texture2D fogTex = CWRAsset.Fog.Value;

            //几团缓慢漂浮的暗色雾气
            for (int i = 0; i < 4; i++) {
                float phase = pulseTimer * 0.05f + i * 1.57f;
                float x = Main.screenWidth * (0.2f + i * 0.2f) + MathF.Sin(phase) * 80f;
                float y = Main.screenHeight * (0.3f + MathF.Sin(phase * 0.7f + i) * 0.2f);
                float scale = 2.5f + MathF.Sin(phase * 0.4f) * 0.5f;
                float alpha = intensity * 0.14f * (MathF.Sin(phase * 0.3f) * 0.4f + 0.6f);

                Color fogColor = Color.Lerp(new Color(18, 8, 28), new Color(10, 16, 35), MathF.Sin(phase) * 0.5f + 0.5f);

                sb.Draw(fogTex, new Vector2(x, y), null,
                    fogColor * alpha, phase * 0.1f,
                    fogTex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>

        /// 远景残骸：极慢移动，暗淡，有边缘冷光

        /// </summary>
        private void DrawFarDebris(SpriteBatch sb) {
            if (farDebris == null) return;
            float flashBoost = lightningFlashIntensity * 0.3f;
            foreach (ref var d in farDebris.AsSpan()) {
                DrawDebrisEntity(sb, ref d, intensity * (0.6f + flashBoost), true);
            }
        }

        /// <summary>

        /// 中景残骸

        /// </summary>
        private void DrawMidDebris(SpriteBatch sb) {
            if (midDebris == null) return;
            float flashBoost = lightningFlashIntensity * 0.35f;
            foreach (ref var d in midDebris.AsSpan()) {
                DrawDebrisEntity(sb, ref d, intensity * (0.75f + flashBoost), true);
            }
        }

        /// <summary>

        /// 近景残骸：偶有能量电弧

        /// </summary>
        private void DrawNearDebris(SpriteBatch sb) {
            if (nearDebris == null) return;
            float flashBoost = lightningFlashIntensity * 0.4f;
            foreach (ref var d in nearDebris.AsSpan()) {
                DrawDebrisEntity(sb, ref d, intensity * (0.9f + flashBoost), false);

                //偶尔在残骸缝隙中绘制能量阵痛光效
                if (d.EnergyPulsePhase > 0) {
                    float pulse = MathF.Sin(d.EnergyPulsePhase * MathF.PI);
                    //深蓝星流脉冲与病态黄绿交替
                    Color pulseColor = MathF.Sin(d.EnergyPulsePhase * 3f) > 0
                        ? new Color(40, 80, 220) * (pulse * intensity * 0.7f)
                        : new Color(180, 200, 50) * (pulse * intensity * 0.5f);

                    DrawSoftGlow(sb, d.ScreenPosition, pulseColor, d.Scale * 45f);
                }
            }
        }

        private static void DrawDebrisEntity(SpriteBatch sb, ref DebrisEntity d, float alpha, bool drawRimOnly) {
            Texture2D tex = ExoGoreAssets.GetTexture(d.TextureIndex);
            if (tex == null)
                return;

            Vector2 origin = tex.Size() * 0.5f;
            SpriteEffects fx = d.FlipH ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //边缘高饱和冷色轮廓线（rim lighting），闪电时更亮更宽
            float flashRim = lightningFlashIntensity;
            Color rimBase = new Color(70, 140, 230);
            Color rimFlash = new Color(130, 220, 255);
            Color rimColor = Color.Lerp(rimBase, rimFlash, flashRim) * (alpha * (0.6f + flashRim * 0.5f));
            float rimOffset = 2f + flashRim * 1.5f;
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.PiOver2 * i;
                Vector2 off = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * rimOffset;
                sb.Draw(tex, d.ScreenPosition + off, null, rimColor, d.Rotation, origin, d.Scale, fx, 0f);
            }

            //主体绘制
            Color bodyColor = d.Tint * alpha;
            sb.Draw(tex, d.ScreenPosition, null, bodyColor, d.Rotation, origin, d.Scale, fx, 0f);
        }

        /// <summary>

        /// 金属碎屑微粒

        /// </summary>
        private void DrawMetalShards(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float flashBoost = 1f + lightningFlashIntensity * 2f;
            foreach (ref var p in metalShards.AsSpan()) {
                Color c = p.Color * (intensity * p.Alpha * 1.2f * flashBoost);
                sb.Draw(px, p.Position, new Rectangle(0, 0, 1, 1), c, p.Rotation,
                    new Vector2(0.5f), new Vector2(p.Size * 1.8f, p.Size * 0.5f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>

        /// 虫群孢子微粒（带拖尾发光）

        /// </summary>
        private void DrawSporeParticles(SpriteBatch sb) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed)
                return;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            float flashBoost = 1f + lightningFlashIntensity * 1.5f;
            foreach (ref var p in sporeParticles.AsSpan()) {
                Color c = p.Color with { A = 0 } * (intensity * p.Alpha * 0.9f * flashBoost);
                sb.Draw(glow, p.Position, null, c, 0f, glow.Size() * 0.5f, p.Size * 0.06f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>

        /// 亚空间裂隙

        /// </summary>
        private void DrawSubspaceRift(SpriteBatch sb) {
            if (currentRift == null) return;

            Texture2D px = CWRAsset.SoftGlow.Value;
            float life = currentRift.LifeProgress;
            float fade = MathF.Sin(life * MathF.PI);

            //极细白线
            Vector2 start = currentRift.Start;
            Vector2 end = currentRift.End;
            Vector2 diff = end - start;
            float len = diff.Length();
            float rot = diff.ToRotation();

            Color riftColor = Color.White * (fade * intensity * 0.85f);
            sb.Draw(px, start, new Rectangle(0, 0, 1, 1), riftColor, rot,
                new Vector2(0, 0.5f), new Vector2(len * fade, 2f), SpriteEffects.None, 0f);

            //辉光
            DrawSoftGlow(sb, (start + end) * 0.5f, new Color(180, 200, 255) * (fade * intensity * 0.3f), len * 0.4f);
        }

        /// <summary>

        /// 地平线光效：锯齿状机械森林影迹 + 暗淡橙红火光

        /// </summary>
        private void DrawHorizonGlow(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2.Value;

            int horizonY = (int)(Main.screenHeight * 0.92f);

            //地平线后方橙红火光（星流矿脉余温），闪电时混入冷色电光
            float glowPulse = MathF.Sin(horizonGlowPhase) * 0.3f + 0.7f;
            float flashFactor = lightningFlashIntensity;
            Color fireBase = new Color(120, 40, 10);
            Color fireFlash = new Color(80, 140, 200);
            Color fireColor = Color.Lerp(fireBase, fireFlash, flashFactor * 0.6f) * (intensity * (0.45f + flashFactor * 0.35f) * glowPulse);

            //渐变火光带（更宽更亮）
            for (int dy = 0; dy < 60; dy++) {
                float grad = 1f - dy / 60f;
                sb.Draw(px, new Rectangle(0, horizonY + dy, Main.screenWidth, 1),
                    fireColor * (grad * grad));
            }

            //火光上方微弱的暖色渐变（增加层次）
            Color upperGlow = new Color(60, 20, 8) * (intensity * 0.15f * glowPulse);
            for (int dy = 0; dy < 40; dy++) {
                float grad = 1f - dy / 40f;
                sb.Draw(px, new Rectangle(0, horizonY - 40 + dy, Main.screenWidth, 1),
                    upperGlow * (grad * grad * grad));
            }

            //锯齿状机械森林剪影
            int silhouetteCount = Main.screenWidth / 8;
            int seed = 42;
            for (int i = 0; i < silhouetteCount; i++) {
                //伪随机高度生成嶙峋轮廓
                seed = seed * 1103515245 + 12345;
                int h = 4 + Math.Abs(seed >> 16) % 25;
                //偶尔出现高尖的废料堆
                if (i % 7 == 3) h += 8 + Math.Abs(seed >> 8) % 12;
                if (i % 13 == 5) h += 15 + Math.Abs(seed >> 12) % 10;

                int x = i * 8;
                Color silColor = new Color(5, 5, 10) * intensity;
                sb.Draw(px, new Rectangle(x, horizonY - h, 8, h + 20), silColor);
            }
        }

        private static void DrawSoftGlow(SpriteBatch sb, Vector2 center, Color color, float radius) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed)
                return;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float scale = radius / (glow.Width * 0.5f);
            sb.Draw(glow, center, null, color with { A = 0 }, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>

        /// 闪电闪光叠加层：全屏冷色闪光 + 闪电源点辉光

        /// </summary>
        private void DrawLightningFlash(SpriteBatch sb) {
            if (lightningFlashIntensity <= 0.01f)
                return;

            Texture2D px = VaultAsset.placeholder2.Value;
            float flash = lightningFlashIntensity;

            //全屏冷色闪光叠加
            Color flashColor = new Color(60, 100, 140) * (flash * intensity * 0.35f);
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                flashColor);

            //闪电源点附近的强烈辉光
            Vector2 flashPos = lightningFlashScreenPos;
            if (flashPos.X > -500 && flashPos.X < Main.screenWidth + 500
                && flashPos.Y > -500 && flashPos.Y < Main.screenHeight + 500) {
                DrawSoftGlow(sb, flashPos, new Color(120, 200, 255) * (flash * intensity * 0.6f), 100f * flash);
                DrawSoftGlow(sb, flashPos, new Color(200, 230, 255) * (flash * intensity * 0.4f), 60f * flash);
            }
        }

        #endregion

        public override Color OnTileColor(Color inColor) {
            if (intensity > 0.1f) {
                //冷色调但保持可见度
                Color tinted = new Color(
                    (int)(inColor.R * 0.6f),
                    (int)(inColor.G * 0.65f),
                    (int)(inColor.B * 0.8f),
                    inColor.A);
                Color result = Color.Lerp(inColor, tinted, intensity * 0.5f);

                //闪电闪光时提亮方块颜色
                if (lightningFlashIntensity > 0.01f) {
                    Color flashTint = new Color(
                        (int)Math.Min(255, result.R + 80 * lightningFlashIntensity),
                        (int)Math.Min(255, result.G + 100 * lightningFlashIntensity),
                        (int)Math.Min(255, result.B + 120 * lightningFlashIntensity),
                        result.A);
                    result = Color.Lerp(result, flashTint, lightningFlashIntensity * 0.6f);
                }
                return result;
            }
            return inColor;
        }

        #region 内部数据结构

        private enum DebrisLayer { Far, Mid, Near }

        private struct DebrisEntity
        {
            public Vector2 ScreenPosition;
            public Vector2 Velocity;
            public float Rotation;
            public float RotationSpeed;
            public float Scale;
            public int TextureIndex;
            public bool FlipH;
            public Color Tint;
            public float EnergyPulsePhase; //0=无脉冲, >0=活跃
            public float EnergyPulseSpeed;

            public static DebrisEntity CreateRandom(DebrisLayer layer) {
                DebrisEntity d = new();
                d.ScreenPosition = new Vector2(
                    Main.rand.NextFloat(-200, Main.screenWidth + 200),
                    Main.rand.NextFloat(Main.screenHeight * 0.05f, Main.screenHeight * 0.8f));

                float speedMult = layer switch {
                    DebrisLayer.Far => 0.05f,
                    DebrisLayer.Mid => 0.15f,
                    DebrisLayer.Near => 0.35f,
                    _ => 0.1f
                };
                d.Velocity = new Vector2(
                    Main.rand.NextFloat(-0.3f, 0.3f) * speedMult,
                    Main.rand.NextFloat(-0.1f, 0.1f) * speedMult);

                d.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                d.RotationSpeed = Main.rand.NextFloat(-0.003f, 0.003f) * (layer == DebrisLayer.Near ? 2f : 1f);

                d.Scale = layer switch {
                    DebrisLayer.Far => Main.rand.NextFloat(0.3f, 0.6f),
                    DebrisLayer.Mid => Main.rand.NextFloat(0.4f, 0.8f),
                    DebrisLayer.Near => Main.rand.NextFloat(0.5f, 1.0f),
                    _ => 0.5f
                };

                d.TextureIndex = ExoGoreAssets.Count > 0 ? Main.rand.Next(ExoGoreAssets.Count) : 0;
                d.FlipH = Main.rand.NextBool();

                //暗淡的灰色调，层越远越暗
                float brightness = layer switch {
                    DebrisLayer.Far => Main.rand.NextFloat(0.3f, 0.5f),
                    DebrisLayer.Mid => Main.rand.NextFloat(0.4f, 0.65f),
                    DebrisLayer.Near => Main.rand.NextFloat(0.5f, 0.8f),
                    _ => 0.45f
                };
                d.Tint = new Color(brightness, brightness * 0.95f, brightness * 1.1f);

                //近景层有概率出现能量脉冲
                if (layer == DebrisLayer.Near && Main.rand.NextBool(3)) {
                    d.EnergyPulsePhase = Main.rand.NextFloat(0.01f, 1f);
                    d.EnergyPulseSpeed = Main.rand.NextFloat(0.005f, 0.015f);
                }

                return d;
            }

            public void Update(float parallaxFactor) {
                ScreenPosition += Velocity;
                Rotation += RotationSpeed;

                //视差跟随摄像机移动
                ScreenPosition.X -= Main.screenPosition.X * 0.0002f * parallaxFactor;

                //循环包裹
                if (ScreenPosition.X < -300) ScreenPosition.X += Main.screenWidth + 600;
                if (ScreenPosition.X > Main.screenWidth + 300) ScreenPosition.X -= Main.screenWidth + 600;
                if (ScreenPosition.Y < -200) ScreenPosition.Y += Main.screenHeight + 400;
                if (ScreenPosition.Y > Main.screenHeight + 200) ScreenPosition.Y -= Main.screenHeight + 400;

                //能量脉冲更新
                if (EnergyPulsePhase > 0) {
                    EnergyPulsePhase += EnergyPulseSpeed;
                    if (EnergyPulsePhase > 1f) {
                        //随机决定是否再来一次脉冲
                        if (Main.rand.NextBool(3)) {
                            EnergyPulsePhase = 0.01f;
                            EnergyPulseSpeed = Main.rand.NextFloat(0.005f, 0.015f);
                        }
                        else {
                            EnergyPulsePhase = 0f;
                        }
                    }
                }
                else if (Main.rand.NextBool(600)) {
                    EnergyPulsePhase = 0.01f;
                    EnergyPulseSpeed = Main.rand.NextFloat(0.005f, 0.015f);
                }
            }
        }

        private struct MicroParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float RotationSpeed;
            public float Size;
            public float Alpha;
            public Color Color;
            private float lifeTimer;
            private float maxLife;

            public static MicroParticle CreateMetalShard() {
                MicroParticle p = new();
                p.Position = new Vector2(
                    Main.rand.NextFloat(Main.screenWidth),
                    Main.rand.NextFloat(Main.screenHeight));
                p.Velocity = Main.rand.NextVector2Circular(0.15f, 0.15f);
                p.Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                p.RotationSpeed = Main.rand.NextFloat(-0.02f, 0.02f);
                p.Size = Main.rand.NextFloat(1f, 3f);
                p.Alpha = Main.rand.NextFloat(0.2f, 0.7f);
                //金属反光的冷白色/钢蓝色
                float hue = Main.rand.NextFloat(0.55f, 0.65f);
                p.Color = Main.hslToRgb(hue, 0.2f, Main.rand.NextFloat(0.4f, 0.7f));
                p.lifeTimer = Main.rand.NextFloat(0, 1f);
                p.maxLife = Main.rand.NextFloat(300, 600);
                return p;
            }

            public static MicroParticle CreateSpore() {
                MicroParticle p = new();
                p.Position = new Vector2(
                    Main.rand.NextFloat(Main.screenWidth),
                    Main.rand.NextFloat(Main.screenHeight));
                p.Velocity = Main.rand.NextVector2Circular(0.08f, 0.08f);
                p.Rotation = 0;
                p.RotationSpeed = 0;
                p.Size = Main.rand.NextFloat(1.5f, 4f);
                p.Alpha = Main.rand.NextFloat(0.15f, 0.5f);
                //病态黄绿发光
                p.Color = Color.Lerp(new Color(120, 160, 30), new Color(80, 200, 60), Main.rand.NextFloat());
                p.lifeTimer = Main.rand.NextFloat(0, 1f);
                p.maxLife = Main.rand.NextFloat(400, 800);
                return p;
            }

            public void Update() {
                Position += Velocity;
                Rotation += RotationSpeed;
                lifeTimer++;

                //缓慢呼吸效果
                Alpha = MathF.Sin(lifeTimer / maxLife * MathF.PI * 2f) * 0.3f + 0.4f;

                //循环
                if (Position.X < -20) Position.X += Main.screenWidth + 40;
                if (Position.X > Main.screenWidth + 20) Position.X -= Main.screenWidth + 40;
                if (Position.Y < -20) Position.Y += Main.screenHeight + 40;
                if (Position.Y > Main.screenHeight + 20) Position.Y -= Main.screenHeight + 40;

                if (lifeTimer > maxLife) {
                    lifeTimer = 0;
                    Position = new Vector2(
                        Main.rand.NextFloat(Main.screenWidth),
                        Main.rand.NextFloat(Main.screenHeight));
                }
            }
        }

        private class SubspaceRift
        {
            public Vector2 Start;
            public Vector2 End;
            public float Life;
            public float MaxLife;

            public float LifeProgress => Life / MaxLife;
            public bool IsDead => Life >= MaxLife;

            public static SubspaceRift CreateRandom() {
                var rift = new SubspaceRift();
                float y = Main.rand.NextFloat(Main.screenHeight * 0.1f, Main.screenHeight * 0.7f);
                float x = Main.rand.NextFloat(Main.screenWidth * 0.1f, Main.screenWidth * 0.9f);
                float angle = Main.rand.NextFloat(-0.3f, 0.3f);
                float length = Main.rand.NextFloat(80, 250);
                Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

                rift.Start = new Vector2(x, y) - dir * length * 0.5f;
                rift.End = new Vector2(x, y) + dir * length * 0.5f;
                rift.Life = 0;
                rift.MaxLife = Main.rand.NextFloat(60, 150);
                return rift;
            }

            public void Update() {
                Life++;
            }
        }

        #endregion
    }
}
