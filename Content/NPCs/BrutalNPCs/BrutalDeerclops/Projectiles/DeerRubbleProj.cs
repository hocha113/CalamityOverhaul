using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles
{
    /// <summary>
    /// 冰岩碎块(掀地投掷)。ai[0]=落点X ai[2]=落点Y ai[1]=帧样式(6~11)；
    /// 升起(无伤)→悬滞→确定性弧线砸向标记点，全程落点标记可读
    /// </summary>
    internal class DeerRubbleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeerclopsRangedProjectile;

        private const int RiseTime = 30;
        private const int HoverTime = 14;
        private const int LaunchFrame = RiseTime + HoverTime;
        private const float ArcTime = 46f;
        private const float Gravity = 0.34f;

        private Vector2 TargetPoint => new Vector2(Projectile.ai[0], Projectile.ai[2]);

        private ref float Elapsed => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.coldDamage = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Elapsed += 1f;
            int t = (int)Elapsed;

            if (t == 1) {
                //破土而出(初速由identity哈希导出，各端一致)
                float hash01 = Projectile.identity * 0.6180339887f % 1f;
                float hash02 = Projectile.identity * 0.4142135623f % 1f;
                Projectile.velocity = new Vector2((hash01 - 0.5f) * 0.8f, -(6.5f + hash02 * 2f));
                if (!Main.dedServ) {
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                            DustID.Snow, Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 4f), 60, default, Main.rand.NextFloat(1f, 1.7f));
                        dust.noGravity = Main.rand.NextBool(3);
                    }
                }
            }

            if (t < RiseTime) {
                //升起减速，无伤
                Projectile.hostile = false;
                Projectile.velocity *= 0.94f;
                Projectile.rotation += Projectile.velocity.X * 0.04f + 0.02f;
            }
            else if (t < LaunchFrame) {
                //悬滞蓄势(战栗放绘制层，逻辑位置各端保持确定性)
                Projectile.hostile = false;
                Projectile.velocity *= 0.8f;
                Projectile.rotation += 0.01f;
            }
            else {
                if (t == LaunchFrame) {
                    //确定性弧线解算(两端一致：位置由生成包同步，其余推导)
                    Vector2 delta = TargetPoint - Projectile.Center;
                    float vx = delta.X / ArcTime;
                    float vy = delta.Y / ArcTime - Gravity * ArcTime * 0.5f;
                    Projectile.velocity = new Vector2(vx, vy);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 0.4f, MaxInstances = 3 }, Projectile.Center);
                    }
                }
                Projectile.hostile = true;
                Projectile.velocity.Y += Gravity;
                Projectile.rotation += Math.Sign(Projectile.velocity.X) * 0.09f;

                //霜尾迹
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Frost,
                        -Projectile.velocity * 0.15f, 130, default, Main.rand.NextFloat(0.8f, 1.3f));
                    dust.noGravity = true;
                }

                //抵达/越过落点即碎；弧线解算保证ArcTime帧后必达，兜底防高台目标漏杀
                bool crossedDown = Projectile.velocity.Y > 0f && Projectile.Center.Y >= TargetPoint.Y - 10f;
                if ((t > LaunchFrame + 8 && crossedDown) || t >= LaunchFrame + (int)ArcTime + 2) {
                    Projectile.Kill();
                    return;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.DeerclopsRangedProjectile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.DeerclopsRangedProjectile].Value;
            //原版帧映射：frame 6~11 → 3列4行
            int frame = (int)Projectile.ai[1];
            Rectangle rect = tex.Frame(3, 4, frame % 3, frame / 3);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = rect.Size() / 2f;

            //落点标记(飞行与悬滞期，客户端可读)
            int t = (int)Elapsed;
            if (t >= RiseTime - 6 && t < LaunchFrame + (int)ArcTime) {
                float markPulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
                float markFade = MathHelper.Clamp((t - (RiseTime - 6)) / 14f, 0f, 1f);
                Texture2D ring = CWRAsset.DiffusionCircle.Value;
                Vector2 markPos = TargetPoint - Main.screenPosition;
                Color markColor = DeerclopsMotion.IceBlue with { A = 0 } * (0.35f * markFade * markPulse);
                Main.EntitySpriteDraw(ring, markPos, null, markColor, 0f, ring.Size() / 2f, 0.16f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(ring, markPos, null, markColor * 0.6f, 0f, ring.Size() / 2f, 0.09f, SpriteEffects.None, 0);
            }

            //升起期白霜渐亮
            float riseGlow = t < LaunchFrame ? MathHelper.Clamp(t / (float)RiseTime, 0f, 1f) : 1f;
            Color body = Projectile.GetAlpha(lightColor);
            body = Color.Lerp(body, DeerclopsMotion.ColdWhite, 0.12f * riseGlow);

            //悬滞战栗(纯视觉抖动)
            if (t >= RiseTime && t < LaunchFrame) {
                float shiver = (t - RiseTime) / (float)HoverTime;
                drawPos += new Vector2(
                    (float)Math.Sin(Main.GlobalTimeWrappedHourly * 52f + Projectile.identity) * 1.4f * shiver,
                    (float)Math.Sin(Main.GlobalTimeWrappedHourly * 47f + Projectile.identity * 2f) * 1.1f * shiver);
            }

            //高速运动残影
            if (t > LaunchFrame) {
                Color ghost = DeerclopsMotion.IceBlue with { A = 0 } * 0.25f;
                Main.EntitySpriteDraw(tex, drawPos - Projectile.velocity * 0.6f, rect, ghost, Projectile.rotation - 0.05f, origin, Projectile.scale * 0.96f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, drawPos, rect, body, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item51 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 4 }, Projectile.Center);
            for (int i = 0; i < 12; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool() ? DustID.Ice : DustID.Snow,
                    Main.rand.NextVector2Circular(3.5f, 2.5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 3f), 70, default, Main.rand.NextFloat(1f, 1.8f));
                dust.noGravity = Main.rand.NextBool(3);
            }
        }
    }
}
