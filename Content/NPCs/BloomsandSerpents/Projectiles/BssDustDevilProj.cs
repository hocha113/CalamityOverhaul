using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 旋沙龙卷：沙暴里拧出的旋沙柱，顺风漂移、贴地形立起。
    /// 成形期只在地面旋沙、无伤（旋沙即预告），成形后高柱有伤，命中把玩家掀上天。
    /// 判定盒与视觉共用 <see cref="BssDirector.DevilHeight"/>/<see cref="BssDirector.DevilHalfWidth"/> × 包络。
    /// P3 沙柱周期甩出漂移花瓣（权威端，伤害源取头）。
    /// ai[0]=风向 ±1；ai[1]=头 whoAmI；ai[2]=成形帧数。
    /// </summary>
    internal class BssDustDevilProj : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        private const int RiseFrames = 14;
        private const int CollapseFrames = 22;
        private const int Layers = 11;

        private int Dir => Projectile.ai[0] < 0f ? -1 : 1;
        private int HeadIndex => (int)Projectile.ai[1];
        private int FormFrames => (int)Projectile.ai[2] > 0 ? (int)Projectile.ai[2] : BssDirector.DevilFormFrames;
        private int LifeEnd => FormFrames + BssDirector.DevilLifeFrames;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float Spin => ref Projectile.localAI[1];

        /// <summary>成形进度 0..1（地面旋沙阶段）</summary>
        private float FormProgress => MathHelper.Clamp(Timer / FormFrames, 0f, 1f);

        /// <summary>柱体包络：成形后拔起 → 全高 → 崩解</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp((Timer - FormFrames) / RiseFrames, 0f, 1f);
                float collapse = MathHelper.Clamp((LifeEnd - Timer) / CollapseFrames, 0f, 1f);
                return (1f - (1f - rise) * (1f - rise)) * collapse;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 500;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            Timer++;
            if (Timer >= LifeEnd) {
                Projectile.Kill();
                return;
            }

            float form = FormProgress;
            float env = Envelope;
            bool formed = Timer > FormFrames;

            //贴地：柱脚钉在地表
            float groundY = BssVfx.FindGroundY(Projectile.Center - new Vector2(0f, 90f), 420f);
            Projectile.Center = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Center.Y, groundY - 8f, 0.3f));

            //顺风漂移：成形后才走，速度带一点呼吸
            Projectile.velocity = formed
                ? new Vector2(Dir * BssDirector.DevilDriftSpeed * (0.85f + 0.15f * MathF.Sin(Timer * 0.07f)), 0f)
                : Vector2.Zero;
            Spin += formed ? 0.24f : 0.1f + 0.2f * form;

            //P3 甩瓣：柱身周期甩出顺风漂移的花瓣（权威端）
            if (formed && !VaultUtils.isClient && (int)Timer % BssDirector.DevilPetalGap == 0
                && HeadIndex >= 0 && HeadIndex < Main.maxNPCs) {
                NPC head = Main.npc[HeadIndex];
                if (head.Alives() && (int)head.ai[2] >= 3) {
                    int damage = BssDirector.ScaleProjectileDamage(head, BssDirector.PetalDamage);
                    Vector2 from = Projectile.Center - new Vector2(0f, BssDirector.DevilHeight * env * Main.rand.NextFloat(0.45f, 0.85f));
                    Vector2 vel = new(Dir * Main.rand.NextFloat(2.5f, 4f), -Main.rand.NextFloat(0.5f, 2f));
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), from, vel,
                        ModContent.ProjectileType<BssPetalProj>(), damage, 0.4f, Main.myPlayer,
                        Dir, Main.rand.NextFloat(MathHelper.TwoPi));
                }
            }

            if (Main.dedServ) {
                return;
            }

            if (!formed) {
                //地面旋沙：沙粒在脚下打圈越转越急，越转越高
                int count = 1 + (int)(form * 3f);
                for (int i = 0; i < count; i++) {
                    float ang = Spin * 1.5f + i * 2.1f + Main.rand.NextFloat(0.4f);
                    float r = (36f + 24f * form) * Main.rand.NextFloat(0.7f, 1.1f);
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r * 0.35f - 4f);
                    Vector2 vel = new Vector2(-MathF.Sin(ang), MathF.Cos(ang) * 0.35f) * (2f + 3f * form)
                        - new Vector2(0f, 0.6f + 2.4f * form);
                    Dust d = Dust.NewDustPerfect(pos, DustID.Sand, vel, 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
                if ((int)Timer % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.3f + 0.3f * form, Pitch = -0.6f + 0.5f * form, MaxInstances = 3 },
                        Projectile.Center);
                }
                return;
            }

            //柱身旋沙：沿螺旋抛出的沙粒，柱顶张开成伞
            float height = BssDirector.DevilHeight * env;
            for (int i = 0; i < 4; i++) {
                float h = Main.rand.NextFloat();
                float r = BssDirector.DevilHalfWidth * env * (0.5f + 0.9f * h);
                float ang = Spin * 1.3f + h * 9f;
                Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(ang) * r, -h * height);
                Vector2 vel = new Vector2(-MathF.Sin(ang) * 3f + Dir * 1.2f, -Main.rand.NextFloat(1f, 3f));
                Dust d = Dust.NewDustPerfect(pos, DustID.Sand, vel, 110, default, Main.rand.NextFloat(0.9f, 1.5f));
                d.noGravity = true;
            }
            if (Main.rand.NextBool(2)) {
                Dust top = Dust.NewDustPerfect(Projectile.Center - new Vector2(Main.rand.NextFloat(-50f, 50f), height),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-4f, 4f) + Dir * 2f, -Main.rand.NextFloat(0.5f, 2f)),
                    130, default, Main.rand.NextFloat(1.3f, 1.9f));
                top.noGravity = true;
            }
            if (Main.rand.NextBool(3)) {
                Dust foot = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -2f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f)),
                    90, default, Main.rand.NextFloat(1f, 1.4f));
                foot.noGravity = false;
            }
            if ((int)Timer % 16 == 0) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //掀上天：卷进柱里被抛起，顺风带一截
            target.velocity.Y = Math.Min(target.velocity.Y, BssDirector.DevilLift);
            target.velocity.X += Dir * 2f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float env = Envelope;
            if (Timer <= FormFrames || env < 0.3f) {
                return false;
            }
            float halfW = BssDirector.DevilHalfWidth * env * 0.9f;
            float h = BssDirector.DevilHeight * env;
            Rectangle column = new((int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - h),
                (int)(halfW * 2f), (int)(h + 8f));
            return column.Intersects(targetHitbox);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            BssVfx.SandBurst(Projectile.Center, 1.1f);
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - new Vector2(0f, Main.rand.NextFloat(0f, 160f)),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(0f, 2f)),
                    120, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        /// <summary>
        /// 螺旋沙柱：沙球贴图分层绕轴旋转，背面压暗前面本色（深度读数），上宽下窄成漏斗；
        /// 成形期只画地面打圈的沙环。漫反射乘光照，全程不加色。
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Color warm = lightColor.MultiplyRGB(BssVfx.SandWarm);
            Color dark = lightColor.MultiplyRGB(BssVfx.SandDark);

            if (Timer <= FormFrames) {
                //地面沙环：椭圆打圈，随进度收紧加速、微微离地
                float form = FormProgress;
                int ringCount = 6;
                for (int i = 0; i < ringCount; i++) {
                    float ang = Spin * 1.5f + i * MathHelper.TwoPi / ringCount;
                    float r = 44f - 10f * form;
                    Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(ang) * r, MathF.Sin(ang) * r * 0.35f - 6f * form);
                    Color tint = Color.Lerp(dark, warm, (MathF.Sin(ang) + 1f) * 0.5f) * (0.5f + 0.5f * form);
                    Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, tint, ang, origin,
                        1.1f + 0.4f * form, SpriteEffects.None, 0);
                }
                return false;
            }

            float env = Envelope;
            if (env < 0.03f) {
                return false;
            }
            float height = BssDirector.DevilHeight * env;

            //两遍：先背面（sin 为负）再正面，得到绕轴的前后关系
            for (int pass = 0; pass < 2; pass++) {
                for (int i = 0; i < Layers; i++) {
                    float t = (i + 0.5f) / Layers;
                    float y = -t * height;
                    float r = BssDirector.DevilHalfWidth * env * (0.45f + 0.8f * t);
                    for (int k = 0; k < 3; k++) {
                        float ang = Spin * (1.25f - 0.03f * i) + i * 0.9f + k * MathHelper.TwoPi / 3f;
                        float depth = MathF.Sin(ang);
                        if ((pass == 0) != (depth < 0f)) {
                            continue;
                        }
                        Vector2 pos = Projectile.Center + new Vector2(MathF.Cos(ang) * r, y);
                        Color tint = Color.Lerp(dark, warm, (depth + 1f) * 0.5f) * (0.75f + 0.25f * env);
                        float scale = (1f + 0.55f * t) * (0.8f + 0.2f * env);
                        Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, tint, ang + Spin * 0.5f, origin,
                            scale, SpriteEffects.None, 0);
                    }
                }
            }
            return false;
        }
    }
}
