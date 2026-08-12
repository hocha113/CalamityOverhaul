using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaTwins
{
    /// <summary>
    /// 鬼奴激光眼的脉冲血矢：一根细直快的凝血光钉，不是光条贴纸。
    /// 义眼镜筒式的精准弹道——出膛即全速、复利续力越飞越钻、绝不下坠；
    /// 飞行中沿途蜕下细血火星，头部白热芯 + 血红裹层 + 暗血描边三层拉伸；
    /// 命中沿弹道向前的窄扇迸溅、贴壁留渍，落空坠回血湖时被湖收走
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
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

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

        public override bool OnTileCollide(Vector2 oldVelocity) {
            burstDone = true;
            ImpactBurst(Projectile.Center, oldVelocity, onTile: true);
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑，队友也看得见）
                ImpactBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
        }

        /// <summary>点射命中：窄扇前向迸溅 + 细环 + 短余闪——精准武器的收口要干脆</summary>
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
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.01f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 gOrigin = glow.Size() * 0.5f;
            float rot = Projectile.velocity.ToRotation();
            float speed = Projectile.velocity.Length();

            //旧位拖出淡光针迹（A=0 加色，主批直接画）
            Vector2[] oldPos = Projectile.oldPos;
            Color trailColor = (KikasaTwinsServant.BloodDeep with { A = 0 }) * (0.3f * fade);
            for (int k = oldPos.Length - 1; k >= 1; k--) {
                if (oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float fall = 1f - k / (float)oldPos.Length;
                Vector2 pos = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(glow, pos, null, trailColor * fall, rot, gOrigin,
                    new Vector2(20f / glow.Width * (1f + speed * 0.02f), 2.2f / glow.Height * fall), SpriteEffects.None, 0f);
            }

            //头部三层：暗血描边→血红裹层→白热芯，全部速度拉伸
            Vector2 head = Projectile.Center - Main.screenPosition;
            float stretch = 1f + speed * 0.05f;
            sb.Draw(glow, head, null, (KikasaTwinsServant.BloodDark with { A = 0 }) * (0.7f * fade), rot, gOrigin,
                new Vector2(30f * stretch / glow.Width, 7f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, head, null, (KikasaTwinsServant.BloodMain with { A = 0 }) * fade, rot, gOrigin,
                new Vector2(26f * stretch / glow.Width, 4.6f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, head, null, (new Color(255, 224, 214) with { A = 0 }) * (0.85f * fade), rot, gOrigin,
                new Vector2(21f * stretch / glow.Width, 1.9f / glow.Height), SpriteEffects.None, 0f);

            return false;
        }
    }
}
