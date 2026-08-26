using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove.Projectiles
{
    /// <summary>
    /// 精英散射矢：ai[0]=样式（0 骷髅箭 / 1 蜗牛激光 / 2 隐士毒镖）。
    /// 直线飞行微增压（弹道几何不变，缺口承诺不破坏）；出膛淡入完成才有杀伤。
    /// 弹体用原版贴图，拖尾同材质降比重画
    /// </summary>
    internal class EMScatterBoltProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowHostile;

        /// <summary>出膛淡入帧：淡入完成前无杀伤（公平阀）</summary>
        internal const int FadeInFrames = 10;

        private ref float Age => ref Projectile.localAI[0];
        private int Style => (int)Projectile.ai[0];

        /// <summary>样式对应的原版弹幕贴图</summary>
        internal static int StyleProjId(int style) => style switch {
            1 => ProjectileID.PinkLaser,
            2 => ProjectileID.PoisonDart,
            _ => ProjectileID.WoodenArrowHostile,
        };

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Age == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound((Style == 1 ? SoundID.Item33 : SoundID.Item5)
                    with { Volume = 0.45f, MaxInstances = 2 }, Projectile.Center);
            }
            Age++;

            //出膛淡入（杀伤窗与可见度同门）
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            //微增压：只改速率不改方向，扇面几何与缺口保持锁定时的承诺
            if (Projectile.velocity.Length() < 16f) {
                Projectile.velocity *= 1.012f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, StyleTint().ToVector3() * 0.16f);
        }

        /// <summary>淡入完成才有杀伤</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Style == 2) {
                target.AddBuff(BuffID.Poisoned, 240);    //毒镖：命中挂原版中毒
            }
        }

        private Color StyleTint() => Style switch {
            1 => new Color(255, 110, 230),
            2 => new Color(150, 230, 90),
            _ => new Color(255, 200, 140),
        };

        public override bool PreDraw(ref Color lightColor) {
            int styleProj = StyleProjId(Style);
            Main.instance.LoadProjectile(styleProj);
            Texture2D tex = TextureAssets.Projectile[styleProj].Value;
            Vector2 origin = tex.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质拖尾：弹体贴图降比降透明重画旧位（横向占比≥0.5）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null,
                    Color.Lerp(lightColor, StyleTint(), 0.5f) * (0.38f * t * opacity),
                    Projectile.rotation, origin, 0.55f + 0.35f * t, SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //本体：原版贴图受光绘制（A>0 遮挡层）
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * opacity,
                Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //辉光敷料：主题色加色薄层
            Main.EntitySpriteDraw(tex, drawPos, null, StyleTint() with { A = 0 } * (0.35f * opacity),
                Projectile.rotation, origin, 1.06f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center,
                    Style == 2 ? DustID.GreenTorch : Style == 1 ? DustID.PinkTorch : DustID.Torch,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.6f)
                        * Main.rand.NextFloat(1f, 3f), 120, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
