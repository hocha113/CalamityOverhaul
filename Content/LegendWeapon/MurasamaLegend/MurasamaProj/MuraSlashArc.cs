using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj
{
    /// <summary>
    /// 次元斩弧形效果弹幕，基于顶点绘制
    /// </summary>
    internal class MuraSlashArc : ModProjectile
    {
        //ai[0]控制材质:0为完整，其他为深红残破
        //ai[1]控制半短轴
        //ai[2]控制椭圆整体旋转角度
        //localAI[0]控制旋转速度
        //localAI[1]控制半长轴

        private Color drawColor;

        public override string Texture => MuraSlayAllAssets.TransparentImg;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.timeLeft = Main.rand.Next(8, 20);
            drawColor = Color.White * (-1 * Main.rand.NextFloat(-1.0f, -0.5f));
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Projectile.localAI[0] = Main.GlobalTimeWrappedHourly * 16;
            Projectile.localAI[1] = 120f / (int)(Main.LocalPlayer.Center - Projectile.Center).Length();
            Projectile.ai[1] = 80f / (int)(Main.LocalPlayer.Center - Projectile.Center).Length();
            Projectile.ai[2] = (Main.LocalPlayer.Center - Projectile.Center).ToRotation();
        }

        public override void PostDraw(Color lightColor) {
            List<ColoredVertex> vertices = new List<ColoredVertex>();

            float a = Projectile.localAI[1];
            float b = Projectile.ai[1];
            float flipRotation = Projectile.ai[2];

            //为村正添加深红色调
            Color finalColor = Projectile.ai[0] == 0
                ? drawColor
                : Color.Lerp(drawColor, Color.IndianRed, 0.6f);

            Vector2 v1 = new Vector2(-300, -300).RotatedBy(Projectile.localAI[0]);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(0, 0, 1)));

            v1 = new Vector2(300, -300).RotatedBy(Projectile.localAI[0]);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(1, 0, 1)));

            v1 = new Vector2(-300, 300).RotatedBy(Projectile.localAI[0]);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(0, 1, 1)));

            v1 = new Vector2(300, 300).RotatedBy(Projectile.localAI[0]);
            vertices.Add(new ColoredVertex(Projectile.Center - Main.screenPosition
                + new Vector2(v1.X / a, v1.Y / b).RotatedBy(flipRotation), finalColor, new Vector3(1, 1, 1)));

            Main.graphics.GraphicsDevice.Textures[0] = Projectile.ai[0] == 0
                ? MuraSlayAllAssets.Roundtry
                : MuraSlayAllAssets.Roundtry2;

            if (vertices.Count > 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices.ToArray(), 0, vertices.Count - 2);
            }
        }
    }
}
