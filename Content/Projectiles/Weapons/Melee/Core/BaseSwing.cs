﻿using CalamityMod;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Common.Effects;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core
{
    internal abstract class BaseSwing : BaseHeldProj
    {
        #region Data
        private float oldRot;
        protected Vector2 vector;
        protected Vector2 startVector;
        int dirs;
        /// <summary>
        /// 弹幕实体中心偏离值，默认为60
        /// </summary>
        public float Length = 60;
        /// <summary>
        /// 弹幕实体中心偏离值，默认为60，该属性不应该直接进行更新，仅仅用于还原
        /// </summary>
        public float OrigLength = 60;
        /// <summary>
        /// 旋转角度，默认为MathHelper.ToRadians(3)
        /// </summary>
        public float Rotation;
        /// <summary>
        /// 旋转速度
        /// </summary>
        protected float rotSpeed;
        protected float shootSengs = 0.5f;
        /// <summary>
        /// 基本速度
        /// </summary>
        protected float speed;
        /// <summary>
        /// 一个挥舞周期的最大时间，默认为22
        /// </summary>
        protected int maxSwingTime = 22;
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
        protected bool canShoot;
        /// <summary>
        /// 是否跟随玩家进行朝向纠正，默认为<see langword="true"/>
        /// </summary>
        protected bool canFormOwnerSetDir = true;
        /// <summary>
        /// 玩家朝向将锁定到弹幕的初始朝向，默认为<see langword="false"/>，如果启用这个，那么<see cref="canFormOwnerSetDir"/>将不再具备意义
        /// </summary>
        protected bool ownerOrientationLock = false;
        /// <summary>
        /// 是否自动设置玩家手臂动作，默认为<see langword="true"/>
        /// </summary>
        protected bool canSetOwnerArmBver = true;
        /// <summary>
        /// 绘制中是否进行对角线翻转
        /// </summary>
        protected bool inDrawFlipdiagonally;
        public virtual string trailTexturePath => "";
        public virtual string gradientTexturePath => "";
        public Texture2D TrailTexture => SwingSystem.trailTextures[Type].Value;
        public Texture2D GradientTexture => SwingSystem.gradientTextures[Type].Value;
        #endregion
        public sealed override void SetDefaults() {
            Length = OrigLength;
            if (PreSetSwingProperty()) {
                Projectile.tileCollide = false;
                Projectile.scale = 1f;
                Projectile.friendly = true;
                Projectile.penetrate = -1;
                Rotation = MathHelper.ToRadians(3);
                SetSwingProperty();
                trailCount = 15 * updateCount;
                oldRotate = new float[trailCount];
                oldDistanceToOwner = new float[trailCount];
                oldLength = new float[trailCount];
                InitializeCaches();
            }
            PostSwingProperty();
            OrigLength = Length;
        }

        #region Utils
        public Vector2 RodingToVer(float radius, float theta) {
            Vector2 vector2 = theta.ToRotationVector2();
            vector2.X *= radius;
            vector2.Y *= radius;
            return vector2;
        }

        public float SetSwingSpeed(float speed) => speed / Owner.GetAttackSpeed(Projectile.DamageType);

        protected virtual void InitializeCaches() {
            for (int j = trailCount - 1; j >= 0; j--) {
                oldRotate[j] = 100f;
                oldDistanceToOwner[j] = distanceToOwner;
                oldLength[j] = Projectile.height * Projectile.scale;
            }
        }

        /// <summary>
        /// 模拟出一个勉强符合物理逻辑的命中粒子效果，最好不要动这些，这个效果是我凑出来的，我也不清楚这具体的数学逻辑，代码太乱了
        /// </summary>
        /// <param name="target"></param>
        /// <param name="sparkCount"></param>
        /// <param name="rotToTargetSpeedTrengsVumVer"></param>
        /// <param name="newSparkCount"></param>
        protected void HitEffectValue(Entity target, int sparkCount, out Vector2 rotToTargetSpeedTrengsVumVer, out int newSparkCount) {
            Vector2 toTarget = Owner.Center.To(target.Center);
            Vector2 norlToTarget = toTarget.GetNormalVector();
            int ownerToTargetSetDir = Math.Sign(toTarget.X);
            if (ownerToTargetSetDir != DirSign) {
                ownerToTargetSetDir = -1;
            }
            else {
                ownerToTargetSetDir = 1;
            }

            if (rotSpeed > 0) {
                norlToTarget *= -1;
            }
            if (rotSpeed < 0) {
                norlToTarget *= 1;
            }

            int pysCount = DRKLoader.GetParticlesCount(DRKLoader.GetParticleType(typeof(PRK_Spark)));
            if (pysCount > 120) {
                sparkCount = 10;
            }
            if (pysCount > 220) {
                sparkCount = 8;
            }
            if (pysCount > 350) {
                sparkCount = 6;
            }
            if (pysCount > 500) {
                sparkCount = 3;
            }

            newSparkCount = sparkCount;

            float rotToTargetSpeedSengs = rotSpeed * 3 * ownerToTargetSetDir;
            rotToTargetSpeedTrengsVumVer = norlToTarget.RotatedBy(-rotToTargetSpeedSengs) * 13;
        }

        public virtual void SwingBever(float starArg = 33, float baseSwingSpeed = 4
            , float ler1_UpLengthSengs = 0.08f, float ler1_UpSpeedSengs = 0.1f, float ler1_UpSizeSengs = 0.012f
            , float ler2_DownLengthSengs = 0.01f, float ler2_DownSpeedSengs = 0.1f, float ler2_DownSizeSengs = 0
            , int minClampLength = 0, int maxClampLength = 0, int ler1Time = 10, int maxSwingTime = 0) {
            if (minClampLength == 0) {
                minClampLength = (int)(OrigLength * 1.2f);
            }
            if (maxClampLength == 0) {
                maxClampLength = (int)(OrigLength * 1.4f);
            }
            if (maxSwingTime == 0) {
                maxSwingTime = this.maxSwingTime;
            }
            if (Time == 0) {
                Rotation = MathHelper.ToRadians(starArg * -Owner.direction);
                startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                speed = MathHelper.ToRadians(baseSwingSpeed) / SetSwingSpeed(1);
            }
            if (Time < ler1Time * SetSwingSpeed(1)) {
                Length *= 1 + ler1_UpLengthSengs / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 + ler1_UpSpeedSengs / updateCount;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale += ler1_UpSizeSengs;
            }
            else {
                Length *= 1 - ler2_DownLengthSengs / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 - ler2_DownSpeedSengs / updateCount / SetSwingSpeed(1);
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale -= ler2_DownSizeSengs;
            }
            if (Time >= maxSwingTime * updateCount * SetSwingSpeed(1)) {
                Projectile.Kill();
            }
            if (Time % updateCount == updateCount - 1) {
                Length = MathHelper.Clamp(Length, minClampLength, maxClampLength);
            }
        }

        #endregion
        public override bool ShouldUpdatePosition() => false;

        public virtual bool PreSetSwingProperty() { return true; }

        public virtual void SetSwingProperty() { }

        public virtual void PostSwingProperty() { }

        public virtual void Shoot() { }
        /// <summary>
        /// 处理一些与玩家相关的逻辑，比如跟随和初始化一些基本数据，运行在<see cref="SwingAI"/>之前
        /// </summary>
        public virtual void InOwner() {
            if (Time == 0) {
                dirs = Projectile.spriteDirection = Owner.direction;
            }

            Projectile.Calamity().timesPierced = 0;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + vector;

            if (canFormOwnerSetDir) {
                Projectile.spriteDirection = Owner.direction;
            }
            if (canSetOwnerArmBver) {
                Owner.SetCompositeArmFront(true, Length >= 80 ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.Quarter
                    , (Owner.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2);
            }
            if (ownerOrientationLock) {
                Owner.direction = Projectile.spriteDirection = dirs;
            }

            if (Projectile.spriteDirection == 1) {
                Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() + MathHelper.PiOver4;
            }
            else {
                Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() - MathHelper.Pi - MathHelper.PiOver4;
            }
        }

        public virtual bool PreInOwnerUpdate() { return true; }
        public virtual void PostInOwnerUpdare() { }

        /// <summary>
        /// 几乎所有的逻辑更新都在这里进行
        /// </summary>
        /// <returns></returns>
        public sealed override bool PreUpdate() {
            canShoot = Time == (int)(maxSwingTime * shootSengs);
            if (PreInOwnerUpdate()) {
                InOwner();
                SwingAI();
                if (Projectile.IsOwnedByLocalPlayer() && canShoot) {
                    Shoot();
                }
                UpdateCaches();
                rotSpeed = Rotation - oldRot;
                oldRot = Rotation;
                Time++;
                canShoot = false;
            }
            PostInOwnerUpdare();
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
        public virtual void WarpDraw() {
            List<CustomVertexInfo> bars = new List<CustomVertexInfo>();
            GetCurrentTrailCount(out float count);

            float w = 1f;
            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f)
                    continue;

                float factor = 1f - i / count;
                Vector2 Center = Owner.GetPlayerStabilityCenter();
                float r = oldRotate[i] % 6.18f;
                float dir = (r >= 3.14f ? r - 3.14f : r + 3.14f) / MathHelper.TwoPi;
                Vector2 Top = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] + trailTopWidth + oldDistanceToOwner[i]);
                Vector2 Bottom = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] - ControlTrailBottomWidth(factor) * 1.25f + oldDistanceToOwner[i]);

                bars.Add(new CustomVertexInfo(Top, new Color(dir, w, 0f, 15), new Vector3(factor, 0f, w)));
                bars.Add(new CustomVertexInfo(Bottom, new Color(dir, w, 0f, 15), new Vector3(factor, 1f, w)));
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, SamplerState.PointWrap, DepthStencilState.Default, RasterizerState.CullNone);

            Matrix projection = Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, 0f, 1f);
            Matrix model = Matrix.CreateTranslation(new Vector3(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)) * Main.GameViewMatrix.TransformationMatrix;
            Effect effect = EffectLoader.KnifeDistortion;
            effect.Parameters["uTransform"].SetValue(model * projection);
            Main.graphics.GraphicsDevice.Textures[0] = TrailTexture;
            Main.graphics.GraphicsDevice.SamplerStates[0] = SamplerState.PointWrap;
            effect.CurrentTechnique.Passes[0].Apply();
            if (bars.Count >= 3) {
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(0, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

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

        public virtual void DrawTrail(List<VertexPositionColorTexture> bars) {
            Effect effect = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "KnifeRendering").Value;

            effect.Parameters["transformMatrix"].SetValue(GetTransfromMaxrix());
            effect.Parameters["sampleTexture"].SetValue(TrailTexture);
            effect.Parameters["gradientTexture"].SetValue(GradientTexture);
            //应用shader，并绘制顶点
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }
        }

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
            if (inDrawFlipdiagonally) {
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