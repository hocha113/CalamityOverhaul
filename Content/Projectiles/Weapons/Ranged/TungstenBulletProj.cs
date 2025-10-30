using CalamityMod;
using CalamityMod.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    /// <summary>
    /// 钨子弹专属弹幕，与火枪共用材质但拥有更好的视觉表现
    /// </summary>
    internal class TungstenBulletProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        //拖尾相关
        private const int TrailLength = 8;
        private float[] trailAlphas = new float[TrailLength];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            //旋转朝向
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //更新拖尾透明度
            UpdateTrailAlphas();

            //飞行粒子效果 - 更稀疏但更精致
            if (Main.rand.NextBool(3)) {
                SpawnFlightDust();
            }

            //光照效果
            Lighting.AddLight(Projectile.Center, 0.3f, 0.3f, 0.4f);
        }

        private void UpdateTrailAlphas() {
            //更新拖尾渐变透明度
            for (int i = 0; i < trailAlphas.Length; i++) {
                float targetAlpha = 1f - (i / (float)trailAlphas.Length);
                trailAlphas[i] = MathHelper.Lerp(trailAlphas[i], targetAlpha, 0.3f);
            }
        }

        private void SpawnFlightDust() {
            //金属光泽粒子
            Vector2 dustPos = Projectile.Center - Projectile.velocity * 0.5f;
            int dustType = Main.rand.NextBool(3) ? DustID.Silver : DustID.Smoke;

            Dust dust = Dust.NewDustPerfect(
                dustPos,
                dustType,
                -Projectile.velocity * 0.2f,
                100,
                default,
                Main.rand.NextFloat(0.6f, 1f)
            );
            dust.noGravity = true;
            dust.fadeIn = 0.8f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
            if (Projectile.numHits == 0)
            //击中特效
            SpawnImpactEffect(Projectile.Center, hit.Crit);
        }

        private void SpawnImpactEffect(Vector2 hitPosition, bool isCrit) {
            //金属碰撞火花
            int sparkCount = isCrit ? 12 : 8;
            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity = VaultUtils.RandVr(6, 14);

                //使用自定义火花粒子
                BasePRT spark = new PRT_Spark(
                    hitPosition,
                    sparkVelocity,
                    false,
                    Main.rand.Next(3, 7),
                    Main.rand.NextFloat(0.6f, 1.2f),
                    isCrit ? Color.Gold : Color.LightGray
                );
                PRTLoader.AddParticle(spark);
            }

            //暴击额外特效
            if (isCrit) {
                for (int i = 0; i < 6; i++) {
                    Dust critDust = Dust.NewDustPerfect(
                        hitPosition,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(4f, 4f),
                        100,
                        Color.Gold,
                        Main.rand.NextFloat(1f, 1.5f)
                    );
                    critDust.noGravity = true;
                }
            }

            //烟尘
            for (int i = 0; i < 4; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    hitPosition,
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(3f, 3f),
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                smoke.noGravity = false;
            }
            Projectile.numHits++;
            Projectile.damage /= 2;
            Projectile.Explode(60, default, false);
            Projectile.Kill();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞音效
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);

            //碰撞特效
            SpawnTileCollisionEffect();

            return true;
        }

        private void SpawnTileCollisionEffect() {
            //金属碰撞火花
            for (int i = 0; i < 6; i++) {
                Vector2 sparkVelocity = VaultUtils.RandVr(4, 10);

                BasePRT spark = new PRT_Spark(
                    Projectile.Center,
                    sparkVelocity,
                    false,
                    Main.rand.Next(2, 5),
                    Main.rand.NextFloat(0.5f, 0.9f),
                    Color.Silver
                );
                PRTLoader.AddParticle(spark);
            }

            //碰撞烟尘
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(2f, 2f),
                    100,
                    default,
                    Main.rand.NextFloat(0.8f, 1.3f)
                );
                dust.noGravity = false;
            }
        }

        public override void OnKill(int timeLeft) {
            //消失时的轻微特效
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Smoke,
                    Scale: Main.rand.NextFloat(0.6f, 1f)
                );
                dust.velocity *= 0.5f;
                dust.noGravity = true;
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //根据速度调整亮度
            float speedFactor = Projectile.velocity.Length() / 20f;
            Color baseColor = new Color(200, 200, 220);
            return Color.Lerp(lightColor, Color.Green, MathHelper.Clamp(speedFactor * 0.5f, 0f, 0.6f));
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.localAI[0] < 4) {
                return false;
            }
            //绘制拖尾
            DrawTrail(lightColor);

            //绘制子弹主体
            DrawBullet(lightColor);

            return false;
        }

        private void DrawTrail(Color lightColor) {
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2) {
                return;
            }

            Texture2D trailTex = VaultAsset.placeholder2.Value;
            Color trailColor = Color.Green;

            for (int i = 0; i < Projectile.oldPos.Length - 1; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i + 1] == Vector2.Zero) {
                    continue;
                }

                float progress = i / (float)Projectile.oldPos.Length;
                float trailAlpha = trailAlphas[i] * 0.6f;

                Vector2 start = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                Vector2 end = Projectile.oldPos[i + 1] + Projectile.Size * 0.5f;
                Vector2 diff = end - start;
                float length = diff.Length();

                if (length < 0.1f) {
                    continue;
                }

                float rotation = diff.ToRotation();
                float width = MathHelper.Lerp(3f, 1f, progress);

                Main.spriteBatch.Draw(
                    trailTex,
                    start - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    trailColor * trailAlpha,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        private void DrawBullet(Color lightColor) {
            Texture2D bulletTex = TextureAssets.Projectile[Type].Value;
            Rectangle sourceRect = bulletTex.Bounds;
            Vector2 origin = sourceRect.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition - Projectile.velocity;

            Color drawColor = Projectile.GetAlpha(lightColor);

            //主体绘制
            Main.EntitySpriteDraw(
                bulletTex,
                drawPos,
                sourceRect,
                drawColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //高速运动时的白色辉光
            float speedFactor = Projectile.velocity.Length() / 20f;
            if (speedFactor > 0.3f) {
                float glowIntensity = (speedFactor - 0.3f) * 0.8f;
                Main.EntitySpriteDraw(
                    bulletTex,
                    drawPos,
                    sourceRect,
                    Color.White * glowIntensity * 0.4f,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * 1.1f,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
}
