using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>光束模式（ai[1] % 10）</summary>
    internal enum EmpressBeamMode : int
    {
        /// <summary>60f 标记追踪→20f 束</summary>
        Normal = 0,
        /// <summary>绊线：标记挂住不发，谁碰到标记 21f 内打出（最多挂 240f）</summary>
        Tripwire = 1,
        /// <summary>持续追踪：发射后仍以 0.005+0.15(1-dot²) 转向，束体延长</summary>
        Persistent = 2,
        /// <summary>屏障：标记后长亮，慢慢向竖直合拢（终末警告）</summary>
        Barrier = 3,
    }

    /// <summary>
    /// 追踪光束（日舞主体）：宽标记窄命中。ai[0]=角度 ai[1]=模式+延长帧×10 ai[2]=宿主 whoAmI。
    /// 视觉半宽 180，命中半宽 16，伤害窗 timeLeft∈[4,20]；标记末 1/8 骤灭、发射前 4f 定向闪光、音在束上最近点
    /// </summary>
    internal class EmpressTrackBeam : ModProjectile, IEmpressAttack, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Beam;
        public bool BeamType => true;
        public float FeedbackIntensity => 1.5f;

        internal const int MarkerTime = 60;
        internal const int BeamTime = 20;
        internal const float Length = 3400f;
        internal const float MarkerHalfWidth = 180f;
        internal const float BeamHalfWidth = 46f;
        internal const float HitHalfWidth = 16f;
        private const int TripwireMaxHold = 240;

        private ref float Angle => ref Projectile.ai[0];
        private EmpressBeamMode Mode => (EmpressBeamMode)((int)Projectile.ai[1] % 10);
        private int ExtraFire => (int)Projectile.ai[1] / 10;
        private NPC Host => ((int)Projectile.ai[2]).TryGetNPC(out NPC n) ? n : null;
        /// <summary>localAI[0]=已过帧 localAI[1]=绊线挂住帧</summary>
        private ref float Elapsed => ref Projectile.localAI[0];
        private ref float HoldFrames => ref Projectile.localAI[1];

        private float markerAlpha;
        private float beamPower;
        private float fireAge;
        private static uint lastWarmupTick;

        private bool Firing => Projectile.timeLeft <= BeamTime;
        private float Hue => (Projectile.identity * 0.173f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;
        private static bool Day => NPC.ShouldEmpressBeEnraged();

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MarkerTime + BeamTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 3;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private Player TargetPlayer() {
            NPC host = Host;
            if (host != null && host.target >= 0 && host.target < Main.maxPlayers && Main.player[host.target].Alives()) {
                return Main.player[host.target];
            }
            return null;
        }

        public override void AI() {
            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }
            Elapsed++;
            if (Elapsed == 1f && Mode == EmpressBeamMode.Barrier) {
                //屏障：记下在她左还是右，两根像门一样各自向外上方合拢到竖直
                HoldFrames = Math.Sign(Projectile.Center.X - host.Center.X);
                if (HoldFrames == 0f) {
                    HoldFrames = 1f;
                }
            }
            Projectile.rotation = Angle;

            if (!Firing) {
                MarkerUpdate(host);
            }
            else {
                FiringUpdate(host);
            }

            //包络：标记不透明度线性爬，束体前半满、后半二次收
            float beamT = Projectile.timeLeft / (float)BeamTime;
            beamPower = Firing ? (beamT > 0.5f ? 1f : beamT * beamT * 4f) : 0f;
            markerAlpha = Firing ? 0f : 1f - (Projectile.timeLeft - BeamTime) / (float)MarkerTime;
            if (Mode == EmpressBeamMode.Tripwire && HoldFrames > 0f) {
                markerAlpha = 1f;
            }

            Vector2 dir = Angle.ToRotationVector2();
            Color light = EmpressMotion.FormColor(Hue, DayBlend, 0.66f);
            for (int i = 1; i <= 6; i++) {
                Lighting.AddLight(Projectile.Center + dir * (Length / 7f * i), light.ToVector3() * (0.25f + 0.55f * beamPower));
            }

            //标记期充能条纹：垂直朝向的光丝沿束随机撒，0.8 不透明度处停止（发射前的静默）
            if (!VaultUtils.isServer && markerAlpha > 0.05f && markerAlpha < 0.8f) {
                for (int i = 0; i < 2; i++) {
                    float along = Main.rand.NextFloat();
                    Vector2 pos = Projectile.Center + dir * Length * along;
                    if (Vector2.Distance(pos, Main.LocalPlayer.Center) > 2200f) {
                        continue;
                    }
                    Vector2 perp = dir.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);
                    float hue = Hue + Main.rand.NextFloat(0.1f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(pos + perp * Main.rand.NextFloat(60f, 200f), -perp * Main.rand.NextFloat(2f, 5f),
                        EmpressMotion.FormColor(hue, DayBlend, 0.7f) * markerAlpha, Main.rand.NextFloat(0.6f, 1f))?.Configure(14, hue, DayBlend);
                }
            }
        }

        private void MarkerUpdate(NPC host) {
            //标记期锚在她身上跟她一起动
            if (Mode != EmpressBeamMode.Barrier) {
                Projectile.Center = host.Center;
                Projectile.velocity = host.velocity;
            }

            if (Elapsed == 1f && !VaultUtils.isServer && Main.GameUpdateCount != lastWarmupTick) {
                //同一帧多束只放一声蓄力音
                lastWarmupTick = Main.GameUpdateCount;
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Volume = 0.9f, Pitch = -0.6f, MaxInstances = 2 }, host.Center);
            }

            Player target = TargetPlayer();
            switch (Mode) {
                case EmpressBeamMode.Normal:
                case EmpressBeamMode.Tripwire:
                    if (target != null) {
                        float maxDelta;
                        if (Day) {
                            bool p2 = ((int)host.ai[3] & 1) != 0;
                            maxDelta = p2 ? 0.0045f : 0.004f;
                        }
                        else {
                            //夜：转速随充能衰减到零，越接近发射越不追
                            maxDelta = 0.0022f * (1f - MathHelper.Clamp(Elapsed / MarkerTime, 0f, 1f));
                        }
                        int framesLeft = Math.Max(Projectile.timeLeft - BeamTime, 0);
                        Vector2 aim = target.Center + target.velocity * framesLeft;
                        Angle = EmpressMotion.TurnToward(Angle, Projectile.AngleTo(aim), maxDelta);
                    }
                    if (Mode == EmpressBeamMode.Tripwire && Projectile.timeLeft == BeamTime + 1) {
                        //挂住：等人踩线，最多 240f
                        HoldFrames++;
                        if (HoldFrames < TripwireMaxHold && !AnyPlayerOnMarker()) {
                            Projectile.timeLeft = BeamTime + 2;
                        }
                    }
                    break;
                case EmpressBeamMode.Persistent:
                    if (target != null) {
                        Angle = EmpressMotion.TurnToward(Angle, Projectile.AngleTo(target.Center), PersistentTurnSpeed(target));
                    }
                    break;
                case EmpressBeamMode.Barrier:
                    Angle = Angle.AngleLerp(-MathHelper.PiOver2 + HoldFrames * 0.02f, 0.01f);
                    break;
            }

            //发射前 4f：沿束方向的定向闪光，先于光束到达
            if (Projectile.timeLeft == BeamTime + 4 && !VaultUtils.isServer) {
                float near = 1f - Math.Min(DistanceToLocalPlayer() / 1000f, 1f);
                EmpressScreenFX.PushFlash(Angle.ToRotationVector2(), 0.35f * near);
            }
        }

        private void FiringUpdate(NPC host) {
            Projectile.velocity *= 0.92f;
            Vector2 dir = Angle.ToRotationVector2();
            if (Projectile.timeLeft == BeamTime) {
                OnFire(dir);
            }
            fireAge++;

            switch (Mode) {
                case EmpressBeamMode.Persistent:
                    if (TargetPlayer() is Player t) {
                        Angle = EmpressMotion.TurnToward(Angle, Projectile.AngleTo(t.Center), PersistentTurnSpeed(t));
                    }
                    if (fireAge < ExtraFire) {
                        Projectile.timeLeft = Math.Max(Projectile.timeLeft, 15);
                        if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                            EmpressMotion.SparkBurst(Projectile.Center + dir * Main.rand.NextFloat(Length * 0.8f), dir, 1, 8f, 14f, DayBlend, 0.3f);
                        }
                    }
                    break;
                case EmpressBeamMode.Barrier:
                    Angle = Angle.AngleLerp(-MathHelper.PiOver2 + HoldFrames * 0.02f, 0.0075f);
                    if (fireAge < ExtraFire) {
                        Projectile.timeLeft = Math.Max(Projectile.timeLeft, 12);
                    }
                    break;
            }
        }

        private float PersistentTurnSpeed(Player target) {
            float dot = Vector2.Dot(Angle.ToRotationVector2(), Projectile.DirectionTo(target.Center));
            return 0.005f + 0.15f * (1f - dot * dot);
        }

        /// <summary>发射帧：沿束尘、原点前推、平衰减震屏、束上最近点放音</summary>
        private void OnFire(Vector2 dir) {
            Projectile.velocity = dir * 10f;
            if (VaultUtils.isServer) {
                return;
            }
            Player me = Main.LocalPlayer;
            Vector2 nearest = Projectile.Center + dir * MathHelper.Clamp(Vector2.Dot(dir, me.Center - Projectile.Center), 0f, Length);
            float dist = Vector2.Distance(me.Center, nearest);
            float shake = 4f * MathF.Pow(Math.Max(1f - dist / 6000f, 0f), 0.1f);
            EmpressMotion.ShakeAlong(nearest, dir, shake * 2.2f, 12);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = 0.4f + Main.rand.NextFloat(-0.2f, 0.2f), MaxInstances = 3 }, nearest);
            SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.5f, Pitch = 0.3f + Main.rand.NextFloat(-0.2f, 0.2f), MaxInstances = 3 }, nearest);
            SoundEngine.PlaySound(SoundID.AbigailUpgrade with { Volume = 0.9f, Pitch = 0.8f + Main.rand.NextFloat(-0.2f, 0.2f), MaxInstances = 3 }, nearest);
            for (int i = 0; i < 40; i++) {
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(Length * 0.7f) + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-10f, 10f);
                if (Vector2.Distance(pos, me.Center) > 2400f) {
                    continue;
                }
                float hue = Hue + Main.rand.NextFloat(0.08f);
                PRTLoader.NewParticle<PRT_EmpressSpark>(pos, dir * Main.rand.NextFloat(11f, 30f) + Projectile.velocity,
                    EmpressMotion.FormColor(hue, DayBlend, 0.72f), Main.rand.NextFloat(0.9f, 1.6f))?.Configure(16, hue, DayBlend);
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.6f)?.Configure(12, Hue, DayBlend);
        }

        private float DistanceToLocalPlayer() {
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 nearest = Projectile.Center + dir * MathHelper.Clamp(Vector2.Dot(dir, Main.LocalPlayer.Center - Projectile.Center), 0f, Length);
            return Vector2.Distance(Main.LocalPlayer.Center, nearest);
        }

        private bool AnyPlayerOnMarker() {
            Vector2 dir = Angle.ToRotationVector2();
            foreach (Player p in Main.ActivePlayers) {
                if (!p.Alives()) {
                    continue;
                }
                float along = MathHelper.Clamp(Vector2.Dot(dir, p.Center - Projectile.Center), 0f, Length);
                Vector2 nearest = Projectile.Center + dir * along;
                if (Vector2.Distance(nearest, p.Center) < MarkerHalfWidth * 0.5f) {
                    return true;
                }
            }
            return false;
        }

        //伤害窗与可见束体对齐：最后 3 帧余光不打人
        public override bool? CanDamage() {
            if (!Firing || Projectile.timeLeft < 4) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 perp = new(dir.Y, -dir.X);
            Vector2 start = Projectile.Center;
            Vector2 end = start + dir * Length;
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, HitHalfWidth * 2f, ref p)
                || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft() + perp * HitHalfWidth, targetHitbox.Size(), start, end)
                || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft() - perp * HitHalfWidth, targetHitbox.Size(), start, end);
        }

        /// <summary>实体批：夜束加一道暗芯保证剪影（真 alpha Extra_98），昼由压暗世界托底</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (beamPower <= 0.01f || DayBlend >= 0.5f) {
                return false;
            }
            Microsoft.Xna.Framework.Graphics.Texture2D dark = CWRAsset.Extra_98.Value;
            float widthRatio = MathHelper.Clamp(beamPower, 0.08f, 1f);
            Main.spriteBatch.Draw(dark, Projectile.Center - Main.screenPosition, null, new Color(40, 20, 60) * (0.6f * beamPower),
                Angle, new Vector2(0f, dark.Height / 2f), new Vector2(Length / dark.Width, 0.4f * widthRatio),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = Angle.ToRotationVector2();
            float day = DayBlend;
            Color main = EmpressMotion.FormColor(Hue, day, 0.68f);

            if (markerAlpha > 0.01f && beamPower <= 0f) {
                float t = MathHelper.Clamp(markerAlpha, 0f, 1f);
                //不透明度：三次爬升+线性尾，末 1/8 骤灭；聚焦 1-(1-t)^4；末段爆亮 ((t-0.33)/0.67)^10
                float opacity = (t * t * t + 0.25f * t * (1f - t)) * MathHelper.Clamp(8f * (1f - t), 0f, 1f);
                if (Mode == EmpressBeamMode.Tripwire && HoldFrames > 0f) {
                    opacity = 0.45f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
                }
                float focus = 1f - MathF.Pow(1f - t, 4f);
                float glow = t > 0.33f ? MathF.Pow((t - 0.33f) / 0.67f, 10f) * 2f : 0f;
                Color mc = Mode == EmpressBeamMode.Tripwire ? Color.Lerp(main, new Color(150, 200, 255), 0.6f) : main;
                EmpressBeamDraw.DrawMarker(Projectile.Center, dir, Length, MarkerHalfWidth, mc, opacity, focus, glow, 1f - t, HitHalfWidth / MarkerHalfWidth);
            }

            if (beamPower > 0.01f) {
                float widthRatio = MathHelper.Clamp(beamPower, 0.08f, 1f);
                //昼：束体金白由顶点色乘；夜：走光谱 hue
                Color tint = Color.Lerp(Color.White, new Color(255, 236, 200), day);
                EmpressBeamDraw.DrawSunbeam(Projectile.Center - dir * 40f, dir, Length + 40f, BeamHalfWidth * 2.4f * widthRatio + 18f, Hue, widthRatio, tint * beamPower);
            }
        }
    }
}
