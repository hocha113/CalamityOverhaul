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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaTwins
{
    /// <summary>
    /// 鬼奴激光眼的脉冲血矢。材质身份：凝血光钉，被镜筒压缩成针的一滴血，烧得发白。
    /// 签名行为：针体沿速度拉丝（越快越长）；针尾拖暗血针迹渐隐；头端星芒热闪。
    /// 弹体 = Extra_98 真 alpha 针形多层（暗血描边/血红裹层/白热芯 + 星闪），非光斑叠层。
    /// 义眼镜筒式的精准弹道，出膛即全速、复利续力越飞越钻、绝不下坠；
    /// 飞行中沿途蜕下细血火星；命中沿弹道向前的窄扇迸溅、贴壁留渍，落空坠回血湖时被湖收走
    /// </summary>
    internal class KikasaTwinsPulseShot : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>各端本地计帧，仅供表现淡入</summary>
        private ref float Life => ref Projectile.localAI[0];

        //贴壁演出已放，OnKill 不再补迸溅
        private bool burstDone;
        //被湖收走：谢幕换成涟漪，不走迸溅
        private bool lakeSwallowed;

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 140;
            Projectile.extraUpdates = 1;
            //不吃引擎地形碰撞：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //贴壁留渍改走 AI 内手动检测（只认水线以上的真地形）
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>是否撞上"看得见"的真地形：湖线以下的墙体被湖面演出盖住，不算贴壁</summary>
        private bool TouchingVisibleTile() {
            if (!Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                return false;
            }
            Player owner = Main.player[Projectile.owner];
            return owner?.active != true
                || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || !domain.AnyActive || domain.RiseT <= 0.5f
                || Projectile.Center.Y < domain.LakeWorldY - 2f;
        }

        public override void AI() {
            Life++;

            //贴壁留渍（机制身份保留）：手动地形检测替代 tileCollide
            if (Life > 3 && TouchingVisibleTile()) {
                burstDone = true;
                ImpactBurst(Projectile.Center, Projectile.velocity, onTile: true);
                Projectile.Kill();
                return;
            }

            //精准弹道：不吃重力、不摇摆，复利续力越飞越快
            Projectile.velocity *= 1.011f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //沿途蜕下细血火星（纯本地表现）
            if (!Main.dedServ && (int)Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.6f,
                    -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    KikasaTwinsServant.PulseHot * 0.7f,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, 10);
            }

            float glow = 0.5f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.16f * glow, 0.14f * glow);

            //落空坠回血湖：湖收回自己的血，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
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
                ImpactBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
        }

        /// <summary>点射命中：窄扇前向迸溅 + 细环 + 短余闪，精准武器的收口要干脆</summary>
        private static void ImpactBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = impactVel.SafeNormalize(Vector2.UnitX);
            float angle = dir.ToRotation();

            //窄扇：血珠贴着入射向前钻
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.24f, 0.24f))
                    * Main.rand.NextFloat(2.5f, 6.5f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(3f, 3f),
                    vel, Main.rand.NextBool(3) ? KikasaTwinsServant.BloodDeep : KikasaTwinsServant.BloodMain,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(3f, 7f),
                    KikasaTwinsServant.PulseHot, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, KikasaTwinsServant.PulseHot, 0.05f)
                ?.Configure(new Vector2(0.5f, 1f), angle, 0.15f, 7);
            if (onTile) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(pos - dir * 2f, Vector2.Zero,
                    KikasaTwinsServant.BloodMain, Main.rand.NextFloat(0.55f, 0.8f))
                    ?.Configure(Main.rand.Next(60, 90));
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.32f, Pitch = 0.25f, MaxInstances = 3 }, pos);
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
            //Extra_98 针体沿 Y 拉长，长轴对齐飞行向
            float rot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            float speed = Projectile.velocity.Length();
            //针长随速度拉丝：越快越长
            float lenScale = (34f + speed * 1.7f) / tex.Height;

            //旧位针迹：暗血细针渐隐（真 alpha 直染，主批直接画）
            Vector2[] oldPos = Projectile.oldPos;
            for (int k = oldPos.Length - 1; k >= 1; k--) {
                if (oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float fall = 1f - k / (float)oldPos.Length;
                Vector2 pos = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(tex, pos, null, KikasaTwinsServant.BloodDeep * (0.3f * fall * fade), rot, origin,
                    new Vector2(0.055f, lenScale * 0.5f * fall), SpriteEffects.None, 0f);
            }

            //针体三层：暗血描边→血红裹层→白热芯（A=0 预乘加色）
            Vector2 head = Projectile.Center - Main.screenPosition;
            sb.Draw(tex, head, null, KikasaTwinsServant.BloodDark * (0.85f * fade), rot, origin,
                new Vector2(0.11f, lenScale), SpriteEffects.None, 0f);
            sb.Draw(tex, head, null, KikasaTwinsServant.BloodMain * fade, rot, origin,
                new Vector2(0.08f, lenScale * 0.88f), SpriteEffects.None, 0f);
            sb.Draw(tex, head, null, (new Color(255, 224, 214) with { A = 0 }) * (0.9f * fade), rot, origin,
                new Vector2(0.035f, lenScale * 0.7f), SpriteEffects.None, 0f);

            //头端星芒热闪：镜筒点射的那一"哒"钉在针尖上（黑底星图只走 A=0 加色）
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star != null) {
                Vector2 tip = head + Projectile.velocity.SafeNormalize(Vector2.UnitX) * (tex.Height * lenScale * 0.32f);
                float flick = 0.75f + 0.25f * MathF.Sin(Life * 1.3f + Projectile.identity);
                sb.Draw(star, tip, null, (KikasaTwinsServant.PulseHot with { A = 0 }) * (0.6f * flick * fade),
                    rot, star.Size() * 0.5f, 0.17f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
