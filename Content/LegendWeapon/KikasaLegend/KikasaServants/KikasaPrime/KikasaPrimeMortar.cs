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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaPrime
{
    /// <summary>
    /// 机械骷髅王鬼奴的迫击血雷：一颗凝着血水的定时雷，明显的迫击弧线抛出，
    /// 飞行中翻滚滴血、引信红光越掉越急，下坠段在湖面投出落点涟漪预告；
    /// 到时/贴壁/命中即爆——血浪新星 + 半球血珠扇，落进湖里则被湖吞成一柱血涌。
    /// 只在 owner 端生成，spawn 参数自带全部初值，各端按同一套规则推进
    /// </summary>
    internal class KikasaPrimeMortar : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>引信总长（timeLeft 语义）</summary>
        private const int FuseFrames = 76;
        /// <summary>爆窗：变宽判定的存活帧数</summary>
        private const int ExplodeWindow = 3;
        private const int GravityDelay = 6;
        private const float Gravity = 0.42f;

        private ref float Life => ref Projectile.localAI[0];

        private bool exploded;
        private bool lakeSwallowed;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color FuseGlow => KikasaDomain.CoolTint(new(255, 120, 84), new(176, 200, 204));

        /// <summary>确定性相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，免得第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FuseFrames;
        }

        public override void AI() {
            Life++;

            if (!exploded) {
                //迫击弧线：短暂平直后被重量拽下去
                if (Life > GravityDelay) {
                    Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + Gravity, 17f);
                }
                Projectile.velocity.X *= 0.997f;
                //翻滚：转向由 identity 定，各端一致
                float spin = Seed > 1.85f ? 1f : -1f;
                Projectile.rotation += spin * (0.05f + MathF.Abs(Projectile.velocity.X) * 0.006f);

                //飞行滴血：从雷体后侧撕下小珠
                if (!Main.dedServ && Life % 3 == 1) {
                    Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center - dir * Main.rand.NextFloat(4f, 12f),
                        Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
                }
                //引信急滴答：越近爆点越密
                if (!Main.dedServ && Projectile.timeLeft < 40 && (int)Life % MathF.Max(Projectile.timeLeft / 8, 3) == 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 2 }, Projectile.Center);
                }

                UpdateLakeInteraction();
            }
            else {
                Projectile.velocity *= 0.4f;
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.14f * glow, 0.1f * glow);

            //定时爆：引信烧到底
            if (!exploded && Projectile.timeLeft <= ExplodeWindow) {
                Detonate(inWater: false);
            }
        }

        /// <summary>下坠段的落点涟漪预告 + 落水被湖吞成血涌</summary>
        private void UpdateLakeInteraction() {
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f) {
                return;
            }
            float lakeY = domain.LakeWorldY;

            //落水：湖把雷吞下去，爆成一柱血涌
            if (Projectile.Center.Y >= lakeY + 8f) {
                lakeSwallowed = true;
                Detonate(inWater: true);
                return;
            }

            //落点预告：下坠段每隔几帧在弹道落水点敲一圈涟漪，越近越大
            if (Projectile.velocity.Y > 0.5f && KikasaDomain.Viewed == domain && (int)Life % 7 == 2) {
                float vy = Projectile.velocity.Y;
                float dy = lakeY - Projectile.Center.Y;
                if (dy > 0f) {
                    //忽略阻力的抛物线解，预告点不必分毫不差
                    float t = (-vy + MathF.Sqrt(vy * vy + 2f * Gravity * dy)) / Gravity;
                    float landX = Projectile.Center.X + Projectile.velocity.X * t;
                    float closeness = 1f - MathHelper.Clamp(t / 60f, 0f, 1f);
                    KikasaDomainDeco.RippleAt(new Vector2(landX, lakeY), 0.35f + closeness * 0.5f);
                }
            }
        }

        //==================== 起爆 ====================

        /// <summary>进入爆窗：变宽判定收 AoE，演出各端自放（幂等）</summary>
        private void Detonate(bool inWater) {
            if (exploded) {
                return;
            }
            exploded = true;
            Projectile.velocity *= 0.2f;
            Projectile.Resize(170, 170);
            if (Projectile.timeLeft > ExplodeWindow) {
                Projectile.timeLeft = ExplodeWindow;
            }
            ExplosionFX(inWater);
        }

        private void ExplosionFX(bool inWater) {
            Vector2 pos = Projectile.Center;
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = inWater ? 0.55f : 0.8f,
                Pitch = inWater ? -0.75f : -0.35f,
                MaxInstances = 3
            }, pos);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 3 }, pos);
            if (Main.dedServ) {
                return;
            }

            Player owner = Main.player[Projectile.owner];
            bool viewed = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && KikasaDomain.Viewed == domain;
            if (viewed) {
                Main.LocalPlayer?.CWR()?.GetScreenShake(inWater ? 2.2f : 3.2f);
            }

            if (inWater && viewed) {
                //湖吞爆：血涌水柱 + 大涟漪
                KikasaDomainPlayer kdp = owner.GetModPlayer<KikasaDomainPlayer>();
                Vector2 hit = new(pos.X, kdp.LakeWorldY);
                KikasaDomainDeco.RippleAt(hit, 2.2f);
                KikasaDomainDeco.SplashAt(hit, 14);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        hit + new Vector2(Main.rand.NextFloat(-10f, 10f), -4f),
                        new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(7f, 12.5f)),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(30, 48));
                }
                PRTLoader.NewParticle<PRT_GhostRainMist>(hit + new Vector2(0f, -10f),
                    new Vector2(0f, -0.8f), KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66)) * 0.85f,
                    Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(50, 80));
                return;
            }

            //空爆/贴壁爆：血浪新星 + 半球血珠扇 + 血雾余韵
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, BloodDeep, 0.12f)
                ?.Configure(new Vector2(1f, 1f), 0f, 0.5f, 12);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, BloodMain, 0.07f)
                ?.Configure(new Vector2(1f, 1f), 0.6f, 0.34f, 9);
            for (int i = 0; i < 16; i++) {
                float ang = MathHelper.TwoPi * i / 16f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    pos + ang.ToRotationVector2() * 6f,
                    ang.ToRotationVector2() * Main.rand.NextFloat(3f, 8.5f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.45f, 0.85f))?.Configure(Main.rand.Next(20, 36));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f),
                    Color.Lerp(new Color(255, 168, 92), Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.7f, 1.2f))?.Configure(true, Main.rand.Next(10, 18));
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(pos, new Vector2(0f, -0.4f),
                KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66)) * 0.8f,
                Main.rand.NextFloat(0.7f, 1f))?.Configure(Main.rand.Next(46, 72));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //贴壁即爆，留一块会滴淌的血渍（爆窗内的重复触地不再补渍）
            bool firstBurst = !exploded;
            Detonate(inWater: false);
            if (firstBurst && !Main.dedServ) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(
                    Projectile.Center - oldVelocity.SafeNormalize(Vector2.UnitY) * 4f,
                    Vector2.Zero, BloodMain, Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(80, 120));
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //直击也走起爆（owner 端；远端靠 kill 包在 OnKill 兜底补演出）
            Detonate(inWater: false);
        }

        public override void OnKill(int timeLeft) {
            //远端可能没经过本地起爆路径（如 owner 直击 NPC），谢幕兜底补一次爆
            if (!exploded && !lakeSwallowed) {
                ExplosionFX(inWater: false);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.01f || exploded) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;

            //旧位残影：雷体沉重，尾迹短而稠
            Vector2[] oldPos = Projectile.oldPos;
            for (int i = oldPos.Length - 1; i >= 1; i--) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)oldPos.Length;
                sb.Draw(tex, oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition, null,
                    BloodDeep * (0.2f * k * fade), Projectile.rotation, origin,
                    new Vector2(0.3f, 0.34f) * k, SpriteEffects.None, 0f);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //表面张力抖动：雷是凝出来的血铁，不是刚体贴图
            float wob = MathF.Sin(Life * 0.5f + Seed * 6f) * 0.08f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //暗血压边→血红主体→血沫亮芯
            sb.Draw(tex, pos, null, BloodDark * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.5f, 0.54f) * jiggle, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.4f, 0.44f) * jiggle, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, (BloodDeep with { A = 200 }) * fade, Projectile.rotation, origin,
                new Vector2(0.22f, 0.26f) * jiggle, SpriteEffects.None, 0f);

            //引信红光：越近爆点闪得越急（A=0 走预乘加色）
            int cycle = (int)MathF.Max(Projectile.timeLeft * 0.16f, 2f);
            if ((int)Life % (cycle * 2) < cycle) {
                Color fuse = FuseGlow with { A = 0 };
                sb.Draw(tex, pos + new Vector2(0f, -6f).RotatedBy(Projectile.rotation), null,
                    fuse * (0.8f * fade), 0f, origin, 0.13f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
