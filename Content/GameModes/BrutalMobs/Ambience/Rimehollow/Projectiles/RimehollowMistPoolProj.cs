using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow.Projectiles
{
    /// <summary>
    /// 「寒雾洼」：低洼处滞留的可见寒雾带（场地实体，恒无伤害）。
    /// ai[0]=半宽 ai[1]=存续帧 ai[2]=档位。
    /// 凝雾 60 帧由薄转浓 → 滞留 → 消散。
    /// 站进浓雾的本机玩家寒意缓慢累积（结算在 <see cref="RimehollowPlayer"/>，
    /// 满则短暂原版寒颤；快速通过无事），可见区=判定区。
    /// 档位只调寒意累积速度，雾带形状不变
    /// </summary>
    internal class RimehollowMistPoolProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int CondenseFrames = 60;
        private const int FadeFrames = 50;
        /// <summary>判定带：雾带自地面向上的高度（像素）</summary>
        private const float BandUp = 40f;
        private const float BandDown = 26f;
        /// <summary>寒意开始累积所需的雾浓度（薄雾不冻人）</summary>
        private const float ChillEnvGate = 0.55f;

        private float HalfWidth => Projectile.ai[0];
        private int ActiveFrames => (int)Projectile.ai[1];
        private int Tier => Math.Clamp((int)Projectile.ai[2], 1, 3);
        private int TotalLife => CondenseFrames + ActiveFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>浓度包络：凝雾升、滞留满、消散落</summary>
        private float Env {
            get {
                int elapsed = Elapsed;
                if (elapsed < CondenseFrames) {
                    return elapsed / (float)CondenseFrames;
                }
                if (elapsed < CondenseFrames + ActiveFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (elapsed - CondenseFrames - ActiveFrames) / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//纯氛围场地，恒无伤害
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
                //存续期由 ai[1] 决定，两端以同一 ai 值各自展开时间轴
                Projectile.timeLeft = TotalLife;
            }

            float env = Env;
            if (Main.dedServ) {
                return;
            }

            //雾内缓漂的冷尘（≤1 粒/9 帧，屏外不花预算）
            if (env > 0.3f && Main.rand.NextBool(9) && RimehollowAmbience.NearScreen(Projectile.Center)) {
                Dust mote = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.9f,
                        Main.rand.NextFloat(-BandUp * 0.6f, BandDown * 0.5f)),
                    DustID.Frost, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.1f, 0.06f)),
                    170, default, Main.rand.NextFloat(0.6f, 0.9f));
                mote.noGravity = true;
            }

            //浓雾里的本机玩家：向自己的 ModPlayer 上报寒意（结算与减益在那边）
            if (env >= ChillEnvGate) {
                Player local = Main.LocalPlayer;
                if (local.active && !local.dead && InZone(local.Hitbox)) {
                    local.GetModPlayer<RimehollowPlayer>().MistTouch(Tier);
                }
            }
        }

        /// <summary>判定盒与绘制共用同一几何（可见区=判定区）</summary>
        private bool InZone(Rectangle hitbox) {
            Rectangle zone = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - BandUp),
                (int)(HalfWidth * 2f), (int)(BandUp + BandDown));
            return zone.Intersects(hitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Env;
            if (env <= 0.02f) {
                return false;
            }
            //真 alpha 烟羽做雾体：两排团絮，底排浓、顶排薄，缓慢呼吸漂移
            Texture2D fog = CWRAsset.Fog.Value;
            Vector2 origin = fog.Size() / 2f;
            float time = Main.GlobalTimeWrappedHourly;
            float seed = Projectile.identity * 1.31f;

            //底排 5 团（贴地滞留的主体）
            for (int i = 0; i < 5; i++) {
                float off = (i / 4f * 2f - 1f) * HalfWidth * 0.82f;
                float edgeK = 1f - Math.Abs(off) / HalfWidth * 0.55f;
                Vector2 drift = new(
                    MathF.Sin(time * 0.4f + seed + i * 1.7f) * 9f,
                    MathF.Sin(time * 0.31f + seed + i * 2.6f) * 3f + 6f);
                float scale = HalfWidth / fog.Width * (0.68f + 0.1f * MathF.Sin(seed + i * 2.2f));
                Color tint = new Color(208, 226, 238) * (0.30f * env * edgeK);
                Main.EntitySpriteDraw(fog, Projectile.Center + new Vector2(off, 0f) + drift - Main.screenPosition,
                    null, tint, MathF.Sin(seed + i) * 0.3f, origin, scale, SpriteEffects.None, 0);
            }
            //顶排 4 团（雾面起伏，稍小稍淡）
            for (int i = 0; i < 4; i++) {
                float off = (i / 3f * 2f - 1f) * HalfWidth * 0.6f;
                float edgeK = 1f - Math.Abs(off) / HalfWidth * 0.5f;
                Vector2 drift = new(
                    MathF.Sin(time * 0.33f + seed + i * 2.1f) * 11f,
                    MathF.Sin(time * 0.27f + seed + i * 1.9f) * 4f - 20f);
                float scale = HalfWidth / fog.Width * 0.5f;
                Color tint = new Color(216, 232, 242) * (0.19f * env * edgeK);
                Main.EntitySpriteDraw(fog, Projectile.Center + new Vector2(off, 0f) + drift - Main.screenPosition,
                    null, tint, MathF.Sin(seed + i * 3f) * 0.4f, origin, scale, SpriteEffects.None, 0);
            }

            //雾心一点冷芯（A=0 加色，克制到只提示"这雾是冷的"）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.7f + 0.3f * MathF.Sin(time * 1.6f + seed);
            Color core = new Color(150, 205, 240, 0) * (0.08f * env * pulse);
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null, core, 0f,
                glow.Size() / 2f, new Vector2(HalfWidth / glow.Width * 1.6f, 0.6f), SpriteEffects.None, 0);
            return false;
        }
    }
}
