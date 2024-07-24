using CalamityMod;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal abstract class BaseSwing : BaseHeldProj
    {
        #region Data
        private float oldRot;
        protected Vector2 vector;
        protected Vector2 startVector;
        /// <summary>
        /// 弹幕实体中心偏离值，默认为60
        /// </summary>
        public float Length = 60;
        /// <summary>
        /// 旋转角度，默认为MathHelper.ToRadians(3)
        /// </summary>
        public float Rotation;
        /// <summary>
        /// 旋转速度
        /// </summary>
        protected float rotSpeed;
        /// <summary>
        /// 基本速度
        /// </summary>
        protected float speed;
        /// /// <summary>
        /// 总时间，记录更新，在每帧的最后更新中自行加1
        /// </summary>
        protected int Time;
        /// <summary>
        /// 弧光的采样点数，默认为15 * <see cref="updateCount"/>
        /// </summary>
        protected int trailCount;
        /// <summary>
        /// 绝对中心距离玩家的距离，默认为75
        /// </summary>
        protected int distanceToOwner = 75;
        /// <summary>
        /// 弧光宽度，默认为50
        /// </summary>
        protected float trailTopWidth = 50;
        protected float[] oldRotate;
        protected float[] oldLength;
        protected float[] oldDistanceToOwner;
        /// <summary>
        /// 更新率，值为<see cref="Projectile.extraUpdates"/>+1，使用之前请注意<see cref="Projectile.extraUpdates"/>是否已经被正确设置
        /// </summary>
        internal int updateCount => Projectile.extraUpdates + 1;
        /// <summary>
        /// 是否绘制弧光，默认为<see langword="false"/>
        /// </summary>
        protected bool canDrawSlashTrail;
        public virtual string trailTexturePath => "";
        public virtual string gradientTexturePath => "";
        public Texture2D TrailTexture => SwingSystem.trailTextures[Type].Value;
        public Texture2D GradientTexture => SwingSystem.gradientTextures[Type].Value;
        #endregion
        public sealed override void SetDefaults() {
            if (PreSetSwingProperty()) {
                SetSwingProperty();
                AIType = Projectile.aiStyle = 0;
                Projectile.scale = 1f;
                Projectile.friendly = true;
                Projectile.penetrate = -1;
                trailCount = 15 * updateCount;
                oldRotate = new float[trailCount];
                oldDistanceToOwner = new float[trailCount];
                oldLength = new float[trailCount];
                Rotation = MathHelper.ToRadians(3);
                InitializeCaches();
            }
            PostSwingProperty();
        }

        #region Utils
        public Vector2 RodingToVer(float radius, float theta) {
            Vector2 vector2 = theta.ToRotationVector2();
            vector2.X *= radius;
            vector2.Y *= radius;
            return vector2;
        }

        protected virtual void InitializeCaches() {
            for (int j = trailCount - 1; j >= 0; j--) {
                oldRotate[j] = 100f;
                oldDistanceToOwner[j] = distanceToOwner;
                oldLength[j] = Projectile.height * Projectile.scale;
            }
        }
        #endregion
        public override bool ShouldUpdatePosition() => false;

        public virtual bool PreSetSwingProperty() { return true; }

        public virtual void SetSwingProperty() { }

        public virtual void PostSwingProperty() { }
        /// <summary>
        /// 处理一些与玩家相关的逻辑，比如跟随和初始化一些基本数据，运行在<see cref="SwingAI"/>之前
        /// </summary>
        public virtual void InOwner() {
            Projectile.Calamity().timesPierced = 0;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + vector;

            if (Projectile.ai[0] != 6) {
                Projectile.spriteDirection = Owner.direction;
                Owner.SetCompositeArmFront(true, Length >= 80 ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.Quarter
                    , (Owner.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2);
            }

            if (Projectile.spriteDirection == 1) {
                Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() + MathHelper.PiOver4;
            }
            else {
                Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() - MathHelper.Pi - MathHelper.PiOver4;
            }
        }
        /// <summary>
        /// 几乎所有的逻辑更新将在这里进行
        /// </summary>
        /// <returns></returns>
        public sealed override bool PreUpdate() {
            InOwner();
            SwingAI();
            UpdateCaches();
            rotSpeed = Rotation - oldRot;
            oldRot = Rotation;
            Time++;
            return false;
        }
        /// <summary>
        /// 用这个函数来处理挥舞相关的逻辑更新，运行在<see cref="InOwner"/>之后，<see cref="UpdateCaches"/>之前
        /// </summary>
        public virtual void SwingAI() {

        }
        /// <summary>
        /// 暂时弃用的
        /// </summary>
        public sealed override void AI() { }

        #region Draw
        public virtual void GetCurrentTrailCount(out float count) {
            count = 0f;
            if (oldRotate == null)
                return;

            for (int i = 0; i < oldRotate.Length; i++)
                if (oldRotate[i] != 100f)
                    count += 1f;
        }
        /// <summary>
        /// 在逻辑帧<see cref="PreUpdate"/>中被最后调用，用于更新弧光相关的点数据
        /// </summary>
        public virtual void UpdateCaches() {
            if (Time < 2) {
                return;
            }

            for (int i = trailCount - 1; i > 0; i--) {
                oldRotate[i] = oldRotate[i - 1];
                oldDistanceToOwner[i] = oldDistanceToOwner[i - 1];
                oldLength[i] = oldLength[i - 1];
            }

            oldRotate[0] = (Projectile.Center - Owner.Center).ToRotation();
            oldDistanceToOwner[0] = distanceToOwner;
            oldLength[0] = Projectile.height * Projectile.scale;
        }

        public void DrawTrailHander(List<VertexPositionColorTexture> bars, GraphicsDevice device, BlendState blendState = null
            , SamplerState samplerState = null, RasterizerState rasterizerState = null) {

            RasterizerState originalState = Main.graphics.GraphicsDevice.RasterizerState;
            BlendState originalBlendState = Main.graphics.GraphicsDevice.BlendState;
            SamplerState originalSamplerState = Main.graphics.GraphicsDevice.SamplerStates[0];

            device.BlendState = blendState ?? originalBlendState;
            device.SamplerStates[0] = samplerState ?? originalSamplerState;
            device.RasterizerState = rasterizerState ?? originalState;

            DrawTrail(bars);

            device.RasterizerState = originalState;
            device.BlendState = originalBlendState;
            device.SamplerStates[0] = originalSamplerState;
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        public virtual void DrawTrail(List<VertexPositionColorTexture> bars) { }

        public virtual Matrix GetTransfromMaxrix() {
            Matrix world = Matrix.CreateTranslation(-Main.screenPosition.Vec3());
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
            return world * view * projection;
        }

        public virtual float ControlTrailBottomWidth(float factor) {
            return 70 * Projectile.scale;
        }

        public virtual void DrawSlashTrail() {
            List<VertexPositionColorTexture> bars = new List<VertexPositionColorTexture>();
            GetCurrentTrailCount(out float count);

            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f)
                    continue;

                float factor = 1f - i / count;
                Vector2 Center = Owner.GetPlayerStabilityCenter();
                Vector2 Top = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] + trailTopWidth + oldDistanceToOwner[i]);
                Vector2 Bottom = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] - ControlTrailBottomWidth(factor) + oldDistanceToOwner[i]);

                var topColor = Color.Lerp(new Color(238, 218, 130, 200), new Color(167, 127, 95, 0), 1 - factor);
                var bottomColor = Color.Lerp(new Color(109, 73, 86, 200), new Color(83, 16, 85, 0), 1 - factor);
                bars.Add(new VertexPositionColorTexture(Top.Vec3(), topColor, new Vector2(factor, 0)));
                bars.Add(new VertexPositionColorTexture(Bottom.Vec3(), bottomColor, new Vector2(factor, 1)));
            }

            if (bars.Count > 2) {
                DrawTrailHander(bars, Main.graphics.GraphicsDevice, BlendState.NonPremultiplied, SamplerState.PointWrap, RasterizerState.CullNone);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public virtual void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 v = Projectile.Center - RodingToVer(48, (Projectile.Center - Owner.Center).ToRotation());

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }
            //烦人的对角线翻转代码，我凑出来了这个效果，它很稳靠，但我仍旧不想细究这其中的数学逻辑
            if (Projectile.ai[0] == 1 || Projectile.ai[0] == 5) {
                effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                drawRoting += MathHelper.PiOver2;
            }

            Main.EntitySpriteDraw(texture, v - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, new Rectangle?(rect)
                , Color.White, drawRoting, drawOrigin, Projectile.scale, effects, 0);
        }

        public sealed override bool PreDraw(ref Color lightColor) {
            if (canDrawSlashTrail) {
                DrawSlashTrail();
            }
            DrawSwing(Main.spriteBatch, lightColor);
            return false;
        }
        #endregion
    }
}
