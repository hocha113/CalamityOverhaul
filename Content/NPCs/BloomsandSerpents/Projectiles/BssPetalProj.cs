using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 绯红花瓣：缓降漂移的伤害花瓣，原版花瓣贴图为体（灾祸的温柔形状）。
    /// 侧摆正弦 + 沙暴横风推偏，落地即谢。ai[0]=风向（±1，出手时锁定），ai[1]=摆相种子。
    /// </summary>
    internal class BssPetalProj : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

        private ref float WindSign => ref Projectile.ai[0];
        private ref float SwaySeed => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            //原版花瓣贴图是竖排三帧，必须切帧绘制
            Main.projFrames[Type] = 3;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.scale = 1.5f;
        }

        public override void AI() {
            float age = ++Projectile.localAI[0];

            if (++Projectile.frameCounter >= 7) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //出手 16 帧保留初速（抖出去的劲），随后交给漂移
            if (age > 16f) {
                float sway = MathF.Sin(age * 0.06f + SwaySeed) * 1.15f;
                float windPush = WindSign * 0.85f;
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, sway + windPush, 0.05f);
                Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, 1.7f, 0.03f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(age * 0.09f + SwaySeed) * 0.4f;

            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                    Vector2.Zero, 160, default, 0.6f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                BssVfx.PetalDrift(Projectile.Center, Main.rand.NextVector2Circular(0.8f, 0.5f), 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frameRect = tex.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
            Vector2 origin = frameRect.Size() * 0.5f;

            //体色压成绯红（保留遮蔽 alpha），同材质残影
            Color body = lightColor.MultiplyRGB(new Color(215, 70, 78));
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, frameRect, body * (0.3f * t), Projectile.rotation,
                    origin, Projectile.scale * 0.9f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frameRect,
                body, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //瓣心一点微红光：沙暴昏光里保可读（加色薄层，本体已遮蔽）
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frameRect,
                new Color(255, 60, 66, 0) * 0.3f, Projectile.rotation, origin,
                Projectile.scale * 1.1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
