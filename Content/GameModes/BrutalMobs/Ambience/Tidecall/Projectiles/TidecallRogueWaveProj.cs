using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Tidecall.Projectiles
{
    /// <summary>
    /// 「疯狗浪」：低频大浪拍岸。ai[0]=向陆方向 ai[1]=浪高像素 ai[2]=岸线列。
    /// 预告：暗水浪包自远海隆起压来（150 帧，浪吼渐强，远超 45 帧公平线；浪峰白线从出生即可读）→
    /// 破碎：浪峰向岸卷落，沫块抛洒、水花炸开（12 帧）→ 扫滩：与判定同高的白水涌锋向陆推进约 24 格，
    /// 对岸上玩家轻推+微量伤害，脚底高于浪峰者免疫（站上高处躲浪）→ 余韵：泡沫湿痕消退+滩水回流（90 帧，判定早已关闭）。
    /// 浪身是流体着色器画的水体（复用猪鲨海啸的世界锚定噪声域：浪形在跑，水留在原地），
    /// 画布底缘压在水线/滩面上——浪从海面里长出来，不是悬浮物；
    /// 浪高由调度端在出生时按档位基数×风雨增益定死（ai[1]），位置全程由出生参数+timeLeft 确定性推演，各端一致；
    /// 判定窗只覆盖破碎+扫滩，接近期的涌浪从泳者头顶无害滚过
    /// </summary>
    internal class TidecallRogueWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //Spray 512² 3×3 真alpha碎沫块贴片：破碎期抛洒的水花块
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Spray = null;

        //汐声自持水色：近岸浅海的深蓝，不用猪鲨那套远洋青绿（共享的只是流体技法）
        private static readonly Vector3 DeepWater = new Color(7, 24, 42).ToVector3();
        private static readonly Vector3 SeaBody = new Color(24, 70, 106).ToVector3();
        private static readonly Vector3 FoamCrest = new Color(230, 242, 248).ToVector3();

        /// <summary>出生点在岸线向海侧的格数（调度端同用）</summary>
        internal const int ApproachTiles = 55;
        private const int ApproachFrames = 150;
        private const int BreakFrames = 12;
        private const int SweepFrames = 56;
        private const int FadeFrames = 90;
        /// <summary>扫滩推进距离（像素，约 24 格）</summary>
        private const float SweepReachPx = 384f;
        /// <summary>浪锋判定半宽</summary>
        private const float FrontHalfWidth = 29f;
        private const float PushAccel = 1.1f;
        private const float PushSpeedCap = 7f;
        /// <summary>余韵泡沫补丁数</summary>
        private const int WashPatches = 7;

        private int LandDir => (int)Projectile.ai[0];
        private float WaveHeight => Projectile.ai[1];
        private float ShoreWorldX => Projectile.ai[2] * 16f + 8f;
        private float SurfaceWorldY => Projectile.Center.Y;
        private int TotalLife => ApproachFrames + BreakFrames + SweepFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        private bool InApproach => Elapsed < ApproachFrames;
        private bool InHostileWindow => Elapsed >= ApproachFrames && Elapsed < ApproachFrames + BreakFrames + SweepFrames;

        /// <summary>接近进度 0~1（浅滩变陡：位置略前倾，浪身随之隆高）</summary>
        private float ApproachProgress => MathHelper.Clamp(Elapsed / (float)ApproachFrames, 0f, 1f);

        /// <summary>破碎进度 0~1（浪峰卷落动画时基）</summary>
        private float BreakProgress {
            get {
                int t = Elapsed - ApproachFrames;
                if (t <= 0) {
                    return 0f;
                }
                return MathHelper.Clamp(t / (float)BreakFrames, 0f, 1f);
            }
        }

        /// <summary>扫滩进度 0~1</summary>
        private float SweepProgress {
            get {
                int t = Elapsed - ApproachFrames - BreakFrames;
                if (t <= 0) {
                    return 0f;
                }
                return MathHelper.Clamp(t / (float)SweepFrames, 0f, 1f);
            }
        }

        /// <summary>余韵消退 1→0</summary>
        private float FadeFactor {
            get {
                int t = Elapsed - ApproachFrames - BreakFrames - SweepFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)FadeFrames, 0f, 1f);
            }
        }

        /// <summary>接近期浪峰所在 X（远慢近快）</summary>
        private float CrestX {
            get {
                float p = MathF.Pow(ApproachProgress, 1.15f);
                float spawnX = ShoreWorldX - LandDir * ApproachTiles * 16f;
                return MathHelper.Lerp(spawnX, ShoreWorldX, p);
            }
        }

        /// <summary>当前浪体高度：接近期浅化隆高，扫滩期逐步泄劲</summary>
        private float CurrentHeight {
            get {
                if (InApproach) {
                    return WaveHeight * (0.25f + 0.75f * MathF.Pow(ApproachProgress, 1.6f));
                }
                return WaveHeight * (1f - 0.55f * SweepProgress);
            }
        }

        /// <summary>扫滩浪锋所在 X（破碎期钉在岸线）</summary>
        private float FrontX => ShoreWorldX + LandDir * SweepReachPx * SweepProgress;

        //==== 客户端绘制缓存 ====
        private float crestSurfaceY;
        private int crestProbeIn;
        private readonly float[] washGroundY = new float[WashPatches];
        private readonly bool[] washOk = new bool[WashPatches];
        private int washProbeIn;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//破碎+扫滩窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ApproachFrames + BreakFrames + SweepFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //Boss 在场伤害机制暂停：各端 HasBoss 一致，判定与轻推同关，视觉转余韵敛度
            bool suspended = CWRWorld.HasBoss;
            Projectile.hostile = !suspended && InHostileWindow;

            if (Main.dedServ) {
                return;
            }

            if (crestProbeIn-- <= 0) {
                crestProbeIn = 4;
                RefreshCrestProbe();
            }

            if (InApproach) {
                //浪吼渐强：随进度与距离喂给氛围声层
                float proximity = 1f - MathHelper.Clamp(
                    (Vector2.Distance(Main.LocalPlayer.Center, new Vector2(CrestX, SurfaceWorldY)) - 400f) / 1600f, 0f, 1f);
                TidecallAmbience.ReportWaveRoar((0.15f + 0.85f * ApproachProgress) * proximity);
                //浪峰唇口白沫（≤30/s，只活 2.5s）；飞沫随现实风向吹偏
                if (Main.rand.NextBool(2)) {
                    Dust lip = Dust.NewDustPerfect(
                        new Vector2(CrestX + Main.rand.NextFloat(-30f, 30f), crestSurfaceY - CurrentHeight),
                        DustID.Cloud,
                        new Vector2(-LandDir * 0.5f + Main.windSpeedCurrent * 4f, -Main.rand.NextFloat(0.5f, 1.5f)),
                        150, new Color(235, 245, 250), Main.rand.NextFloat(0.8f, 1.3f) * ApproachProgress);
                    lip.noGravity = true;
                }
                //临破前浪唇起羽：卷落的最后预兆
                if (ApproachProgress > 0.8f && Main.rand.NextBool(2)) {
                    Dust feather = Dust.NewDustPerfect(
                        new Vector2(CrestX + LandDir * Main.rand.NextFloat(0f, 26f),
                            crestSurfaceY - CurrentHeight * Main.rand.NextFloat(0.9f, 1.05f)),
                        DustID.Cloud,
                        new Vector2(LandDir * Main.rand.NextFloat(0.5f, 1.8f) + Main.windSpeedCurrent * 3f,
                            -Main.rand.NextFloat(1f, 2.2f)),
                        130, new Color(240, 248, 252), Main.rand.NextFloat(1f, 1.5f));
                    feather.noGravity = true;
                }
            }
            else if (elapsed == ApproachFrames) {
                BreakBurst();
            }
            else if (Projectile.hostile) {
                PushLocalPlayer();
                //浪锋水幕（短窗口）
                if (Main.rand.NextBool(2)) {
                    Dust curtain = Dust.NewDustPerfect(
                        new Vector2(FrontX + Main.rand.NextFloat(-20f, 20f),
                            SurfaceWorldY - CurrentHeight * Main.rand.NextFloat(0.2f, 1f)),
                        DustID.Rain, new Vector2(LandDir * Main.rand.NextFloat(2f, 4f), Main.rand.NextFloat(-1f, 2f)),
                        90, default, Main.rand.NextFloat(0.9f, 1.3f));
                    curtain.noGravity = true;
                }
                //浪锋底部擦地飞溅：白水推进时贴着滩涂踢起水花
                if (Main.rand.NextBool(3)) {
                    Dust kick = Dust.NewDustPerfect(
                        new Vector2(FrontX + LandDir * 10f, GroundYNear(FrontX) - 6f),
                        DustID.Water, new Vector2(LandDir * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(1.5f, 3.5f)),
                        70, default, Main.rand.NextFloat(0.9f, 1.4f));
                    kick.noGravity = false;
                }
            }
            //扫滩地面探针破碎起即跑：涌锋与余韵泡沫都要贴着滩涂
            if (!InApproach && washProbeIn-- <= 0) {
                washProbeIn = 10;
                RefreshWashProbe();
            }
        }

        //破碎帧：水花炸开+拍岸闷响（听觉落点与判定开窗同拍）
        private void BreakBurst() {
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 1f, Pitch = -0.4f, MaxInstances = 3 },
                new Vector2(ShoreWorldX, SurfaceWorldY));
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.75f, MaxInstances = 3 },
                new Vector2(ShoreWorldX, SurfaceWorldY));
            for (int i = 0; i < 34; i++) {
                bool cloud = i % 3 == 0;
                Dust burst = Dust.NewDustPerfect(
                    new Vector2(ShoreWorldX + Main.rand.NextFloat(-40f, 40f), SurfaceWorldY - Main.rand.NextFloat(0f, WaveHeight)),
                    cloud ? DustID.Cloud : DustID.Water,
                    new Vector2(LandDir * Main.rand.NextFloat(1f, 6f), -Main.rand.NextFloat(1f, 7f)),
                    cloud ? 140 : 60, cloud ? new Color(235, 245, 250) : default,
                    Main.rand.NextFloat(1f, 1.7f));
                burst.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>轻推本机玩家：浪锋带内且脚底低于浪峰者向陆推离，位移逐端本地结算</summary>
        private void PushLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (!player.active || player.dead) {
                return;
            }
            float crestTop = SurfaceWorldY - CurrentHeight;
            if (player.Bottom.Y <= crestTop + 4f) {
                return;//站上高处免疫
            }
            if (MathF.Abs(player.Center.X - FrontX) > FrontHalfWidth + player.width * 0.5f + 12f) {
                return;
            }
            if (LandDir * player.velocity.X < PushSpeedCap) {
                player.velocity.X += LandDir * PushAccel;
            }
            if (player.velocity.Y > -1.6f) {
                player.velocity.Y -= 0.55f;
            }
        }

        /// <summary>判定带：浪锋竖带，脚底高于浪峰的目标直接豁免（可站高处躲浪）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float crestTop = SurfaceWorldY - CurrentHeight;
            if (targetHitbox.Bottom <= crestTop + 4f) {
                return false;
            }
            Rectangle band = Utils.CenteredRectangle(
                new Vector2(FrontX, SurfaceWorldY - CurrentHeight * 0.5f + 10f),
                new Vector2(FrontHalfWidth * 2f, CurrentHeight + 44f));
            return band.Intersects(targetHitbox);
        }

        //接近期浪峰列的真实水面（画浪身贴着海面走）
        private void RefreshCrestProbe() {
            Point pt = new Vector2(CrestX, SurfaceWorldY).ToTileCoordinates();
            crestSurfaceY = TidecallAmbience.TryFindWaterSurface(pt.X, pt.Y + 1, out int sy)
                ? sy * 16f : SurfaceWorldY;
        }

        /// <summary>扫滩带内某列的滩涂地面（探针插值，无值时退回水面基线）</summary>
        private float GroundYNear(float worldX) {
            float u = (worldX - ShoreWorldX) / (LandDir * SweepReachPx);
            int i = (int)MathHelper.Clamp(u * WashPatches - 0.5f, 0f, WashPatches - 1);
            return washOk[i] ? washGroundY[i] : SurfaceWorldY;
        }

        //扫滩带内的地面高度（涌锋与余韵泡沫都要贴着滩涂）
        private void RefreshWashProbe() {
            for (int i = 0; i < WashPatches; i++) {
                float x = ShoreWorldX + LandDir * SweepReachPx * ((i + 0.5f) / WashPatches);
                int tileX = (int)(x / 16f);
                int refY = (int)(SurfaceWorldY / 16f);
                washOk[i] = false;
                for (int y = refY - 10; y <= refY + 8; y++) {
                    if (TidecallAmbience.SolidAt(tileX, y)) {
                        washGroundY[i] = y * 16f;
                        washOk[i] = true;
                        break;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D spraySheet = Spray?.Value;
            Texture2D foam = CWRAsset.Fog?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            if (spraySheet == null || foam == null || glow == null || spindle == null) {
                return false;
            }
            float dim = TidecallAmbience.BossDim;

            if (InApproach) {
                DrawApproachSwell(foam, glow, spindle, dim);
            }
            else {
                DrawBreakAndWash(spraySheet, foam, glow, spindle, dim);
            }
            return false;
        }

        /// <summary>环境光系数：夜间压暗但保留可读下限（带伤害的实体不许在黑夜隐形）</summary>
        private static float LitK(Color ambient) {
            float lum = MathHelper.Clamp((ambient.R + ambient.G + ambient.B) / 700f, 0f, 1f);
            return 0.35f + 0.65f * lum;
        }

        /// <summary>泡沫受环境光调制</summary>
        private static Color LitFoam(Color baseColor, Color ambient) {
            float k = LitK(ambient);
            return new Color((int)(baseColor.R * k), (int)(baseColor.G * k), (int)(baseColor.B * k), baseColor.A);
        }

        /// <summary>浪前脸在画布横向的落位：着色器的水体到此截断，画布余下部分是向海的后坡</summary>
        private const float FaceU = 0.72f;

        /// <summary>
        /// 浪体：流体着色器画的水（噪声域锚在世界坐标，浪形推进而水体留在原地）。
        /// 画布契约同猪鲨海啸——底缘=水线/滩面，顶部三成留给抛沫，前脸朝 <see cref="LandDir"/>；
        /// 入参 <paramref name="frontFoot"/> 是浪锋脚点（可见浪锋=判定浪锋，画布自行向海回退）；
        /// 调色板乘环境光后上载，夜里是暗水而不是自发光贴片
        /// </summary>
        private void DrawWaveBody(Vector2 frontFoot, float widthPx, float heightPx,
            float growth, float collapse, float speedRatio, float intensity, float lit) {
            Vector2 bottomCenter = frontFoot - new Vector2(LandDir * widthPx * (FaceU - 0.5f), 0f);
            Effect effect = EffectLoader.FishronTsunami?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (effect == null || noise == null || pixel == null) {
                DrawWaveBodyFallback(bottomCenter, widthPx, heightPx, intensity, lit);
                return;
            }
            Vector2 center = bottomCenter - new Vector2(0f, heightPx * 0.5f);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uGrowth"]?.SetValue(growth);
            effect.Parameters["uCollapse"]?.SetValue(collapse);
            effect.Parameters["uDir"]?.SetValue((float)LandDir);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.271f);
            effect.Parameters["uWorldX"]?.SetValue(center.X);
            effect.Parameters["uCanvasPx"]?.SetValue(widthPx);
            effect.Parameters["uSpeedRatio"]?.SetValue(speedRatio);
            effect.Parameters["uDeepColor"]?.SetValue(DeepWater * lit);
            effect.Parameters["uSeaColor"]?.SetValue(SeaBody * lit);
            effect.Parameters["uFoamColor"]?.SetValue(FoamCrest * lit);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图，参数式绑定实机失效
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(pixel, center - Main.screenPosition, null, Color.White, 0f, pixel.Size() * 0.5f,
                new Vector2(widthPx / pixel.Width, heightPx / pixel.Height), SpriteEffects.None, 0f);
            sb.End();
            gd.Textures[1] = null;//归还噪声槽
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺席兜底：真 alpha 雾团堆出一道暗水丘，保证带伤害的浪不会隐形</summary>
        private void DrawWaveBodyFallback(Vector2 bottomCenter, float widthPx, float heightPx,
            float intensity, float lit) {
            Texture2D foam = CWRAsset.Fog?.Value;
            if (foam == null) {
                return;
            }
            Color deep = new((int)(DeepWater.X * 255f * lit), (int)(DeepWater.Y * 255f * lit),
                (int)(DeepWater.Z * 255f * lit));
            for (int i = 0; i < 3; i++) {
                float u = (i + 0.5f) / 3f;
                Vector2 pos = bottomCenter + new Vector2(
                    LandDir * (u - 0.65f) * widthPx * 0.5f, -heightPx * (0.16f + 0.20f * u));
                Main.EntitySpriteDraw(foam, pos - Main.screenPosition, null,
                    deep * (0.72f * intensity), 0f, foam.Size() / 2f,
                    new Vector2(widthPx / foam.Width * 0.75f, heightPx / foam.Height * (0.5f + 0.3f * u)),
                    SpriteEffects.None, 0);
            }
        }

        //接近期：从海面里长出来的行进浪包——水体由着色器画，肩部涌线把浪接回平静海面
        private void DrawApproachSwell(Texture2D foam, Texture2D glow, Texture2D spindle, float dim) {
            float p = ApproachProgress;
            float height = CurrentHeight;
            Vector2 crest = new(CrestX, crestSurfaceY);
            Vector2 screen = crest - Main.screenPosition;
            Color ambient = Lighting.GetColor((int)(CrestX / 16f), (int)((crestSurfaceY - height * 0.5f) / 16f));
            float lean = LandDir * (0.06f + 0.16f * p);//浅滩化前倾：越近岸越陡

            //肩部涌线：浪前后两道半沉于水线的暗涌，把浪包接回平静海面（底部锚定连续，不悬空）
            for (int s = 0; s < 2; s++) {
                float off = (s == 0 ? 1.4f : -1f) * (46f + height * 0.55f);
                Main.EntitySpriteDraw(spindle, screen + new Vector2(LandDir * off, 2f), null,
                    new Color(12, 32, 50) * (0.30f * p * dim), MathHelper.PiOver2,
                    spindle.Size() / 2f, new Vector2(0.4f - 0.08f * s, 2.6f - 0.6f * s), SpriteEffects.None, 0);
            }

            //浪体：浪锋落在 CrestX、底缘压进水线 6px（浪从海里长出来），越近岸浪面越陡、翻涌越烈
            DrawWaveBody(new Vector2(CrestX, crestSurfaceY + 6f),
                height * 2.1f + 120f, height * 1.6f,
                0.52f + 0.48f * p, 0f, 0.30f + 0.55f * p,
                (0.55f + 0.45f * p) * dim, LitK(ambient));

            //远海预告白线：150 帧预告的远视载体，从出生起可读（A=0 加色仅作细线点缀）
            float lineAlpha = (0.20f + 0.35f * MathF.Sqrt(p)) * dim;
            Main.EntitySpriteDraw(glow, screen + new Vector2(0f, -height), null,
                new Color(205, 232, 244, 0) * lineAlpha, 0f,
                glow.Size() / 2f, new Vector2(1.5f + 1.3f * p, 0.14f), SpriteEffects.None, 0);
            //前坡水光：湿面反光敷料（A=0，占比很小）
            Main.EntitySpriteDraw(glow, screen + new Vector2(LandDir * height * 0.30f, -height * 0.45f), null,
                new Color(120, 190, 215, 0) * (0.28f * p * dim), lean + LandDir * 1.1f,
                glow.Size() / 2f, new Vector2(height / 64f, 0.32f), SpriteEffects.None, 0);
            //临破前浪唇白沫：真 alpha 沫块压在冠线上，破碎的最后预兆
            float foamGrow = MathHelper.Clamp((p - 0.55f) / 0.45f, 0f, 1f);
            if (foamGrow > 0.01f) {
                Color foamLit = LitFoam(new Color(230, 242, 248), ambient);
                float time = (float)Main.timeForVisualEffects;
                for (int i = -1; i <= 1; i++) {
                    float jig = MathF.Sin(time * 0.12f + i * 2.1f + Projectile.identity);
                    Vector2 lip = screen + new Vector2(
                        i * height * 0.18f + jig * 3f + Main.windSpeedCurrent * 14f,
                        -height * 0.96f + MathF.Abs(i) * height * 0.05f + jig * 2f);
                    Main.EntitySpriteDraw(foam, lip, null,
                        foamLit * ((0.30f - 0.06f * MathF.Abs(i)) * foamGrow * dim),
                        i * 0.8f + jig * 0.2f, foam.Size() / 2f,
                        new Vector2(0.14f, 0.08f) * (0.7f + 0.5f * foamGrow), SpriteEffects.None, 0);
                }
            }
        }

        //破碎+扫滩+余韵：浪峰卷落→与判定同高的白水涌锋推进→泡沫残迹消退+滩水回流
        private void DrawBreakAndWash(Texture2D spraySheet, Texture2D foam,
            Texture2D glow, Texture2D spindle, float dim) {
            float sweep = SweepProgress;
            float fade = FadeFactor;
            float height = CurrentHeight;
            float frontX = FrontX;
            float baseY = SurfaceWorldY;
            float time = (float)Main.timeForVisualEffects;
            Vector2 foamOrigin = foam.Size() / 2f;
            Color ambient = Lighting.GetColor((int)(frontX / 16f), (int)((baseY - height * 0.5f) / 16f));
            Color foamLit = LitFoam(new Color(228, 240, 246), ambient);

            //身后白涌：岸线到浪锋之间贴着滩涂的湿白水毯，离锋越远越薄
            int washSegs = 5;
            for (int i = 0; i < washSegs; i++) {
                float u = (i + 0.5f) / washSegs;
                float x = MathHelper.Lerp(ShoreWorldX, frontX, u);
                float thin = 0.3f + 0.7f * u;//锋后新湿，岸边先干
                float alpha = 0.30f * thin * fade * MathHelper.Clamp(sweep * 3f, 0f, 1f) * dim;
                if (alpha < 0.01f) {
                    continue;
                }
                float carpetY = MathF.Min(GroundYNear(x), baseY);
                Main.EntitySpriteDraw(foam, new Vector2(x, carpetY - 4f) - Main.screenPosition, null,
                    foamLit * alpha, 0f, foamOrigin,
                    new Vector2(0.30f, 0.05f), SpriteEffects.None, 0);
            }

            //浪锋涌锋：与判定带同轴同高的水体，破碎期钉在岸线过卷，扫滩期向陆推进，余韵期自冠而下蚀掉
            float boreAlpha = InHostileWindow ? 1f : MathHelper.Clamp((fade - 0.35f) / 0.65f, 0f, 1f);
            if (boreAlpha > 0.01f) {
                float boreBase = MathF.Min(GroundYNear(frontX), baseY) + 6f;
                float boreH = height * 1.5f;
                //破碎拍最猛：前倾与翻涌烈度在破碎期冲到峰值，扫滩期回落成稳定推进
                float churn = 0.55f + 0.85f * BreakProgress - 0.30f * sweep;
                float collapse = InHostileWindow ? 0f : MathHelper.Clamp((1f - fade) * 1.5f, 0f, 1f);
                //浪锋对齐判定带轴：可见白水锋到哪，判定就到哪
                DrawWaveBody(new Vector2(frontX, boreBase), height * 1.7f + 90f, boreH,
                    1f, collapse, churn, boreAlpha * dim, LitK(ambient));
                //锋后翻滚沫团：滚动的碎沫贴地跟进，白沫脱离锋面独立活着
                for (int i = 0; i < 4; i++) {
                    float back = (i + 1) * (26f + height * 0.10f);
                    float bx = frontX - LandDir * back;
                    if (LandDir > 0 ? bx < ShoreWorldX - 30f : bx > ShoreWorldX + 30f) {
                        continue;//不越过岸线向海
                    }
                    float roll = time * 0.22f * LandDir + i * 1.7f + Projectile.identity;
                    float bh = height * (0.62f - 0.11f * i);
                    float by = MathF.Min(GroundYNear(bx), baseY);
                    Main.EntitySpriteDraw(foam, new Vector2(bx, by - bh * 0.5f) - Main.screenPosition, null,
                        foamLit * (boreAlpha * (0.44f - 0.08f * i) * dim), roll,
                        foamOrigin, new Vector2(0.14f + 0.02f * i, 0.11f), SpriteEffects.None, 0);
                }
                //锋顶亮沫（A=0 加色敷料）
                Main.EntitySpriteDraw(glow, new Vector2(frontX, boreBase - boreH * 0.62f) - Main.screenPosition, null,
                    new Color(220, 240, 248, 0) * (0.42f * boreAlpha * dim), 0f,
                    glow.Size() / 2f, new Vector2(0.9f, 0.3f), SpriteEffects.None, 0);
            }

            //破碎抛沫块：碎沫块抛洒，活过破碎+扫滩前段
            DrawSprayChunks(spraySheet, foamLit, dim);

            //余韵泡沫补丁：贴着滩涂地面明灭消退，活得比判定窗久（消散而非删除）
            if (!InHostileWindow) {
                for (int i = 0; i < WashPatches; i++) {
                    if (!washOk[i]) {
                        continue;
                    }
                    float hash = (Projectile.identity * 3.7f + i * 2.9f) % 1f;
                    float flicker = 0.7f + 0.3f * MathF.Sin(time * 0.08f + hash * 9f);
                    float x = ShoreWorldX + LandDir * SweepReachPx * ((i + 0.5f) / WashPatches);
                    Main.EntitySpriteDraw(foam, new Vector2(x, washGroundY[i] - 3f) - Main.screenPosition, null,
                        foamLit * (0.30f * fade * flicker * dim),
                        hash * 6f, foamOrigin, new Vector2(0.10f + hash * 0.05f, 0.05f), SpriteEffects.None, 0);
                }
                //滩水回流：一道半沉暗涌沿坡退回海里（水有去处，不凭空消失）
                if (fade > 0.05f) {
                    float slide = (1f - fade) * 150f;
                    Main.EntitySpriteDraw(spindle,
                        new Vector2(ShoreWorldX - LandDir * (30f + slide), baseY + 2f) - Main.screenPosition, null,
                        new Color(12, 32, 50) * (0.26f * fade * dim), MathHelper.PiOver2,
                        spindle.Size() / 2f, new Vector2(0.4f, 2.4f), SpriteEffects.None, 0);
                }
            }
        }

        //破碎抛沫块：无状态确定性轨迹（哈希出生点+初速+重力），各端画面一致
        private void DrawSprayChunks(Texture2D spraySheet, Color foamLit, float dim) {
            int t = Elapsed - ApproachFrames;
            const int ChunkLife = 34;
            if (t < 0 || t >= ChunkLife) {
                return;
            }
            float baseY = SurfaceWorldY;
            for (int k = 0; k < 8; k++) {
                float h1 = Hash01(k * 7 + 1);
                float h2 = Hash01(k * 7 + 2);
                float h3 = Hash01(k * 7 + 3);
                float h4 = Hash01(k * 7 + 4);
                float vx = LandDir * (1.2f + 3.4f * h1);
                float vy = -(2.0f + 3.2f * h2);
                float x = ShoreWorldX + LandDir * (h3 * 40f - 8f) + vx * t;
                float y = baseY - WaveHeight * (0.7f + 0.28f * h4) + vy * t + 0.11f * t * t;
                float life = MathHelper.Clamp(t / 3f, 0f, 1f) * MathHelper.Clamp((ChunkLife - t) / 10f, 0f, 1f);
                int cell = k % 9;
                Rectangle src = new(cell % 3 * 170 + 2, cell / 3 * 170 + 2, 166, 166);
                Main.EntitySpriteDraw(spraySheet, new Vector2(x, y) - Main.screenPosition, src,
                    foamLit * (0.85f * life * dim), h2 * MathHelper.TwoPi + LandDir * t * 0.09f,
                    new Vector2(83f, 83f), 0.30f + 0.22f * h1, SpriteEffects.None, 0);
            }
        }

        /// <summary>0~1 确定性哈希：以弹幕 identity 为种子，各端一致</summary>
        private float Hash01(int salt) => (Projectile.identity * 0.6180339f + salt * 0.7548777f) % 1f;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 4 },
                new Vector2(ShoreWorldX, SurfaceWorldY));
        }
    }
}
