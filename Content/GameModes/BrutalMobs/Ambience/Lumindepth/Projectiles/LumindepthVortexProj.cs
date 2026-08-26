using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Lumindepth.Projectiles
{
    /// <summary>
    /// 静谧涡流（水下优雅漩涡，场地实体，恒零伤害）。ai[0]=绑定档位。
    /// 预告 75 帧：绕圈旋转的光斑收拢、水流线可见聚拢，配双声道听觉预告 →
    /// 缓拉 270 帧：向心+切向的温和拽引，只封顶朝心分速，玩家自己的逃逸出力不设限 →
    /// 消散 60 帧：光斑散开平息。
    /// 原型区分：与海面离岸流（直线拖向深海）和深渊下沉流（垂直下坠）不同，
    /// 本体全程水下、缓慢、以旋转美感为主。
    /// 两端以同一 ai 值各自展开时间轴；拽引由各端只对本机玩家结算（玩家位置本机权威）
    /// </summary>
    internal class LumindepthVortexProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 75;
        /// <summary>缓拉存续帧（档位不改时长，只调频率与拉力）</summary>
        private const int PullFrames = 270;
        private const int FadeFrames = 60;
        private const int TotalLife = TelegraphFrames + PullFrames + FadeFrames;
        /// <summary>影响半径（像素，可见光斑圈=判定圈）</summary>
        private const float Radius = 330f;
        /// <summary>向心加速度档位表</summary>
        private static readonly float[] PullAccelByTier = [0.030f, 0.040f, 0.050f];
        /// <summary>朝心分速封顶档位表（低于玩家泳速上限，保证可挣脱）</summary>
        private static readonly float[] PullInSpeedCapByTier = [1.30f, 1.55f, 1.80f];

        private int Tier => (int)MathHelper.Clamp(Projectile.ai[0], 1f, 3f);
        private int Elapsed => TotalLife - Projectile.timeLeft;
        /// <summary>旋向：identity 决定，所有端一致</summary>
        private float SwirlDir => (Projectile.identity & 1) == 0 ? 1f : -1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;//纯位移挑战，恒无伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //存续期常量展开，两端各自走同一时间轴，生成后不再改 timeLeft
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    //听觉预告起拍：清亮晶音+闷水声双层
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.30f, Pitch = -0.05f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.42f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            bool pulling = elapsed >= TelegraphFrames && elapsed < TelegraphFrames + PullFrames;

            if (!Main.dedServ) {
                ClientPresentation(elapsed, pulling);
                //Boss 在场暂停一切位移机制，视觉照常收尾
                if (pulling && !CWRWorld.HasBoss) {
                    ApplyPullToLocalPlayer(elapsed);
                }
            }
        }

        /// <summary>本机演出：节拍音效与水尘（决不影响任何判定）</summary>
        private void ClientPresentation(int elapsed, bool pulling) {
            if (elapsed == TelegraphFrames / 2) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.26f, Pitch = -0.25f, MaxInstances = 3 }, Projectile.Center);
            }
            if (elapsed == TelegraphFrames) {
                //涡成之拍
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.55f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.20f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (pulling && elapsed % 45 == 0) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.18f, Pitch = -0.65f, MaxInstances = 3 }, Projectile.Center);
            }
            if (elapsed == TelegraphFrames + PullFrames) {
                //平息之拍
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.30f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            }

            //预告期：外缘向心的聚拢水尘，让"水流线聚拢"离开屏幕中心也可读
            if (elapsed < TelegraphFrames && elapsed % 5 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = Projectile.Center + ang.ToRotationVector2() * (Radius * Main.rand.NextFloat(0.8f, 1f));
                Vector2 vel = (Projectile.Center - rim).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.6f, 1.1f);
                Dust converge = Dust.NewDustPerfect(rim, DustID.DungeonSpirit, vel, 150, new Color(120, 220, 255), 0.8f);
                converge.noGravity = true;
            }
            //缓拉期：螺旋入涡的水尘流
            if (pulling) {
                if (elapsed % 4 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float r = Radius * Main.rand.NextFloat(0.35f, 0.95f);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
                    Vector2 inward = (Projectile.Center - pos) / r;
                    Vector2 swirl = inward.RotatedBy(MathHelper.PiOver2 * SwirlDir);
                    Dust stream = Dust.NewDustPerfect(pos, DustID.DungeonSpirit,
                        inward * 1.1f + swirl * 0.9f, 160, new Color(110, 210, 250), Main.rand.NextFloat(0.7f, 1f));
                    stream.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.08f, 0.20f, 0.26f));
            }
        }

        /// <summary>拽引结算：各端只动本机玩家（位置本机权威，天然同步）</summary>
        private void ApplyPullToLocalPlayer(int elapsed) {
            Player player = Main.LocalPlayer;
            if (!player.active || player.dead || !player.wet || player.grapCount > 0) {
                return;//出水、上钩、死亡都不吃拽引
            }
            Vector2 to = Projectile.Center - player.Center;
            float dist = to.Length();
            if (dist >= Radius || dist < 12f) {
                return;
            }
            //起止各 30 帧缓入缓出，拽引不突兀
            int pullT = elapsed - TelegraphFrames;
            float envelope = MathHelper.Clamp(Math.Min(pullT / 30f, (PullFrames - pullT) / 30f), 0f, 1f);
            //缘轻心沉的平滑衰减
            float t = 1f - dist / Radius;
            float strength = t * t * (3f - 2f * t);
            int tier = Tier;
            float accel = PullAccelByTier[tier - 1] * strength * envelope;
            Vector2 inward = to / dist;
            Vector2 swirl = inward.RotatedBy(MathHelper.PiOver2 * SwirlDir);
            player.velocity += inward * accel + swirl * (accel * 0.6f);
            //只封顶朝心分速：玩家自己的逃逸出力不设限（可挣脱契约）
            float inSpeed = Vector2.Dot(player.velocity, inward);
            float cap = PullInSpeedCapByTier[tier - 1];
            if (inSpeed > cap) {
                player.velocity -= inward * (inSpeed - cap);
            }
        }

        /// <summary>分段线性角速度的解析积分：预告缓起转、缓拉全速、消散滑停，相位连续</summary>
        private float SpinAngle(int elapsed) {
            const float SlowW = 0.010f;
            const float FastW = 0.030f;
            if (elapsed <= TelegraphFrames) {
                return SlowW * elapsed + (FastW - SlowW) * elapsed * elapsed / (2f * TelegraphFrames);
            }
            float thetaTel = (SlowW + FastW) * 0.5f * TelegraphFrames;
            int pullEnd = TelegraphFrames + PullFrames;
            if (elapsed <= pullEnd) {
                return thetaTel + FastW * (elapsed - TelegraphFrames);
            }
            float thetaPull = thetaTel + FastW * PullFrames;
            float ft = elapsed - pullEnd;
            float brake = MathHelper.Clamp(ft / FadeFrames, 0f, 1f);
            return thetaPull + FastW * ft * (1f - 0.5f * brake);
        }

        /// <summary>identity 播种的确定性杂值，所有端一致（弃用 Main.rand 防端间漂移）</summary>
        private float Hash01(int n) {
            float v = MathF.Sin(n * 12.9898f + Projectile.identity * 78.233f) * 43758.5453f;
            return v - MathF.Floor(v);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.localAI[0] == 0f) {
                return false;//时间轴尚未展开
            }
            int elapsed = Elapsed;
            float phaseIn = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            int pullEnd = TelegraphFrames + PullFrames;
            float fadeT = elapsed <= pullEnd ? 0f : MathHelper.Clamp((elapsed - pullEnd) / (float)FadeFrames, 0f, 1f);
            float env = elapsed < TelegraphFrames ? 0.35f + 0.65f * phaseIn : 1f - fadeT;
            if (env <= 0.01f) {
                return false;
            }
            bool pulling = elapsed >= TelegraphFrames && elapsed < pullEnd;
            float theta = SpinAngle(elapsed) * SwirlDir;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //水体涡盘：真 alpha 同心旋涡打底，携带深色水感（黑底图物理上做不了暗层，这里必须用真 alpha 图）
            Texture2D cyclone = CWRAsset.Cyclone.Value;
            float dishScale = Radius * 1.85f / cyclone.Width;
            Color dish = new Color(26, 88, 108) * (0.30f * env * (pulling ? 1f : 0.8f));
            Main.EntitySpriteDraw(cyclone, center, null, dish, theta,
                cyclone.Size() / 2f, dishScale, SpriteEffects.None, 0);

            //亮旋纹：同向更快的内层（加色敷料 A=0）
            Color innerSwirl = new Color(120, 216, 255, 0) * (0.26f * env);
            Main.EntitySpriteDraw(cyclone, center, null, innerSwirl, theta * 1.6f,
                cyclone.Size() / 2f, dishScale * 0.62f, SpriteEffects.None, 0);

            //汇聚流线：切向摆放的水流弧（预告期自外缘收拢，读作"水流线聚拢"）
            Texture2D flow = CWRAsset.Airflow.Value;
            float flowR = pulling ? Radius * 0.60f : Radius * (0.92f - 0.32f * phaseIn);
            if (fadeT > 0f) {
                flowR = Radius * (0.60f + 0.35f * fadeT);
            }
            for (int j = 0; j < 7; j++) {
                float a = theta * 0.8f + MathHelper.TwoPi * j / 7f;
                Vector2 pos = center + a.ToRotationVector2() * flowR;
                float rot = a + MathHelper.PiOver2 * SwirlDir;
                Rectangle src = new(0, (int)(Hash01(j) * 190f), flow.Width, 62);
                Color flowCol = new Color(140, 226, 255, 0) * (0.16f * env);
                Main.EntitySpriteDraw(flow, pos, src, flowCol, rot,
                    new Vector2(flow.Width / 2f, 31f), new Vector2(0.55f, 0.5f), SpriteEffects.None, 0);
            }

            //绕圈光斑：预告收拢、缓拉疾旋、消散松开平息
            Texture2D speck = CWRAsset.StarGlow01.Value;
            for (int i = 0; i < 10; i++) {
                float speed = 0.85f + 0.5f * Hash01(i + 20);
                float a = theta * speed + MathHelper.TwoPi * i / 10f + Hash01(i) * MathHelper.TwoPi;
                float orbitR = pulling
                    ? Radius * (0.55f + 0.06f * MathF.Sin(elapsed * 0.05f + i))
                    : Radius * (0.95f - 0.40f * phaseIn);
                if (fadeT > 0f) {
                    orbitR = Radius * (0.55f + 0.55f * fadeT);
                }
                Vector2 pos = center + a.ToRotationVector2() * orbitR;
                float sc = 0.13f + 0.08f * Hash01(i + 40);
                float twinkle = 0.40f + 0.20f * MathF.Sin(elapsed * 0.11f + i * 1.7f);
                Color speckCol = new Color(168, 232, 255, 0) * (twinkle * env);
                //切向对齐：旋转的是光斑群而不是一群平移的贴图
                Main.EntitySpriteDraw(speck, pos, null, speckCol, a + MathHelper.PiOver2,
                    speck.Size() / 2f, sc, SpriteEffects.None, 0);
            }

            //涡心柔光：缓拉期才亮起（垫底层，占比小）
            if (pulling || fadeT > 0f) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                float coreA = pulling ? 0.30f + 0.08f * MathF.Sin(elapsed * 0.09f) : 0.30f * (1f - fadeT);
                Color core = new Color(150, 230, 255, 0) * (coreA * env);
                Main.EntitySpriteDraw(glow, center, null, core, 0f,
                    glow.Size() / 2f, 1.6f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
