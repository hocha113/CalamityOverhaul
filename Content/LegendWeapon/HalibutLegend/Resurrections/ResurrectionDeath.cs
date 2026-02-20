using CalamityOverhaul.Content.Items.Tools;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 深渊复苏死亡系统，处理复苏满时的死亡机制和演出
    /// </summary>
    public class ResurrectionDeath : ModPlayer, ILocalizedModType
    {
        #region 死亡状态枚举
        /// <summary>
        /// 死亡流程状态
        /// </summary>
        private enum DeathState
        {
            None,           //无状态
            Warning,        //警告阶段
            DeathAnimation, //死亡动画阶段
            Executing,      //执行死亡
            Cooldown        //冷却期（防止重复触发）
        }

        private DeathState currentState = DeathState.None;
        #endregion

        #region 死亡状态数据
        /// <summary>
        /// 死亡演出计时器
        /// </summary>
        private int stateTimer = 0;

        /// <summary>
        /// 警告阶段持续时间
        /// </summary>
        private const int WarningDuration = 60; //1秒

        /// <summary>
        /// 死亡动画持续时间
        /// </summary>
        private const int DeathAnimationDuration = 180; //3秒

        /// <summary>
        /// 冷却时间（防止连续触发）
        /// </summary>
        private const int CooldownDuration = 30; //0.5秒

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
        /// 是否已执行死亡
        /// </summary>
        private bool hasExecutedDeath = false;

        /// <summary>中段音效是否已播放</summary>
        private bool midPhaseSoundPlayed = false;
        /// <summary>
        /// 玩家透明度修改
        /// </summary>
        private float playerAlphaMultiplier = 1f;
        /// <summary>
        /// 由Kill设置，标记玩家已死亡需要在下次PreUpdate时重置状态
        /// （PreUpdate在玩家死亡期间不运行，所以不能依赖Player.dead）
        /// </summary>
        private bool needsDeathReset = false;
        #endregion

        public static LocalizedText DeathText { get; private set; }
        public static LocalizedText WarningCombatText { get; private set; } //深渊正在吞噬你...
        public static LocalizedText DeathNoticeText { get; private set; } //你被深渊吞噬了...
        public static LocalizedText MarkRemainText { get; private set; } //深渊的印记依然缠绕着你...
        public static LocalizedText RetainValueFormat { get; private set; } //复苏值: {0}%

        public override void SetStaticDefaults() {
            DeathText = this.GetLocalization(nameof(DeathText), () => "{0}死于深渊复苏");
            WarningCombatText = this.GetLocalization(nameof(WarningCombatText), () => "深渊正在吞噬你...");
            DeathNoticeText = this.GetLocalization(nameof(DeathNoticeText), () => "你被深渊吞噬了...");
            MarkRemainText = this.GetLocalization(nameof(MarkRemainText), () => "深渊的印记依然缠绕着你...");
            RetainValueFormat = this.GetLocalization(nameof(RetainValueFormat), () => "复苏值: {0}%");
        }

        #region 主更新逻辑
        public override void PreUpdate() {
            if (!Player.Alives()) {
                return;
            }

            var system = Player.GetResurrectionSystem();
            if (system == null) {
                ResetState();
                return;
            }

            //如果Kill中标记了需要重置，在此处执行
            if (needsDeathReset) {
                needsDeathReset = false;
                Respawn();
                return;
            }

            //状态机更新
            UpdateStateMachine(system);

            //更新视觉效果
            UpdateVisualEffects();
        }

        /// <summary>
        /// 状态机更新
        /// </summary>
        private void UpdateStateMachine(ResurrectionSystem system) {
            stateTimer++;

            switch (currentState) {
                case DeathState.None:
                    //检查是否应该开始死亡流程
                    if (system.IsFull) {
                        StartWarningPhase();
                    }
                    break;

                case DeathState.Warning:
                    UpdateWarningPhase();
                    break;

                case DeathState.DeathAnimation:
                    UpdateDeathAnimationPhase();
                    break;

                case DeathState.Executing:
                    UpdateExecutingPhase();
                    break;

                case DeathState.Cooldown:
                    UpdateCooldownPhase(system);
                    break;
            }
        }
        #endregion

        #region 警告阶段
        /// <summary>
        /// 开始警告阶段
        /// </summary>
        private void StartWarningPhase() {
            currentState = DeathState.Warning;
            stateTimer = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with {
                    Volume = 1.2f,
                    Pitch = -0.5f,
                    MaxInstances = 1
                }, Player.Center);
            }
            CombatText.NewText(Player.getRect(), Color.DarkRed,
                WarningCombatText.Value, dramatic: true, dot: false);
            for (int i = 0; i < 30; i++) {
                SpawnAbyssParticle(Player.Center, large: true);
            }
        }

        /// <summary>
        /// 更新警告阶段
        /// </summary>
        private void UpdateWarningPhase() {
            //视觉效果
            screenShakeIntensity = MathHelper.Lerp(screenShakeIntensity, 8f, 0.15f);
            shadowDistortionIntensity = MathHelper.Lerp(shadowDistortionIntensity, 1f, 0.1f);

            //生成粒子
            if (stateTimer % 5 == 0) {
                SpawnAbyssParticle(Player.Center, large: Main.rand.NextBool());
            }

            //生成触手
            if (stateTimer % 12 == 0) {
                SpawnTentacle();
            }

            //警告阶段结束，进入死亡动画
            if (stateTimer >= WarningDuration) {
                StartDeathAnimationPhase();
            }
        }
        #endregion

        #region 死亡动画阶段
        /// <summary>
        /// 开始死亡动画阶段
        /// </summary>
        private void StartDeathAnimationPhase() {
            currentState = DeathState.DeathAnimation;
            stateTimer = 0;
            hasExecutedDeath = false;
            midPhaseSoundPlayed = false;
            Player.noItems = true;
            Player.noBuilding = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath59 with {
                    Volume = 1.5f,
                    Pitch = -0.8f
                }, Player.Center);
            }
            for (int i = 0; i < 80; i++) {
                SpawnAbyssParticle(Player.Center, large: true);
            }
            for (int i = 0; i < 6; i++) {
                SpawnTentacle();
            }
            Main.NewText(DeathNoticeText.Value, 150, 0, 0);
        }

        /// <summary>
        /// 更新死亡动画阶段
        /// </summary>
        private void UpdateDeathAnimationPhase() {
            float progress = stateTimer / (float)DeathAnimationDuration;

            //第一阶段：拉扯效果（0-0.4）
            if (progress < 0.4f) {
                float pullProgress = progress / 0.4f;

                //向玩家下方拉扯（模拟被深渊吞噬）
                Player.velocity.Y += pullProgress * 0.8f; //增加拉扯力度
                Player.velocity.X *= 0.9f; //更强的减速

                //强烈震动
                screenShakeIntensity = MathHelper.Lerp(15f, 10f, pullProgress);
                shadowDistortionIntensity = 1f + pullProgress * 1.5f;

                //大量粒子
                if (stateTimer % 2 == 0) {
                    SpawnAbyssParticle(Player.Center, large: true);
                    //额外生成一些向上的气泡粒子
                    if (Main.rand.NextBool(3)) {
                        Dust.NewDust(Player.position, Player.width, Player.height, DustID.BubbleBlock, 0, -2f, 100, default, 1.5f);
                    }
                }

                //玩家开始变透明
                playerAlphaMultiplier = 1f - pullProgress * 0.3f;
            }
            //第二阶段：扭曲消失（0.4-0.7）
            else if (progress < 0.7f) {
                float dissolveProgress = (progress - 0.4f) / 0.3f;

                //玩家继续变透明
                playerAlphaMultiplier = 0.7f - dissolveProgress * 0.6f; //变得更透明

                //减速
                Player.velocity *= 0.85f;

                //视觉效果
                screenShakeIntensity = MathHelper.Lerp(10f, 5f, dissolveProgress);
                shadowDistortionIntensity = 2.5f + dissolveProgress * 2f; //更强的扭曲

                //持续粒子
                if (stateTimer % 3 == 0) {
                    SpawnAbyssParticle(Player.Center, large: true);
                }

                //播放额外音效（不要影响最终死亡触发）
                if (dissolveProgress >= 0.5f && !midPhaseSoundPlayed) {
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with {
                            Volume = 1.0f,
                            Pitch = -1.0f
                        }, Player.Center);
                        //添加一个冲击波效果
                        for (int i = 0; i < 20; i++) {
                            Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                            Dust.NewDust(Player.Center, 0, 0, DustID.Shadowflame, velocity.X, velocity.Y, 100, Color.Black, 2f);
                        }
                    }
                    midPhaseSoundPlayed = true; //只记录中段音效播放
                }
            }
            //第三阶段：执行死亡（0.7-1.0）
            else {
                float fadeProgress = (progress - 0.7f) / 0.3f;

                //玩家几乎透明
                playerAlphaMultiplier = 0.1f - fadeProgress * 0.1f;

                //淡出效果
                screenShakeIntensity = MathHelper.Lerp(5f, 0f, fadeProgress);
                shadowDistortionIntensity = MathHelper.Lerp(4.5f, 0f, fadeProgress);

                //在70%时执行死亡
                if (progress >= 0.7f && !hasExecutedDeath) {
                    hasExecutedDeath = true;
                    StartExecutingPhase();
                    return; //立即切换状态
                }
            }

            //动画结束，进入执行阶段（如果还没有）
            if (stateTimer >= DeathAnimationDuration) {
                if (!hasExecutedDeath) {
                    StartExecutingPhase();
                }
                else {
                    //如果已经执行了死亡，进入冷却
                    StartCooldownPhase();
                }
            }

            //禁用玩家控制
            DisablePlayerControls();
        }
        #endregion

        #region 执行死亡阶段
        /// <summary>
        /// 开始执行死亡
        /// </summary>
        private void StartExecutingPhase() {
            currentState = DeathState.Executing;
            stateTimer = 0;
            hasExecutedDeath = true; //最终死亡标记

            //执行真正的死亡
            ExecuteDeath();
        }

        /// <summary>
        /// 更新执行阶段
        /// </summary>
        private void UpdateExecutingPhase() {
            //等待一小段时间确保死亡完成
            if (stateTimer >= 10 || Player.dead) {
                StartCooldownPhase();
            }
        }

        /// <summary>
        /// 执行真正的死亡
        /// </summary>
        private void ExecuteDeath() {
            //使用深渊的死亡原因
            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(
                DeathText.ToNetworkText(Player.name)
            );

            SirenMusicalBoxPlayerDeath.MusichasEnded = true;
            //杀死玩家
            Player.KillMe(damageSource, Player.statLife + 1, 0, false);

            //生成死亡特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 50; i++) {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                        DustID.DungeonWater, 0f, 0f, 100, Color.DarkBlue, 2.5f);
                    dust.velocity *= 3f;
                    dust.noGravity = true;
                }

                //额外的深色粒子
                for (int i = 0; i < 30; i++) {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                        DustID.Shadowflame, 0f, 0f, 100, Color.Black, 2f);
                    dust.velocity = Main.rand.NextVector2Circular(8f, 8f);
                    dust.noGravity = true;
                }
            }
        }
        #endregion

        #region 冷却阶段
        /// <summary>
        /// 开始冷却阶段
        /// </summary>
        private void StartCooldownPhase() {
            currentState = DeathState.Cooldown;
            stateTimer = 0;
            playerAlphaMultiplier = 1f;
        }

        /// <summary>
        /// 更新冷却阶段
        /// </summary>
        private void UpdateCooldownPhase(ResurrectionSystem system) {
            //冷却期间淡出效果
            screenShakeIntensity *= 0.9f;
            shadowDistortionIntensity *= 0.9f;

            //冷却结束
            if (stateTimer >= CooldownDuration) {
                //如果复苏值仍然是满的，重新开始流程
                if (system.IsFull && !Player.dead) {
                    ResetState();
                    StartWarningPhase();
                }
                else {
                    ResetState();
                }
            }
        }
        #endregion

        #region 状态重置
        /// <summary>
        /// 重置状态
        /// </summary>
        private void ResetState() {
            currentState = DeathState.None;
            stateTimer = 0;
            screenShakeIntensity = 0f;
            shadowDistortionIntensity = 0f;
            hasExecutedDeath = false;
            midPhaseSoundPlayed = false;
            playerAlphaMultiplier = 1f;
            needsDeathReset = false;

            //清理效果
            abyssParticles.Clear();
            tentacles.Clear();

            //恢复玩家控制
            if (!Player.dead) {
                Player.noItems = false;
                Player.noBuilding = false;
            }
        }
        #endregion

        #region 重生处理
        public void Respawn() {
            var system = Player.GetResurrectionSystem();
            if (system == null) {
                return;
            }
            //如果复苏值过高，重置为保留值
            if (system.CurrentValue >= system.MaxValue * 0.95f) {
                float retainedValue = system.MaxValue * ResurrectionRetainRatio;
                system.SetValue(retainedValue, triggerEvents: false);
                Main.NewText(MarkRemainText.Value, 200, 50, 50);
                Main.NewText(string.Format(RetainValueFormat.Value, (int)(ResurrectionRetainRatio * 100)), 255, 150, 50);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie103 with {
                        Volume = 0.8f,
                        Pitch = -0.5f
                    });
                }
            }

            ResetState();
            //强制重置复苏速度
            if (Player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                halibutPlayer.CanCloseEye = true;
                halibutPlayer.ResurrectionSystem.ResurrectionRate = 0f;
            }
        }

        public override void OnRespawn() {
            needsDeathReset = false;
            Respawn();
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            needsDeathReset = true;
            if (Player.TryGetHalibutPlayer(out var halibutPlayer)) {
                halibutPlayer.CanCloseEye = true;
            }
        }
        #endregion

        #region 视觉效果更新
        /// <summary>
        /// 更新视觉效果
        /// </summary>
        private void UpdateVisualEffects() {
            //更新粒子
            for (int i = abyssParticles.Count - 1; i >= 0; i--) {
                abyssParticles[i].Update();
                if (abyssParticles[i].IsDead) {
                    abyssParticles.RemoveAt(i);
                }
            }

            //更新触手
            for (int i = tentacles.Count - 1; i >= 0; i--) {
                tentacles[i].Update(Player.Center);
                if (tentacles[i].IsDead) {
                    tentacles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 生成深渊粒子
        /// </summary>
        private void SpawnAbyssParticle(Vector2 center, bool large = false) {
            Vector2 position = center + Main.rand.NextVector2Circular(200, 200);
            Vector2 velocity = (center - position).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 6f);

            abyssParticles.Add(new AbyssParticle(position, velocity, large));
        }

        /// <summary>
        /// 生成触手
        /// </summary>
        private void SpawnTentacle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 startPos = Player.Center + angle.ToRotationVector2() * 400f;

            tentacles.Add(new AbyssTentacle(startPos, Player.Center, angle));
        }
        #endregion

        #region 玩家控制
        /// <summary>
        /// 禁用玩家控制
        /// </summary>
        private void DisablePlayerControls() {
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
            //应用玩家透明度
            if (playerAlphaMultiplier < 1f) {
                //通过调整玩家颜色来模拟透明度
                Player.immuneAlpha = (int)((1f - playerAlphaMultiplier) * 255f);
            }
        }

        /// <summary>
        /// 检查是否处于死亡演出状态
        /// </summary>
        public bool IsInDeathSequence => currentState != DeathState.None && currentState != DeathState.Cooldown;

        public string LocalizationCategory => "Resurrections";

        /// <summary>
        /// 绘制死亡演出特效
        /// </summary>
        public void DrawDeathEffects(SpriteBatch spriteBatch) {
            if (currentState == DeathState.None) return;

            //绘制触手
            foreach (var tentacle in tentacles) {
                tentacle.Draw(spriteBatch);
            }

            //绘制粒子
            foreach (var particle in abyssParticles) {
                particle.Draw(spriteBatch);
            }

            //绘制屏幕暗化效果
            if (currentState == DeathState.DeathAnimation || currentState == DeathState.Executing) {
                float darkness = 0f;

                if (currentState == DeathState.DeathAnimation) {
                    float progress = stateTimer / (float)DeathAnimationDuration;
                    darkness = MathHelper.Clamp(progress * 1.5f, 0f, 0.7f);
                }
                else {
                    darkness = 0.7f;
                }

                if (darkness > 0.01f) {
                    Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
                    spriteBatch.Draw(VaultAsset.placeholder2.Value, screenRect,
                        Color.Black * darkness);
                }
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
