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

            //出生波前：涟漪+一撮迸散光屑，弹幕不是凭空出现
            if (Timer == 1f && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero,
                    PrismColor(0.8f), 0.46f)?.Configure(12, Hue);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, VaultUtils.RandVr(1.5f, 3.5f),
                        PrismColor(0.9f), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(10, Hue);
                }
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

        //余韵：棱晶碎成三两粒色散光屑+一圈小涟漪，不许瞬灭
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //自然到寿=完整余韵；被整场清弹（转阶段/大招/死亡）时降载防同帧粒子风暴
            bool forceCleared = timeLeft > 4;
            if (forceCleared) {
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, VaultUtils.RandVr(1f, 3f),
                        PrismColor(0.8f), Main.rand.NextFloat(0.5f, 0.8f))?.Configure(12, Hue);
                }
                return;
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero,
                PrismColor(0.7f), 0.3f)?.Configure(10, Hue);
            for (int i = 0; i < 3; i++) {
                //碎屑各带一点色相偏移：碎裂即色散
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center,
                    Projectile.velocity * 0.2f + VaultUtils.RandVr(1f, 3.6f),
                    Main.hslToRgb((Hue + (i - 1) * 0.07f + 1f) % 1f, 1f, 0.66f),
                    Main.rand.NextFloat(0.5f, 0.85f))?.Configure(14, Hue);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //材质：折射棱晶——签名行为=光谱拖影/红紫色散副像/定相闪烁的折射十字
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 glowOrigin = glow.Size() / 2f;
            Vector2 starOrigin = star.Size() / 2f;
            float envelope = SpeedEnvelope();

            Color prism = PrismColor(1f) with { A = 0 };

            //光谱拖尾：相邻轨迹点间拉伸星条连成连续缎带——逐点盖章会读作离散星星复制；
            //悬滞时段间距趋零自然隐没，无需乘速度包络
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i - 1] == Vector2.Zero) {
                    continue;
                }
                Vector2 a = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 b = Projectile.oldPos[i - 1] + Projectile.Size / 2f - Main.screenPosition;
                Vector2 seg = b - a;
                float segLen = seg.Length();
                if (segLen < 0.5f) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Color spectral = Main.hslToRgb((Hue + i * 0.045f) % 1f, 1f, 0.6f) with { A = 0 };
                Main.EntitySpriteDraw(star, (a + b) * 0.5f, null, spectral * (0.42f * k * Projectile.Opacity),
                    seg.ToRotation(), starOrigin,
                    new Vector2((segLen + 8f) / star.Width * 1.3f, 0.026f * (0.6f + k)), SpriteEffects.None, 0);
            }

            //小内晕（只衬底，不再是主体）
            float charge = Projectile.localAI[1];
            float chargePulse = charge > 0f ? 1f + 0.22f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (18f + charge * 26f)) : 1f;
            Main.EntitySpriteDraw(glow, drawPos, null, prism * (0.5f * Projectile.Opacity), 0f, glowOrigin,
                0.4f * chargePulse, SpriteEffects.None, 0);

            //色散镶边：红/紫副像错位量收进主体轮廓内，读作棱晶折射的彩边而非两张分离的图
            float speedStretch = 1f + Projectile.velocity.Length() * envelope * 0.05f;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Color fringeR = Main.hslToRgb((Hue + 0.93f) % 1f, 1f, 0.58f) with { A = 0 };
            Color fringeV = Main.hslToRgb((Hue + 0.07f) % 1f, 1f, 0.58f) with { A = 0 };
            Main.EntitySpriteDraw(star, drawPos + dir * 2f, null, fringeR * (0.38f * Projectile.Opacity),
                Projectile.rotation, starOrigin, new Vector2(0.1f * speedStretch, 0.055f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos - dir * 2f, null, fringeV * (0.38f * Projectile.Opacity),
                Projectile.rotation, starOrigin, new Vector2(0.1f * speedStretch, 0.055f), SpriteEffects.None, 0);

            //主星芒（本色）+白热芯
            Main.EntitySpriteDraw(star, drawPos, null, prism * Projectile.Opacity, Projectile.rotation,
                starOrigin, new Vector2(0.11f * speedStretch, 0.078f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, Color.White with { A = 0 } * (0.9f * Projectile.Opacity),
                Projectile.rotation, starOrigin, new Vector2(0.058f * speedStretch, 0.045f), SpriteEffects.None, 0);

            //折射十字：垂直于飞行向的细闪，identity定相、低频缓闪不刺眼
            float glint = 0.62f + 0.28f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6.5f + Projectile.identity * 1.71f);
            Main.EntitySpriteDraw(star, drawPos, null, Color.White with { A = 0 } * (0.5f * glint * Projectile.Opacity),
                Projectile.rotation + MathHelper.PiOver2, starOrigin,
                new Vector2(0.065f * glint * chargePulse, 0.022f), SpriteEffects.None, 0);
            return false;
        }
    }
}
