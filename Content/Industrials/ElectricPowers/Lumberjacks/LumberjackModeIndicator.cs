using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    /// <summary>
    /// 伐木者模式指示器Actor，显示当前工作模式的图标动画
    /// </summary>
    internal class LumberjackModeIndicator : Actor
    {
        //模式类型(0=循环模式显示橡子 1=清理模式显示斧头)
        private int modeType;
        //动画计时器
        private int animTimer;
        //生命周期
        private int lifeTime;
        private const int MaxLifeTime = 90;
        //初始Y位置
        private float startY;
        //淡入淡出
        private float alpha;
        //斧头摇摆角度
        private float axeSwingAngle;
        //斧头摇摆方向
        private int swingDirection = 1;
        //斧头摇摆速度
        private float swingSpeed;

        public override void OnSpawn(params object[] args) {
            Width = 14;
            Height = 14;
            DrawExtendMode = 200;
            DrawLayer = ActorDrawLayer.Default;

            if (args is not null && args.Length >= 1) {
                modeType = (int)args[0];
            }

            startY = Position.Y;
            lifeTime = 0;
            alpha = 0f;
            animTimer = 0;
            axeSwingAngle = 0f;
            swingDirection = 1;
            swingSpeed = 0f;

            //播放切换音效
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Volume = 0.8f,
                Pitch = modeType == 0 ? 0.2f : -0.1f
            }, Center);
        }

        public override void AI() {
            lifeTime++;
            animTimer++;

            //淡入阶段
            if (lifeTime < 15) {
                alpha = lifeTime / 15f;
            }
            //淡出阶段
            else if (lifeTime > MaxLifeTime - 20) {
                alpha = (MaxLifeTime - lifeTime) / 20f;
            }
            else {
                alpha = 1f;
            }

            //上升动画
            float riseProgress = Math.Min(lifeTime / 30f, 1f);
            float easeRise = 1f - (1f - riseProgress) * (1f - riseProgress);
            Position.Y = startY - easeRise * 40f;

            //根据模式类型执行不同动画
            if (modeType == 1) {
                //斧头摇摆动画
                UpdateAxeSwing();
            }
            else {
                //橡子漂浮动画
                UpdateAcornFloat();
            }

            //生成粒子效果
            SpawnModeParticles();

            //生命结束
            if (lifeTime >= MaxLifeTime) {
                ActorLoader.KillActor(WhoAmI);
            }
        }

        private void UpdateAxeSwing() {
            //斧头来回摇摆模拟砍伐动作
            swingSpeed += 0.008f * swingDirection;

            //限制摇摆速度
            if (swingSpeed > 0.15f) {
                swingSpeed = 0.15f;
                swingDirection = -1;
            }
            else if (swingSpeed < -0.15f) {
                swingSpeed = -0.15f;
                swingDirection = 1;
            }

            axeSwingAngle += swingSpeed;

            //限制摇摆角度范围
            float maxAngle = MathHelper.ToRadians(35f);
            axeSwingAngle = MathHelper.Clamp(axeSwingAngle, -maxAngle, maxAngle);

            //添加轻微的缩放效果
            float swingIntensity = Math.Abs(swingSpeed) / 0.15f;
            Scale = 1f + swingIntensity * 0.1f;
        }

        private void UpdateAcornFloat() {
            //橡子轻柔漂浮动画
            float floatOffset = (float)Math.Sin(animTimer * 0.08f) * 3f;
            Position.Y += floatOffset * 0.1f;

            //轻微旋转
            Rotation = (float)Math.Sin(animTimer * 0.05f) * MathHelper.ToRadians(10f);

            //轻微缩放呼吸效果
            Scale = 1f + (float)Math.Sin(animTimer * 0.1f) * 0.05f;
        }

        private void SpawnModeParticles() {
            if (Main.netMode == NetmodeID.Server) return;
            if (lifeTime % 5 != 0) return;

            if (modeType == 1) {
                //斧头模式：金属火花粒子
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f));
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 8, 16, 16, DustID.Iron, dustVel.X, dustVel.Y, 100, default, 0.8f);
                dust.noGravity = true;
                dust.fadeIn = 0.8f;
            }
            else {
                //橡子模式：绿色自然粒子
                Vector2 dustVel = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1f, 0.5f));
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 8, 16, 16, DustID.Grass, dustVel.X, dustVel.Y, 100, default, 0.9f);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            Texture2D texture;
            Vector2 origin;
            float finalRotation;
            float finalScale = Scale;

            if (modeType == 1) {
                //绘制斧头(使用铁斧纹理)
                texture = TextureAssets.Item[ItemID.IronAxe].Value;
                origin = texture.Size() / 2f;
                finalRotation = axeSwingAngle + MathHelper.ToRadians(-45f);
            }
            else {
                //绘制橡子
                texture = TextureAssets.Item[ItemID.Acorn].Value;
                origin = texture.Size() / 2f;
                finalRotation = Rotation;
            }

            Color color = Lighting.GetColor((int)(Center.X / 16), (int)(Center.Y / 16));
            color *= alpha;

            //绘制发光背景
            float glowPulse = 0.5f + (float)Math.Sin(animTimer * 0.15f) * 0.3f;
            Color glowColor = modeType == 1 ? new Color(255, 200, 100) : new Color(100, 255, 100);
            glowColor *= alpha * glowPulse * 0.4f;

            //绘制光晕
            spriteBatch.Draw(CWRAsset.SoftGlow.Value, Center - Main.screenPosition,
                null, glowColor with { A = 0 }, 0f,
                CWRAsset.SoftGlow.Size() / 2, finalScale * 4f, SpriteEffects.None, 0f);

            //绘制主图标
            spriteBatch.Draw(texture, Center - Main.screenPosition, null, color, finalRotation, origin, finalScale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
