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
    /// 附体绞锯：致命球拆下一环锯齿铆进猎物，原地空转研磨。跟随目标，
    /// 三相 = 咬合 8 帧（锯环张开咬入，无伤害）/ 研磨 44 帧（每 15 帧一段锯伤，
    /// 锯口喷钢花，磨得越久锯缘越烫）/ 脱转 8 帧（锯环甩脱飞散，无伤害）。
    /// ai[0] = 目标索引，ai[1] = 目标类型校验。材质：磨光钢锯 + 摩擦炽缘
    /// </summary>
    internal class GsDeadlySphereSawProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private static readonly Color SteelGray = new(168, 172, 186);
        private static readonly Color SteelDark = new(74, 78, 92);
        private static readonly Color FrictionOrange = new(255, 148, 54);

        private const int BiteFrames = 8;
        private const int GrindFrames = 44;
        private const int LooseFrames = 8;
        private const int TotalFrames = BiteFrames + GrindFrames + LooseFrames;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Grinding => Elapsed >= BiteFrames && Elapsed < BiteFrames + GrindFrames;

        private bool Loosing => Elapsed >= BiteFrames + GrindFrames;

        private float Seed => Projectile.identity * 0.7213f % MathHelper.TwoPi;

        /// <summary>研磨热度 0~1（决定锯缘炽烫程度）</summary>
        private float HeatT => MathHelper.Clamp((Elapsed - BiteFrames) / (float)GrindFrames, 0f, 1f);

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
            //研磨 44 帧内约 3 段锯伤
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            NPC target = BoundTarget;
            if (target == null) {
                //目标失效：各端本地同判甩脱
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center,
                FrictionOrange.ToVector3() * (0.12f + 0.3f * HeatT));
            //咬合首帧：锯环铆入
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.6f, Pitch = -0.15f },
                    Projectile.Center);
            }
            //研磨相：锯口持续喷钢花（频率随热度升高）
            if (Grinding && Main.rand.NextBool(HeatT > 0.5f ? 2 : 4)) {
                float ang = Seed + Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + ang.ToRotationVector2() * 20f,
                    ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * 0.8f)
                        * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? FrictionOrange : SteelGray,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(10, 18));
            }
            //脱转首帧：锯环甩脱四溅
            if (Elapsed == BiteFrames + GrindFrames) {
                SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.45f, Pitch = 0.2f },
                    Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    float ang = Seed + i / 6f * MathHelper.TwoPi;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(3f, 6f),
                        SteelGray, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(12, 18));
                }
            }
        }

        /// <summary>只有研磨相结算伤害</summary>
        public override bool? CanDamage() => Grinding ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.3f, Pitch = 0.3f },
                Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //咬合张缩：环径从 1.4 咬进到 1.0；脱转期反向甩大并淡出
            float biteT = MathHelper.Clamp(Elapsed / (float)BiteFrames, 0f, 1f);
            float ringScale = MathHelper.Lerp(1.4f, 1f, biteT * biteT);
            float fade = 1f;
            if (Loosing) {
                float t = (Elapsed - BiteFrames - GrindFrames) / (float)LooseFrames;
                ringScale = 1f + t * 0.8f;
                fade = 1f - t;
            }
            //空转角速度随热度提升
            float spin = Seed + Elapsed * (0.22f + 0.3f * HeatT);
            float radius = 21f * ringScale;

            //八根锯齿辐条绕环布置（钢灰垫底 + 热度炽缘）
            for (int i = 0; i < 8; i++) {
                float ang = spin + i / 8f * MathHelper.TwoPi;
                Vector2 toothPos = pos + ang.ToRotationVector2() * radius;
                Main.EntitySpriteDraw(soft, toothPos, null, SteelDark * (0.9f * fade),
                    ang + MathHelper.PiOver2 + 0.5f, soft.Size() / 2f,
                    new Vector2(11f / soft.Width, 4f / soft.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(soft, toothPos, null, SteelGray * (0.7f * fade),
                    ang + MathHelper.PiOver2 + 0.5f, soft.Size() / 2f,
                    new Vector2(8f / soft.Width, 2.4f / soft.Height), SpriteEffects.None, 0);
            }
            //锯环体：环带 = 两圈错相细环（用横条绕圈近似，钢色）
            for (int i = 0; i < 12; i++) {
                float ang = -spin * 0.7f + i / 12f * MathHelper.TwoPi;
                Main.EntitySpriteDraw(soft, pos + ang.ToRotationVector2() * (radius - 5f), null,
                    SteelGray * (0.55f * fade), ang + MathHelper.PiOver2, soft.Size() / 2f,
                    new Vector2(9f / soft.Width, 2f / soft.Height), SpriteEffects.None, 0);
            }
            //摩擦炽缘（加色，热度驱动）
            if (HeatT > 0.05f && !Loosing) {
                Main.EntitySpriteDraw(glow, pos, null,
                    (FrictionOrange with { A = 0 }) * (0.5f * HeatT * fade), 0f,
                    glow.Size() / 2f, 0.75f * ringScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
