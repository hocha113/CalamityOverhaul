using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 深渊复苏死亡系统 - 处理复苏满时的死亡机制和演出
    /// </summary>
    public class ResurrectionDeathSystem : ModPlayer
    {
        #region 死亡状态数据
        /// <summary>
        /// 是否正在进行复苏死亡演出
        /// </summary>
        public bool IsResurrectionDeathActive { get; private set; }

        /// <summary>
        /// 死亡演出计时器
        /// </summary>
        private int deathAnimationTimer = 0;

        /// <summary>
        /// 死亡演出总时长（帧）
        /// </summary>
        private const int DeathAnimationDuration = 240; //4秒

        /// <summary>
        /// 死亡前的短暂预警时间
        /// </summary>
        private const int DeathWarningDuration = 60; //1秒

        /// <summary>
        /// 是否处于死亡预警阶段
        /// </summary>
        private bool isDeathWarning = false;

        /// <summary>
        /// 预警计时器
        /// </summary>
        private int warningTimer = 0;

        /// <summary>
        /// 死亡后复苏值保留比例（0-1）
        /// </summary>
        private const float ResurrectionRetainRatio = 0.8f; //保留80%，即减少20

        /// <summary>
        /// 屏幕震动强度
        /// </summary>
        private float screenShakeIntensity = 0f;

        /// <summary>
        /// 深渊效果粒子列表
        /// </summary>
        private readonly List<AbyssParticle> abyssParticles = new();

        /// <summary>
        /// 深渊触手效果列表
        /// </summary>
        private readonly List<AbyssTentacle> tentacles = new();

        /// <summary>
        /// 暗影扭曲效果强度
        /// </summary>
        private float shadowDistortionIntensity = 0f;

        /// <summary>
        /// 音效播放标记
        /// </summary>
        private bool hasPlayedDeathSound = false;
        #endregion

        #region 系统更新
        public override void PreUpdate() {
            var system = Player.GetResurrectionSystem();
            if (system == null) return;

            //检查是否达到致命阈值
            if (system.IsFull && !Player.dead && !IsResurrectionDeathActive) {
                StartDeathWarning();
            }

            //更新死亡预警
            if (isDeathWarning) {
                UpdateDeathWarning();
            }

            //更新死亡演出
            if (IsResurrectionDeathActive) {
                UpdateDeathAnimation();
            }

            //更新粒子效果
            UpdateParticles();

            //更新触手效果
            UpdateTentacles();
        }

        /// <summary>
        /// 开始死亡预警
        /// </summary>
        private void StartDeathWarning() {
            isDeathWarning = true;
            warningTimer = 0;
            
            //播放警告音效
            SoundEngine.PlaySound(SoundID.Roar with { 
                Volume = 1.2f, 
                Pitch = -0.5f,
                MaxInstances = 1
            }, Player.Center);

            //生成警告文本
            CombatText.NewText(Player.getRect(), Color.DarkRed, 
                "深渊正在吞噬你...", dramatic: true, dot: false);

            //开始生成大量粒子
            for (int i = 0; i < 50; i++) {
                SpawnAbyssParticle(Player.Center, large: true);
            }
        }

        /// <summary>
        /// 更新死亡预警
        /// </summary>
        private void UpdateDeathWarning() {
            warningTimer++;

            //预警期间的视觉效果
            screenShakeIntensity = MathHelper.Lerp(screenShakeIntensity, 8f, 0.1f);
            shadowDistortionIntensity = MathHelper.Lerp(shadowDistortionIntensity, 1f, 0.05f);

            //持续生成粒子
            if (warningTimer % 3 == 0) {
                SpawnAbyssParticle(Player.Center, large: Main.rand.NextBool());
            }

            //生成触手
            if (warningTimer % 10 == 0) {
                SpawnTentacle();
            }

            //预警结束，触发死亡
            if (warningTimer >= DeathWarningDuration) {
                TriggerResurrectionDeath();
            }
        }

        /// <summary>
        /// 触发复苏死亡
        /// </summary>
        private void TriggerResurrectionDeath() {
            isDeathWarning = false;
            IsResurrectionDeathActive = true;
            deathAnimationTimer = 0;
            hasPlayedDeathSound = false;

            //锁定玩家控制
            Player.noItems = true;
            Player.noBuilding = true;

            //播放死亡音效
            SoundEngine.PlaySound(SoundID.NPCDeath59 with { 
                Volume = 1.5f, 
                Pitch = -0.8f 
            }, Player.Center);

            //生成大量死亡粒子
            for (int i = 0; i < 100; i++) {
                SpawnAbyssParticle(Player.Center, large: true);
            }

            //生成更多触手
            for (int i = 0; i < 8; i++) {
                SpawnTentacle();
            }

            Main.NewText("你被深渊吞噬了...", 150, 0, 0);
        }

        /// <summary>
        /// 更新死亡演出
        /// </summary>
        private void UpdateDeathAnimation() {
            deathAnimationTimer++;

            float progress = deathAnimationTimer / (float)DeathAnimationDuration;

            //第一阶段：拉扯效果（0-0.3）
            if (progress < 0.3f) {
                float pullProgress = progress / 0.3f;
                
                //向中心拉扯
                Vector2 pullDirection = Vector2.Zero - Player.Center;
                if (pullDirection.Length() > 10f) {
                    pullDirection.Normalize();
                    Player.velocity += pullDirection * pullProgress * 2f;
                }

                //强烈震动
                screenShakeIntensity = 15f * (1f - pullProgress);
                shadowDistortionIntensity = 1f;

                //大量粒子
                for (int i = 0; i < 3; i++) {
                    SpawnAbyssParticle(Player.Center, large: true);
                }
            }
            //第二阶段：扭曲消失（0.3-0.7）
            else if (progress < 0.7f) {
                float dissolveProgress = (progress - 0.3f) / 0.4f;
                
                //玩家变得半透明并扭曲
                Player.velocity *= 0.9f;
                screenShakeIntensity = MathHelper.Lerp(15f, 5f, dissolveProgress);
                shadowDistortionIntensity = 1f + dissolveProgress * 2f;

                //在50%时播放最终音效
                if (!hasPlayedDeathSound && dissolveProgress >= 0.5f) {
                    hasPlayedDeathSound = true;
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with { 
                        Volume = 1.0f, 
                        Pitch = -1.0f 
                    }, Player.Center);
                }

                //持续粒子
                if (deathAnimationTimer % 2 == 0) {
                    SpawnAbyssParticle(Player.Center, large: true);
                }
            }
            //第三阶段：完全死亡（0.7-1.0）
            else {
                if (deathAnimationTimer == (int)(DeathAnimationDuration * 0.7f)) {
                    //执行真正的死亡
                    ExecuteDeath();
                }
                //淡出效果
                float fadeProgress = (progress - 0.7f) / 0.3f;
                screenShakeIntensity = MathHelper.Lerp(5f, 0f, fadeProgress);
                shadowDistortionIntensity = MathHelper.Lerp(3f, 0f, fadeProgress);
            }

            //演出结束
            if (deathAnimationTimer >= DeathAnimationDuration) {
                EndDeathAnimation();
            }
        }

        /// <summary>
        /// 执行真正的死亡
        /// </summary>
        private void ExecuteDeath() {
            //使用深渊主题的死亡原因
            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(CWRLocText.Instance.BloodAltar_Text3.ToNetworkText(Player.name));
            //杀死玩家
            Player.Hurt(damageSource, int.MaxValue - 1, 0);

            //生成死亡特效
            for (int i = 0; i < 50; i++) {
                Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, 
                    DustID.DungeonWater, 0f, 0f, 100, Color.DarkBlue, 2.5f);
                dust.velocity *= 3f;
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// 结束死亡演出
        /// </summary>
        private void EndDeathAnimation() {
            IsResurrectionDeathActive = false;
            deathAnimationTimer = 0;
            screenShakeIntensity = 0f;
            shadowDistortionIntensity = 0f;
            
            //清理效果
            abyssParticles.Clear();
            tentacles.Clear();
        }
        #endregion

        #region 重生处理
        public override void OnRespawn() {
            var system = Player.GetResurrectionSystem();
            if (system == null) return;

            //死亡后，复苏值不会完全清零，而是保持在危险水平
            //这会给玩家持续的压迫感
            if (system.CurrentValue >= system.MaxValue * 0.95f) {
                float retainedValue = system.MaxValue * ResurrectionRetainRatio;
                system.SetValue(retainedValue, triggerEvents: false);
                //播放不祥音效
                SoundEngine.PlaySound(SoundID.Zombie103 with { 
                    Volume = 0.8f, 
                    Pitch = -0.5f 
                });
            }

            //重置演出状态
            IsResurrectionDeathActive = false;
            isDeathWarning = false;
            warningTimer = 0;
            deathAnimationTimer = 0;
            hasPlayedDeathSound = false;
        }
        #endregion

        #region 粒子效果系统
        /// <summary>
        /// 生成深渊粒子
        /// </summary>
        private void SpawnAbyssParticle(Vector2 center, bool large = false) {
            Vector2 position = center + Main.rand.NextVector2Circular(200, 200);
            Vector2 velocity = (center - position).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 6f);
            
            abyssParticles.Add(new AbyssParticle(position, velocity, large));
        }

        /// <summary>
        /// 更新粒子
        /// </summary>
        private void UpdateParticles() {
            for (int i = abyssParticles.Count - 1; i >= 0; i--) {
                abyssParticles[i].Update();
                if (abyssParticles[i].IsDead) {
                    abyssParticles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 生成触手
        /// </summary>
        private void SpawnTentacle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 startPos = Player.Center + angle.ToRotationVector2() * 400f;
            
            tentacles.Add(new AbyssTentacle(startPos, Player.Center, angle));
        }

        /// <summary>
        /// 更新触手
        /// </summary>
        private void UpdateTentacles() {
            for (int i = tentacles.Count - 1; i >= 0; i--) {
                tentacles[i].Update(Player.Center);
                if (tentacles[i].IsDead) {
                    tentacles.RemoveAt(i);
                }
            }
        }
        #endregion

        #region 渲染效果
        public override void ModifyScreenPosition() {
            //应用屏幕震动
            if (screenShakeIntensity > 0.1f) {
                Main.screenPosition += new Vector2(
                    Main.rand.NextFloat(-screenShakeIntensity, screenShakeIntensity),
                    Main.rand.NextFloat(-screenShakeIntensity, screenShakeIntensity)
                );
                screenShakeIntensity *= 0.95f;
            }
        }

        public override void PostUpdate() {
            //应用死亡演出期间的玩家效果
            if (IsResurrectionDeathActive) {
                //禁用玩家控制
                Player.noItems = true;
                Player.noBuilding = true;
                Player.controlJump = false;
                Player.controlDown = false;
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlUseItem = false;
                Player.controlUseTile = false;
                Player.controlThrow = false;
            }
        }

        /// <summary>
        /// 绘制死亡演出特效
        /// </summary>
        public void DrawDeathEffects(SpriteBatch spriteBatch) {
            if (!IsResurrectionDeathActive && !isDeathWarning) {
                return;
            }
            //绘制触手
            foreach (var tentacle in tentacles) {
                tentacle.Draw(spriteBatch);
            }

            //绘制粒子
            foreach (var particle in abyssParticles) {
                particle.Draw(spriteBatch);
            }

            //绘制屏幕暗化效果
            if (IsResurrectionDeathActive) {
                float progress = deathAnimationTimer / (float)DeathAnimationDuration;
                float darkness = MathHelper.Clamp(progress * 1.5f, 0f, 0.8f);
                
                Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, screenRect, 
                    Color.Black * darkness);
            }
        }
        #endregion
    }

    #region 粒子和特效类
    /// <summary>
    /// 深渊粒子
    /// </summary>
    internal class AbyssParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Rotation;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public bool IsLarge;
        public Color Color;

        public bool IsDead => Life >= MaxLife;

        public AbyssParticle(Vector2 position, Vector2 velocity, bool large) {
            Position = position;
            Velocity = velocity;
            IsLarge = large;
            Scale = large ? Main.rand.NextFloat(1.5f, 3f) : Main.rand.NextFloat(0.8f, 1.5f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Alpha = 1f;
            Life = 0;
            MaxLife = large ? Main.rand.Next(60, 120) : Main.rand.Next(30, 60);
            
            //深渊主题颜色
            Color = Main.rand.Next(4) switch {
                0 => new Color(5, 10, 40),
                1 => new Color(10, 5, 50),
                2 => new Color(20, 10, 60),
                _ => new Color(5, 20, 45)
            };
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.97f;
            Rotation += 0.05f;

            float lifeRatio = Life / (float)MaxLife;
            Alpha = 1f - lifeRatio;
            Scale *= 0.99f;
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (IsDead) return;

            Texture2D texture = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            Color drawColor = Color * Alpha;

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, 
                drawColor, Rotation, texture.Size() / 2f, Scale, 
                SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 深渊触手
    /// </summary>
    internal class AbyssTentacle
    {
        public Vector2 StartPosition;
        public Vector2 TargetPosition;
        public float Angle;
        public float Progress;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public List<Vector2> Segments;
        private const int SegmentCount = 12;

        public bool IsDead => Life >= MaxLife;

        public AbyssTentacle(Vector2 start, Vector2 target, float angle) {
            StartPosition = start;
            TargetPosition = target;
            Angle = angle;
            Progress = 0f;
            Alpha = 1f;
            Life = 0;
            MaxLife = 120;
            Segments = new List<Vector2>();
            
            //初始化分段
            for (int i = 0; i < SegmentCount; i++) {
                Segments.Add(start);
            }
        }

        public void Update(Vector2 playerCenter) {
            Life++;
            TargetPosition = playerCenter;

            //生长阶段（0-0.3）
            if (Progress < 0.3f) {
                Progress += 0.02f;
            }
            //保持阶段（0.3-0.7）
            else if (Progress < 0.7f) {
                Progress += 0.005f;
            }
            //消失阶段（0.7-1.0）
            else {
                Progress += 0.02f;
                Alpha *= 0.95f;
            }

            Progress = Math.Min(Progress, 1f);

            //更新触手分段位置
            UpdateSegments();
        }

        private void UpdateSegments() {
            Vector2 direction = TargetPosition - StartPosition;
            float totalLength = direction.Length() * Progress;
            
            for (int i = 0; i < SegmentCount; i++) {
                float segmentProgress = i / (float)(SegmentCount - 1);
                Vector2 basePos = Vector2.Lerp(StartPosition, TargetPosition, 
                    segmentProgress * Progress);
                
                //添加波浪效果
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + 
                    segmentProgress * MathHelper.Pi) * 30f;
                Vector2 perpendicular = Vector2.Normalize(
                    new Vector2(-direction.Y, direction.X));
                
                Segments[i] = basePos + perpendicular * wave;
            }
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (IsDead || Alpha < 0.01f) return;

            Texture2D texture = TextureAssets.Chain12.Value;
            
            for (int i = 0; i < Segments.Count - 1; i++) {
                Vector2 start = Segments[i] - Main.screenPosition;
                Vector2 end = Segments[i + 1] - Main.screenPosition;
                
                Vector2 diff = end - start;
                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                float distance = diff.Length();
                
                Color color = new Color(20, 10, 50) * Alpha * 0.8f;
                
                spriteBatch.Draw(texture, start, null, color, rotation, 
                    new Vector2(texture.Width / 2f, 0), 
                    new Vector2(1f, distance / texture.Height), 
                    SpriteEffects.None, 0f);
            }
        }
    }
    #endregion
}
