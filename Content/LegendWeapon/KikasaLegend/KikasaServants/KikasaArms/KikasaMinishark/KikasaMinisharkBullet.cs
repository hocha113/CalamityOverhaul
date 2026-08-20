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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaArms.KikasaMinishark
{
    /// <summary>
    /// 械奴迷你鲨的湖水梭弹。材质身份：被枪膛压紧的一梭湖水——出膛即全速、
    /// 复利续力越飞越钻，飞行中带极小幅的鱼摆尾（转向恒为弧、幅度随速度收紧）。
    /// 签名行为：梭体沿速度拉丝（越快越长）；尾拖暗水梭迹渐隐；沿途蜕下细水珠。
    /// 弹体 = Extra_98 真 alpha 梭形多层（暗水描边/血水裹层/亮芯），非光斑叠层。
    /// 命中沿弹道向前的窄扇迸溅、贴壁留渍；落空坠回血湖时被湖收走，谢幕换涟漪
    /// </summary>
    internal class KikasaMinisharkBullet : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>各端本地计帧，仅供表现淡入与摆尾相位</summary>
        private ref float Life => ref Projectile.localAI[0];

        //贴壁演出已放，OnKill 不再补迸溅
        private bool burstDone;
        //被湖收走：谢幕换成涟漪，不走迸溅
        private bool lakeSwallowed;

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
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

            //复利续力越飞越钻 + 极小幅鱼摆尾（转向恒为弧，速度越快摆得越收）
            Projectile.velocity *= 1.009f;
            float sway = MathF.Sin(Life * 0.52f + Projectile.identity * 1.3f)
                * 0.011f * MathHelper.Clamp(28f / (Projectile.velocity.Length() + 1f), 0.5f, 1f);
            Projectile.velocity = Projectile.velocity.RotatedBy(sway);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //沿途蜕下细水珠（纯本地表现）
            if (!Main.dedServ && (int)Life % 5 == 0) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    KikasaMinisharkServant.BloodMain * 0.55f,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(8, 14), 0.1f);
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.4f * glow, 0.12f * glow, 0.11f * glow);

            //落空坠回血湖：湖收回自己的水，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.5f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 2);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
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

        /// <summary>梭弹命中：窄扇前向水珠 + 细环 + 拖尾余韵——高射速武器的收口要碎而不闹</summary>
        private static void ImpactBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = impactVel.SafeNormalize(Vector2.UnitX);
            float angle = dir.ToRotation();

            //窄扇：水珠贴着入射向前钻
            for (int i = 0; i < 4; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.28f, 0.28f))
                    * Main.rand.NextFloat(2f, 5.5f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(3f, 3f),
                    vel, Main.rand.NextBool(3) ? KikasaMinisharkServant.BloodDeep : KikasaMinisharkServant.BloodMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22));
            }
            //余韵：继续飞出去的两粒水珠，活得比弹体久
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(pos,
                    dir.RotatedBy(Main.rand.NextFloat(-0.16f, 0.16f)) * Main.rand.NextFloat(3f, 6f),
                    KikasaMinisharkServant.BloodMain * 0.6f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20), 0.3f);
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, KikasaMinisharkServant.BloodBright, 0.04f)
                ?.Configure(new Vector2(0.5f, 1f), angle, 0.13f, 6);
            if (onTile) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(pos - dir * 2f, Vector2.Zero,
                    KikasaMinisharkServant.BloodMain, Main.rand.NextFloat(0.45f, 0.65f))
                    ?.Configure(Main.rand.Next(50, 80));
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.24f, Pitch = 0.35f, MaxInstances = 3 }, pos);
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
            //Extra_98 梭体沿 Y 拉长，长轴对齐飞行向
            float rot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            float speed = Projectile.velocity.Length();
            //梭长随速度拉丝：越快越长（比激光眼的针短一号，是水不是光钉）
            float lenScale = (26f + speed * 1.4f) / tex.Height;

            //旧位梭迹：暗水细梭渐隐（真 alpha 直染，主批直接画）
            Vector2[] oldPos = Projectile.oldPos;
            for (int k = oldPos.Length - 1; k >= 1; k--) {
                if (oldPos[k] == Vector2.Zero) {
                    continue;
                }
                float fall = 1f - k / (float)oldPos.Length;
                Vector2 pos = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                sb.Draw(tex, pos, null, KikasaMinisharkServant.BloodDeep * (0.28f * fall * fade), rot, origin,
                    new Vector2(0.06f, lenScale * 0.5f * fall), SpriteEffects.None, 0f);
            }

            //梭体三层：暗水描边→血水裹层→亮芯（A=0 预乘加色）
            Vector2 head = Projectile.Center - Main.screenPosition;
            sb.Draw(tex, head, null, KikasaMinisharkServant.BloodDark * (0.8f * fade), rot, origin,
                new Vector2(0.13f, lenScale), SpriteEffects.None, 0f);
            sb.Draw(tex, head, null, KikasaMinisharkServant.BloodMain * fade, rot, origin,
                new Vector2(0.095f, lenScale * 0.86f), SpriteEffects.None, 0f);
            sb.Draw(tex, head, null, (KikasaMinisharkServant.BloodBright with { A = 0 }) * (0.85f * fade), rot, origin,
                new Vector2(0.045f, lenScale * 0.66f), SpriteEffects.None, 0f);

            return false;
        }
    }
}
