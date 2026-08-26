using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.TiroFinales
{
    /// <summary>
    /// 金色铅弹。手中燧发枪的实弹，弹丸本体+顺速金streak，飞行复利加速，
    /// 命中绽金星芒，余韵火花活得比弹头久。<br/>
    /// ai0=扰动种子
    /// </summary>
    internal class FinaleMusketRound : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Ranged + "TiroFinaleBall";

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> StreakTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;

        private float Seed => Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = RangedMagicDamageClass.Instance;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 420;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            //出生tick驻留：逐步抵消位移让首帧绘制正在枪口
            if (Projectile.localAI[0] < 1f) {
                Projectile.localAI[0] += 1f / (Projectile.extraUpdates + 1);
                Projectile.position -= Projectile.velocity;
            }
            //复利加速：铅弹在魔力推送下越飞越快
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = Projectile.velocity.Length();
            }
            float speed = Projectile.velocity.Length();
            if (speed < Projectile.localAI[1] * 1.55f) {
                Projectile.velocity *= 1.005f;
            }

            //弹丸自旋
            Projectile.rotation += 0.24f * MathF.Sign(Projectile.velocity.X);
            Lighting.AddLight(Projectile.Center, new Vector3(0.9f, 0.72f, 0.32f) * 0.28f);

            //沿途甩金屑(extraUpdates 下按几率稀释)
            if (Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(3f, 3f)
                    , Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f)
                    , new Color(255, 216, 128), Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中星芒:巴麻美式金色闪光
            PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Vector2.Zero
                , new Color(255, 238, 180), Main.rand.NextFloat(0.7f, 1f))
                ?.Configure(new Color(255, 196, 92), 14, 0.05f, 0.9f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //撞击余韵：环波+回溅火花，活得比弹头久
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero
                , new Color(255, 210, 120) * 0.55f, 0.1f)?.Configure(Vector2.One, 0f, 0.5f, 12);
            for (int i = 0; i < 4; i++) {
                Vector2 ev = back.RotatedBy(Main.rand.NextFloat(-0.75f, 0.75f)) * Main.rand.NextFloat(1.5f, 4.2f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, ev, new Color(255, 212, 118)
                    , Main.rand.NextFloat(0.3f, 0.52f))?.Configure(true, Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.82f, 0.4f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = StreakTex?.Value;
            Texture2D glow = GlowTex?.Value;
            Texture2D ball = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;

            //段带式拖尾：相邻轨迹点拉成条带，暗琥珀→金→白热三层
            if (streak != null) {
                Vector2 half = Projectile.Size * 0.5f;
                int len = Projectile.oldPos.Length;
                for (int i = len - 2; i >= 0; i--) {
                    Vector2 a = Projectile.oldPos[i + 1];
                    Vector2 b = Projectile.oldPos[i];
                    if (a == Vector2.Zero || b == Vector2.Zero) {
                        continue;
                    }
                    Vector2 seg = b - a;
                    float segLen = seg.Length();
                    if (segLen < 0.5f) {
                        continue;
                    }
                    float u = i / (float)len;
                    Vector2 mid = (a + b) * 0.5f + half - Main.screenPosition;
                    float rot = seg.ToRotation() + MathHelper.PiOver2;
                    float sy = segLen * 1.3f / (streak.Height * 0.58f);
                    float fade = 1f - u;
                    Main.EntitySpriteDraw(streak, mid, null, (new Color(168, 104, 34) with { A = 0 }) * (fade * 0.4f)
                        , rot, streak.Size() * 0.5f, new Vector2(0.2f, sy), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(streak, mid, null, (new Color(255, 198, 96) with { A = 0 }) * (fade * 0.5f)
                        , rot, streak.Size() * 0.5f, new Vector2(0.12f, sy * 0.92f), SpriteEffects.None, 0);
                    if (u < 0.55f) {
                        Main.EntitySpriteDraw(streak, mid, null, (new Color(255, 246, 214) with { A = 0 }) * (fade * 0.55f)
                            , rot, streak.Size() * 0.5f, new Vector2(0.05f, sy * 0.8f), SpriteEffects.None, 0);
                    }
                }
            }

            //弹体：辉光衬底+旋转铅弹
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            if (glow != null) {
                Main.EntitySpriteDraw(glow, drawPos, null, (new Color(255, 208, 110) with { A = 0 }) * 0.5f, 0f
                    , glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(ball, drawPos, null, lightColor, Projectile.rotation
                , ball.Size() * 0.5f, 0.7f, SpriteEffects.None, 0);
            //弹面掠光
            float glint = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Seed);
            Main.EntitySpriteDraw(ball, drawPos, null, (new Color(255, 232, 168) with { A = 0 }) * (0.3f * glint)
                , Projectile.rotation, ball.Size() * 0.5f, 0.7f, SpriteEffects.None, 0);
            return false;
        }
    }
}
