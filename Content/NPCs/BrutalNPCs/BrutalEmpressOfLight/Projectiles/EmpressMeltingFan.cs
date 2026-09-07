using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 熔光扇：锚在她身上的七射线扇，120f 充能里由线变楔并旋转 sweep 弧度，
    /// 昼形态吸附式追踪（让某一根射线对准玩家，75% 锁），发射窗 10f（6 满 + 4 收）。
    /// ai[0]=起始角 ai[1]=扫过弧度（带符号） ai[2]=宿主 whoAmI。多组同屏只亮最快发射的一组
    /// </summary>
    internal class EmpressMeltingFan : ModProjectile, IEmpressAttack, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public EmpressScorchTier ScorchTier => EmpressScorchTier.Heavy;
        public bool BeamType => true;
        public float FeedbackIntensity => 1f;

        internal const int Rays = 7;
        internal const int ChargeTime = 120;
        internal const int FireTime = 10;
        internal const int TotalTime = 150;
        internal const float Length = 2600f;
        private const float LockFraction = 0.75f;
        private const float MaxHalfAngleDeg = 10f;

        private ref float StartAngle => ref Projectile.ai[0];
        private ref float Sweep => ref Projectile.ai[1];
        private NPC Host => ((int)Projectile.ai[2]).TryGetNPC(out NPC n) ? n : null;
        private ref float Timer => ref Projectile.localAI[0];

        private float Hue => (Projectile.identity * 0.191f) % 1f;
        private static float DayBlend => VaultUtils.isServer ? 0f : EmpressDayDrive.Intensity;
        private float ChargeT => MathHelper.Clamp(Timer / ChargeTime, 0f, 1f);
        private float Rotation => StartAngle + Sweep * ChargeT;
        private float HalfAngle => MathHelper.ToRadians(MaxHalfAngleDeg) * ChargeT * ChargeT;

        //只亮最快组：帧末结算的静态注意力管理
        private static readonly List<EmpressMeltingFan> live = new();
        private static int soonest = -1;
        private static float soonestFade;
        private float attention = 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalTime;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 0;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC host = Host;
            if (host == null || !host.active) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = host.Center;
            Projectile.velocity = Vector2.Zero;
            Timer++;

            Player target = host.target >= 0 && host.target < Main.maxPlayers && Main.player[host.target].Alives() ? Main.player[host.target] : null;
            bool day = NPC.ShouldEmpressBeEnraged();

            //吸附式追踪：把终角向"某根射线正对玩家"推，最近的一根中选；75% 后锁死
            if (day && target != null && ChargeT < LockFraction) {
                float aimAngle = Projectile.AngleTo(target.Center + target.velocity * (ChargeTime - Timer) * 0.5f);
                float endRot = StartAngle + Sweep;
                float step = MathHelper.TwoPi / Rays;
                float diff = MathHelper.WrapAngle(aimAngle - endRot);
                //折到最近射线格
                diff -= MathF.Round(diff / step) * step;
                Sweep += MathF.Sign(diff) * 0.0072f * ChargeT;
            }

            if (VaultUtils.isServer) {
                return;
            }
            live.Add(this);

            Color light = EmpressMotion.FormColor(Hue, DayBlend, 0.66f);
            Lighting.AddLight(Projectile.Center, light.ToVector3() * (0.4f + 0.6f * ChargeT));

            if (Timer == 1f) {
                EmpressProjectileSystem.EnqueueBuildup(Projectile.Center, SoundID.DD2_EtherianPortalOpen with { Volume = 0.55f, Pitch = -0.4f });
            }
            //发射前 60f 震屏缓升：(1 - 剩余/60)² × 0.7，每 6f 落一记
            if (Timer >= ChargeTime - 60 && Timer < ChargeTime && Timer % 6 == 0) {
                float k = 1f - (ChargeTime - Timer) / 60f;
                EmpressMotion.Shake(Projectile.Center, k * k * 3f, 6);
            }
            if (Timer == ChargeTime) {
                OnFire();
            }
        }

        private void OnFire() {
            EmpressProjectileSystem.EnqueueShot(Projectile.Center, SoundID.Item122 with { Volume = 0.9f, Pitch = 0.6f });
            EmpressMotion.Shake(Projectile.Center, 8f, 16);
            EmpressScreenFX.PushFlash(Projectile.DirectionTo(Main.LocalPlayer.Center), 0.25f);
            float rot = Rotation;
            for (int r = 0; r < Rays; r++) {
                Vector2 dir = (rot + MathHelper.TwoPi / Rays * r).ToRotationVector2();
                for (int i = 0; i < 18; i++) {
                    float u = Main.rand.NextFloat(-1f, 1f);
                    u *= MathF.Abs(u);
                    Vector2 d = dir.RotatedBy(u * HalfAngle);
                    Vector2 pos = Projectile.Center + d * MathF.Sqrt(Main.rand.NextFloat()) * 1800f;
                    if (Vector2.Distance(pos, Main.LocalPlayer.Center) > 2200f) {
                        continue;
                    }
                    PRTLoader.NewParticle<PRT_EmpressSpark>(pos, d * Main.rand.NextFloat(13f, 17f) * (1f - MathF.Abs(u)),
                        EmpressMotion.FormColor(Hue + r * 0.03f, DayBlend, 0.72f), Main.rand.NextFloat(0.8f, 1.5f))?.Configure(18, Hue, DayBlend);
                }
            }
        }

        /// <summary>帧末：选出最快发射的扇为焦点，其余压到 33%</summary>
        internal static void ResolveAttention() {
            int pick = -1;
            float best = float.PositiveInfinity;
            foreach (EmpressMeltingFan fan in live) {
                if (fan.Timer >= ChargeTime) {
                    continue;
                }
                float left = ChargeTime - fan.Timer;
                if (left < best) {
                    best = left;
                    pick = fan.Projectile.whoAmI;
                }
            }
            if (pick != soonest) {
                soonest = pick;
                soonestFade = 0f;
            }
            soonestFade = Math.Min(soonestFade + 0.1f, 1f);
            foreach (EmpressMeltingFan fan in live) {
                float target = fan.Timer >= ChargeTime ? 1f : (fan.Projectile.whoAmI == soonest ? 0.33f + 0.67f * soonestFade : 0.33f);
                fan.attention = MathHelper.Lerp(fan.attention, target, 0.2f);
            }
            live.Clear();
        }

        /// <summary>伤害弧：发射后 6f 满宽，随后 4f 线性收</summary>
        private float DamageArc() {
            if (Timer < ChargeTime || Timer >= ChargeTime + FireTime) {
                return 0f;
            }
            float since = Timer - ChargeTime;
            return since < 6f ? 1f : 1f - (since - 6f) / 4f;
        }

        public override bool? CanDamage() => DamageArc() > 0f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 toTarget = targetHitbox.Center.ToVector2() - Projectile.Center;
            if (toTarget.LengthSquared() > Length * Length) {
                return false;
            }
            float arc = DamageArc();
            float rot = Rotation;
            Vector2 dirT = toTarget.SafeNormalize(Vector2.UnitY);
            for (int r = 0; r < Rays; r++) {
                float rayAngle = rot + MathHelper.TwoPi / Rays * r;
                Vector2 rayDir = rayAngle.ToRotationVector2();
                float ang = MathF.Acos(MathHelper.Clamp(Vector2.Dot(rayDir, dirT), -1f, 1f));
                if (rayDir.X * dirT.Y - rayDir.Y * dirT.X < 0f) {
                    ang = -ang;
                }
                //把射线夹到楔内最靠近目标的方向再做线段检测
                float half = HalfAngle * arc;
                Vector2 end = Projectile.Center + (rayAngle + MathHelper.Clamp(ang, -half, half)).ToRotationVector2() * Length;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, end)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }
            float t = ChargeT;
            float rot = Rotation;
            float day = DayBlend;
            Color main = EmpressMotion.FormColor(Hue, day, 0.68f);
            float fadeOut = Timer >= ChargeTime ? MathF.Pow(1f - MathHelper.Clamp((Timer - ChargeTime) / (TotalTime - ChargeTime), 0f, 1f), 2f) : 1f;
            float endHalf = MathF.Tan(HalfAngle) * Length;

            for (int r = 0; r < Rays; r++) {
                Vector2 dir = (rot + MathHelper.TwoPi / Rays * r).ToRotationVector2();
                if (Timer < ChargeTime) {
                    //充能：线→楔，中段抖、末段稳（t(1-t³)），最后 10% 向白推
                    float wobble = t * (1f - t * t * t) * 0.02f;
                    Vector2 wdir = dir.RotatedBy(MathF.Sin(Main.GlobalTimeWrappedHourly * 20f + r) * wobble);
                    float opacity = (0.25f + 0.75f * t) * attention;
                    Color c = Color.Lerp(main, Color.White, 0.25f * MathF.Pow(Math.Max((t - 0.9f) / 0.1f, 0f), 2f));
                    EmpressBeamDraw.DrawMarkerTaper(Projectile.Center, wdir, Length, 6f + 10f * t, 12f + endHalf, c, opacity, t, t * t * 1.2f, 1f - t, 0.5f);
                }
                else {
                    //发射：满宽楔束，衰减期从中心掏空（起端变窄）
                    float since = Timer - ChargeTime;
                    float spike = MathF.Pow(Math.Max(1f - since * 0.1f, 0f), 3f);
                    float w = MathHelper.Clamp(fadeOut, 0.08f, 1f);
                    Color tint = Color.Lerp(Color.White, new Color(255, 238, 205), day) * (0.35f + 0.65f * fadeOut);
                    EmpressBeamDraw.DrawSunbeamTaper(Projectile.Center + dir * (since * 40f), dir, Length - since * 40f,
                        (10f + 30f * spike) * w, (endHalf * 0.9f + 30f) * w, Hue, w, tint);
                }
            }
        }
    }
}
