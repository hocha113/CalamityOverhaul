using CalamityOverhaul.Common;
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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaMoonLord
{
    /// <summary>
    /// 幻月球：心跳拍上自心口挤出的缓行血月——半透明球体内有旋涡缓转，
    /// 幻月苍青只描一线月缘。漂向目标，到位或超时爆成十字血芒弹。
    /// ai[0]=目标 whoAmI（各端同源，寻的确定性）ai[1]=挤出序号（错开相位）；
    /// 爆点裁决在 owner 端，十字弹只在 owner 端生成
    /// </summary>
    internal class KikasaMoonOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float SqueezeIndex => ref Projectile.ai[1];
        private ref float Life => ref Projectile.localAI[0];

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 6 帧从心口鼓出来，别一帧弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 6f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            int target = (int)TargetIndex;
            bool targetAlive = target >= 0 && target < Main.maxNPCs
                && Main.npc[target].active && Main.npc[target].CanBeChasedBy(Projectile);

            if (targetAlive && Life > 10) {
                //缓行寻的：小加速度慢慢弯过去，读得出"漂"字
                Vector2 want = (Main.npc[target].Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 4.6f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.035f);
            }
            else {
                Projectile.velocity *= 0.99f;
                Projectile.velocity.Y += 0.012f;
            }
            //侧向微漂：确定性相位，各端一致
            Projectile.velocity += (Main.GlobalTimeWrappedHourly * 1.7f + Seed + SqueezeIndex * 2.1f)
                .ToRotationVector2() * 0.028f;

            Projectile.rotation += 0.012f + Projectile.velocity.Length() * 0.004f;

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.13f * glow, 0.12f * glow);

            //血珠自球底渗落
            if (!Main.dedServ && Life % 9 == 3) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 10f),
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.4f)),
                    BloodDeep * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(16, 26), 0.3f);
            }

            //爆点裁决只在 owner 端：到位即爆，kill 包带走远端
            if (Main.myPlayer == Projectile.owner && targetAlive && Life > 20
                && Vector2.Distance(Main.npc[target].Center, Projectile.Center) < 44f) {
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            //爆裂演出各端都放；十字血芒只在 owner 端生成，方向定死在爆帧速度上
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.35f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);

            Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            if (!Main.dedServ) {
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 30));
                }
                //十字形的爆光先行，弹紧随其后
                for (int k = 0; k < 4; k++) {
                    Vector2 dir = axis.RotatedBy(MathHelper.PiOver2 * k);
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center + dir * 8f, Vector2.Zero,
                        KikasaMoonLordServant.MoonGlint * 0.8f, 0.05f)
                        ?.Configure(new Vector2(0.4f, 1f), dir.ToRotation(), 0.2f, 8);
                }
                PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, BloodDeep, 0.09f)
                    ?.Configure(new Vector2(1f, 1f), 0f, 0.3f, 10);
            }

            if (Main.myPlayer == Projectile.owner) {
                for (int k = 0; k < 4; k++) {
                    Vector2 dir = axis.RotatedBy(MathHelper.PiOver2 * k);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center + dir * 6f, dir * 12.5f,
                        ModContent.ProjectileType<KikasaMoonShard>(), Projectile.damage, 3f,
                        Projectile.owner);
                }
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (tex == null || glowTex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            //残影拖尾：半透明球身的旧位
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                sb.Draw(tex, oldCenter - Main.screenPosition, null, BloodMain * (0.12f * fall * fade),
                    0f, origin, 0.5f * fall + 0.14f, SpriteEffects.None, 0f);
            }

            //表面张力抖动
            float wob = MathF.Sin(Life * 0.4f + Seed * 5f) * 0.06f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //暗血压边 → 半透球身 → 旋涡内层（两瓣错相绕心转）
            sb.Draw(tex, pos, null, BloodDark * (0.7f * fade), 0f, origin, 0.72f * jiggle.X, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * (0.55f * fade), 0f, origin, 0.6f * jiggle.Y, SpriteEffects.None, 0f);
            for (int k = 0; k < 2; k++) {
                float ang = Projectile.rotation * 2.6f + k * MathHelper.Pi + Seed;
                Vector2 off = ang.ToRotationVector2() * 7f;
                sb.Draw(tex, pos + off, null, BloodDeep * (0.5f * fade), ang,
                    origin, new Vector2(0.34f, 0.2f), SpriteEffects.None, 0f);
            }
            //旋涡芯的湿亮 + 幻月缘光（A=0 加色）
            Color glint = KikasaMoonLordServant.MoonGlint with { A = 0 };
            sb.Draw(tex, pos + new Vector2(-4f, -5f), null, glint * (0.4f * fade), 0f,
                origin, new Vector2(0.16f, 0.12f), SpriteEffects.None, 0f);
            sb.Draw(glowTex, pos + new Vector2(0f, -8f), null, glint * (0.3f * fade), 0f,
                glowTex.Size() * 0.5f, new Vector2(30f / glowTex.Width * 2f, 8f / glowTex.Height), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>十字血芒弹：幻月球爆出的四向速矢——血光短矢带苍青芯，命中即溃成血珠</summary>
    internal class KikasaMoonShard : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.localAI[0];

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 46;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //复利续力：射出后越飞越急
            Projectile.velocity *= 1.03f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.3f, 0.08f, 0.07f);

            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * 0.3f,
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(Main.rand.Next(8, 14), 0f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    Projectile.velocity * 0.14f + Main.rand.NextVector2Circular(1.6f, 1.6f),
                    BloodMain, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.045f, 0.3f, 1.1f);
            float fade = MathHelper.Clamp(Life / 4f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);

            //暗缘→血身→苍青亮芯的速矢
            sb.Draw(tex, pos, null, BloodDark * (0.8f * fade), Projectile.rotation, origin,
                new Vector2(0.3f, 0.5f + stretch * 0.9f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.22f, 0.42f + stretch * 0.8f), SpriteEffects.None, 0f);
            Color glint = KikasaMoonLordServant.MoonGlint with { A = 0 };
            sb.Draw(tex, pos, null, glint * (0.65f * fade), Projectile.rotation, origin,
                new Vector2(0.09f, 0.3f + stretch * 0.5f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
