using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 次元斩斩击线弹幕，基于顶点绘制
    /// </summary>
    internal class MuraSlashLine : ModProjectile
    {
        //ai[0]控制颜色模式:0为白色马上消散，其他为深红残破
        //ai[1]控制半短轴
        //ai[2]控制椭圆整体旋转角度
        //localAI[0]控制旋转速度=>0
        //localAI[1]控制半长轴

        private Color drawColor;
        private float distance;
        private int timeSinceSpawn = 0;

        public override string Texture => MuraSlayAllAssets.TransparentImg;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            drawColor = Color.White;
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void OnSpawn(IEntitySource source) {
            distance = (Main.LocalPlayer.Center - Projectile.Center).Length();
            Projectile.ai[2] = (Main.LocalPlayer.Center - Projectile.Center).ToRotation();
            Projectile.localAI[1] = 420f / distance;
            Projectile.ai[1] = 2f / distance;
            Projectile.timeLeft = 80 + (int)Projectile.ai[0] * 110 + (int)distance / 40;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            timeSinceSpawn++;

            if (30 < timeSinceSpawn && timeSinceSpawn < 51) {
                drawColor = Color.White * ((50 - timeSinceSpawn) * 0.05f);
                if (Projectile.ai[0] > 0) {
                    drawColor.A = 255;
                }
            }

            if (160 + (int)distance / 40 < timeSinceSpawn) {
                drawColor *= 0.7f;
            }

            Projectile.localAI[1] += 8f / distance;
        }

        public override void PostDraw(Color lightColor) {
            List<ColoredVertex> vertices = new List<ColoredVertex>();

            float a = Projectile.localAI[1];
            float b = Projectile.ai[1];
            float flipRotation = Projectile.ai[2];

            //深红色调
            Color finalColor = Projectile.ai[0] > 0
                ? Color.Lerp(drawColor, new Color(150, 30, 50), 0.5f)
                : drawColor;

            Vector2 v1 = new Vector2(-25, -600);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(0, 0, 1)));

            v1 = new Vector2(25, -600);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(1, 0, 1)));

            v1 = new Vector2(-25, 600);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(0, 1, 1)));

            v1 = new Vector2(25, 600);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(1, 1, 1)));

            Main.graphics.GraphicsDevice.Textures[0] = MuraSlayAllAssets.Iinetry;

            if (vertices.Count > 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }
        }
    }
}
