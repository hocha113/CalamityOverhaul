using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Tidecall.Projectiles
{
    /// <summary>
    /// 「离岸流」：海面表层的周期性暗流走廊。ai[0]=向海方向 ai[1]=水面行锚。
    /// 预告 75 帧（走廊泡沫线渐亮+海底水草倾斜指向深海+气泡上浮）→
    /// 拖拽 240 帧（表层水中玩家受向深海的持续拖拽，无伤害纯位移）→ 平息 55 帧。
    /// 只作用于海水表层（水面下 ≤7 格）：岸上、浅滩涉水、深潜一概不受影响（防撞契约：
    /// 表层归 Tidecall，水下漩涡归 Lumindepth，深渊下沉流归 Nyxdepth）。
    /// 拖拽对本机玩家逐端本地施加（位移权威在玩家客户端），生成决策在权威端
    /// </summary>
    internal class TidecallRipCurrentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TelegraphFrames = 75;
        private const int PullFrames = 240;
        private const int FadeFrames = 55;
        /// <summary>走廊半长（像素）</summary>
        private const float HalfLenPx = 540f;
        /// <summary>拖拽加速度与速度上限：可对抗、可垂直脱离的位移挑战</summary>
        private const float PullAccel = 0.22f;
        private const float PullSpeedCap = 5f;
        /// <summary>表面采样列数（绘制用）</summary>
        private const int SurfSamples = 24;
        private const int KelpCount = 4;

        private int SeaDir => (int)Projectile.ai[0];
        private int AnchorSurfaceY => (int)Projectile.ai[1];
        private int TotalLife => TelegraphFrames + PullFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>预告亮度 0~1 / 拖拽期恒 1 / 平息期回落</summary>
        private float Envelope {
            get {
                int t = Elapsed;
                if (t < TelegraphFrames) {
                    return t / (float)TelegraphFrames;
                }
                int fade = t - TelegraphFrames - PullFrames;
                if (fade <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - fade / (float)FadeFrames, 0f, 1f);
            }
        }

        private bool InPullWindow => Elapsed >= TelegraphFrames && Elapsed < TelegraphFrames + PullFrames;

        //==== 客户端绘制缓存 ====
        private readonly float[] surfaceY = new float[SurfSamples];
        private readonly bool[] surfaceOk = new bool[SurfSamples];
        private readonly float[] kelpBedY = new float[KelpCount];
        private readonly bool[] kelpOk = new bool[KelpCount];
        private int probeRefreshIn;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//全程无伤害，纯位移挑战
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + PullFrames + FadeFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //Boss 在场：位移机制暂停，时间轴照走，表现转入低敛（各端 HasBoss 结论一致）
            bool suspended = CWRWorld.HasBoss;

            if (Main.dedServ) {
                return;//服务端只承载时间轴与同步
            }

            if (elapsed == 0) {
                //预告起点的听觉通道：低沉两声水响
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.62f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 4 },
                    Projectile.Center + new Vector2(SeaDir * 220f, 0f));
            }
            else if (elapsed == TelegraphFrames && !suspended) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
            }

            if (--probeRefreshIn <= 0) {
                probeRefreshIn = 20;
                RefreshProbes();
            }

            if (!suspended && InPullWindow) {
                //水声增益上报：随本机玩家与走廊的距离衰减
                float proximity = 1f - MathHelper.Clamp(
                    (MathF.Abs(Main.LocalPlayer.Center.X - Projectile.Center.X) - HalfLenPx) / 900f, 0f, 1f);
                TidecallAmbience.ReportRipFlow(Envelope * proximity);
                PullLocalPlayer();
            }

            SpawnAmbientDust(elapsed, suspended);
        }

        /// <summary>拖拽本机玩家：只在走廊内且处于海水表层时生效，位移由各自客户端本地结算</summary>
        private void PullLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (!player.active || player.dead) {
                return;
            }
            if (MathF.Abs(player.Center.X - Projectile.Center.X) > HalfLenPx) {
                return;
            }
            if (!TidecallAmbience.InSurfWater(player)) {
                return;//岸上与浅滩不受影响；深潜脱离表层也不受影响
            }
            if (SeaDir * player.velocity.X < PullSpeedCap) {
                player.velocity.X += SeaDir * PullAccel;
            }
        }

        //表面与水草锚点探针（纯绘制用，各端本地取样）
        private void RefreshProbes() {
            int centerX = (int)(Projectile.Center.X / 16f);
            int refY = AnchorSurfaceY;
            for (int i = 0; i < SurfSamples; i++) {
                float fx = (i + 0.5f) / SurfSamples * 2f - 1f;
                int tileX = centerX + (int)(fx * HalfLenPx / 16f);
                surfaceOk[i] = TidecallAmbience.TryFindWaterSurface(tileX, refY + 2, out int sy);
                surfaceY[i] = sy * 16f;
            }
            //水草长在走廊向陆半段的海床上
            for (int k = 0; k < KelpCount; k++) {
                int tileX = KelpTileX(k);
                kelpOk[k] = false;
                if (!TidecallAmbience.TryFindWaterSurface(tileX, refY + 2, out int sy)) {
                    continue;
                }
                int depth = TidecallAmbience.WaterDepthTiles(tileX, sy);
                if (depth < 3 || depth >= 24) {
                    continue;
                }
                kelpBedY[k] = (sy + depth) * 16f;
                kelpOk[k] = true;
            }
        }

        private int KelpTileX(int k) {
            //确定性散布：向陆半段 0.15~0.85 位置
            float u = (k + 0.5f) / KelpCount * 0.7f + 0.15f;
            return (int)((Projectile.Center.X - SeaDir * HalfLenPx * u) / 16f);
        }

        //气泡上浮与表层流尘（预告 ≤20/s，拖拽期 ≤30/s，屏外剔除由引擎粉尘距离机制兜底）
        private void SpawnAmbientDust(int elapsed, bool suspended) {
            float env = Envelope;
            if (env < 0.1f || suspended) {
                return;
            }
            if (elapsed < TelegraphFrames) {
                if (Main.rand.NextBool(3)) {
                    int i = Main.rand.Next(SurfSamples);
                    if (surfaceOk[i]) {
                        Dust bubble = Dust.NewDustPerfect(
                            new Vector2(SampleWorldX(i), surfaceY[i] + Main.rand.NextFloat(12f, 60f)),
                            DustID.Water, new Vector2(SeaDir * 0.4f, -Main.rand.NextFloat(0.8f, 1.8f)),
                            120, default, 0.9f * env);
                        bubble.noGravity = true;
                    }
                }
                return;
            }
            if (InPullWindow && Main.rand.NextBool(2)) {
                //表层向海流尘：方向本身就是信息
                int i = Main.rand.Next(SurfSamples);
                if (surfaceOk[i]) {
                    Dust flow = Dust.NewDustPerfect(
                        new Vector2(SampleWorldX(i), surfaceY[i] + Main.rand.NextFloat(6f, 90f)),
                        DustID.Water, new Vector2(SeaDir * Main.rand.NextFloat(2.5f, 4f), 0f),
                        100, default, Main.rand.NextFloat(0.8f, 1.3f));
                    flow.noGravity = true;
                }
            }
        }

        private float SampleWorldX(int i)
            => Projectile.Center.X + ((i + 0.5f) / SurfSamples * 2f - 1f) * HalfLenPx;

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.02f) {
                return false;
            }
            float dim = (CWRWorld.HasBoss ? 0.45f : 1f) * TidecallAmbience.BossDim;
            Texture2D foam = CWRAsset.Fog?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            if (foam == null || glow == null || spindle == null) {
                return false;
            }

            bool pulling = InPullWindow;
            float time = (float)Main.timeForVisualEffects;
            Vector2 foamOrigin = foam.Size() / 2f;
            Vector2 glowOrigin = glow.Size() / 2f;

            //一、泡沫线：沿水面逐点铺泡沫团；拖拽期叠加一列向海行进的明暗波（方向可读）
            for (int i = 0; i < SurfSamples; i++) {
                if (!surfaceOk[i]) {
                    continue;
                }
                float wave = pulling
                    ? 0.65f + 0.35f * MathF.Sin(SeaDir * (SampleWorldX(i) * 0.016f) - time * 0.30f)
                    : 0.75f + 0.25f * MathF.Sin(time * 0.06f + i * 1.7f);
                float alpha = env * wave * dim;
                Vector2 pos = new Vector2(SampleWorldX(i), surfaceY[i]) - Main.screenPosition;
                //白沫团（真 alpha 实体层）
                Main.EntitySpriteDraw(foam, pos, null, new Color(228, 240, 246) * (0.34f * alpha),
                    i * 0.7f, foamOrigin, new Vector2(0.16f, 0.07f), SpriteEffects.None, 0);
                //亮沫敷料（A=0 加色）
                Main.EntitySpriteDraw(glow, pos, null, new Color(190, 230, 240, 0) * (0.5f * alpha),
                    0f, glowOrigin, new Vector2(0.55f, 0.13f), SpriteEffects.None, 0);
            }

            //二、水草倾斜：三节梭段自海床向上堆叠，倾角随预告增长压向深海
            float lean = env * (pulling ? 1f : 0.8f);
            for (int k = 0; k < KelpCount; k++) {
                if (!kelpOk[k]) {
                    continue;
                }
                float sway = MathF.Sin(time * 0.05f + k * 2.3f + Projectile.identity) * 0.08f;
                float baseRot = SeaDir * (0.32f + 0.55f * lean) + sway;
                Vector2 seg = new(KelpTileX(k) * 16f + 8f, kelpBedY[k]);
                Color kelpColor = new Color(30, 72, 52) * (0.75f * MathHelper.Clamp(env + 0.35f, 0f, 1f) * dim);
                for (int s = 0; s < 3; s++) {
                    float rot = baseRot * (0.6f + 0.28f * s);
                    Main.EntitySpriteDraw(spindle, seg - Main.screenPosition, null,
                        kelpColor * (1f - 0.18f * s), rot,
                        new Vector2(spindle.Width * 0.5f, spindle.Height * 0.86f),
                        new Vector2(0.34f - 0.06f * s, 0.62f), SpriteEffects.None, 0);
                    //下一节从本节顶端继续生长
                    seg += new Vector2(MathF.Sin(rot), -MathF.Cos(rot)) * 26f;
                }
            }

            //三、拖拽期表层流线：几条向海拉伸的窄亮带在表层滑动
            if (pulling) {
                for (int j = 0; j < 6; j++) {
                    float hash = (Projectile.identity * 2.39f + j * 5.71f) % 1f;
                    float drift = (time * (2.6f + hash * 1.4f) * SeaDir + j * 360f) % (HalfLenPx * 2f);
                    if (drift < 0f) {
                        drift += HalfLenPx * 2f;
                    }
                    float x = Projectile.Center.X - HalfLenPx + drift;
                    int nearest = (int)MathHelper.Clamp(
                        (x - Projectile.Center.X + HalfLenPx) / (HalfLenPx * 2f) * SurfSamples, 0, SurfSamples - 1);
                    if (!surfaceOk[nearest]) {
                        continue;
                    }
                    Vector2 pos = new Vector2(x, surfaceY[nearest] + 22f + hash * 64f) - Main.screenPosition;
                    //端部由贴图自身径向衰减收口，避免两端硬切
                    Main.EntitySpriteDraw(glow, pos, null, new Color(150, 208, 224, 0) * (0.30f * env * dim),
                        0f, glowOrigin, new Vector2(2.4f, 0.10f), SpriteEffects.None, 0);
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //平息收尾：一声轻水响
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
