using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaSeaShrimp
{
    /// <summary>
    /// 鬼奴海虾的血空泡：空泡拳的第二拍。生长期无害（泡本身即预告，
    /// 外围血滴被向心吸入、爆缩前收缩静默——吸气拍），爆缩瞬间白闪 +
    /// 冲击波前撕裂外扩，判定半径逐帧对齐可见波前（伤害窗=视觉窗）。
    /// ai[0]=爆缩延迟帧，ai[1]=爆缩半径；计时由 localAI 逐端计数。
    /// boss 空泡的血湖变调：泡体与崩爆环手绘血色，不借深渊着色器
    /// </summary>
    internal class KikasaCavitationOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle")]
        private static Asset<Texture2D> RingTex = null;

        private int Delay => (int)Projectile.ai[0];
        private float BlastRadius => Projectile.ai[1];
        /// <summary>爆缩后的余帧（冲击环外扩+消散）</summary>
        private const int AfterFrames = 12;
        /// <summary>伤害窗帧数：覆盖波前从中心推进到 BlastRadius 的整段</summary>
        private const int DamageFrames = 8;
        /// <summary>冲击环最终可见半径 = 爆缩半径 × 此系数（环越过判定圈后是无害消散波）</summary>
        private const float RingOvershoot = 1.3f;

        /// <summary>本地帧龄：localAI 逐端计数，各端从收到生成包起步</summary>
        private int Age => (int)Projectile.localAI[0];

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private float Seed => Projectile.identity * 0.7391f % 4.17f;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
            Projectile.timeLeft = 120;
        }

        /// <summary>当前可见半径：三次方生长→爆缩前收缩拍</summary>
        private float VisualRadius() {
            int age = Age;
            if (age >= Delay) {
                return BlastRadius;
            }
            float growEnd = Delay - 8;
            if (age < growEnd) {
                float t = age / growEnd;
                return BlastRadius * (0.12f + 0.88f * t * t * (3f - 2f * t));
            }
            float s = (age - growEnd) / 8f;
            return BlastRadius * MathHelper.Lerp(1f, 0.42f, s);
        }

        /// <summary>涨压 0~1</summary>
        private float Charge => MathHelper.Clamp(Age / MathF.Max(Delay - 8, 1f), 0f, 1f);

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= Delay + AfterFrames) {
                Projectile.Kill();
                return;
            }

            //慢漂：出拳的余劲带着泡前行，逐渐驻停
            Projectile.velocity *= 0.965f;

            float lum = age < Delay ? 0.3f : 0.85f;
            Lighting.AddLight(Projectile.Center, 0.4f * lum, 0.12f * lum, 0.18f * lum);

            if (age == Delay) {
                //爆缩帧：白闪 + 冲击波前 + 径向血滴锥
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.8f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                if (!Main.dedServ) {
                    int globs = (int)(10 * MathHelper.Clamp(BlastRadius / 108f, 0.8f, 1.6f));
                    for (int i = 0; i < globs; i++) {
                        Vector2 dir = Main.rand.NextVector2Unit();
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Projectile.Center + dir * 10f,
                            dir * Main.rand.NextFloat(4f, 9f),
                            Color.Lerp(BloodDeep, BloodMain, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26), 1.4f);
                    }
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.1f)
                        ?.Configure(new Vector2(1f, 1f), Main.rand.NextFloat(MathHelper.TwoPi), 0.4f, 10);
                    if (Main.LocalPlayer != null
                        && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 1200f) {
                        Main.LocalPlayer.CWR()?.GetScreenShake(4.5f);
                    }
                }
            }
            else if (age < Delay && !Main.dedServ) {
                //生长期：外围血滴被向心吸入，密度∝√涨压，收缩拍截停——吸气静默
                bool quiet = age > Delay - 8;
                if (!quiet && Main.rand.NextFloat() < 0.25f + 0.5f * MathF.Sqrt(Charge)) {
                    float dist = VisualRadius() * Main.rand.NextFloat(1.25f, 1.8f) + 12f;
                    Vector2 rim = Projectile.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * dist;
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(rim,
                        (Projectile.Center - rim) * 0.08f,
                        Color.Lerp(BloodDeep, BloodMain, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.22f, 0.4f))?.Configure(13, 1.6f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //余波：血雾在弹体死后继续存在
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(BlastRadius * 0.45f, BlastRadius * 0.45f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.2f, 0.6f)),
                    MistBlood * 0.6f, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(34, 56));
            }
        }

        /// <summary>伤害窗：仅爆缩后 8 帧，波前推进段</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= Delay && age < Delay + DamageFrames ? null : false;
        }

        /// <summary>冲击环某进度下的可见半径（与绘制同式，判定对齐可见波前）</summary>
        private static float RingRadius(float finalRingPx, float progress) {
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float ringT = 1f - (1f - t) * (1f - t);
            return finalRingPx * MathHelper.Lerp(0.125f, 1f, ringT);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float progress = MathHelper.Clamp((Age - Delay) / (float)AfterFrames, 0f, 1f);
            float shockR = MathF.Min(BlastRadius, RingRadius(BlastRadius * RingOvershoot, progress));
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= shockR;
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            SpriteBatch sb = Main.spriteBatch;
            Texture2D ring = RingTex?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (age >= Delay) {
                //崩爆：白热针点急速衰减 + 血色冲击环外扩
                float progress = MathHelper.Clamp((age - Delay) / (float)AfterFrames, 0f, 1f);
                float fade = 1f - progress;
                if (glow != null && fade > 0f) {
                    sb.Draw(glow, drawPos, null, new Color(255, 255, 255, 0) * (0.85f * fade), 0f,
                        glow.Size() * 0.5f, BlastRadius * 1.3f / glow.Width * 2f * fade, SpriteEffects.None, 0f);
                    sb.Draw(glow, drawPos, null, (BloodBright with { A = 0 }) * (0.7f * fade), 0f,
                        glow.Size() * 0.5f, BlastRadius * 2.2f / glow.Width * 2f, SpriteEffects.None, 0f);
                }
                if (ring != null) {
                    float r = RingRadius(BlastRadius * RingOvershoot, progress);
                    float ringScale = r * 2f / ring.Width;
                    sb.Draw(ring, drawPos, null, BloodMain * (0.8f * fade), 0f,
                        ring.Size() * 0.5f, ringScale, SpriteEffects.None, 0f);
                    sb.Draw(ring, drawPos, null, (BloodBright with { A = 0 }) * (0.5f * fade), 0f,
                        ring.Size() * 0.5f, ringScale * 0.92f, SpriteEffects.None, 0f);
                }
                return false;
            }

            //生长期泡体：双环 + 偏心高光 + 表面张力抖动（血色手绘，读作绷紧的血膜）
            if (ring == null) {
                return false;
            }
            float radius = VisualRadius();
            bool shrinking = age > Delay - 8;
            float wobbleAmp = shrinking ? 0.012f : 0.045f;
            float wobble = 1f + wobbleAmp * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f + Seed * 6f);
            float scale = radius * 2f / ring.Width * wobble;
            float tension = 0.55f + 0.35f * Charge;

            sb.Draw(ring, drawPos, null, BloodMain * (0.8f * tension), 0f,
                ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            sb.Draw(ring, drawPos, null, BloodDeep * 0.4f, 0f,
                ring.Size() * 0.5f, scale * 0.86f, SpriteEffects.None, 0f);
            if (glow != null) {
                //偏心高光：泡面挂一粒亮点，读作球面反光
                sb.Draw(glow, drawPos + new Vector2(-radius * 0.34f, -radius * 0.4f), null,
                    (BloodBright with { A = 0 }) * (0.5f + 0.3f * Charge), 0f, glow.Size() * 0.5f,
                    radius * 0.5f / glow.Width * 2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
