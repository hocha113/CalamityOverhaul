using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaQueenSlime
{
    /// <summary>
    /// 鬼奴史莱姆皇后的血晶晶片：晶格雷碎裂与加冕俯冲晶爆的伤害载体。
    /// 一枚细长晶棱直线疾飞（晶体有惯性，迟滞后才吃重力），
    /// 飞行中晶面偶发反光、身后拖短晶影；命中/贴壁碎成晶屑 + 玻璃脆响；
    /// 落回血湖被湖收走（晶体化回血水，不迸溅）
    /// </summary>
    internal class KikasaQueenCrystalShard : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出膛后多少帧开始吃重力：晶片是射出去的，弹道大体平直</summary>
        private const int GravityDelay = 16;

        private ref float Life => ref Projectile.ai[0];

        //贴壁碎裂已放过演出，OnKill 不再补
        private bool burstDone;
        //被湖收走：谢幕换成涟漪
        private bool lakeSwallowed;

        /// <summary>连续量抖动的确定性相位，各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 4.13f;

        /// <summary>出生 3 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停
            //（俯冲晶爆的下半圈就出生在湖面下）；贴壁碎裂改走 AI 内手动检测
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //晶体惯性：先平直疾飞，迟滞后才被重量缓缓压弯
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.13f, 12f);
            }
            Projectile.velocity *= 0.996f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行余韵：偶发一粒随行微晶光（纯本地表现）
            if (!Main.dedServ && Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.4f, 1.1f),
                    -Projectile.velocity * 0.05f,
                    KikasaQueenSlimeServant.CrystalGlint * 0.55f, Main.rand.NextFloat(0.16f, 0.3f))
                    ?.Configure(KikasaQueenSlimeServant.CrystalGlint * 0.4f, 10, 0f, 0.6f);
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.42f * glow, 0.16f * glow, 0.22f * glow);

            //落回血湖：晶体化回血水被湖收走，不迸溅。
            //只收"坠落中"的晶片并给出生宽限——俯冲晶爆可能在湖面下起爆，
            //新星的下半圈得先飞完自己的攻击行程，不能出生即被湖吞
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (lakeAlive && Life > 12f && Projectile.velocity.Y > 0f
                && Projectile.Center.Y >= kdp.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == kdp) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, kdp.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
                return;
            }

            //贴壁碎裂（机制身份保留）：手动地形检测替代 tileCollide——
            //湖线以下的真地形被湖面盖住，撞上去像凭空截停，不算贴壁
            if (Life > 3
                && (!lakeAlive || Projectile.Center.Y < kdp.LakeWorldY - 2f)
                && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                burstDone = true;
                ShatterBurst(Projectile.Center, Projectile.velocity);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑，队友也看得见）
                ShatterBurst(Projectile.Center, Projectile.velocity);
            }
            //晶影失稳：拖尾旧位上散两粒回落的碎屑
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos == null) {
                return;
            }
            for (int i = 2; i < oldPos.Length; i += 4) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                    oldPos[i] + Projectile.Size * 0.5f + Main.rand.NextVector2Circular(3f, 3f),
                    Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextBool(3) ? KikasaQueenSlimeServant.CrystalDeep : KikasaQueenSlimeServant.GelBlood,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        /// <summary>晶片碎裂：半球晶屑扇 + 微型扩散环 + 玻璃/水晶层叠脆响</summary>
        internal static void ShatterBurst(Vector2 pos, Vector2 impactVel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float mainAngle = normal.ToRotation();

            for (int i = 0; i < 6; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                Vector2 vel = (mainAngle + spread).ToRotationVector2() * Main.rand.NextFloat(1.8f, 5.4f);
                PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(pos + Main.rand.NextVector2Circular(4f, 4f),
                    vel, Main.rand.NextBool(3) ? KikasaQueenSlimeServant.CrystalDeep : KikasaQueenSlimeServant.GelBlood,
                    Main.rand.NextFloat(0.38f, 0.66f))
                    ?.Configure(Main.rand.Next(18, 30), 0.22f, Main.rand.NextFloat(-0.14f, 0.14f));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, KikasaQueenSlimeServant.CrystalGlint, 0.05f)
                ?.Configure(new Vector2(0.75f, 1f), mainAngle, 0.17f, 7);
            PRTLoader.NewParticle<PRT_Sparkle>(pos, Vector2.Zero,
                KikasaQueenSlimeServant.CrystalGlint, 0.5f)
                ?.Configure(KikasaQueenSlimeServant.CrystalGlint * 0.6f, 10, 0.1f, 0.8f);

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = Main.rand.NextFloat(-0.15f, 0.2f), MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.16f, Pitch = 0.35f, MaxInstances = 2 }, pos);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.1f, 0.7f);
            Vector2 sliver = new(0.15f, 0.42f + stretch * 0.5f);

            //晶影拖尾：三段旧位残棱，越远越淡
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos != null) {
                for (int k = 6; k >= 2; k -= 2) {
                    if (oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    float fall = 1f - k / 8f;
                    Vector2 gp = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                    sb.Draw(tex, gp, null, KikasaQueenSlimeServant.CrystalDeep * (0.3f * fall * fade),
                        Projectile.oldRot[k], origin, sliver * (0.9f - k * 0.04f), SpriteEffects.None, 0f);
                }
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //暗缘→主体→亮芯：晶棱三层
            sb.Draw(tex, pos, null, KikasaQueenSlimeServant.CrystalDeep * (0.9f * fade),
                Projectile.rotation, origin, sliver * new Vector2(1.4f, 1.06f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, KikasaQueenSlimeServant.GelBlood * fade,
                Projectile.rotation, origin, sliver, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, KikasaQueenSlimeServant.CrystalGlint * (0.85f * fade),
                Projectile.rotation, origin, sliver * new Vector2(0.4f, 0.9f), SpriteEffects.None, 0f);

            //晶面反光：飞行自旋相位到反光角时一点锐光（A=0 加色）
            float tw = MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Seed * 5f);
            float flash = MathF.Max(0f, tw);
            flash = flash * flash * flash;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star != null && flash > 0.2f) {
                Color glint = KikasaQueenSlimeServant.CrystalCore * (flash * 0.8f * fade);
                sb.Draw(star, pos, null, glint, Projectile.rotation,
                    star.Size() * 0.5f, 0.2f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
