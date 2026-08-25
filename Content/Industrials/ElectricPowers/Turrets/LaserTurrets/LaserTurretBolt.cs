using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.LaserTurrets
{
    /// <summary>
    /// 激光塔光束弹:高 extraUpdates 的准直线弹,单体命中即灭。
    /// 普通 ModProjectile,由权威端生成,spawn包天然广播
    /// </summary>
    internal class LaserTurretBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>激光红,与塔身色调同源</summary>
        internal static readonly Color LaserRed = new(255, 90, 90);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 24;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            //每帧实际位移约248px,1200px射程内5帧到达,读作射线
            Projectile.extraUpdates = 30;
            Projectile.DamageType = DamageClass.Default;
        }

        public override void AI() {
            //首帧发射声(每端各自播放,位置声自然衰减)
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, LaserRed.ToVector3() * 0.5f);

            //高倍更新下低概率补光尘,拖尾主体靠 oldPos 绘制
            if (Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, Vector2.Zero, 120, default, 0.7f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //命中/撞墙迸溅,OnKill 在每个端各自执行,队友可见
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                    VaultUtils.RandVr(3.5f), 100, default, 1.1f);
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 origin = star.Size() / 2;
            //黑底贴图,A=0 加色画法
            Color core = LaserRed;
            core.A = 0;

            //oldPos 光尾:向后渐缩渐隐,速度拉伸感由缓存点距自然给出
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Main.spriteBatch.Draw(star, pos + Projectile.Size / 2 - Main.screenPosition, null,
                    core * (0.5f * fade), 0f, origin, 0.10f * fade + 0.03f, SpriteEffects.None, 0f);
            }

            //本体亮核:外层色光+内层白心
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(star, drawPos, null, core, 0f, origin, 0.22f, SpriteEffects.None, 0f);
            Color white = Color.White;
            white.A = 0;
            Main.spriteBatch.Draw(star, drawPos, null, white * 0.8f, 0f, origin, 0.10f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
