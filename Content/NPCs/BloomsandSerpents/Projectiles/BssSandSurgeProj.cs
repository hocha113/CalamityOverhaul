using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 掀沙浪：沙下翻身掀起的贴地沙浪，沿地形起伏行进，浪高低于单跳（原地起跳即安全）。
    /// 判定盒与视觉共用 <see cref="BssDirector.SurgeWaveHeight"/> × 寿命包络，一个数字同时驱动。
    /// 撞上陡壁提前崩解；命中把玩家顺浪卷带一截。
    /// ai[0]=行进方向 ±1；ai[1]=行进帧数（≤0 取 Director 默认）。
    /// </summary>
    internal class BssSandSurgeProj : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>浪体脚印长度（像素）</summary>
        private const float WaveLength = 300f;
        private const int GrowFrames = 12;
        private const int CollapseFrames = 16;

        private int Dir => Projectile.ai[0] < 0f ? -1 : 1;
        private int TravelFrames => (int)Projectile.ai[1] > 0 ? (int)Projectile.ai[1] : BssDirector.SurgeWaveTravelFrames;
        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>寿命包络：起势 → 全高 → 崩解</summary>
        private float Envelope {
            get {
                float grow = MathHelper.Clamp(Timer / GrowFrames, 0f, 1f);
                float collapse = MathHelper.Clamp((TravelFrames - Timer) / CollapseFrames, 0f, 1f);
                return (1f - (1f - grow) * (1f - grow)) * collapse * collapse;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = new Vector2(Dir * BssDirector.SurgeWaveSpeed, 0f);

            //贴地形起伏：前探一段取地表，上坡快贴、下坡缓落
            Vector2 probe = Projectile.Center + new Vector2(Dir * WaveLength * 0.2f, -70f);
            float groundY = BssVfx.FindGroundY(probe, 320f);
            float targetY = groundY - 6f;
            float rate = targetY < Projectile.Center.Y ? 0.35f : 0.16f;
            Projectile.Center = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Center.Y, targetY, rate));

            //迎面陡壁：浪拍在壁上崩解
            if (groundY < Projectile.Center.Y - 120f && Timer < TravelFrames - CollapseFrames) {
                Timer = TravelFrames - CollapseFrames;
            }
            if (Timer >= TravelFrames) {
                Projectile.Kill();
                return;
            }

            float env = Envelope;
            if (Main.dedServ || env < 0.15f) {
                return;
            }

            //浪峰扬沙：峰顶向前甩沙、浪脚拖尘（漫反射材质，不发光）
            Vector2 crest = Projectile.Center + new Vector2(Dir * WaveLength * 0.22f, -BssDirector.SurgeWaveHeight * env);
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(crest + Main.rand.NextVector2Circular(30f, 10f), DustID.Sand,
                    new Vector2(Dir * Main.rand.NextFloat(2f, 6f), -Main.rand.NextFloat(1.5f, 4.5f)),
                    90, default, Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = false;
            }
            if (Main.rand.NextBool(3)) {
                Dust stone = Dust.NewDustPerfect(Projectile.Center + new Vector2(Dir * Main.rand.NextFloat(-40f, 80f), -8f),
                    DustID.Dirt, new Vector2(Dir * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(2f, 5f)),
                    80, default, Main.rand.NextFloat(0.7f, 1f));
                stone.noGravity = false;
            }
            if (Main.rand.NextBool(2)) {
                Dust foot = Dust.NewDustPerfect(Projectile.Center + new Vector2(-Dir * Main.rand.NextFloat(0f, WaveLength * 0.5f), -4f),
                    DustID.Sand, new Vector2(-Dir * Main.rand.NextFloat(0.5f, 1.5f), -Main.rand.NextFloat(0.3f, 1.2f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                foot.noGravity = true;
            }
            if ((int)Timer % 14 == 0) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
                BssVfx.Shake(Projectile.Center, 1.2f, 700f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //顺浪卷带：被掀翻的读数，不是硬控
            target.velocity.X += Dir * 4f;
            target.velocity.Y = Math.Min(target.velocity.Y, -3f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float env = Envelope;
            if (env < 0.25f) {
                return false;
            }
            //贴地扁盒，略窄于视觉；高度 = 声明浪高 × 包络
            float halfW = WaveLength * 0.4f;
            float h = BssDirector.SurgeWaveHeight * env * 0.9f;
            Rectangle wave = new((int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - h),
                (int)(halfW * 2f), (int)(h + 12f));
            return wave.Intersects(targetHitbox);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            BssVfx.SandBurst(Projectile.Center, 0.8f);
        }

        /// <summary>
        /// 沙堆浪体：原版沙球贴图沿前陡后缓的浪形堆成两排，随行进滚动；
        /// 漫反射乘光照，后排压暗给厚度。浪形高度由同一浪高常量驱动。
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env < 0.03f) {
                return false;
            }
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float height = BssDirector.SurgeWaveHeight * env;
            float roll = Timer * 0.16f * Dir;

            //后排：压暗、略大、错半格
            DrawRow(tex, origin, lightColor.MultiplyRGB(BssVfx.SandDark), height * 0.92f, 8, 0.5f, 1.55f, roll, 6f);
            //前排：本色
            DrawRow(tex, origin, lightColor.MultiplyRGB(BssVfx.SandWarm), height, 9, 0f, 1.35f, roll + 0.9f, 0f);
            return false;
        }

        private void DrawRow(Texture2D tex, Vector2 origin, Color tint, float height, int count,
            float stagger, float scale, float roll, float depthShift) {
            for (int i = 0; i < count; i++) {
                //u 从浪脚（后）到浪头（前）：前陡后缓，峰在 0.7
                float u = (i + 0.5f + stagger) / count;
                float shape = u < 0.7f
                    ? MathF.Sin(u / 0.7f * MathHelper.PiOver2)
                    : MathF.Cos((u - 0.7f) / 0.3f * MathHelper.PiOver2);
                shape = MathF.Pow(Math.Max(shape, 0f), 0.85f);
                float along = (u - 0.5f) * WaveLength;
                Vector2 pos = Projectile.Center + new Vector2(Dir * along, -shape * height * 0.55f + depthShift);
                float s = scale * (0.75f + 0.5f * shape);
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, tint, roll + i * 0.7f, origin, s, SpriteEffects.None, 0);
                //峰顶再叠一颗撑高：轮廓到达声明浪高
                if (shape > 0.85f) {
                    Vector2 top = pos - new Vector2(0f, height * 0.42f);
                    Main.EntitySpriteDraw(tex, top - Main.screenPosition, null, tint, roll * 1.3f + i, origin, s * 0.8f, SpriteEffects.None, 0);
                }
            }
        }
    }
}
