using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 舷炮铁弹：沿冻结车道水平直飞，无重力不追踪（车道承诺自预演起恒定成立），
    /// ai[0]=1 的头弹在出生帧承担齐射轰鸣（各端随实体同步本地触发）。
    /// 寿命按车道长度封顶（危险不越过画出的车道），触墙即碎，无爆炸溅射
    /// </summary>
    internal class PrtBroadsideBall : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CannonballHostile;

        /// <summary>出膛淡入帧数（透明度与判定门同读此值，伤害窗=可见窗）</summary>
        private const int FadeInFrames = 6;

        private static readonly Color CannonAmber = new Color(255, 176, 84);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            if (Age == 0f) {
                //寿命=车道长度/速度：各端从同步的 velocity 确定性得到同一值
                float speed = Projectile.velocity.Length();
                if (speed > 0.1f) {
                    Projectile.timeLeft = (int)(PrtBroadsideOmen.LaneLength / speed) + 2;
                }
                if (Projectile.ai[0] == 1f && !Main.dedServ) {
                    //齐射轰鸣：挂在头弹出生帧，各端本地触发，不与击杀包竞速
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            Age++;

            //出膛淡入
            Projectile.alpha = (int)MathHelper.Lerp(140f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));
            Projectile.rotation += 0.14f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            //黑火药尾烟（≤2 粒/帧）
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.6f,
                    DustID.Smoke, -Projectile.velocity * 0.06f + new Vector2(0f, -0.3f), 150, default, 1f);
                smoke.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, CannonAmber.ToVector3() * 0.16f);
        }

        /// <summary>出膛淡入完成才有杀伤（伤害窗=可见窗）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.45f, Pitch = 0.2f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                    Main.rand.NextVector2Circular(2.5f, 2.5f), 120, default, 1.1f);
                burst.noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f), 90, default, 1f);
                ember.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质拖尾：铁弹贴图降比例重画旧位（横轴粗细≥弹体一半）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null,
                    Color.Lerp(CannonAmber, lightColor, 0.5f) * (0.45f * t * opacity),
                    Projectile.rotation, origin, 0.9f * (0.55f + 0.45f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor * opacity, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //热芯挂光
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                (CannonAmber with { A = 0 }) * (0.25f * opacity),
                Projectile.rotation, origin, 1.05f, SpriteEffects.None, 0);
            return false;
        }
    }
}
