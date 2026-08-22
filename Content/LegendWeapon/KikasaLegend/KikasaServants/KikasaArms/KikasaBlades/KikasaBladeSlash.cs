using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaBlades
{
    /// <summary>
    /// 刀奴的湖水斩痕：一刀是事件不是过程——两帧暴力显现（超冲 1.2 倍再落回），
    /// 消散温柔（水读得出的收法）。形状语法走压扁月牙：主轴贴着冲线（切过去，
    /// 不绕圈），腹部垂直微弓、力点偏向出刀端（不对称厚度=看得见的发力点）；
    /// 材质身份是"湖水凝成的刀光"——暗血压边、血红主体、加色亮缘线，
    /// 显现帧沿刃撕落水珠，不做通用白刀光。伤害窗只开爆发前几帧、
    /// 判定沿冲线整段加宽（贴脸与擦边都得算数），击退方向由弹速给（顺劈向推）。
    /// owner 生成、生成包自含（ai0=判定半长，ai1=重拍），各端从 ai 自配一致
    /// </summary>
    internal class KikasaBladeSlash : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>各端本地计帧：显现/消散与判定窗的时间轴</summary>
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>冲线方向角：首个本地更新从弹速缓存（弹速随后衰减只管击退）</summary>
        private ref float LockedAng => ref Projectile.localAI[1];

        /// <summary>判定与绘制的半长 px（生成包自带）</summary>
        private float HalfLen => Projectile.ai[0];

        /// <summary>重拍（穿心/终结）：更宽的月牙、更多撕珠</summary>
        private bool HeavyBeat => Projectile.ai[1] > 0.5f;

        /// <summary>伤害窗帧数：只在爆发帧算数，之后纯余像</summary>
        private const int DamageWindow = 8;

        private const int LifeTotal = 26;

        /// <summary>月牙弓向：按 identity 定侧，各端一致</summary>
        private float BowSign => Projectile.identity % 2 == 0 ? 1f : -1f;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTotal;
            //一道斩痕对每个敌人只算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if ((int)Life == 1) {
                LockedAng = Projectile.velocity.ToRotation();
                Projectile.rotation = LockedAng;
                SpawnTearDrops();
            }
            //弹速只负责给击退一个顺劈的方向，斩痕本体钉在原地
            Projectile.velocity *= 0.78f;

            float glow = 0.5f * RevealAlpha();
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.12f * glow, 0.11f * glow);
        }

        /// <summary>显现帧沿刃撕落水珠：切开的水没跟上刀，各端自演（纯表现）</summary>
        private void SpawnTearDrops() {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int drops = HeavyBeat ? 10 : 7;
            for (int k = 0; k < drops; k++) {
                float u = Main.rand.NextFloat(-0.9f, 0.95f);
                Vector2 pos = Projectile.Center + dir * (u * HalfLen);
                Vector2 vel = perp * Main.rand.NextFloat(-2.2f, 2.2f) + dir * Main.rand.NextFloat(0.5f, 2.5f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos, vel,
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
            }
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + dir * Main.rand.NextFloat(-HalfLen, HalfLen) * 0.8f,
                    perp * Main.rand.NextFloat(-1.4f, 1.4f) + new Vector2(0f, Main.rand.NextFloat(0.4f, 1.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24), 0f);
            }
        }

        //==================== 判定：慷慨的整线捕获 ====================

        /// <summary>伤害窗只开爆发帧；之后斩痕只是余像</summary>
        public override bool? CanDamage() => Life <= DamageWindow ? null : false;

        /// <summary>沿冲线整段的宽线判定：贴脸与擦边都算数（判定比画面略宽）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = LockedAng.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * HalfLen;
            Vector2 end = Projectile.Center + dir * HalfLen;
            float width = MathHelper.Clamp(HalfLen * 0.5f, 18f, 40f) * (HeavyBeat ? 1.2f : 1f);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, width, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中的血水回赠：贴着劈向溅出（仅命中裁决端可见，队友有 NPC 受击反馈兜底）
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            for (int k = 0; k < (HeavyBeat ? 6 : 4); k++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26));
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.3f,
                Pitch = HeavyBeat ? -0.1f : 0.15f,
                MaxInstances = 3
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕：斩痕散成几粒回落的水珠（Kill 各端都跑，队友也看得见）
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + dir * Main.rand.NextFloat(-HalfLen, HalfLen) * 0.6f,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.8f, 2f)),
                    BloodMain * 0.45f, Main.rand.NextFloat(0.28f, 0.45f))?.Configure(Main.rand.Next(12, 20), 0f);
            }
        }

        //==================== 绘制：压扁月牙 ====================

        /// <summary>显现-余像透明度：出生即满（暴力），伤害窗过后温柔消散</summary>
        private float RevealAlpha() {
            if (Life <= 0f) {
                return 0f;
            }
            if (Life <= DamageWindow) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (Life - DamageWindow) / (LifeTotal - DamageWindow), 0f, 1f);
        }

        /// <summary>显现超冲：第 1 帧 1.22 倍、第 2 帧 1.06，随后落回 1——生得暴烈</summary>
        private float RevealPop() => (int)Life switch {
            1 => 1.22f,
            2 => 1.06f,
            _ => 1f,
        };

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || Life < 1f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 gOrigin = glow.Size() * 0.5f;
            float alpha = RevealAlpha();
            float pop = RevealPop();
            Vector2 dir = LockedAng.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            //消散期整体沿弓向轻退：水散开时的松弛
            float relax = MathHelper.Clamp((Life - DamageWindow) / (float)(LifeTotal - DamageWindow), 0f, 1f);
            Vector2 drift = perp * BowSign * relax * 6f;

            float maxW = MathHelper.Clamp(HalfLen * 0.34f, 12f, 30f) * (HeavyBeat ? 1.3f : 1f) * pop;
            float bowAmp = HalfLen * 0.15f * BowSign * (HeavyBeat ? 1.25f : 1f);

            const int segs = 11;
            for (int k = 0; k < segs; k++) {
                float u = k / (segs - 1f) * 2f - 1f;
                //力点偏向出刀端：厚度峰值不在正中——不对称即发力
                float shifted = MathHelper.Clamp(u - 0.22f, -1f, 1f);
                float lens = MathF.Pow(MathF.Max(1f - shifted * shifted, 0f), 0.72f);
                if (lens <= 0.04f) {
                    continue;
                }
                //腹部垂直微弓的月牙路径
                Vector2 pos = Projectile.Center + drift
                    + dir * (u * HalfLen)
                    + perp * bowAmp * (1f - u * u);
                float segLen = HalfLen * 2.3f / segs;
                float w = maxW * lens;

                //暗血压边（略宽）
                sb.Draw(glow, pos - Main.screenPosition, null, BloodDark * (0.7f * alpha), LockedAng,
                    gOrigin, new Vector2(segLen * 1.3f / glow.Width, (w + 6f) / glow.Height), SpriteEffects.None, 0f);
                //血红主体
                sb.Draw(glow, pos - Main.screenPosition, null, BloodMain * (0.92f * alpha), LockedAng,
                    gOrigin, new Vector2(segLen * 1.18f / glow.Width, w / glow.Height), SpriteEffects.None, 0f);
                //加色亮缘线：贴着前缘的一道窄光，白是结构不是增益
                Color edge = (u > -0.3f ? MuzzleHot : BloodBright) with { A = 0 };
                sb.Draw(glow, pos + perp * BowSign * (w * 0.28f) - Main.screenPosition, null,
                    edge * (0.55f * alpha * lens), LockedAng,
                    gOrigin, new Vector2(segLen * 1.1f / glow.Width, MathF.Max(w * 0.22f, 2.2f) / glow.Height), SpriteEffects.None, 0f);
            }

            //出刀端一点收束亮心：力的去向
            if (alpha > 0.25f) {
                Vector2 tip = Projectile.Center + drift + dir * (HalfLen * 0.86f);
                sb.Draw(glow, tip - Main.screenPosition, null,
                    (BloodBright with { A = 0 }) * (0.5f * alpha), LockedAng,
                    gOrigin, new Vector2(26f / glow.Width * pop, 8f / glow.Height), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
