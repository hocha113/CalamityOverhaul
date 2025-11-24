using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// <summary>
    /// 吸积盘特效渲染器
    /// </summary>
    internal class AccretionDisk : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //吸积盘参数
        public ref float RotationSpeed => ref Projectile.ai[0];
        public ref float InnerRadius => ref Projectile.ai[1];
        public ref float OuterRadius => ref Projectile.ai[2];
        
        private float time;
        private float brightness = 1f;
        private float distortionStrength = 0.15f;
        
        //颜色配置
        private Color innerColor = new Color(255, 200, 100); //内圈 - 明亮的黄橙色
        private Color midColor = new Color(255, 120, 50);    //中圈 - 橙红色
        private Color outerColor = new Color(100, 50, 150);  //外圈 - 紫色

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 400;
            Projectile.height = 400;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //淡入效果
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 5;
            }
            
            time += 0.016f; //假设60fps
            
            //默认参数设置
            if (RotationSpeed == 0) {
                RotationSpeed = 1f;
            }
            if (InnerRadius == 0) {
                InnerRadius = 0.15f;
            }
            if (OuterRadius == 0) {
                OuterRadius = 0.85f;
            }
            
            //脉动效果
            float pulse = (float)Math.Sin(time * 2f) * 0.1f + 0.9f;
            brightness = pulse;
            
            //缓慢旋转整个投射物
            Projectile.rotation += 0.005f;
            
            //生成环绕粒子
            if (Projectile.timeLeft % 3 == 0 && !Main.dedServ) {
                SpawnDiskParticles();
            }
            
            //淡出效果
            if (Projectile.timeLeft < 60) {
                Projectile.alpha += 4;
                brightness *= Projectile.timeLeft / 60f;
            }
        }

        private void SpawnDiskParticles() {
            //在吸积盘边缘生成粒子
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(InnerRadius, OuterRadius) * Projectile.width * 0.5f;
            
            Vector2 offset = new Vector2(
                (float)Math.Cos(angle) * distance,
                (float)Math.Sin(angle) * distance
            );
            
            Vector2 particlePos = Projectile.Center + offset;
            Vector2 particleVel = Vector2.Normalize(offset.RotatedBy(MathHelper.PiOver2)) * Main.rand.NextFloat(1f, 3f);
            
            int dustType = Main.rand.Next(new[] { 6, 259, 158 }); //火焰、发光、魔法类型的尘埃
            Dust dust = Dust.NewDustPerfect(particlePos, dustType, particleVel, 100, 
                Color.Lerp(innerColor, outerColor, Main.rand.NextFloat()), Main.rand.NextFloat(1.5f, 2.5f));
            dust.noGravity = true;
            dust.fadeIn = 1.2f;
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer) {
                return;
            }

            DrawAccretionDisk();
        }

        [VaultLoaden(CWRConstant.Masking)]
        private static Texture2D TransverseTwill;

        private void DrawAccretionDisk() {
            SpriteBatch spriteBatch = Main.spriteBatch;
            
            //准备渲染状态
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.AccretionDisk.Value;
            
            //计算实际渲染尺寸
            float actualWidth = Projectile.width * Projectile.scale;
            float actualHeight = Projectile.height * Projectile.scale;
            
            //世界空间到屏幕空间的变换矩阵
            //这里不需要复杂的矩阵变换，shader中会处理纹理坐标
            Matrix world = Matrix.Identity;
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Matrix projection = Matrix.CreateOrthographicOffCenter(
                0, Main.screenWidth, 
                Main.screenHeight, 0, 
                -1, 1);
            
            //组合矩阵
            Matrix finalMatrix = world * view * projection;
            
            shader.Parameters["transformMatrix"]?.SetValue(finalMatrix);
            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["rotationSpeed"]?.SetValue(RotationSpeed);
            shader.Parameters["innerRadius"]?.SetValue(InnerRadius);
            shader.Parameters["outerRadius"]?.SetValue(OuterRadius);
            shader.Parameters["brightness"]?.SetValue(brightness);
            shader.Parameters["distortionStrength"]?.SetValue(distortionStrength);
            
            //设置中心位置
            Vector2 screenCenter = Projectile.Center - Main.screenPosition;
            shader.Parameters["centerPos"]?.SetValue(screenCenter);
            
            //设置颜色
            shader.Parameters["innerColor"]?.SetValue(innerColor.ToVector4());
            shader.Parameters["midColor"]?.SetValue(midColor.ToVector4());
            shader.Parameters["outerColor"]?.SetValue(outerColor.ToVector4());
            
            //设置噪声纹理
            Main.graphics.GraphicsDevice.Textures[1] = TransverseTwill;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            
            shader.CurrentTechnique.Passes["AccretionDiskPass"].Apply();
            
            //计算绘制
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            //绘制一个简单的四边形，shader会处理所有的视觉效果
            //使用TransverseTwill作为基础纹理，但实际效果由shader生成
            spriteBatch.Draw(
                TransverseTwill,
                drawPosition,
                null, //使用完整纹理
                Color.White * (1f - Projectile.alpha / 255f),
                Projectile.rotation,
                TransverseTwill.Size() * 0.5f, //使用纹理中心作为原点
                new Vector2(actualWidth / TransverseTwill.Width, actualHeight / TransverseTwill.Height), //缩放到目标大小
                SpriteEffects.None,
                0
            );
            
            //恢复默认渲染状态
            spriteBatch.End();
        }
    }
}
