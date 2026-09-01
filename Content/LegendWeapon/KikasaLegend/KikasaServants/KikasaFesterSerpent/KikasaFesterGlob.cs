using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.NPCs.FestersandSerpents;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaFesterSerpent
{
    /// <summary>
    /// 鬼奴脓蕾沙蟒的灵液痰：一口渗金的粘稠血痰，飞行中不断掉金珠碎滴。
    /// 贴壁 / 砸上血湖面都在落点播一汪小脓池（<see cref="KikasaFesterPool"/>，
    /// 场上封顶由池生成口自查）；直接命中 NPC 只溅不播池。
    /// 与世吞腐蚀血痰同一族手法：手动地形检测替代 tileCollide，
    /// 弹体只在 owner 端生成，脓池只由 owner 端补生
    /// </summary>
    internal class KikasaFesterGlob : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力：痰是抛出去的，不是射出去的</summary>
        private const int GravityDelay = 6;

        private ref float Life => ref Projectile.ai[0];

        //贴壁/落湖已定爆点，OnKill 按标记走对应戏
        private bool tileBurst;
        private bool lakeBurst;

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));

        /// <summary>连续量抖动的确定性相位（绘制路径不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，贴壁改走 AI 内手动检测
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //抛物线：短暂平直后被重量拽下去，粘性阻力缓慢泄劲
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.28f, 16f);
            }
            Projectile.velocity *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //渗金沸腾：团面掉金珠碎滴
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 side = dir.RotatedBy(MathHelper.PiOver2);
                if (Main.rand.NextBool(3)) {
                    Dust gold = Dust.NewDustPerfect(
                        Projectile.Center - dir * Main.rand.NextFloat(2f, 8f),
                        DustID.Ichor, Projectile.velocity * 0.2f + side * Main.rand.NextFloat(-1f, 1f),
                        40, default, Main.rand.NextFloat(0.7f, 1f));
                    gold.noGravity = false;
                }
                else {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        Projectile.Center - dir * Main.rand.NextFloat(2f, 10f)
                            + side * Main.rand.NextFloat(-5f, 5f),
                        Projectile.velocity * Main.rand.NextFloat(0.15f, 0.4f)
                            + side * Main.rand.NextFloat(-1.4f, 1.4f),
                        BloodMain, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 22));
                }
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.3f * glow, 0.08f * glow);

            //砸上血湖面：脓池坐在水面上滚
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive
                && Projectile.velocity.Y > 0f
                && Projectile.Center.Y >= kdp.LakeWorldY - 2f) {
                lakeBurst = true;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    Vector2 hit = new(Projectile.Center.X, kdp.LakeWorldY);
                    KikasaDomainDeco.RippleAt(hit, 1f);
                    KikasaDomainDeco.SplashAt(hit, 5);
                }
                Projectile.Kill();
                return;
            }

            //贴壁播池：手动地形检测，只认水线以上的真地形
            if (Life > 3
                && (!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                tileBurst = true;
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            Vector2 burstAt = Projectile.Center;
            if (lakeBurst && Main.player[Projectile.owner].TryGetModPlayer(out KikasaDomainPlayer domain)) {
                burstAt.Y = domain.LakeWorldY - 8f;
            }
            if (!Main.dedServ) {
                //金浆爆裂：迸金掺血珠
                FssVfx.IchorBurst(burstAt, 0.95f, -Projectile.velocity.SafeNormalize(Vector2.UnitY));
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        burstAt + Main.rand.NextVector2Circular(5f, 5f),
                        -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(1f)
                            * Main.rand.NextFloat(1.5f, 5f),
                        Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 28));
                }
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.25f, MaxInstances = 3 }, burstAt);
            }
            //只有贴壁/落湖播池；命中 NPC 的痰只溅不留场
            if (tileBurst || lakeBurst) {
                SpawnPool(burstAt);
            }
        }

        /// <summary>小脓池只由 owner 端补生：场上同主人的池封顶，超了不再铺</summary>
        private void SpawnPool(Vector2 pos) {
            if (Main.myPlayer != Projectile.owner) {
                return;
            }
            int poolType = ModContent.ProjectileType<KikasaFesterPool>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == poolType && proj.owner == Projectile.owner) {
                    count++;
                }
            }
            if (count >= KikasaFesterSerpentServant.PoolCap) {
                return;
            }
            int damage = Math.Max((int)(Projectile.damage
                * (KikasaFesterSerpentServant.PoolDamage / (float)KikasaFesterSerpentServant.GlobDamage)), 1);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), pos, Vector2.Zero,
                poolType, damage, 0f, Projectile.owner, lakeBurst ? 1f : 0f);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.01f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;

            //短拖影：速度拉伸的旧位残团
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos != null) {
                for (int k = oldPos.Length - 1; k >= 2; k -= 2) {
                    if (oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 gp = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                    float fall = 1f - k / (float)oldPos.Length;
                    sb.Draw(tex, gp, null, BloodDeep * (0.3f * fall * fade), Projectile.rotation, origin,
                        new Vector2(0.22f, 0.3f) * (0.9f - k * 0.05f), SpriteEffects.None, 0f);
                }
            }

            //液团三层：暗血压边→血红主体→灼金沸芯
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.35f - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.034f, 0.15f, 0.85f);
            float wob = MathF.Sin(Life * 0.6f + Seed * 6f) * 0.13f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            sb.Draw(tex, pos, null, BloodDeep * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.46f, 0.5f + stretch * 0.8f) * jiggle, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, BloodMain * fade, Projectile.rotation, origin,
                new Vector2(0.36f, 0.42f + stretch * 0.7f) * jiggle, SpriteEffects.None, 0f);
            //灼金沸芯：金团在团心打转，读作里头有灵液在滚
            float churn = MathF.Sin(Life * 0.9f + Seed * 3f);
            Vector2 churnOff = new(churn * 3f, MathF.Cos(Life * 0.7f + Seed) * 2.5f);
            sb.Draw(tex, pos + churnOff, null,
                (KikasaFesterSerpentServant.GhostIchor with { A = 0 }) * (0.55f * fade),
                Projectile.rotation, origin,
                new Vector2(0.16f, 0.22f + stretch * 0.25f) * jiggle, SpriteEffects.None, 0f);

            return false;
        }
    }
}
