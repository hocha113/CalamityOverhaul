using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.LaserTurrets
{
    /// <summary>
    /// 激光塔光束绘制辅助:白热芯线+外辉的分段光束,活束与余辉共用一支笔。
    /// 灰度线性贴图禁整条拉伸(两端平切),体层走12段炮口端软收包络,
    /// 芯线用四芒星横拉(星臂自然收尖,两端天然收口)
    /// </summary>
    internal static class DefLaserBeamDraw
    {
        private const int Segments = 12;

        /// <param name="widthMul">宽度乘数,余辉塌缩用</param>
        /// <param name="alphaMul">整体透明度乘数</param>
        /// <param name="erodeFrom">体层自炮口侧侵蚀的进度 0~1,活束传 0</param>
        internal static void Draw(SpriteBatch sb, Vector2 start, Vector2 end,
            float widthMul, float alphaMul, float erodeFrom) {
            Vector2 delta = end - start;
            float len = delta.Length();
            if (len < 8f || alphaMul <= 0.01f) {
                return;
            }
            Texture2D line = CWRUtils.GetT2DAsset(CWRConstant.Masking + "MaskLaserLine")?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (line == null || star == null) {
                return;
            }

            float rot = delta.ToRotation();
            Color red = LaserTurretBolt.LaserRed;
            red.A = 0;
            Color white = new(255, 235, 235, 0);

            //---- 体层:12段分段包络,炮口端8%软收+侵蚀前沿 ----
            float segLen = len / Segments;
            Vector2 segOriginMid = new(0f, line.Height * 0.5f);
            for (int i = 0; i < Segments; i++) {
                float u0 = i / (float)Segments;
                float mid = (i + 0.5f) / Segments;
                //炮口端软收(收口契约:起点不许平切)
                float env = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(mid / 0.08f, 0f, 1f));
                //余辉自炮口侧向命中点排空
                float erode = MathHelper.Clamp((mid - erodeFrom) / 0.10f, 0f, 1f);
                float a = env * erode * alphaMul;
                if (a <= 0.01f) {
                    continue;
                }
                Vector2 pos = start + delta * u0;
                //1.06 段间重叠防接缝
                Vector2 scale = new(segLen * 1.06f / line.Width, 0.11f * widthMul);
                sb.Draw(line, pos, null, red * (0.55f * a), rot, segOriginMid, scale, SpriteEffects.None, 0f);
            }

            //---- 芯线:四芒星横拉,星臂自然收尖,两端天然收口 ----
            Vector2 mid2 = (start + end) * 0.5f;
            Vector2 starOrigin = star.Size() * 0.5f;
            float lenScale = len / star.Width * 1.02f;
            sb.Draw(star, mid2, null, red * (0.75f * alphaMul), rot, starOrigin,
                new Vector2(lenScale, 0.055f * widthMul), SpriteEffects.None, 0f);
            sb.Draw(star, mid2, null, white * (0.9f * alphaMul), rot, starOrigin,
                new Vector2(lenScale * 0.97f, 0.026f * widthMul), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 激光塔光束弹:高 extraUpdates 的准直线弹,单体命中即灭。
    /// 普通 ModProjectile,由权威端生成,spawn包天然广播。
    /// 视觉=炮口到弹头的实时光束(白热芯+外辉),炮口聚焦环收口,
    /// 死亡时交棒给 <see cref="PRT_DefLaserAfterline"/> 余辉+灼痕(四相预算的余相)
    /// </summary>
    internal class LaserTurretBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>激光红,与塔身色调同源</summary>
        internal static readonly Color LaserRed = new(255, 90, 90);

        /// <summary>发射口位置,首个AI帧从spawn位置捕获(各端一致)</summary>
        private Vector2 spawnPos;
        private bool spawnInit;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 24;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //每帧实际位移约248px,1200px射程内5帧到达,读作射线
            Projectile.extraUpdates = 30;
            Projectile.DamageType = DamageClass.Default;
        }

        public override void AI() {
            //首个子更新:捕获炮口位置+回调塔重置伪冷却(全端各自执行)
            if (!spawnInit) {
                spawnInit = true;
                spawnPos = Projectile.Center;
                NotifySourceTurret();
            }

            //首帧发射声(每端各自播放,位置声自然衰减)
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, LaserRed.ToVector3() * 0.5f);

            //高倍更新下低概率补光尘,光束主体由 PreDraw 绘制
            if (Main.rand.NextBool(10)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, Vector2.Zero, 120, default, 0.7f);
                dust.noGravity = true;
            }
        }

        /// <summary>找到64px内的发射塔:重置其客户端伪冷却+点亮炮口闪光</summary>
        private void NotifySourceTurret() {
            if (Main.dedServ) {
                return;
            }
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (tp is LaserTurretTP laser && laser.Active
                    && laser.MuzzlePosition.DistanceSQ(spawnPos) < 64f * 64f) {
                    laser.NotifyFired();
                    break;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //命中/撞墙迸溅,OnKill 在每个端各自执行,队友可见
            Vector2 impact = Projectile.Center;
            if (!spawnInit) {
                spawnPos = impact;
            }
            Vector2 dir = impact == spawnPos ? Vector2.UnitX : spawnPos.To(impact).UnitVector();

            //火花:沿反射向散开,重力下坠
            for (int i = 0; i < 9; i++) {
                Vector2 vel = (-dir).RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f)) * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_DefEmber>(impact, vel, LaserRed,
                    Main.rand.NextFloat(0.5f, 1f))?.Configure(Main.rand.Next(14, 26), 0.14f);
            }
            //命中灼痕:余温短存,活得比光束久
            PRTLoader.NewParticle<PRT_DefScorch>(impact, Vector2.Zero, new Color(255, 120, 90),
                Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(38, 50), 0.8f);
            //余辉光束:自炮口侧排空塌缩+命中点爆闪,接管四相预算的余相
            PRTLoader.NewParticle<PRT_DefLaserAfterline>(impact, Vector2.Zero, LaserRed, 1f)
                ?.Configure(spawnPos, impact);

            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!spawnInit) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //活束:炮口→弹头
            DefLaserBeamDraw.Draw(sb, spawnPos, Projectile.Center, 1f, 1f, 0f);

            //弹龄(实帧),供炮口聚焦环塌缩包络
            float ageFrames = (150 - Projectile.timeLeft) / 31f;

            //炮口聚焦环收口:两圈向炮口收拢的环,出膛后数帧内塌缩闭合
            Texture2D ringTex = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DiffusionCircle4")?.Value;
            if (ringTex != null && ageFrames < 6f) {
                float ringT = ageFrames / 6f;
                Color ring = LaserRed;
                ring.A = 0;
                Vector2 muzzleScreen = spawnPos - Main.screenPosition;
                float s1 = MathHelper.Lerp(0.34f, 0.10f, ringT);
                float s2 = MathHelper.Lerp(0.22f, 0.06f, MathHelper.Clamp(ringT * 1.3f, 0f, 1f));
                sb.Draw(ringTex, muzzleScreen, null, ring * (0.7f * (1f - ringT)), 0f,
                    ringTex.Size() * 0.5f, s1, SpriteEffects.None, 0f);
                sb.Draw(ringTex, muzzleScreen, null, ring * (0.5f * (1f - ringT)), 0f,
                    ringTex.Size() * 0.5f, s2, SpriteEffects.None, 0f);
            }

            //弹头亮核:外层色光+内层白心(飞行中的前端收口)
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 origin = star.Size() / 2;
            Color core = LaserRed;
            core.A = 0;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            sb.Draw(star, drawPos, null, core, 0f, origin, 0.24f, SpriteEffects.None, 0f);
            Color white = Color.White;
            white.A = 0;
            sb.Draw(star, drawPos, null, white * 0.85f, 0f, origin, 0.11f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
