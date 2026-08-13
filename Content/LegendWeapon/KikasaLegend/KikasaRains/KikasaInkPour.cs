using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨瀑:倒撑重击的倾覆主体。自碗口沿基准角(近竖直向下,小幅摆动)倾下的宽墨柱,
    /// 前锋推进→持续冲刷→自源头排空;射线逐帧找落点(实心或域内湖面),
    /// 落点持续搅浊留渍;前 12 帧沿瀑缘散射特大墨滴。
    /// 判定为线碰撞,排空过半即失能;绘制走 KikasaInkDrop.fx 的 TechPour
    /// </summary>
    internal class KikasaInkPour : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int ExpandFrames = 8;
        public const int SustainFrames = 26;
        public const int CollapseFrames = 10;
        public const int TotalFrames = ExpandFrames + SustainFrames + CollapseFrames;

        private const float MaxLenPx = 860f;

        /// <summary>基准倾泻角(弧度,近竖直向下)</summary>
        private ref float BaseAngle => ref Projectile.ai[0];

        /// <summary>蓄力档 0~1,吃宽度与伤害表现</summary>
        private ref float Fill => ref Projectile.ai[1];

        private float life;
        private float lenPx = MaxLenPx;
        private bool hitGround;
        private bool hitLake;
        private int scatterCount;

        private float DirAngle
            => BaseAngle + MathF.Sin(life * 0.16f + Projectile.identity * 0.71f) * 0.085f;

        private float WidthPx => 54f + Fill * 36f;

        private float LenT {
            get {
                float t = MathHelper.Clamp(life / ExpandFrames, 0f, 1f);
                return 1f - (1f - t) * (1f - t);
            }
        }

        private float DrainT
            => MathHelper.Clamp((life - ExpandFrames - SustainFrames) / (float)CollapseFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
            Projectile.netImportant = true;
        }

        public override void AI() {
            life++;
            Vector2 dir = DirAngle.ToRotationVector2();

            //域内湖面:墨倾进湖里,落点换涟漪
            Player owner = Main.player[Projectile.owner];
            bool lakeAlive = owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            float lakeY = lakeAlive ? owner.GetModPlayer<KikasaDomainPlayer>().LakeWorldY : float.MaxValue;

            //射线找落点:实心或湖面,各端确定性一致
            lenPx = MaxLenPx;
            hitGround = false;
            hitLake = false;
            for (float d = 32f; d <= MaxLenPx; d += 16f) {
                Vector2 p = Projectile.Center + dir * d;
                if (p.Y >= lakeY) {
                    lenPx = d;
                    hitGround = true;
                    hitLake = true;
                    break;
                }
                if (Collision.SolidCollision(p - new Vector2(4f, 4f), 8, 8)) {
                    lenPx = d;
                    hitGround = true;
                    break;
                }
            }

            //落点余韵:冲刷期间持续留渍/搅涟漪,节流一拍一次
            if (hitGround && LenT >= 0.99f && DrainT <= 0f && life % 8 == 0) {
                Vector2 end = Projectile.Center + dir * lenPx;
                if (hitLake) {
                    if (!Main.dedServ && KikasaDomain.Viewed != null) {
                        KikasaDomainDeco.RippleAt(new Vector2(end.X, lakeY), 1.1f);
                        KikasaDomainDeco.SplashAt(new Vector2(end.X, lakeY), 6);
                    }
                }
                else {
                    KikasaInkFX.AddGroundSplat(end + dir * 6f, dir * 14f, 46f + Fill * 28f);
                }
                //落点翻涌:反弹墨珠+一口墨雾
                if (!Main.dedServ) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = (-dir).RotatedByRandom(0.9f) * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(end + Main.rand.NextVector2Circular(WidthPx * 0.3f, 6f),
                            vel, Main.rand.NextBool(3) ? KikasaInk.InkDeep : KikasaInk.InkBody,
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 28));
                    }
                    PRTLoader.NewParticle<PRT_KikasaInkMist>(end - dir * 10f,
                        -dir * Main.rand.NextFloat(0.5f, 1.2f), KikasaInk.InkDeep,
                        Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(26, 40));
                }
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.42f, Pitch = -0.5f, MaxInstances = 3 }, end);
            }

            //特大墨滴沿瀑缘散射(所有者端)
            if (Main.myPlayer == Projectile.owner && life <= 12f && (int)life % 2 == 0 && scatterCount < 7) {
                scatterCount++;
                float along = Main.rand.NextFloat(0.12f, 0.5f) * lenPx;
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2) * side;
                Vector2 pos = Projectile.Center + dir * along + perp * (WidthPx * 0.5f);
                Vector2 vel = perp * Main.rand.NextFloat(2f, 4.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f);
                float fallbackX = Projectile.Center.X + dir.X * lenPx + Main.rand.NextFloat(-150f, 150f);
                int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, vel,
                    ModContent.ProjectileType<KikasaInkDrop>(), (int)(Projectile.damage * 1.25f),
                    Projectile.knockBack, Projectile.owner, -1f, fallbackX, 0f);
                if (p >= 0 && p < Main.maxProjectiles) {
                    Main.projectile[p].scale = 1.3f;
                    Main.projectile[p].netUpdate = true;
                }
            }

            Lighting.AddLight(Projectile.Center + dir * lenPx * 0.5f, 0.14f, 0.03f, 0.04f);
        }

        /// <summary>线碰撞:柱体全程;排空过半即失能</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (DrainT > 0.35f) {
                return false;
            }
            float _ = 0f;
            Vector2 dir = DirAngle.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * (lenPx * LenT), WidthPx * 0.7f, ref _);
        }

        //==================== 绘制(由 KikasaRainRender 集中调用) ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>着色器路径:TechPour 长条 quad,源头在 quad 上缘</summary>
        internal void DrawPourQuad(SpriteBatch sb, Effect fx, Texture2D canvas) {
            float fade = MathHelper.Clamp(life / 4f, 0f, 1f);
            float churn = hitGround ? MathHelper.Clamp((LenT - 0.9f) * 10f, 0f, 1f) * (1f - DrainT) : 0f;
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.7391f % 3.71f);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uLen"]?.SetValue(LenT);
            fx.Parameters["uDrain"]?.SetValue(DrainT);
            fx.Parameters["uChurn"]?.SetValue(churn);
            fx.CurrentTechnique = fx.Techniques["TechPour"];
            fx.CurrentTechnique.Passes[0].Apply();

            //shader 可见半宽 ≈0.33,quad 宽按其折算
            float quadW = WidthPx / 0.62f;
            Vector2 scale = new(quadW / canvas.Width, lenPx / canvas.Height);
            sb.Draw(canvas, Projectile.Center - Main.screenPosition, null, Color.White,
                DirAngle - MathHelper.PiOver2, new Vector2(canvas.Width * 0.5f, 0f), scale,
                SpriteEffects.None, 0f);
        }

        /// <summary>精灵回退:一条速度拉伸的暗墨柱</summary>
        internal void DrawPourFallback(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float alpha = (1f - DrainT) * MathHelper.Clamp(life / 4f, 0f, 1f);
            Vector2 mid = Projectile.Center + DirAngle.ToRotationVector2() * (lenPx * LenT * 0.5f);
            Vector2 scale = new(WidthPx / tex.Width * 1.2f, lenPx * LenT / tex.Height * 1.1f);
            sb.Draw(tex, mid - Main.screenPosition, null, KikasaInk.InkBody * (0.85f * alpha),
                DirAngle - MathHelper.PiOver2, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
