using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 精灵机炮弹：ai[0]=最大航程（像素，超出即消散，威胁不越出预告航线末端）。
    /// 只在航线扫射窗内由已锁定的航线方向发出，本体直线飞行不转向
    /// </summary>
    internal class FrmStrafeBulletProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletSnowman;

        private float MaxTravel => Projectile.ai[0] <= 0f ? FrmStrafeLaneProj.LaneLength : Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            //航程记账：各端由同步初速确定性同步消散，弹幕不越出航线末端
            Projectile.localAI[0] += Projectile.velocity.Length();
            if (Projectile.localAI[0] >= MaxTravel) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.1f, 0.16f, 0.2f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    Main.rand.NextVector2Circular(1.6f, 1.6f), 110, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.BulletSnowman);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.BulletSnowman].Value;
            int frames = Main.projFrames[ProjectileID.BulletSnowman] > 0 ? Main.projFrames[ProjectileID.BulletSnowman] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, 0);
            Vector2 orig = rect.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation + MathHelper.PiOver2;

            //同材质拖尾（横轴与体同宽，密集小口径的曳光感）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, rect, new Color(170, 220, 255) * (0.4f * t), rot, orig,
                    new Vector2(0.8f, 1f) * t + new Vector2(0.2f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, pos, rect, lightColor, rot, orig, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
