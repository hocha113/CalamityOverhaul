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
    /// 永恒虹瓣：缓行螺旋，身后拖出久驻的虹彩缎带；
    /// ai[0]=每帧曲率(带符号) ai[1]=色相 ai[2]=初速增益
    /// 判定=亮头+近段缎带，远段余辉纯装饰（视觉与判定分档）
    /// </summary>
    internal class EmpressPetal : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int Life = 260;
        private const int TrailLen = 34;
        /// <summary>判定只认最近几个轨迹点</summary>
        private const int HazardTrailPoints = 7;

        private ref float Timer => ref Projectile.localAI[0];
        private float CurveRate => Projectile.ai[0];
        private float Hue => Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            //螺旋曲率+轻微加速：飞行期有演化，不许匀速直线
            Projectile.velocity = Projectile.velocity.RotatedBy(CurveRate);
            float gain = 1f + Projectile.ai[2] * 0.0016f;
            if (Projectile.velocity.Length() < 9f) {
                Projectile.velocity *= gain;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            float fadeIn = MathHelper.Clamp(Timer / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 26f, 0f, 1f);
            Projectile.Opacity = fadeIn * fadeOut;

            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 1f, 0.55f).ToVector3() * 0.36f * Projectile.Opacity);

            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_EmpressPetalDust>(Projectile.Center, Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.hslToRgb(Hue, 0.9f, 0.66f), Main.rand.NextFloat(0.4f, 0.7f))?.Configure(30, Hue);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //亮头
            if (projHitbox.Intersects(targetHitbox)) {
                return true;
            }
            //近段缎带（亮区），远段余辉不判定；未初始化的轨迹点(零值)直接截断
            float p = 0f;
            for (int i = 1; i < HazardTrailPoints && i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i - 1] == Vector2.Zero || Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 a = Projectile.oldPos[i - 1] + Projectile.Size / 2f;
                Vector2 b = Projectile.oldPos[i] + Projectile.Size / 2f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, b, 14f, ref p)) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            //材质：光织花瓣——签名行为=扑动翻瓣/危险段白热芯线/远段光谱余辉
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Vector2 half = Projectile.Size / 2f;
            Vector2 glowOrigin = glow.Size() / 2f;
            Vector2 starOrigin = star.Size() / 2f;

            //缎带：近段亮而宽（危险区），远段快速衰减成余辉
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 pos = Projectile.oldPos[i] + half;
                if (pos == half) {
                    continue;
                }
                float k = 1f - i / (float)Projectile.oldPos.Length;
                bool hazard = i < HazardTrailPoints;
                float alpha = hazard ? 0.55f : 0.16f * k;
                float scale = hazard ? 0.46f : 0.34f * (0.4f + k);
                Color c = Main.hslToRgb((Hue + i * 0.012f) % 1f, 1f, hazard ? 0.62f : 0.5f) with { A = 0 };
                Main.EntitySpriteDraw(glow, pos - Main.screenPosition, null, c * (alpha * Projectile.Opacity),
                    0f, glowOrigin, scale, SpriteEffects.None, 0);

                //危险段芯线：段间拉一根白热星条，判定区读作实体缎带而非糊团
                if (hazard && Projectile.oldPos[i - 1] != Vector2.Zero) {
                    Vector2 prev = Projectile.oldPos[i - 1] + half;
                    Vector2 seg = prev - pos;
                    float segLen = seg.Length();
                    if (segLen > 0.5f) {
                        Main.EntitySpriteDraw(star, (pos + prev) * 0.5f - Main.screenPosition, null,
                            Color.White with { A = 0 } * (0.5f * Projectile.Opacity), seg.ToRotation(),
                            starOrigin, new Vector2(segLen / star.Width * 1.4f, 0.02f), SpriteEffects.None, 0);
                    }
                }
            }

            //亮头：扑动的瓣形——用横轴开合表现翻瓣，两层星芒夹角收小、同步缓摆，
            //避免大角度反向摆动读作抖动重影
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color prism = Main.hslToRgb(Hue, 1f, 0.66f) with { A = 0 };
            float flutter = (float)Math.Sin(Timer * 0.11f + Projectile.identity * 0.83f);
            float petalWide = 0.055f + 0.035f * Math.Abs(flutter);
            Main.EntitySpriteDraw(glow, drawPos, null, prism * (0.85f * Projectile.Opacity), 0f, glowOrigin, 0.5f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, prism * Projectile.Opacity, Projectile.rotation + 0.2f * flutter,
                starOrigin, new Vector2(0.105f, petalWide), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, Main.hslToRgb((Hue + 0.06f) % 1f, 1f, 0.6f) with { A = 0 } * (0.5f * Projectile.Opacity),
                Projectile.rotation + 0.2f * flutter - 0.15f, starOrigin, new Vector2(0.085f, petalWide * 0.8f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, Color.White with { A = 0 } * (0.85f * Projectile.Opacity),
                Projectile.rotation, starOrigin, new Vector2(0.05f, 0.035f), SpriteEffects.None, 0);
            return false;
        }

        //余韵：瓣散作光尘，向外绽一小圈；被整场清弹时降载
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            if (timeLeft > 4 && !Main.rand.NextBool(3)) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_EmpressPetalDust>(Projectile.Center, VaultUtils.RandVr(0.8f, 2.4f),
                    Main.hslToRgb((Hue + i * 0.05f) % 1f, 0.9f, 0.64f), Main.rand.NextFloat(0.45f, 0.8f))?.Configure(28, Hue);
            }
        }
    }
}
