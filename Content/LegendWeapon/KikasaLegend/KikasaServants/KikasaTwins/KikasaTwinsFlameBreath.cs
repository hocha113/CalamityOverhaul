using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaTwins
{
    /// <summary>
    /// 鬼奴魔焰眼的锥形血焰吐息：短程扇形火舌，燃烧的是血不是气
    /// 液态血焰粒子浓密推涌、烧尽的血滴从锥端坠回，病绿诅咒余烬只作次要点缀。
    /// ai[0]=起始角；逐帧锚定鬼奴魔焰眼口器，追踪转率有限（推着目标走而不是黏着烧）；
    /// 宿主没了/开始溶解就快进熄火。命中方向沿吐息轴，把目标往外推
    /// </summary>
    internal class KikasaTwinsFlameBreath : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int ExpandFrames = 6;
        internal const int FadeFrames = 10;
        internal const int TotalLife = KikasaTwinsServant.FlameBreathFrames + FadeFrames;

        /// <summary>锥长与半张角：短程近逼的火舌，不是横贯全屏的光柱</summary>
        private const float ConeLength = 216f;
        private const float ConeHalfAngle = 0.4f;

        private ref float Timer => ref Projectile.localAI[0];
        private ref float AimAngle => ref Projectile.ai[0];

        /// <summary>展开/熄火包络 0~1</summary>
        private float envelope;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.timeLeft = TotalLife + 20;
        }

        public override void AI() {
            KikasaTwinsServant host = KikasaTwinsServant.FindFor(Projectile.owner);

            //宿主没了/开始溶解：快进熄火
            if ((host == null || host.IsDismissing) && Timer < TotalLife - FadeFrames) {
                Timer = TotalLife - FadeFrames;
            }

            //有限转率追踪：吐息是推进走位，不是黏着制导
            int target = FindTarget();
            if (target >= 0 && Timer < TotalLife - FadeFrames) {
                float want = (Main.npc[target].Center - Projectile.Center).ToRotation();
                AimAngle = AimAngle.AngleTowards(want, 0.028f);
            }
            Projectile.rotation = AimAngle;
            Vector2 dir = AimAngle.ToRotationVector2();

            //锚定口器
            if (host != null && host.EyesReady) {
                Projectile.Center = host.EyeCenter(1) + dir * 30f;
            }

            //展开→稳态→熄火包络
            float collapseStart = TotalLife - FadeFrames;
            if (Timer < ExpandFrames) {
                envelope = Timer / ExpandFrames;
            }
            else if (Timer >= collapseStart) {
                envelope = MathHelper.Clamp(1f - (Timer - collapseStart) / FadeFrames, 0f, 1f);
            }
            else {
                envelope = 1f;
            }

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            float len = ConeLength * envelope;
            for (int i = 0; i < 3; i++) {
                Lighting.AddLight(Projectile.Center + dir * (len * (i + 0.5f) / 3f),
                    0.5f * envelope, 0.14f * envelope, 0.1f * envelope);
            }

            if (Main.dedServ || envelope < 0.2f) {
                return;
            }

            //喷吐主体：浓密血焰舌，越靠锥心越快
            for (int i = 0; i < 3; i++) {
                float spread = Main.rand.NextFloat(-ConeHalfAngle * 0.8f, ConeHalfAngle * 0.8f);
                float coreK = 1f - MathF.Abs(spread) / ConeHalfAngle;
                Vector2 vel = (AimAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(6.5f, 11.5f) * (0.6f + coreK * 0.5f) * envelope;
                PRTLoader.NewParticle<PRT_KikasaTwinsFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), vel,
                    Main.rand.NextBool(4) ? KikasaTwinsServant.BloodDeep : KikasaTwinsServant.BloodMain,
                    Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(0.03f, 0.07f));
            }
            //诅咒病绿余烬：次要点缀层，稀疏
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_KikasaTwinsFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(5f, 9f) * envelope,
                    KikasaTwinsServant.CursedTinge * 0.8f, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(0.05f, 0.09f));
            }
            //烧尽的血滴从锥中后段坠回，血比火重
            if (Main.rand.NextBool(3)) {
                Vector2 dropPos = Projectile.Center + dir * len * Main.rand.NextFloat(0.45f, 0.95f)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-30f, 30f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(dropPos,
                    dir * 1.2f + new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)),
                    KikasaTwinsServant.BloodDeep, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(18, 30), 0.4f);
            }
            //锥端烟雾余韵
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + dir * len * Main.rand.NextFloat(0.8f, 1.05f),
                    dir * 0.8f + new Vector2(0f, -0.4f),
                    KikasaTwinsServant.MistBlood * 0.75f, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(36, 60));
            }

            //喷吐声与低鸣：位置声各端自然衰减
            if ((int)Timer % 9 == 2) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = -0.25f, MaxInstances = 2 }, Projectile.Center);
            }
            if ((int)Timer % 22 == 11) {
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.28f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
            }
            if ((int)Timer % 8 == 0 && ViewedOwner) {
                ShakeViewer(0.6f);
            }

            UpdateLakeSizzle(dir, len);
        }

        /// <summary>火舌燎过湖面：交点滋出蒸腾与涟漪（观看域门控）</summary>
        private void UpdateLakeSizzle(Vector2 dir, float len) {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f
                || KikasaDomain.Viewed != domain) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            float crossT = MathF.Abs(dir.Y) > 0.02f ? (lakeY - Projectile.Center.Y) / dir.Y : -1f;
            if (crossT < 20f || crossT > len) {
                return;
            }
            Vector2 cross = new(Projectile.Center.X + dir.X * crossT, lakeY);
            int t = (int)Timer;
            if (t % 10 == 3) {
                KikasaDomainDeco.RippleAt(cross, 0.7f);
                PRTLoader.NewParticle<PRT_GhostRainMist>(cross + new Vector2(0f, -6f),
                    new Vector2(dir.X * 0.3f, -0.7f),
                    KikasaTwinsServant.MistBlood * 0.85f, Main.rand.NextFloat(0.55f, 0.85f))
                    ?.Configure(Main.rand.Next(30, 50));
            }
        }

        private int FindTarget() {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true) {
                return -1;
            }
            if (owner.HasMinionAttackTargetNPC) {
                NPC picked = Main.npc[owner.MinionAttackTargetNPC];
                if (picked.CanBeChasedBy(Projectile)
                    && Vector2.Distance(picked.Center, owner.Center) < 1500f) {
                    return picked.whoAmI;
                }
            }
            int best = -1;
            float bestDist = 1050f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, owner.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== 命中 ====================

        /// <summary>伤害窗与可见火舌严格对齐：展开完成到熄火开始</summary>
        public override bool? CanDamage()
            => Timer > ExpandFrames && Timer < TotalLife - FadeFrames ? null : false;

        /// <summary>锥形命中：沿轴取样圆，半径随距离张开</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float len = ConeLength * envelope;
            const int samples = 6;
            for (int i = 0; i < samples; i++) {
                float f = (i + 0.5f) / samples;
                Vector2 point = Projectile.Center + dir * len * f;
                float radius = MathHelper.Lerp(16f, 70f, f);
                if (targetHitbox.Distance(point) < radius) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>火舌推人：命中方向沿吐息轴，读出被火压着走的感觉</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int dir = MathF.Sign(Projectile.rotation.ToRotationVector2().X);
            if (dir != 0) {
                modifiers.HitDirectionOverride = dir;
            }
        }

        public override bool? CanCutTiles() => false;

        //==================== 绘制：近口楔形亮体，主体交给粒子 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || envelope <= 0.05f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 gOrigin = glow.Size() * 0.5f;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            float flicker = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 34f + Projectile.identity);

            //近口楔形三层（A=0 加色）：火舌根部的实体感，远端交给粒子浓度
            for (int i = 0; i < 3; i++) {
                float f = (i + 0.5f) / 3f;
                Vector2 pos = Projectile.Center + dir * (ConeLength * 0.42f * envelope * f) - Main.screenPosition;
                float wide = MathHelper.Lerp(10f, 34f, f) * envelope * flicker;
                float a = (1f - f * 0.55f) * envelope;
                sb.Draw(glow, pos, null, (KikasaTwinsServant.BloodMain with { A = 0 }) * (0.5f * a),
                    Projectile.rotation, gOrigin,
                    new Vector2(58f / glow.Width, wide * 2f / glow.Height), SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, (new Color(255, 196, 170) with { A = 0 }) * (0.35f * a),
                    Projectile.rotation, gOrigin,
                    new Vector2(46f / glow.Width, wide * 1.1f / glow.Height), SpriteEffects.None, 0f);
            }
            //口器亮球
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                (KikasaTwinsServant.BloodBright with { A = 0 }) * (0.7f * envelope * flicker), 0f,
                gOrigin, new Vector2(30f * envelope / glow.Width), SpriteEffects.None, 0f);
            return false;
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
