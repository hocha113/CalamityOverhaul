using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Tidecall.Projectiles
{
    /// <summary>
    /// 「疯狗浪」：低频大浪拍岸。ai[0]=向陆方向 ai[1]=浪高像素 ai[2]=岸线列。
    /// 预告：海面隆起白线自远海压来（150 帧，浪吼渐强，远超 45 帧公平线）→
    /// 破碎：浪峰在岸线炸开（12 帧）→ 扫滩：浪锋向陆推进约 24 格，对岸上玩家轻推+微量伤害，
    /// 脚底高于浪峰者免疫（站上高处躲浪）→ 余韵：泡沫湿痕在滩涂上消退（90 帧，判定早已关闭）。
    /// 位置全程由出生参数+timeLeft 确定性推演，各端一致，无需追加同步；
    /// 判定窗只覆盖破碎+扫滩，接近期的涌浪从泳者头顶无害滚过
    /// </summary>
    internal class TidecallRogueWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
                //浪峰唇口白沫（≤30/s，只活 2.5s）
                if (Main.rand.NextBool(2)) {
                    Dust lip = Dust.NewDustPerfect(
                        new Vector2(CrestX + Main.rand.NextFloat(-30f, 30f), crestSurfaceY - CurrentHeight),
                        DustID.Cloud, new Vector2(-LandDir * 0.5f, -Main.rand.NextFloat(0.5f, 1.5f)),
                        150, new Color(235, 245, 250), Main.rand.NextFloat(0.8f, 1.3f) * ApproachProgress);
                    lip.noGravity = true;
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
            }
            else if (!InApproach && washProbeIn-- <= 0) {
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

        //扫滩带内的地面高度（余韵泡沫要贴着滩涂）
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
            Texture2D foam = CWRAsset.Fog?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            if (foam == null || glow == null || spindle == null) {
                return false;
            }
            float dim = TidecallAmbience.BossDim;

            if (InApproach) {
                DrawApproachSwell(foam, glow, spindle, dim);
            }
            else {
                DrawBreakAndWash(foam, glow, dim);
            }
            return false;
        }

        //接近期：暗色水体隆包+顶缘白线，由远及近、越近越高
        private void DrawApproachSwell(Texture2D foam, Texture2D glow, Texture2D spindle, float dim) {
            float p = ApproachProgress;
            float height = CurrentHeight;
            Vector2 crest = new(CrestX, crestSurfaceY);
            Vector2 screen = crest - Main.screenPosition;
            float time = (float)Main.timeForVisualEffects;

            //浪肩：身后更宽更矮的暗水鼓包（真 alpha 实体，能真正压暗水面）
            Main.EntitySpriteDraw(spindle, screen + new Vector2(-LandDir * 60f, -height * 0.16f), null,
                new Color(14, 34, 52) * (0.34f * p * dim), MathHelper.PiOver2,
                spindle.Size() / 2f, new Vector2(height / 46f * 0.8f, 3.4f), SpriteEffects.None, 0);
            //浪身主体：横置暗水梭，抬离水面半个浪高
            Main.EntitySpriteDraw(spindle, screen + new Vector2(0f, -height * 0.42f), null,
                new Color(18, 42, 64) * (0.55f * p * dim), MathHelper.PiOver2,
                spindle.Size() / 2f, new Vector2(height / 46f * 1.15f, 2.5f), SpriteEffects.None, 0);

            //顶缘白线：远看是一条亮线，近了碎成沫团
            for (int i = -2; i <= 2; i++) {
                float jig = MathF.Sin(time * 0.11f + i * 1.9f + Projectile.identity);
                Vector2 lip = screen + new Vector2(i * 24f + jig * 3f, -height + jig * 2f);
                Main.EntitySpriteDraw(foam, lip, null,
                    new Color(232, 244, 250) * (0.42f * p * dim * (1f - 0.18f * MathF.Abs(i))),
                    i * 1.3f, foam.Size() / 2f, new Vector2(0.13f, 0.075f), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(glow, screen + new Vector2(0f, -height), null,
                new Color(210, 236, 246, 0) * (0.55f * p * dim), 0f,
                glow.Size() / 2f, new Vector2(1.7f + p, 0.16f), SpriteEffects.None, 0);
        }

        //破碎+扫滩+余韵：浪锋泡沫墙、身后白涌湿痕、退潮后滩涂泡沫补丁
        private void DrawBreakAndWash(Texture2D foam, Texture2D glow, float dim) {
            float sweep = SweepProgress;
            float fade = FadeFactor;
            float height = CurrentHeight;
            float frontX = FrontX;
            float baseY = SurfaceWorldY;
            float time = (float)Main.timeForVisualEffects;
            Vector2 foamOrigin = foam.Size() / 2f;

            //浪锋泡沫墙：扫滩期挺立前压，余韵期塌缩
            if (fade > 0.25f) {
                float wallAlpha = (InHostileWindow ? 0.62f : 0.62f * (fade - 0.25f) / 0.75f) * dim;
                for (int s = 0; s < 3; s++) {
                    float segY = baseY - height * (0.22f + 0.3f * s);
                    float jig = MathF.Sin(time * 0.14f + s * 2.2f) * 3f;
                    Main.EntitySpriteDraw(foam, new Vector2(frontX + jig, segY) - Main.screenPosition, null,
                        new Color(230, 242, 248) * (wallAlpha * (1f - 0.16f * s)),
                        LandDir * (0.22f + 0.1f * s), foamOrigin,
                        new Vector2(0.19f - 0.03f * s, 0.14f), SpriteEffects.None, 0);
                }
                //锋顶亮沫
                Main.EntitySpriteDraw(glow, new Vector2(frontX, baseY - height) - Main.screenPosition, null,
                    new Color(220, 240, 248, 0) * (0.5f * wallAlpha), 0f,
                    glow.Size() / 2f, new Vector2(0.9f, 0.3f), SpriteEffects.None, 0);
            }

            //身后白涌：岸线到浪锋之间的湿白水毯，离锋越远越薄
            int washSegs = 5;
            for (int i = 0; i < washSegs; i++) {
                float u = (i + 0.5f) / washSegs;
                float x = MathHelper.Lerp(ShoreWorldX, frontX, u);
                float thin = 0.3f + 0.7f * u;//锋后新湿，岸边先干
                float alpha = 0.30f * thin * fade * MathHelper.Clamp(sweep * 3f, 0f, 1f) * dim;
                if (alpha < 0.01f) {
                    continue;
                }
                Main.EntitySpriteDraw(foam, new Vector2(x, baseY - 4f) - Main.screenPosition, null,
                    new Color(225, 238, 245) * alpha, 0f, foamOrigin,
                    new Vector2(0.30f, 0.05f), SpriteEffects.None, 0);
            }

            //余韵泡沫补丁：贴着滩涂地面明灭消退，活得比判定窗久
            if (!InHostileWindow) {
                for (int i = 0; i < WashPatches; i++) {
                    if (!washOk[i]) {
                        continue;
                    }
                    float hash = (Projectile.identity * 3.7f + i * 2.9f) % 1f;
                    float flicker = 0.7f + 0.3f * MathF.Sin(time * 0.08f + hash * 9f);
                    float x = ShoreWorldX + LandDir * SweepReachPx * ((i + 0.5f) / WashPatches);
                    Main.EntitySpriteDraw(foam, new Vector2(x, washGroundY[i] - 3f) - Main.screenPosition, null,
                        new Color(228, 240, 246) * (0.30f * fade * flicker * dim),
                        hash * 6f, foamOrigin, new Vector2(0.10f + hash * 0.05f, 0.05f), SpriteEffects.None, 0);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 4 },
                new Vector2(ShoreWorldX, SurfaceWorldY));
        }
    }
}
