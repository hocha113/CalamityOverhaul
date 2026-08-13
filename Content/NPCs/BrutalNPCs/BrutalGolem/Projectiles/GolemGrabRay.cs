using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>投技专用射线：预警 → 扫掠横扫（跨过钉压点）或定角点烙 → 熄灭
    /// ai[0]=预警帧数, ai[1]=中心角, ai[2]=扫掠半幅（0=点烙模式）；发射口静止，几何在生成时定死</summary>
    internal class GolemGrabRay : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int SweepFireFrames = 26;
        internal const int BurnFireFrames = 18;
        internal const int FadeFrames = 8;
        internal const float MaxLength = 2400f;

        private int TelegraphFrames => (int)Math.Max(Projectile.ai[0], 1f);
        private float CenterAngle => Projectile.ai[1];
        private float SweepHalfSpan => Projectile.ai[2];
        private bool SweepMode => SweepHalfSpan > 0.01f;
        private int FireFrames => SweepMode ? SweepFireFrames : BurnFireFrames;
        private int TotalFrames => TelegraphFrames + FireFrames + FadeFrames;
        /// <summary>寿命进度推导阶段（各端一致）</summary>
        private int Elapsed => TotalFrames - Projectile.timeLeft;
        private bool Firing => Elapsed >= TelegraphFrames && Elapsed < TelegraphFrames + FireFrames;
        private bool Fading => Elapsed >= TelegraphFrames + FireFrames;

        //射线实际长度（撞地裁剪）
        private float beamLength = MaxLength;
        private bool initialized;

        /// <summary>当前角度：预警停在扫掠起点，开火期匀速掠过中心到终点（确定性，判伤端一致）</summary>
        private float CurrentAngle {
            get {
                if (!SweepMode) {
                    return CenterAngle;
                }
                if (Elapsed < TelegraphFrames) {
                    return CenterAngle - SweepHalfSpan;
                }
                float t = MathHelper.Clamp((Elapsed - TelegraphFrames) / (float)FireFrames, 0f, 1f);
                return CenterAngle + MathHelper.Lerp(-SweepHalfSpan, SweepHalfSpan, t);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //哨兵值：首帧未被网络校时则本地归位
            Projectile.timeLeft = 60000;
            Projectile.netImportant = true;
        }

        /// <summary>中途加入校时：同步已流逝帧数</summary>
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write((short)Math.Max(Elapsed, 0));
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            short elapsed = reader.ReadInt16();
            Projectile.timeLeft = Math.Max(TotalFrames - elapsed, 1);
        }

        public override void AI() {
            if (!initialized) {
                initialized = true;
                //未收到校时的端（服务端/单机）本地归位
                if (Projectile.timeLeft > TotalFrames) {
                    Projectile.timeLeft = TotalFrames;
                }
            }

            beamLength = ScanLength();
            Lighting.AddLight(Projectile.Center, new Vector3(0.6f, 0.42f, 0.13f));

            //开火首帧音画
            if (Elapsed == TelegraphFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item33 with {
                    Pitch = SweepMode ? 0.3f : -0.1f,
                    Volume = SweepMode ? 0.7f : 0.9f
                }, Projectile.Center);
                if (!SweepMode) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f, Volume = 0.7f }, Projectile.Center);
                }
                Vector2 dir = CurrentAngle.ToRotationVector2();
                for (int i = 0; i < 12; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(beamLength),
                        DustID.SolarFlare, dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f), 0, default, 1.25f);
                    dust.noGravity = true;
                }
            }
        }

        //LaserScan 采样暂存（主线程复用）
        private static readonly float[] scanSamples = new float[3];

        /// <summary>激光扫描裁剪长度</summary>
        private float ScanLength() {
            Collision.LaserScan(Projectile.Center, CurrentAngle.ToRotationVector2(), 8f, MaxLength, scanSamples);
            float total = 0f;
            foreach (float s in scanSamples) {
                total += s;
            }
            return Math.Max(total / scanSamples.Length, 120f);
        }

        public override bool? CanDamage() => Firing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Firing) {
                return false;
            }
            float collisionPoint = 0f;
            Vector2 end = Projectile.Center + CurrentAngle.ToRotationVector2() * beamLength;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, end, SweepMode ? 20f : 26f, ref collisionPoint);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new(0f, line.Height / 2f);
            float rotation = CurrentAngle;
            float lenScale = beamLength / line.Width;

            if (!Firing && !Fading) {
                //预警细线：进度推进 + 末端白热（扫掠模式停在起扫角）
                float progress = Elapsed / (float)TelegraphFrames;
                float flash = MathHelper.Clamp((progress - 0.78f) / 0.22f, 0f, 1f);
                Color baseCol = Color.Lerp(new Color(255, 150, 30), new Color(255, 230, 150), flash) with { A = 0 };
                Main.EntitySpriteDraw(line, drawPos, null, baseCol * (0.4f + 0.35f * progress),
                    rotation, origin, new Vector2(lenScale, 0.14f + 0.1f * flash), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos, null, baseCol * 0.8f,
                    0f, glow.Size() / 2f, 0.4f + 0.25f * progress, SpriteEffects.None, 0);
                Vector2 telegraphTip = drawPos + rotation.ToRotationVector2() * beamLength;
                Main.EntitySpriteDraw(glow, telegraphTip, null, baseCol * (0.45f + 0.4f * progress),
                    0f, glow.Size() / 2f, 0.3f + 0.22f * flash, SpriteEffects.None, 0);
                return false;
            }

            //射击/衰减期：三层实束，点烙模式更粗
            float life = Fading
                ? 1f - (Elapsed - TelegraphFrames - FireFrames) / (float)FadeFrames
                : 1f;
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);
            float widthMul = SweepMode ? 1f : 1.35f;

            Main.EntitySpriteDraw(line, drawPos, null, new Color(200, 90, 20, 0) * (0.75f * life),
                rotation, origin, new Vector2(lenScale, 0.68f * widthMul * life * pulse), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 180, 70, 0) * (0.9f * life),
                rotation, origin, new Vector2(lenScale, 0.4f * widthMul * life), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, new Color(255, 245, 205, 0) * life,
                rotation, origin, new Vector2(lenScale, 0.16f * widthMul * life), SpriteEffects.None, 0);
            //根部与末端辉光
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 200, 90, 0) * life,
                0f, glow.Size() / 2f, 0.85f * life, SpriteEffects.None, 0);
            Vector2 tip = drawPos + rotation.ToRotationVector2() * beamLength;
            Main.EntitySpriteDraw(glow, tip, null, new Color(255, 200, 90, 0) * (0.8f * life),
                0f, glow.Size() / 2f, (SweepMode ? 0.6f : 0.8f) * life, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>发射助手（服务端）：发射口与角度在此刻定死，各端确定性推演</summary>
        internal static void Fire(NPC owner, Vector2 muzzle, float centerAngle, int telegraphFrames,
            float sweepHalfSpan, int damage) {
            if (VaultUtils.isClient || owner == null || !owner.active) {
                return;
            }
            int id = Projectile.NewProjectile(owner.GetSource_FromAI(), muzzle, Vector2.Zero,
                ModContent.ProjectileType<GolemGrabRay>(), damage, 0f, Main.myPlayer,
                telegraphFrames, centerAngle, sweepHalfSpan);
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].netUpdate = true;
            }
        }
    }
}
