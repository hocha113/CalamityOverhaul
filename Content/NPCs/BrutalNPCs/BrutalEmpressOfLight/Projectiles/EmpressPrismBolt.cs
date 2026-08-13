using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 棱彩弹，弹幕图案的基本单元；
    /// ai[0]=模式 0直线 1定转率螺旋 2悬滞蓄释 3限时缓追踪；
    /// ai[1]=色相 0~1；ai[2]=模式参数（1转率rad 2悬滞帧数 3目标玩家索引）
    /// 所有行为是生成参数与Time的确定函数，各端图案一致
    /// </summary>
    internal class EmpressPrismBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int FadeInTime = 8;
        private const int HomingStart = 15;
        private const int HomingEnd = 80;

        private ref float Timer => ref Projectile.localAI[0];
        private int Mode => (int)Projectile.ai[0];
        private float Hue => Projectile.ai[1];
        private float ModeParam => Projectile.ai[2];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 380;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>悬滞模式的速度包络：滞留期缓慢漂浮，释放后10帧内推满</summary>
        private float SpeedEnvelope() {
            if (Mode != 2) {
                return 1f;
            }
            float delay = Math.Max(ModeParam, 1f);
            if (Timer < delay) {
                return 0.05f;
            }
            return MathHelper.Clamp((Timer - delay) / 10f, 0.05f, 1f);
        }

        public override void AI() {
            Timer++;

            //出生涟漪，客户端一次
            if (Timer == 1f && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero,
                    PrismColor(0.8f), 0.32f)?.Configure(14, Hue);
            }

            //模式行为，全部确定性
            switch (Mode) {
                case 1:
                    //定转率螺旋：velocity 每帧旋转 ModeParam
                    Projectile.velocity = Projectile.velocity.RotatedBy(ModeParam);
                    break;
                case 3:
                    //限时缓追踪，锁定生成时指定的玩家
                    if (Timer > HomingStart && Timer < HomingEnd) {
                        int idx = (int)ModeParam;
                        if (idx >= 0 && idx < Main.maxPlayers && Main.player[idx].active && !Main.player[idx].dead) {
                            float speed = Projectile.velocity.Length();
                            Vector2 desired = (Main.player[idx].Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                            float current = Projectile.velocity.ToRotation();
                            float next = current.AngleTowards(desired.ToRotation(), 0.036f);
                            Projectile.velocity = next.ToRotationVector2() * speed;
                        }
                    }
                    break;
            }

            //悬滞包络：把当帧引擎位移退回未释放的部分
            float envelope = SpeedEnvelope();
            if (envelope < 1f) {
                Projectile.position -= Projectile.velocity * (1f - envelope);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //透明度：入场淡入+末期淡出
            float fadeIn = MathHelper.Clamp(Timer / FadeInTime, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);
            Projectile.Opacity = fadeIn * fadeOut;

            //悬滞蓄能提示：滞留末期亮度呼吸加速
            if (Mode == 2 && Timer < ModeParam) {
                float chargeT = Timer / Math.Max(ModeParam, 1f);
                Projectile.localAI[1] = chargeT;
            }
            else {
                Projectile.localAI[1] = 0f;
            }

            Lighting.AddLight(Projectile.Center, PrismColor(1f).ToVector3() * 0.32f * Projectile.Opacity);

            //低频闪尘
            if (!VaultUtils.isServer && Main.rand.NextBool(24)) {
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, Projectile.velocity * envelope * 0.08f,
                    PrismColor(0.75f), Main.rand.NextFloat(0.45f, 0.7f))?.Configure(12, Hue);
            }
        }

        private Color PrismColor(float lum) => Main.hslToRgb(Hue % 1f, 1f, 0.5f + 0.22f * lum);

        //悬滞期不结算伤害，视觉与判定同窗
        public override bool? CanDamage() => SpeedEnvelope() > 0.5f ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, VaultUtils.RandVr(2f, 7f),
                        PrismColor(1f), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(16, Hue);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 glowOrigin = glow.Size() / 2f;
            Vector2 starOrigin = star.Size() / 2f;
            float envelope = SpeedEnvelope();

            Color prism = PrismColor(1f) with { A = 0 };
            Color halo = PrismColor(0.5f) with { A = 0 };

            //残影链（越旧越窄越淡），零值轨迹点跳过
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(glow, old, null, prism * (0.24f * k * Projectile.Opacity * envelope),
                    0f, glowOrigin, 0.42f * k, SpriteEffects.None, 0);
            }

            //晕层：色相外晕+主晕
            float charge = Projectile.localAI[1];
            float chargePulse = charge > 0f ? 1f + 0.22f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (18f + charge * 26f)) : 1f;
            Main.EntitySpriteDraw(glow, drawPos, null, halo * (0.65f * Projectile.Opacity), 0f, glowOrigin,
                0.72f * chargePulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, prism * (0.9f * Projectile.Opacity), 0f, glowOrigin,
                0.44f * chargePulse, SpriteEffects.None, 0);

            //四芒星核：速度拉伸
            float speedStretch = 1f + Projectile.velocity.Length() * envelope * 0.05f;
            Main.EntitySpriteDraw(star, drawPos, null, prism * Projectile.Opacity, Projectile.rotation,
                starOrigin, new Vector2(0.1f * speedStretch, 0.075f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, Color.White with { A = 0 } * (0.85f * Projectile.Opacity),
                Projectile.rotation, starOrigin, new Vector2(0.055f * speedStretch, 0.045f), SpriteEffects.None, 0);
            return false;
        }
    }
}
