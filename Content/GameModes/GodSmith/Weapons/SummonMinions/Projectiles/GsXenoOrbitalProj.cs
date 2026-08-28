using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 轨道歼灭：UFO 舰群校准完毕后从高轨落下的相干光矛。
    /// 四相 = 锁定 16 帧（全息括弧收拢咬住目标，跟随移动，无伤害）/
    /// 光矛 10 帧（伤害窗；顶端发射器辉芒收口、底端着弹光暴收口，柱宽有生命周期）/
    /// 电离余辉 18 帧（柱体残像褪色，离子光尘沿柱上浮）。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验。材质：外星相干光（芯白热、体青柠、缘青）
    /// </summary>
    internal class GsXenoOrbitalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color IonLime = new(150, 255, 96);
        private static readonly Color IonTeal = new(66, 214, 198);
        private static readonly Color HotCore = new(240, 255, 228);

        private const int LockFrames = 16;
        private const int BeamFrames = 10;
        private const int IonFrames = 18;
        private const int TotalFrames = LockFrames + BeamFrames + IonFrames;
        /// <summary>光柱上端相对锚点的高度</summary>
        private const float BeamTop = 230f;
        /// <summary>光柱下探深度（没入目标脚下）</summary>
        private const float BeamBottom = 58f;
        private const float BeamWidth = 46f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Locking => Elapsed < LockFrames;

        private bool Firing => Elapsed >= LockFrames && Elapsed < LockFrames + BeamFrames;

        private bool Ionizing => Elapsed >= LockFrames + BeamFrames;

        private float Seed => Projectile.identity * 0.5903f % MathHelper.TwoPi;

        private NPC BoundTarget {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                return npc.active && npc.type == (int)Projectile.ai[1] ? npc : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = TotalFrames;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            //锁定期咬住目标移动；目标失踪即中止校准（不放空矛）
            if (Locking) {
                NPC target = BoundTarget;
                if (target == null) {
                    Projectile.Kill();
                    return;
                }
                Projectile.Center = target.Center;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, IonLime.ToVector3() * (Firing ? 0.8f : 0.3f));
            //锁定起始：校准滴答
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.6f },
                    Projectile.Center);
            }
            //光矛落下：轨道炮鸣 + 着弹离子飞溅
            if (Elapsed == LockFrames) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.85f, Pitch = -0.45f },
                    Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    float ang = Seed + i / 12f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Spark>(
                        Projectile.Center + new Vector2(0f, BeamBottom - 8f),
                        ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6.5f)
                            * new Vector2(1f, 0.55f),
                        i % 3 == 0 ? HotCore : IonLime,
                        Main.rand.NextFloat(0.26f, 0.44f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            //电离余辉：离子光尘沿柱上浮
            if (Ionizing && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-BeamWidth, BeamWidth) * 0.4f,
                        Main.rand.NextFloat(-BeamTop * 0.7f, BeamBottom)),
                    new Vector2(0f, -Main.rand.NextFloat(1.2f, 2.6f)),
                    Main.rand.NextBool() ? IonLime : IonTeal,
                    Main.rand.NextFloat(0.08f, 0.14f))?.Configure(18, 0.75f);
            }
        }

        /// <summary>只有光矛相结算伤害</summary>
        public override bool? CanDamage() => Firing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle column = new((int)(Projectile.Center.X - BeamWidth / 2f),
                (int)(Projectile.Center.Y - BeamTop), (int)BeamWidth,
                (int)(BeamTop + BeamBottom));
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (soft == null || glow == null || flare == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (Locking) {
                //全息括弧：四角 L 形括弧旋转收拢（每角两根短亮条拼成）
                float t = Elapsed / (float)LockFrames;
                float dist = MathHelper.Lerp(58f, 26f, t * t);
                float spin = Seed + t * 1.4f;
                float holo = 0.35f + 0.45f * t;
                for (int i = 0; i < 4; i++) {
                    float ang = spin + MathHelper.PiOver2 * i + MathHelper.PiOver4;
                    Vector2 corner = pos + ang.ToRotationVector2() * dist;
                    for (int j = 0; j < 2; j++) {
                        float barAng = ang + MathHelper.Pi + (j == 0 ? 0.55f : -0.55f);
                        Main.EntitySpriteDraw(soft, corner, null,
                            (IonLime with { A = 0 }) * holo, barAng,
                            new Vector2(0f, soft.Height / 2f),
                            new Vector2(11f / soft.Width, 2.2f / soft.Height),
                            SpriteEffects.None, 0);
                    }
                }
                //中心校准点：脉冲呼吸
                float pulse = 0.7f + 0.3f * (float)Math.Sin(Elapsed * 0.9f + Seed);
                Main.EntitySpriteDraw(glow, pos, null, (IonTeal with { A = 0 }) * (0.5f * t),
                    0f, glow.Size() / 2f, 0.22f * pulse, SpriteEffects.None, 0);
                return false;
            }

            //光矛与余辉共用柱体绘制，宽度与亮度有生命周期
            float beamT = Firing ? (Elapsed - LockFrames) / (float)BeamFrames : 1f;
            float fade = Ionizing
                ? MathHelper.Clamp(Projectile.timeLeft / (float)IonFrames, 0f, 1f) * 0.45f : 1f;
            //宽度：2 帧展宽 → 驻留脉动 → 余辉收窄
            float widen = Firing
                ? MathHelper.Clamp((Elapsed - LockFrames + 1) / 2f, 0f, 1f)
                : fade;
            float pulseW = 1f + (Firing ? 0.1f * (float)Math.Sin(Elapsed * 1.3f + Seed) : 0f);
            float columnH = BeamTop + BeamBottom;
            Vector2 mid = pos + new Vector2(0f, (BeamBottom - BeamTop) * 0.5f);

            //柱体三层：青缘 → 青柠体 → 白热芯（全加色）
            Main.EntitySpriteDraw(soft, mid, null, (IonTeal with { A = 0 }) * (0.5f * fade), 0f,
                soft.Size() / 2f,
                new Vector2(BeamWidth * 1.25f * widen * pulseW / soft.Width, columnH / soft.Height),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, mid, null, (IonLime with { A = 0 }) * (0.8f * fade), 0f,
                soft.Size() / 2f,
                new Vector2(BeamWidth * 0.72f * widen * pulseW / soft.Width, columnH * 0.98f / soft.Height),
                SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, mid, null, (HotCore with { A = 0 }) * (0.9f * fade), 0f,
                soft.Size() / 2f,
                new Vector2(BeamWidth * 0.3f * widen / soft.Width, columnH * 0.94f / soft.Height),
                SpriteEffects.None, 0);
            //顶端发射器辉芒（上端收口）
            Vector2 top = pos - new Vector2(0f, BeamTop);
            Main.EntitySpriteDraw(flare, top, null, (IonLime with { A = 0 }) * (0.9f * fade),
                Seed + Elapsed * 0.05f, flare.Size() / 2f, 0.3f * widen, SpriteEffects.None, 0);
            //底端着弹光暴（下端收口：光球 + 横向溅光）
            Vector2 impact = pos + new Vector2(0f, BeamBottom);
            Main.EntitySpriteDraw(glow, impact, null, (HotCore with { A = 0 }) * (0.85f * fade),
                0f, glow.Size() / 2f, new Vector2(0.9f, 0.5f) * widen, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, impact, null, (IonLime with { A = 0 }) * (0.7f * fade),
                0f, soft.Size() / 2f,
                new Vector2(BeamWidth * 2.1f * widen / soft.Width, 6f / soft.Height),
                SpriteEffects.None, 0);
            return false;
        }
    }
}
