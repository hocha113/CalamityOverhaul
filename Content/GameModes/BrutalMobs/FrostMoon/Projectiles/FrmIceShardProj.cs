using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 霜月冰晶碎片（雪花怪自爆放射 / 雪怪震地锥刺共用弹体）：ai[0]=每帧重力
    /// （放射用微坠、震地用重坠弧，两端从同一 ai 值确定性展开）。
    /// 出膛淡入期无判定（公平阀）、触物即碎；原版冰锥贴图实体层 + 同材质拖尾
    /// </summary>
    internal class FrmIceShardProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        /// <summary>淡入帧数，判定开启与可见同门</summary>
        private const int FadeInFrames = 6;
        /// <summary>下坠终端速度</summary>
        private const float MaxFallSpeed = 13f;

        private ref float Gravity => ref Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 80;
            Projectile.coldDamage = true;
        }

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //沿途霜屑（低频）
            if (!Main.dedServ && Main.rand.NextBool(7)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Frost,
                    -Projectile.velocity * 0.08f, 140, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.08f, 0.14f, 0.22f));
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > 5 ? null : false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, new Color(198, 234, 255), 0.6f) * opacity;

            //同材质拖尾（横轴粗细 ≥ 弹体一半）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, drawPos, null, body * (0.4f * t), Projectile.rotation,
                    orig, Projectile.scale * (0.55f + 0.3f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, body,
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Circular(1.8f, 1.8f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
