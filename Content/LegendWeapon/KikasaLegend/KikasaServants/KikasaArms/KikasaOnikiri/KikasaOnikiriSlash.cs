using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using CSR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs.CrimsonSlashRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaOnikiri
{
    /// <summary>
    /// 鬼切械奴的绯红斩痕：与刀奴湖水斩痕同一事件契约（owner 生成、生成包自含、
    /// 判定窗只开爆发前几帧、每敌一次），但视觉走绯红裂空的水墨刀光管线
    /// （<see cref="CrimsonSlashRenderer"/> 三层异步 + OniCrimsonSlash shader）。
    /// ai0=判定半长，ai1=0 居合直线 / 1 终结月牙；方向由初速定，帧内缓存后弹速只管击退。
    /// 命中走 <see cref="OnikiriItem.ApplySlashPenetration"/>：鬼切斩击的无视防御是复制体同享的签名
    /// </summary>
    internal class KikasaOnikiriSlash : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>各端本地计帧：生命周期与判定窗的时间轴</summary>
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>冲线方向角：首个本地更新从弹速缓存</summary>
        private ref float LockedAng => ref Projectile.localAI[1];

        /// <summary>判定与绘制的半长 px（生成包自带）</summary>
        private float HalfLen => Projectile.ai[0];

        /// <summary>终结月牙：更大的弧形刀光、更宽的判定、更长的余像</summary>
        private bool HeavyArc => Projectile.ai[1] > 0.5f;

        private CSR.SlashDef def;
        private bool defBuilt;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            //一道斩痕对每个敌人只算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>斩痕定义：全部由 ai/identity 确定性构建，各端一致</summary>
        private void BuildDef() {
            defBuilt = true;
            float flip = Projectile.identity % 2 == 0 ? 1f : -1f;
            float seed = Projectile.identity * 0.7391f % 10f;
            if (HeavyArc) {
                def = new CSR.SlashDef {
                    Birth = 0,
                    SweepFrames = 5,
                    Life = 32,
                    ErodeStart = 12,
                    ErodeFrames = 16,
                    ColorShiftDelay = 5f,
                    ColorShiftFrames = 14f,
                    DamageStart = 0,
                    DamageEnd = 10,
                    Mode = 0f,
                    Rot = LockedAng,
                    Span = 2.9f,
                    Thick = 0.34f,
                    HalfX = HalfLen,
                    HalfY = HalfLen * 0.62f,
                    Flip = flip,
                    Opacity = 1f,
                    FrontGlow = 1.3f,
                    Seed = seed,
                    TailErode = 0.55f,
                    FlashPower = 1.15f,
                    Ink = 0.4f,
                    FeiBai = 0.5f,
                    Bleed = 0.35f,
                    SplitTail = 0.5f,
                };
                Projectile.timeLeft = def.Life + 2;
                return;
            }
            def = new CSR.SlashDef {
                Birth = 0,
                SweepFrames = 3,
                Life = 24,
                ErodeStart = 8,
                ErodeFrames = 14,
                ColorShiftDelay = 4f,
                ColorShiftFrames = 12f,
                DamageStart = 0,
                DamageEnd = 8,
                Mode = 1f,
                Rot = LockedAng,
                Span = 1f,
                Thick = 0.42f,
                HalfX = HalfLen,
                HalfY = HalfLen * 0.3f,
                Flip = flip,
                Opacity = 0.96f,
                FrontGlow = 1.1f,
                Seed = seed,
                TailErode = 0.5f,
                FlashPower = 0.9f,
                RazorTailWiden = 0.35f,
                Ink = 0.35f,
                FeiBai = 0.45f,
                Bleed = 0.3f,
                SplitTail = 0.4f,
            };
            Projectile.timeLeft = def.Life + 2;
        }

        public override void AI() {
            Life++;
            if ((int)Life == 1) {
                LockedAng = Projectile.velocity.ToRotation();
                Projectile.rotation = LockedAng;
                BuildDef();
                SpawnBirthSparks();
            }
            //弹速只负责给击退一个顺劈的方向，斩痕本体钉在原地
            Projectile.velocity *= 0.78f;

            float glow = 0.55f * FadeAlpha();
            Lighting.AddLight(Projectile.Center, 0.55f * glow, 0.09f * glow, 0.07f * glow);
        }

        private float FadeAlpha() {
            if (!defBuilt) {
                return 0f;
            }
            return MathHelper.Clamp(1f - (Life - def.DamageEnd) / (float)(def.Life - def.DamageEnd), 0f, 1f);
        }

        /// <summary>显现帧沿刃撒绯红火花：撕开的空气还烧着，各端自演（纯表现）</summary>
        private void SpawnBirthSparks() {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int sparks = HeavyArc ? 12 : 8;
            for (int k = 0; k < sparks; k++) {
                float u = Main.rand.NextFloat(-0.85f, 0.95f);
                Vector2 pos = Projectile.Center + dir * (u * HalfLen);
                Vector2 vel = perp * Main.rand.NextFloat(-2.6f, 2.6f) + dir * Main.rand.NextFloat(0.8f, 3.2f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel,
                    Main.rand.NextBool(3) ? new Color(255, 168, 92) : new Color(255, 64, 44),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 22), affectedByGravity: true);
            }
        }

        //==================== 判定：沿刀光带采样的慷慨捕获 ====================

        /// <summary>伤害窗只开爆发帧；之后斩痕只是余像</summary>
        public override bool? CanDamage() => defBuilt && Life <= def.DamageEnd ? null : false;

        /// <summary>沿刀光带逐段线判定：弧与直线共用 SampleBand 的几何，贴脸与擦边都算数</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!defBuilt) {
                return false;
            }
            int lt = (int)Life;
            const int samples = 10;
            Vector2 prev = CSR.SampleBand(in def, Projectile.Center, 0f, lt).Center;
            for (int k = 1; k <= samples; k++) {
                CSR.SlashBandSample band = CSR.SampleBand(in def, Projectile.Center, k / (float)samples, lt);
                float width = MathF.Max(band.Width, 26f) * (HeavyArc ? 1.15f : 1f);
                float _ = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    prev, band.Center, width, ref _)) {
                    return true;
                }
                prev = band.Center;
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //鬼切斩击管线的签名：无视防御 + 半穿 DR，复制体同享
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound((HeavyArc ? CWRSound.KatanaHitB : CWRSound.KatanaHit) with {
                Volume = HeavyArc ? 0.5f : 0.38f,
                Pitch = HeavyArc ? -0.08f : 0.1f,
                MaxInstances = 3
            }, target.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            for (int k = 0; k < (HeavyArc ? 7 : 4); k++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2.5f, 6f),
                    new Color(255, 70, 46), Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24), affectedByGravity: true);
            }
        }

        //==================== 绘制：绯红裂空水墨刀光 ====================

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !defBuilt) {
                return;
            }
            int lt = (int)Life;
            if (lt < 1 || lt >= def.Life) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!CSR.BeginDraw(device, out Effect fx, out BlendState pb, out RasterizerState pr, out DepthStencilState pd)) {
                return;
            }
            CSR.DrawThreeLayers(device, fx, in def, Projectile.Center, lt, 0f);
            CSR.EndDraw(device, pb, pr, pd);
        }
    }
}
