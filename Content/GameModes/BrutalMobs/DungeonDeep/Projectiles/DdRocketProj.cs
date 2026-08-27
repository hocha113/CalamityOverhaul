using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles
{
    /// <summary>
    /// SkeletonCommando 火箭抛物：落点在发射帧锁死，警示环从第 0 帧画到爆点（≥36 帧）。
    /// ai[0]/ai[1]=锁定落点 ai[2]=弹道帧数（权威端解算随生成包同步）。
    /// 火箭全程无判定、不撞墙（弹道纯表现层，穿墙不会产生环外伤害）；
    /// 伤害窗=落点环的爆窗，判定圆与爆焰视觉同帧开合（伤害窗=可见环）
    /// </summary>
    internal class DdRocketProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RocketSkeleton;

        /// <summary>警示环最短可见帧（弹道帧数下限；契约 ≥34）</summary>
        internal const int RingWarnMinFrames = 36;
        /// <summary>爆窗帧数（伤害窗=此窗）</summary>
        internal const int BlastFrames = 14;
        /// <summary>爆炸判定半径（=警示环半径，所见即判定）</summary>
        internal const float BlastRadius = 96f;
        /// <summary>火箭自施重力（抛物解算与之对齐）</summary>
        internal const float Gravity = 0.24f;
        /// <summary>爆点吸附距离平方：接近承诺落点即引爆</summary>
        private const float ArriveDistSq = 24f * 24f;

        private static readonly Color RingWarn = new Color(255, 90, 50, 0);

        private Vector2 LockPoint => new(Projectile.ai[0], Projectile.ai[1]);
        private int FlightFrames => Math.Max(RingWarnMinFrames, (int)Projectile.ai[2]);
        private ref float Age => ref Projectile.localAI[0];
        /// <summary>爆窗计龄：0=未爆，1..BlastFrames=爆窗（各端由位置/龄数确定性触发）</summary>
        private ref float BlastAge => ref Projectile.localAI[1];
        private bool Blasting => BlastAge > 0f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = false;//飞行全程无判定，爆窗才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;//弹道纯表现层：判定只在落点环内
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        /// <summary>伤害窗=爆窗（可见环闪光期）</summary>
        public override bool? CanDamage() => Blasting && BlastAge <= BlastFrames ? null : false;

        /// <summary>圆形判定：以承诺落点为心、警示环半径为界</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Blasting || BlastAge > BlastFrames) {
                return false;
            }
            Vector2 lockPoint = LockPoint;
            float nearX = MathHelper.Clamp(lockPoint.X, targetHitbox.Left, targetHitbox.Right);
            float nearY = MathHelper.Clamp(lockPoint.Y, targetHitbox.Top, targetHitbox.Bottom);
            return Vector2.DistanceSquared(lockPoint, new Vector2(nearX, nearY)) <= BlastRadius * BlastRadius;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f && Projectile.localAI[1] == 0f && Projectile.timeLeft == 600) {
                Projectile.timeLeft = FlightFrames + BlastFrames + 10;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
                }
            }

            if (!Blasting) {
                Age++;
                //抛物飞行：自施重力，弹头顺速度
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, 15f);
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                if (!Main.dedServ) {
                    if (Main.rand.NextBool(2)) {
                        Dust smoke = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.5f,
                            DustID.Smoke, -Projectile.velocity * 0.1f, 120, default, 1.1f);
                        smoke.noGravity = true;
                    }
                    if (Main.rand.NextBool(3)) {
                        Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                            -Projectile.velocity * 0.2f, 100, default, 1.0f);
                        spark.noGravity = true;
                    }
                }
                Lighting.AddLight(Projectile.Center, 0.3f, 0.18f, 0.06f);

                //到点起爆：弹道龄满或贴近承诺落点（各端从同步的生成参数确定性得到同一结论）
                if (Age >= FlightFrames || Vector2.DistanceSquared(Projectile.Center, LockPoint) <= ArriveDistSq) {
                    StartBlast();
                }
                return;
            }

            //爆窗推进
            BlastAge++;
            Projectile.hostile = BlastAge <= BlastFrames;
            Lighting.AddLight(LockPoint, 1.0f, 0.55f, 0.2f);
            if (BlastAge > BlastFrames + 6f) {
                Projectile.Kill();
            }
        }

        /// <summary>起爆：吸附回承诺环心（伤害圆=警示环），本端播放爆音与环形火尘</summary>
        private void StartBlast() {
            BlastAge = 1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.Center = LockPoint;
            Projectile.hostile = true;
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 4 }, LockPoint);
            for (int i = 0; i < 18; i++) {
                float ang = MathHelper.TwoPi * i / 18f;
                Dust flame = Dust.NewDustPerfect(LockPoint + ang.ToRotationVector2() * Main.rand.NextFloat(10f, BlastRadius * 0.8f),
                    DustID.Torch, ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5f), 90, default,
                    Main.rand.NextFloat(1.4f, 2f));
                flame.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Dust smoke = Dust.NewDustPerfect(LockPoint, DustID.Smoke,
                    Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 1.5f, 140, default, 1.4f);
                smoke.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D dark = CWRAsset.Extra_98.Value;
            Vector2 ringPos = LockPoint - Main.screenPosition;
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);

            if (!Blasting) {
                //落点警示环：从发射帧画满全程（≥36 帧），随邻近爆点渐亮
                float urgency = MathHelper.Clamp(Age / FlightFrames, 0f, 1f);
                float fadeIn = MathHelper.Clamp(Age / 8f, 0f, 1f);
                Main.EntitySpriteDraw(dark, ringPos, null, new Color(58, 22, 16, 220) * (0.75f * fadeIn), 0f,
                    dark.Size() / 2f, new Vector2(BlastRadius * 2.2f / dark.Width, 0.15f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, ringPos, null, RingWarn * ((0.3f + 0.45f * urgency) * fadeIn * pulse), 0f,
                    glow.Size() / 2f, new Vector2(BlastRadius * 2f / glow.Width, 0.18f), SpriteEffects.None, 0);
                //环缘光点：环的边界即判定边界
                for (int i = 0; i < 6; i++) {
                    float ang = Main.GlobalTimeWrappedHourly * 1.6f + MathHelper.TwoPi * i / 6f;
                    Vector2 rim = ringPos + new Vector2(MathF.Cos(ang) * BlastRadius, MathF.Sin(ang) * BlastRadius * 0.18f);
                    Main.EntitySpriteDraw(glow, rim, null, RingWarn * (0.5f * fadeIn * pulse), 0f,
                        glow.Size() / 2f, 0.05f, SpriteEffects.None, 0);
                }

                //火箭本体：原版贴图 + 尾焰加色衬
                int donor = ProjectileID.RocketSkeleton;
                Main.instance.LoadProjectile(donor);
                Texture2D tex = TextureAssets.Projectile[donor].Value;
                int donorFrames = Math.Max(1, Main.projFrames[donor]);
                Rectangle frameRect = new(0, 0, tex.Width, tex.Height / donorFrames);
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Main.EntitySpriteDraw(glow, drawPos - Projectile.velocity.SafeNormalize(Vector2.UnitY) * 10f, null,
                    new Color(255, 170, 90, 0) * (0.5f * pulse), 0f, glow.Size() / 2f, 0.12f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, drawPos, frameRect, Color.Lerp(Color.White, lightColor, 0.4f),
                    Projectile.rotation, frameRect.Size() / 2f, 1f, SpriteEffects.None, 0);
                return false;
            }

            //爆窗：白热芯 + 扩张环 + 暗烟座（可见环=伤害窗）
            float blastT = MathHelper.Clamp(BlastAge / BlastFrames, 0f, 1f);
            float ringScale = 0.4f + 0.6f * blastT;
            float blastFade = 1f - blastT * blastT;
            Main.EntitySpriteDraw(dark, ringPos, null, new Color(40, 30, 26, 200) * (0.8f * blastFade), 0f,
                dark.Size() / 2f, new Vector2(BlastRadius * 2.4f / dark.Width, 0.6f) * ringScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, ringPos, null, new Color(255, 150, 70, 0) * (0.9f * blastFade), 0f,
                glow.Size() / 2f, new Vector2(BlastRadius * 2.6f / glow.Width, BlastRadius * 2.6f / glow.Height) * ringScale,
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, ringPos, null, new Color(255, 240, 210, 0) * (0.8f * blastFade), 0f,
                glow.Size() / 2f, new Vector2(BlastRadius * 1.4f / glow.Width, BlastRadius * 1.4f / glow.Height) * ringScale,
                SpriteEffects.None, 0);
            return false;
        }
    }
}
