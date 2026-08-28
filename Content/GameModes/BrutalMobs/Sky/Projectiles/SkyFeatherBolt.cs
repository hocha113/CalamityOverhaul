using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Sky.Projectiles
{
    /// <summary>
    /// 羽刃：扇面齐射的弹体，直线飞行保持预览扇形不变形（原版鸟妖羽毛贴图）。
    /// ai[0]=档位。淡入完成才有杀伤（伤害窗口=可见窗口）
    /// </summary>
    internal class SkyFeatherBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;

        /// <summary>出膛淡入帧数，未淡入无判定（公平阀）</summary>
        private const int FadeInFrames = 10;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 220;
            Projectile.aiStyle = -1;
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud,
                    -Projectile.velocity * 0.1f, 160, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.14f, 0.17f, 0.22f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Cloud,
                    Main.rand.NextVector2Circular(2f, 2f), 110, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质拖尾（横轴粗细与本体同贴图同比例，≥0.5×体宽）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, new Color(190, 220, 255, 60) * (0.35f * fade * opacity),
                    Projectile.rotation, origin, Projectile.scale * (0.7f + 0.3f * fade), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * opacity,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            //风刃辉光敷料（加色只做勾边）
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(170, 215, 255, 0) * (0.3f * opacity),
                Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
