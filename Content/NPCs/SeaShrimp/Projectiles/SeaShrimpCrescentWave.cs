using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 合钳水刃：双螯对拍挤出的大型水新月。复利加速+轻微下垂（液体不走直线），
    /// 本体=CrescentSoft 真alpha 新月贴图双层（暗底承体+青缘点缀）+同素材鬼影拖尾。
    /// 判定圆半径 62（窄于 ~140px 的可见月弧）
    /// </summary>
    internal class SeaShrimpCrescentWave : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "CrescentSoft01")]
        private static Asset<Texture2D> CrescentTex = null;
        [VaultLoaden(CWRConstant.Masking + "CrescentSoft02")]
        private static Asset<Texture2D> CrescentInnerTex = null;

        /// <summary>贴图内在朝向修正（素材校准点，实机核对时只调这一个数）</summary>
        private const float CrescentAxis = MathHelper.PiOver2;
        /// <summary>判定圆半径（声明式：窄于可见月弧）</summary>
        private const float HitRadius = 62f;
        private const int TrailLen = 8;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 76;
            Projectile.height = 76;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 80;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            //复利续压 + 轻微下垂：液体波不走匀速直线
            if (Projectile.velocity.Length() < 19f) {
                Projectile.velocity *= 1.011f;
            }
            Projectile.velocity.Y += 0.045f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.07f, 0.17f, 0.32f);

            if (Main.dedServ) {
                return;
            }
            if (Main.GameUpdateCount % 2 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                    -Projectile.velocity * 0.07f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(11, 1.5f);
            }
            if (Main.rand.NextBool(7)) {
                EverdeepVFX.ShedDroplet(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    -Projectile.velocity * 0.05f, 0.8f);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= HitRadius;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            EverdeepVFX.SplashBurst(Projectile.Center, Projectile.velocity, 1.05f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f), SeaShrimpVFX.Glow,
                    Main.rand.NextFloat(0.6f, 0.9f))?.Configure(10);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CrescentTex?.Value;
            Texture2D inner = CrescentInnerTex?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;
            float rot = Projectile.rotation + CrescentAxis;
            float inflate = MathHelper.Clamp((int)Projectile.localAI[0] / 6f, 0.3f, 1f);

            //鬼影拖尾：同素材递缩重绘（契约5）
            for (int i = TrailLen - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = i / (float)TrailLen;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color col = Color.Lerp(SeaShrimpVFX.Body, SeaShrimpVFX.Deep, t) * (0.34f * (1f - t));
                Main.spriteBatch.Draw(tex, pos, null, col, Projectile.oldRot[i] + CrescentAxis,
                    origin, (1.15f - t * 0.4f) * inflate, SpriteEffects.None, 0f);
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            //暗底承体（真alpha遮挡）→ 主体 → 青缘点缀（A=0 加色只做缘光）
            Main.spriteBatch.Draw(tex, center, null, SeaShrimpVFX.Deep * 0.95f, rot,
                origin, 1.3f * inflate, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, center, null,
                Color.Lerp(SeaShrimpVFX.Body, SeaShrimpVFX.Glow, 0.35f), rot,
                origin, 1.15f * inflate, SpriteEffects.None, 0f);
            if (inner != null) {
                Main.spriteBatch.Draw(inner, center, null, SeaShrimpVFX.Glow with { A = 0 } * 0.75f, rot,
                    inner.Size() * 0.5f, 1.0f * inflate, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
