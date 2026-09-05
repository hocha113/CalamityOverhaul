using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>
    /// 横风花刃：被沙暴横风裹走的花瓣。出手后 <see cref="MergeFrames"/> 帧内并入声明的车道高度，
    /// 随后沿车道直线横飞（车道内只有细微飘摆），落地即谢。
    /// 与缓降的 <see cref="BssPetalProj"/> 是两种读法：那是从上落下的幕，这是横着扫过的刃。
    /// ai[0]=风向 ±1；ai[1]=车道世界 Y；ai[2]=摆相种子。
    /// </summary>
    internal class BssWindPetalProj : BssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPetal;

        private const int MergeFrames = 14;

        private int Dir => Projectile.ai[0] < 0f ? -1 : 1;
        private ref float LaneY => ref Projectile.ai[1];
        private ref float SwaySeed => ref Projectile.ai[2];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 3;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 170;
            Projectile.scale = 1.35f;
        }

        public override void AI() {
            float age = ++Projectile.localAI[0];

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            float speed = BssDirector.GalePetalSpeed;
            if (age <= MergeFrames) {
                //并道：竖向快速收敛到车道，横速起步
                float dy = LaneY - Projectile.Center.Y;
                Projectile.velocity.Y = MathHelper.Clamp(dy * 0.22f, -12f, 12f);
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, Dir * speed, 0.22f);
            }
            else {
                //车道内直飞：微飘摆，车道半高内
                float sway = MathF.Sin(age * 0.27f + SwaySeed) * 0.7f;
                float correct = (LaneY - Projectile.Center.Y) * 0.08f;
                Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, sway + correct, 0.3f);
                Projectile.velocity.X = Dir * speed * (1f + 0.05f * MathF.Sin(age * 0.19f + SwaySeed));
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(age * 0.31f + SwaySeed) * 0.35f;

            if (!Main.dedServ) {
                if (Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                        -Projectile.velocity * 0.08f + new Vector2(0f, -Main.rand.NextFloat(0.5f)), 140, default, 0.8f);
                    d.noGravity = true;
                }
                if (Main.rand.NextBool(8)) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, Vector2.Zero, 160, default, 0.6f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                BssVfx.PetalDrift(Projectile.Center, new Vector2(Dir * Main.rand.NextFloat(0.5f, 1.5f), Main.rand.NextFloat(-0.5f, 0.5f)), 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frameRect = tex.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
            Vector2 origin = frameRect.Size() * 0.5f;

            //体色压成绯红（保留遮蔽 alpha），长残影拉出横飞的速度线
            Color body = lightColor.MultiplyRGB(new Color(215, 70, 78));
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, frameRect, body * (0.38f * t), Projectile.rotation,
                    origin, Projectile.scale * (0.7f + 0.3f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frameRect,
                body, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            //瓣心微红光：沙暴昏光里保可读（加色薄层，本体已遮蔽）
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frameRect,
                new Color(255, 60, 66, 0) * 0.35f, Projectile.rotation, origin,
                Projectile.scale * 1.1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
