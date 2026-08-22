using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaQueenBee
{
    /// <summary>
    /// 鬼奴蜂后的血蜂：水珠凝成的小蜂，短命的独立追踪单位。
    /// 出腹先散开一拍再咬向猎物，飞行带蜂式摆尾与翅点微光，
    /// 沿途偶发坠珠；命中、超时或坠回血湖都化回血珠，绝不常驻。
    /// 追踪规则确定性（最近可追目标 + 种子摆动），不掷 Main.rand 进弹道
    /// </summary>
    internal class KikasaQueenBeeBloodBee : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出腹后自由散开的帧数，散完才开始咬人</summary>
        private const int ScatterFrames = 10;

        /// <summary>巡航速度</summary>
        private const float FlySpeed = 12.5f;

        private ref float Life => ref Projectile.ai[0];

        //被湖收走：谢幕换成涟漪，不走血珠爆
        private bool lakeSwallowed;

        //==================== 血色板（随观看域鬼雨异化冷化）====================

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color WingShine => KikasaDomain.CoolTint(new(246, 166, 120), new(176, 200, 204));

        /// <summary>连续量抖动的确定性相位（9.1：弹道不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 4.13f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //独立命中冷却：蜂群不写全局无敌帧，免得互相吞伤害、饿死耙扫接触窗
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            if (Life <= ScatterFrames) {
                //散开拍：出腹的惯性自然放缓，一窝蜂先炸开再收拢
                Projectile.velocity *= 0.97f;
            }
            else {
                int target = FindTarget();
                if (target >= 0) {
                    //咬向猎物：转率封顶的追踪 + 种子摆尾，活的飞行不是直线平移
                    Vector2 want = (Main.npc[target].Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    want = want.RotatedBy(MathF.Sin(Life * 0.5f + Seed * 3f) * 0.34f);
                    float wantRot = want.ToRotation();
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    float newRot = dir.ToRotation().AngleTowards(wantRot, 0.085f);
                    float speed = MathF.Min(Projectile.velocity.Length() + 0.35f, FlySpeed);
                    Projectile.velocity = newRot.ToRotationVector2() * speed;
                }
                else {
                    //没猎物：慢下来嗡嗡打转，等超时化珠
                    Projectile.velocity = Projectile.velocity.RotatedBy(0.03f) * 0.985f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途坠珠：飞得越急甩得越勤
            if (!Main.dedServ && Life % 6 == 2 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    BloodMain * 0.45f, Main.rand.NextFloat(0.2f, 0.35f))
                    ?.Configure(Main.rand.Next(10, 18), 0.28f);
            }

            float glow = 0.3f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.12f * glow, 0.1f * glow);

            //坠回血湖：湖收回自己的血
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.4f);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        /// <summary>就近咬人：以蜂自身为圆心找最近可追目标，规则各端一致</summary>
        private int FindTarget() {
            int best = -1;
            float bestDist = 900f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //命中或超时化血珠（penetrate=1，Kill 各端都跑，队友也看得见）
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.25f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.6f, 1.6f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.045f, 0.1f, 0.7f);

            //短线尾迹：几粒旧位残点收着变小
            for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                if (oldCenter == Projectile.Size * 0.5f) {
                    continue;
                }
                float fall = 1f - k / (float)Projectile.oldPos.Length;
                sb.Draw(tex, oldCenter - Main.screenPosition, null,
                    BloodDeep * (0.3f * fall * fade), Projectile.rotation, origin,
                    new Vector2(0.1f, 0.16f) * fall, SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //蜂体：暗血压边→血红主体，沿速度微拉伸
            sb.Draw(tex, pos, null, BloodDeep * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.3f + stretch * 0.24f, 0.24f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.24f + stretch * 0.2f, 0.18f), SpriteEffects.None, 0f);
            //头点：行进端一粒略亮的凝珠
            Vector2 headOff = Projectile.rotation.ToRotationVector2() * 6f;
            sb.Draw(tex, pos + headOff, null, BloodMain * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.12f, 0.12f), SpriteEffects.None, 0f);

            //翅点微光：高频明灭的两粒小亮斑，蜂之所以读作蜂
            if (glow != null) {
                float flick = 0.5f + 0.5f * MathF.Sin(Life * 1.8f + Seed * 5f);
                Vector2 gOrigin = glow.Size() * 0.5f;
                Vector2 up = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
                Color wing = (WingShine with { A = 0 }) * (0.5f * flick * fade);
                sb.Draw(glow, pos + up * 6f - headOff * 0.4f, null, wing, 0f, gOrigin,
                    new Vector2(12f / glow.Width * 2f, 7f / glow.Height * 2f), SpriteEffects.None, 0f);
                sb.Draw(glow, pos + up * 5f - headOff * 0.9f, null, wing * 0.7f, 0f, gOrigin,
                    new Vector2(9f / glow.Width * 2f, 6f / glow.Height * 2f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
