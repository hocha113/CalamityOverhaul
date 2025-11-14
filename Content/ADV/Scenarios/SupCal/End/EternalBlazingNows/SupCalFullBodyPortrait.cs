using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 至尊灾厄女巫全身立绘演出
    /// 用于女巫告别场景，带有红色淡入效果和最终燃烧消失演出
    /// </summary>
    internal class SupCalFullBodyPortrait : FullBodyPortraitBase
    {
        public override string PortraitKey => "SupremeCalamitasFullBody";

        //演出状态
        private enum PerformanceState
        {
            Inactive,         //未激活
            WaitingDialogue,  //等待对话框淡入
            PortraitFadeIn,   //立绘淡入
            Hold,             //保持显示
            BurningDissolve   //燃烧消失
        }

        private PerformanceState currentState = PerformanceState.Inactive;

        //淡入参数
        private float fadeInProgress = 0f;
        private const float FadeInDuration = 120f; //立绘淡入持续时间(帧)

        //燃烧消失参数
        private float burnProgress = 0f;
        private const float BurnDuration = 180f; //燃烧消失持续时间(帧)
        private float burnHeight = 0f; //燃烧高度（从下往上，0-1）
        private float fireAnimationTimer = 0f;
        
        //火焰粒子系统
        private class FireParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public float RotationSpeed;
            public float Alpha;
            public int FrameIndex;
        }
        
        private readonly System.Collections.Generic.List<FireParticle> fireParticles = new();
        private int particleSpawnTimer = 0;

        //颜色覆盖
        private Color currentTint = Color.White;

        protected override float FadeSpeed => 0.05f;

        protected override void OnInitialize()
        {
            currentState = PerformanceState.Inactive;
            fadeInProgress = 0f;
            burnProgress = 0f;
            burnHeight = 0f;
            fireAnimationTimer = 0f;
            currentTint = Color.White;
            fireParticles.Clear();
            particleSpawnTimer = 0;
        }

        protected override void OnStartPerformance()
        {
            //首先等待对话框完全淡入
            currentState = PerformanceState.WaitingDialogue;
            fadeInProgress = 0f;
            SetBlockAdvance(false);
        }

        protected override void OnEndPerformance()
        {
            //触发燃烧消失演出
            if (currentState != PerformanceState.BurningDissolve)
            {
                StartBurningDissolve();
            }
        }

        /// <summary>
        /// 启动燃烧消失演出
        /// </summary>
        public void StartBurningDissolve()
        {
            currentState = PerformanceState.BurningDissolve;
            burnProgress = 0f;
            burnHeight = 0f;
            fireParticles.Clear();
            SetBlockAdvance(true);
            SetBlockClose(true);
        }

        protected override void OnUpdate()
        {
            fireAnimationTimer += 0.12f;
            if (fireAnimationTimer > 16f) //16帧循环
            {
                fireAnimationTimer -= 16f;
            }

            switch (currentState)
            {
                case PerformanceState.Inactive:
                    break;

                case PerformanceState.WaitingDialogue:
                    UpdateWaitingDialogue();
                    break;

                case PerformanceState.PortraitFadeIn:
                    UpdatePortraitFadeIn();
                    break;

                case PerformanceState.Hold:
                    UpdateHold();
                    break;

                case PerformanceState.BurningDissolve:
                    UpdateBurningDissolve();
                    break;
            }

            scale = 1.4f;
        }

        /// <summary>
        /// 等待对话框完全淡入
        /// </summary>
        private void UpdateWaitingDialogue()
        {
            if (ownerDialogue != null && ownerDialogue.showProgress >= 1f)
            {
                //对话框已完全淡入，开始立绘淡入
                currentState = PerformanceState.PortraitFadeIn;
                fadeInProgress = 0f;
            }
        }

        /// <summary>
        /// 更新立绘淡入状态
        /// </summary>
        private void UpdatePortraitFadeIn()
        {
            fadeInProgress++;

            if (fadeInProgress >= FadeInDuration)
            {
                currentState = PerformanceState.Hold;
                fadeInProgress = FadeInDuration;
                TargetFade = 1f;
                return;
            }

            float t = fadeInProgress / FadeInDuration;
            float eased = CWRUtils.EaseOutCubic(t);
            
            TargetFade = eased;
            currentTint = Color.White;
        }

        /// <summary>
        /// 更新保持显示状态
        /// </summary>
        private void UpdateHold()
        {
            currentTint = Color.White;
            TargetFade = 1f;
        }

        /// <summary>
        /// 更新燃烧消失状态
        /// </summary>
        private void UpdateBurningDissolve()
        {
            burnProgress++;

            if (burnProgress >= BurnDuration)
            {
                Active = false;
                currentState = PerformanceState.Inactive;
                SetBlockAdvance(false);
                SetBlockClose(false);
                return;
            }

            float t = burnProgress / BurnDuration;

            //燃烧从底部向上推进
            burnHeight = CWRUtils.EaseInOutQuad(t);

            //生成火焰粒子
            particleSpawnTimer++;
            if (particleSpawnTimer >= 2 && burnHeight > 0.01f)
            {
                particleSpawnTimer = 0;
                SpawnFireParticles();
            }

            //更新现有粒子
            for (int i = fireParticles.Count - 1; i >= 0; i--)
            {
                var particle = fireParticles[i];
                particle.Life++;
                
                if (particle.Life >= particle.MaxLife)
                {
                    fireParticles.RemoveAt(i);
                    continue;
                }

                //粒子向上飘动
                particle.Position += particle.Velocity;
                particle.Velocity.Y -= 0.08f; //向上加速
                particle.Velocity.X *= 0.98f; //空气阻力
                particle.Rotation += particle.RotationSpeed;

                //淡出
                float lifeRatio = particle.Life / particle.MaxLife;
                particle.Alpha = 1f - CWRUtils.EaseInQuad(lifeRatio);
            }

            //整体透明度：立绘在燃烧过程中逐渐消失
            TargetFade = 1f - CWRUtils.EaseInCubic(t * 0.8f);
        }

        /// <summary>
        /// 生成火焰粒子
        /// </summary>
        private void SpawnFireParticles()
        {
            if (ADVAsset.SupCalADV == null) return;

            Vector2 portraitSize = ADVAsset.SupCalADV.Size() * scale;
            
            //在燃烧边缘生成粒子
            float edgeY = position.Y + portraitSize.Y * (1f - burnHeight);
            
            int particleCount = (int)(8f + (float)Math.Sin(timer * 0.1f) * 3f);
            for (int i = 0; i < particleCount; i++)
            {
                float xOffset = Main.rand.NextFloat(-10f, portraitSize.X + 10f);
                float yOffset = Main.rand.NextFloat(-20f, 20f);
                
                fireParticles.Add(new FireParticle
                {
                    Position = new Vector2(position.X + xOffset, edgeY + yOffset),
                    Velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-2.5f, -0.8f)),
                    Life = 0f,
                    MaxLife = Main.rand.NextFloat(35f, 70f),
                    Scale = Main.rand.NextFloat(0.6f, 1.2f),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f),
                    Alpha = 1f,
                    FrameIndex = Main.rand.Next(16)
                });
            }
        }

        protected override void OnDraw(SpriteBatch spriteBatch, float alpha)
        {
            Texture2D portrait = ADVAsset.SupCalADV;
            if (portrait == null)
            {
                return;
            }

            position = ownerDialogue.GetPanelRect().Top() + new Vector2(-portrait.Width + 60, -portrait.Height + 20) * scale;
            Vector2 portraitSize = portrait.Size() * scale;

            //燃烧状态下的特殊绘制
            if (currentState == PerformanceState.BurningDissolve)
            {
                DrawBurningPortrait(spriteBatch, position, portraitSize, alpha);
            }
            else
            {
                //正常绘制
                Color drawColor = currentTint * alpha;
                spriteBatch.Draw(portrait, position, null, drawColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 绘制燃烧中的立绘
        /// </summary>
        private void DrawBurningPortrait(SpriteBatch spriteBatch, Vector2 pos, Vector2 size, float alpha)
        {
            Texture2D portrait = ADVAsset.SupCalADV;
            Texture2D fireMask = CWRAsset.Fire?.Value;
            
            if (fireMask == null)
            {
                //降级到简单绘制
                spriteBatch.Draw(portrait, pos, null, currentTint * alpha, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            //计算火焰帧
            int frameWidth = fireMask.Width / 4;
            int frameHeight = fireMask.Height / 4;

            //1. 绘制未燃烧的上半部分（正常显示）
            if (burnHeight < 1f)
            {
                int unburntSourceHeight = (int)(portrait.Height * (1f - burnHeight));
                if (unburntSourceHeight > 0)
                {
                    Rectangle unburntSource = new Rectangle(0, 0, portrait.Width, unburntSourceHeight);
                    Vector2 unburntPos = pos;
                    Color unburntColor = currentTint * alpha;
                    
                    spriteBatch.Draw(portrait, unburntPos, unburntSource, unburntColor, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            //2. 绘制燃烧边缘效果
            if (burnHeight > 0f)
            {
                //边缘厚度
                float edgeThickness = 80f / portrait.Height; //相对于原图的厚度
                float edgeStart = Math.Max(0f, burnHeight - edgeThickness);
                float edgeEnd = burnHeight;
                
                if (edgeStart < edgeEnd)
                {
                    int edgeSourceY = (int)(portrait.Height * edgeStart);
                    int edgeSourceHeight = (int)(portrait.Height * (edgeEnd - edgeStart));
                    
                    if (edgeSourceHeight > 0)
                    {
                        Rectangle edgeSource = new Rectangle(0, edgeSourceY, portrait.Width, edgeSourceHeight);
                        Vector2 edgePos = pos + new Vector2(0, edgeStart * size.Y);
                        int fireCount = Math.Max(1, (int)(size.X / (frameWidth * scale * 0.5f)));
                        
                        for (int i = 0; i < fireCount; i++)
                        {
                            float xPos = i * (size.X / fireCount);
                            float waveOffset = (float)Math.Sin(fireAnimationTimer * 0.5f + i * 0.8f) * 10f;
                            
                            //计算当前火焰帧
                            int frameOffset = (int)(fireAnimationTimer + i * 2) % 16;
                            int frameX = (frameOffset % 4) * frameWidth;
                            int frameY = (frameOffset / 4) * frameHeight;
                            Rectangle fireFrame = new Rectangle(frameX, frameY, frameWidth, frameHeight);
                            
                            Vector2 firePos = edgePos + new Vector2(xPos, waveOffset);
                            float fireScale = scale * 0.8f;
                            
                            //火焰颜色从黄到橙到红渐变
                            float gradientT = (float)Math.Sin(fireAnimationTimer * 0.3f + i * 0.5f) * 0.5f + 0.5f;
                            Color fireColor1 = new Color(255, 240, 100); //亮黄
                            Color fireColor2 = new Color(255, 150, 50);  //橙色
                            Color fireColor3 = new Color(255, 80, 50);   //深橙红
                            Color fireColor = Color.Lerp(Color.Lerp(fireColor1, fireColor2, gradientT), fireColor3, burnHeight * 0.5f);
                            fireColor.A = 0; //灰度图，A值设为0
                            fireColor *= alpha * 0.9f;
                            
                            spriteBatch.Draw(fireMask, firePos, fireFrame, fireColor, rotation, Vector2.Zero, fireScale, SpriteEffects.None, 0f);
                        }
                    }
                }
            }

            //3. 绘制火焰粒子
            foreach (var particle in fireParticles)
            {
                int frameX = (particle.FrameIndex % 4) * frameWidth;
                int frameY = (particle.FrameIndex / 4) * frameHeight;
                Rectangle particleFrame = new Rectangle(frameX, frameY, frameWidth, frameHeight);
                
                //粒子颜色从亮黄到橙到深红再到灰
                Color particleColor;
                float lifeRatio = particle.Life / particle.MaxLife;
                
                if (lifeRatio < 0.3f)
                {
                    particleColor = Color.Lerp(new Color(255, 240, 120), new Color(255, 180, 80), lifeRatio / 0.3f);
                }
                else if (lifeRatio < 0.6f)
                {
                    particleColor = Color.Lerp(new Color(255, 180, 80), new Color(255, 100, 60), (lifeRatio - 0.3f) / 0.3f);
                }
                else
                {
                    particleColor = Color.Lerp(new Color(255, 100, 60), new Color(100, 100, 100), (lifeRatio - 0.6f) / 0.4f);
                }
                
                particleColor.A = 0; //灰度图，A值设为0
                particleColor *= alpha * particle.Alpha * 0.7f;
                
                float particleScale = particle.Scale * scale * 0.4f;
                
                spriteBatch.Draw(
                    fireMask,
                    particle.Position,
                    particleFrame,
                    particleColor,
                    particle.Rotation,
                    new Vector2(frameWidth / 2f, frameHeight / 2f),
                    particleScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //4. 绘制灰烬粒子（小点）
            if (burnHeight > 0.1f)
            {
                int ashCount = (int)(burnHeight * 15f);
                Texture2D pixel = VaultAsset.placeholder2.Value;
                
                for (int i = 0; i < ashCount; i++)
                {
                    float ashTimer = (timer + i * 5f) * 0.025f;
                    float ashX = pos.X + (float)Math.Sin(ashTimer * 2f + i) * size.X * 0.2f + size.X * Main.rand.NextFloat(0.4f, 0.6f);
                    float ashY = pos.Y + size.Y * (1f - burnHeight) + (ashTimer % 1f) * size.Y * 0.4f;
                    
                    float ashAlpha = (1f - (ashTimer % 1f)) * 0.6f;
                    Color ashColor = new Color(80, 80, 80) * (alpha * ashAlpha);
                    
                    Vector2 ashPos = new Vector2(ashX, ashY);
                    float ashSize = Main.rand.NextFloat(1.5f, 2.5f);
                    
                    spriteBatch.Draw(pixel, ashPos, null, ashColor, 0f, new Vector2(0.5f, 0.5f), ashSize, SpriteEffects.None, 0f);
                }
            }
        }

        protected override void OnDeactivate()
        {
            currentState = PerformanceState.Inactive;
            fireParticles.Clear();
            SetBlockAdvance(false);
            SetBlockClose(false);
        }
    }
}
