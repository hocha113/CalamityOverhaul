using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaArmsPalette;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaZenith
{
    /// <summary>
    /// 天顶械奴的诸剑记忆斩痕：与刀奴湖水斩痕同一事件契约（owner 生成、生成包自含、
    /// 判定窗只开爆发前几帧、每敌一次），但每道斩痕带着留下它的那柄剑的档案色——
    /// 血湖底色上浮出各剑的记忆残光，刃尖缀原版签名的四芒星闪。
    /// ai0=判定半长，ai1=0 幻影剑过心斩 / 1 主刀终结巨斩（更宽、更久、双星芒），
    /// ai2=剑谱索引（负数=天顶本体色）。方向由初速定，帧内缓存后弹速只管击退
    /// </summary>
    internal class KikasaZenithSlash : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>各端本地计帧：显现/消散与判定窗的时间轴</summary>
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>冲线方向角：首个本地更新从弹速缓存（弹速随后衰减只管击退）</summary>
        private ref float LockedAng => ref Projectile.localAI[1];

        /// <summary>判定与绘制的半长 px（生成包自带）</summary>
        private float HalfLen => Projectile.ai[0];

        /// <summary>终结巨斩：主刀亲自留下的痕，更宽的月牙、更长的余像</summary>
        private bool Finisher => Projectile.ai[1] > 0.5f;

        /// <summary>剑谱索引：定这道斩痕的记忆色（生成包自带）</summary>
        private int ProfileIdx => (int)Projectile.ai[2];

        /// <summary>伤害窗帧数：只在爆发帧算数，之后纯余像</summary>
        private int DamageWindow => Finisher ? 10 : 8;

        private int LifeTotal => Finisher ? 34 : 26;

        /// <summary>月牙弓向：按 identity 定侧，各端一致</summary>
        private float BowSign => Projectile.identity % 2 == 0 ? 1f : -1f;

        //==================== 记忆色（从剑谱取，向血湖底色微拉保持系列身份）====================

        private Color SwordColor => KikasaZenithArsenal.ColorOf(ProfileIdx);

        private Color BodyMain => Color.Lerp(SwordColor, BloodMain, 0.26f);

        private Color BodyDark => Color.Lerp(SwordColor, BloodDark, 0.68f);

        private Color EdgeBright => Color.Lerp(SwordColor, Color.White, 0.52f);

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

        public override void AI() {
            Life++;
            if ((int)Life == 1) {
                LockedAng = Projectile.velocity.ToRotation();
                Projectile.rotation = LockedAng;
                Projectile.timeLeft = LifeTotal + 2;
                SpawnBirthSparks();
            }
            //弹速只负责给击退一个顺劈的方向，斩痕本体钉在原地
            Projectile.velocity *= 0.78f;

            float glow = 0.5f * RevealAlpha();
            Vector3 tint = SwordColor.ToVector3();
            Lighting.AddLight(Projectile.Center, tint * 0.5f * glow);
        }

        /// <summary>显现帧沿刃撒记忆星屑与水珠：剑色的火花是记忆，水珠是湖，各端自演（纯表现）</summary>
        private void SpawnBirthSparks() {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int sparks = Finisher ? 14 : 8;
            for (int k = 0; k < sparks; k++) {
                float u = Main.rand.NextFloat(-0.85f, 0.95f);
                Vector2 pos = Projectile.Center + dir * (u * HalfLen);
                Vector2 vel = perp * Main.rand.NextFloat(-2.4f, 2.4f) + dir * Main.rand.NextFloat(0.8f, 3f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel,
                    Main.rand.NextBool(3) ? EdgeBright : SwordColor,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 22), affectedByGravity: true);
            }
            for (int k = 0; k < (Finisher ? 6 : 3); k++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + dir * Main.rand.NextFloat(-HalfLen, HalfLen) * 0.8f,
                    perp * Main.rand.NextFloat(-1.3f, 1.3f) + new Vector2(0f, Main.rand.NextFloat(0.4f, 1.4f)),
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
            float width = MathHelper.Clamp(HalfLen * 0.5f, 18f, 46f) * (Finisher ? 1.25f : 1f);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, width, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = Finisher ? 0.42f : 0.3f,
                Pitch = Finisher ? -0.1f : 0.15f,
                MaxInstances = 3
            }, target.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = LockedAng.ToRotationVector2();
            for (int k = 0; k < (Finisher ? 7 : 4); k++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.45f, 0.45f)) * Main.rand.NextFloat(2.5f, 6f),
                    SwordColor, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24), affectedByGravity: true);
            }
        }

        public override void OnKill(int timeLeft) {
            //谢幕：记忆散成几粒回落的水珠（Kill 各端都跑，队友也看得见）
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

        //==================== 绘制：剑色压扁月牙 + 刃尖星芒 ====================

        /// <summary>显现-余像透明度：出生即满（暴力），伤害窗过后温柔消散</summary>
        private float RevealAlpha() {
            if (Life <= 0f) {
                return 0f;
            }
            if (Life <= DamageWindow) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (Life - DamageWindow) / (float)(LifeTotal - DamageWindow), 0f, 1f);
        }

        /// <summary>显现超冲：第 1 帧 1.22 倍、第 2 帧 1.06，随后落回 1：生得暴烈</summary>
        private float RevealPop() => (int)Life switch {
            1 => 1.22f,
            2 => 1.06f,
            _ => 1f,
        };

        public override bool PreDraw(ref Color lightColor) {
            //暗压边与主体走真 alpha 圆片，亮缘线与星芒走 A=0 加色（黑背景贴图铁律）
            Texture2D body = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (body == null || glow == null || Life < 1f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 bOrigin = body.Size() * 0.5f;
            Vector2 gOrigin = glow.Size() * 0.5f;
            float alpha = RevealAlpha();
            float pop = RevealPop();
            Vector2 dir = LockedAng.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);

            //消散期整体沿弓向轻退：记忆散开时的松弛
            float relax = MathHelper.Clamp((Life - DamageWindow) / (float)(LifeTotal - DamageWindow), 0f, 1f);
            Vector2 drift = perp * BowSign * relax * 6f;

            float maxW = MathHelper.Clamp(HalfLen * 0.34f, 12f, 34f) * (Finisher ? 1.35f : 1f) * pop;
            float bowAmp = HalfLen * 0.15f * BowSign * (Finisher ? 1.3f : 1f);

            const int segs = 11;
            for (int k = 0; k < segs; k++) {
                float u = k / (segs - 1f) * 2f - 1f;
                //力点偏向出刀端：厚度峰值不在正中，不对称即发力
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

                //暗压边（略宽）：剑色沉进血里
                sb.Draw(body, pos - Main.screenPosition, null, BodyDark * (0.66f * alpha), LockedAng,
                    bOrigin, new Vector2(segLen * 1.3f / body.Width, (w + 6f) / body.Height), SpriteEffects.None, 0f);
                //记忆色主体
                sb.Draw(body, pos - Main.screenPosition, null, BodyMain * (0.9f * alpha), LockedAng,
                    bOrigin, new Vector2(segLen * 1.18f / body.Width, w / body.Height), SpriteEffects.None, 0f);
                //加色亮缘线：贴着前缘的一道窄光，白是结构不是增益
                Color edge = (u > -0.3f ? EdgeBright : SwordColor) with { A = 0 };
                sb.Draw(glow, pos + perp * BowSign * (w * 0.28f) - Main.screenPosition, null,
                    edge * (0.55f * alpha * lens), LockedAng,
                    gOrigin, new Vector2(segLen * 1.1f / glow.Width, MathF.Max(w * 0.22f, 2.2f) / glow.Height), SpriteEffects.None, 0f);
            }

            //出刀端收束亮心：力的去向
            if (alpha > 0.25f) {
                Vector2 tip = Projectile.Center + drift + dir * (HalfLen * 0.86f);
                sb.Draw(glow, tip - Main.screenPosition, null,
                    (EdgeBright with { A = 0 }) * (0.5f * alpha), LockedAng,
                    gOrigin, new Vector2(28f / glow.Width * pop, 8f / glow.Height), SpriteEffects.None, 0f);

                //刃尖四芒星：原版天顶的签名闪（白芯 + 剑色缘），终结斩双星更盛
                Texture2D star = CWRAsset.StarTexture?.Value;
                if (star != null) {
                    float starT = MathHelper.Clamp(Life / 7f, 0f, 1f);
                    float starA = alpha * (1f - relax * 0.6f);
                    float starScale = (Finisher ? 0.24f : 0.15f) * (0.6f + 0.4f * starT) * pop;
                    Vector2 sOrigin = star.Size() * 0.5f;
                    sb.Draw(star, tip - Main.screenPosition, null,
                        (SwordColor with { A = 0 }) * (0.55f * starA), LockedAng * 0.5f,
                        sOrigin, starScale, SpriteEffects.None, 0f);
                    sb.Draw(star, tip - Main.screenPosition, null,
                        (Color.White with { A = 0 }) * (0.4f * starA), LockedAng * 0.5f,
                        sOrigin, starScale * 0.55f, SpriteEffects.None, 0f);
                    if (Finisher) {
                        sb.Draw(star, tip - Main.screenPosition, null,
                            (KikasaZenithArsenal.ZenithColor with { A = 0 }) * (0.45f * starA),
                            LockedAng * 0.5f + MathHelper.PiOver4,
                            sOrigin, starScale * 0.8f, SpriteEffects.None, 0f);
                    }
                }
            }
            return false;
        }
    }
}
