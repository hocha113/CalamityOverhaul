using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 压制铅弹：船长齐射的扇面直射弹。ai[0]=1 的头弹在出生帧承担齐射轰响（各端随实体同步本地触发）。
    /// 直线飞行不追踪（缺口几何自预演起恒定成立），触墙即碎，寿命封顶
    /// </summary>
    internal class PrtFanShot : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        /// <summary>飞行寿命（帧）</summary>
        private const int FlightLife = 110;
        /// <summary>出膛淡入帧数（透明度与判定门同读此值，伤害窗=可见窗）</summary>
        private const int FadeInFrames = 6;

        private static readonly Color PowderGold = new Color(255, 214, 120);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = FlightLife;
        }

        public override void AI() {
            Age++;
            if (Age == 1f && Projectile.ai[0] == 1f && !Main.dedServ) {
                //齐射轰响：挂在头弹出生帧，各端本地触发，不与击杀包竞速
                SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.85f, Pitch = -0.15f, MaxInstances = 5 }, Projectile.Center);
            }
            //出膛淡入（判定同步门控在 CanDamage）
            Projectile.alpha = (int)MathHelper.Lerp(160f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    -Projectile.velocity * 0.04f, 170, default, 0.6f);
                smoke.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, PowderGold.ToVector3() * 0.12f);
        }

        /// <summary>出膛淡入完成才有杀伤（伤害窗=可见窗）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust chip = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 140, default, 0.8f);
                chip.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质拖尾：弹体贴图降比例重画旧位
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null,
                    Color.Lerp(PowderGold, lightColor, 0.4f) * (0.5f * t * opacity),
                    Projectile.rotation, origin, 0.9f * (0.5f + 0.5f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                Color.Lerp(lightColor, Color.White, 0.3f) * opacity,
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
