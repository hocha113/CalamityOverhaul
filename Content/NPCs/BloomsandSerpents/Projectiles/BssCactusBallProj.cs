using CalamityOverhaul.Common;
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
    /// 仙人掌刺球：重力弧线 + 落地弹跳（squash），弹跳耗尽后闪烁引信，
    /// 爆成上半 240 度扇面的放射钉刺。
    /// 公平阀声明：BurstArcDeg=240 顶心朝上，贴地两侧各留 60 度逃生道，发射环真读此常量。
    /// ai[0]=已弹跳次数。
    /// </summary>
    internal class BssCactusBallProj : BssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/CactusBall";

        /// <summary>爆裂扇面总角（度），顶心朝正上</summary>
        private const float BurstArcDeg = 240f;

        private ref float Bounces => ref Projectile.ai[0];
        /// <summary>引信剩余帧（&gt;0 进入引爆倒计时）</summary>
        private ref float Fuse => ref Projectile.ai[1];
        /// <summary>squash 余帧（本地表现）</summary>
        private ref float SquashFrames => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.scale = 1.45f;
        }

        public override void AI() {
            if (Fuse <= 0f) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + BssDirector.BallGravity, -30f, 16f);
                Projectile.rotation += Projectile.velocity.X * 0.045f;
            }
            else {
                //引信期：钉在原地滚停，加速闪烁 + 升调滴答
                Projectile.velocity.X *= 0.86f;
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + BssDirector.BallGravity, -30f, 16f);
                Fuse--;
                float p = 1f - Fuse / BssDirector.BallFuseFrames;
                int tickGap = p > 0.66f ? 4 : 8;
                if ((int)Fuse % tickGap == 0 && !Main.dedServ) {
                    //干木咔嗒升调引信
                    SoundEngine.PlaySound(SoundID.Item102 with { Volume = 0.45f, Pitch = -0.2f + 0.8f * p, MaxInstances = 4 },
                        Projectile.Center);
                }
                if (Fuse <= 0f) {
                    Burst();
                    return;
                }
            }

            if (SquashFrames > 0f) {
                SquashFrames--;
            }

            //保底：寿命将尽也引爆，不留哑弹
            if (Projectile.timeLeft == 30 && Fuse <= 0f) {
                Fuse = BssDirector.BallFuseFrames;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Bounces < BssDirector.BallBounces && Fuse <= 0f) {
                Bounces++;
                SquashFrames = 8f;
                if (Math.Abs(oldVelocity.Y) > 1.5f) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.72f;
                }
                Projectile.velocity.X = oldVelocity.X * 0.9f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Bottom, DustID.Sand,
                            new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.8f, 2.2f)),
                            100, default, Main.rand.NextFloat(0.8f, 1.2f));
                        d.noGravity = false;
                    }
                }
                return false;
            }
            if (Fuse <= 0f) {
                //弹跳耗尽：落定点火
                Fuse = BssDirector.BallFuseFrames;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
            }
            return false;
        }

        /// <summary>爆裂：上半扇面放射钉刺（权威端），贴地两侧声明逃生道</summary>
        private void Burst() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = 0.45f, MaxInstances = 3 }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                        Main.rand.NextVector2Circular(3f, 3f) - new Vector2(0f, 1.5f),
                        90, default, Main.rand.NextFloat(0.8f, 1.3f));
                    d.noGravity = Main.rand.NextBool();
                }
                BssVfx.SandBurst(Projectile.Center, 0.6f);
            }

            if (!VaultUtils.isClient) {
                int needleType = ModContent.ProjectileType<BssNeedleProj>();
                int count = BssDirector.BallBurstNeedles;
                float arc = MathHelper.ToRadians(BurstArcDeg);
                for (int i = 0; i < count; i++) {
                    //扇面顶心朝正上：贴地两侧 (360-BurstArcDeg)/2 度内不发射 = 声明的逃生道
                    float ang = -MathHelper.PiOver2 + (i / (float)(count - 1) - 0.5f) * arc;
                    Vector2 vel = ang.ToRotationVector2() * BssDirector.NeedleSpeed;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        needleType, Projectile.damage, 0.5f, Main.myPlayer);
                }
            }
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    Main.rand.NextVector2Circular(2f, 2f), 100, default, 1f);
                d.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //squash：落地压扁回弹
            Vector2 scale = Vector2.One * Projectile.scale;
            if (SquashFrames > 0f) {
                float s = SquashFrames / 8f;
                scale = new Vector2(1f + 0.3f * s, 1f - 0.28f * s) * Projectile.scale;
            }

            //引信闪烁：加速白闪（用体色变亮实现，保持遮蔽）
            Color tint = lightColor;
            if (Fuse > 0f) {
                float p = 1f - Fuse / BssDirector.BallFuseFrames;
                float blinkRate = MathHelper.Lerp(10f, 26f, p);
                float blink = MathF.Sin(Main.GlobalTimeWrappedHourly * blinkRate * MathHelper.TwoPi) > 0f ? 1f : 0f;
                tint = Color.Lerp(lightColor, Color.White, blink * (0.35f + 0.45f * p));
                scale *= 1f + 0.06f * MathF.Sin(p * 24f);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                tint, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
