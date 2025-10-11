using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 深渊厉鬼 - 玩家死亡后的复仇实体
    /// 具有高度的压迫感和恐怖氛围，通过水触发杀戮规律
    /// </summary>
    internal class TheSpiritofTheAbyss : ModNPC
    {
        public override string Texture => CWRConstant.Placeholder; // 占位符，使用特殊绘制

        #region 状态枚举
        /// <summary>
        /// 厉鬼行为状态
        /// </summary>
        private enum SpiritState
        {
            Dormant,      // 休眠状态（站立不动）
            Wandering,    // 游荡状态（缓慢移动）
            Observing,    // 观察状态（盯着玩家）
            Hunting,      // 狩猎状态（被水触发）
            Killing       // 杀戮状态（执行处决）
        }

        private SpiritState CurrentState {
            get => (SpiritState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }
        #endregion

        #region AI字段定义
        private ref float StateTimer => ref NPC.ai[1];
        private ref float TargetPlayerIndex => ref NPC.ai[2];
        private ref float CustomAI1 => ref NPC.ai[3];
        
        // 本地字段（不同步）
        private Player targetPlayer;
        private Vector2 spawnPosition;
        private bool hasPlayedSpawnSound = false;
        private float ghostOpacity = 0f;
        private float distortionIntensity = 0f;
        private float eyeGlowIntensity = 0f;
        private Vector2 headTilt = Vector2.Zero;
        private int frameCounter = 0;
        private int currentFrame = 0;
        
        // 环境音效
        private int ambientSoundTimer = 0;
        private const int AmbientSoundCooldown = 300; // 5秒
        
        // 粒子效果
        private System.Collections.Generic.List<AbyssGhostParticle> particles = new();
        #endregion

        #region 常量配置
        private const int LifeTime = 3600; // 60秒生存时间
        private const float DormantDuration = 180; // 3秒休眠
        private const float WanderSpeed = 0.8f; // 游荡速度
        private const float HuntingSpeed = 16f; // 狩猎速度（极快）
        private const float ObservationRange = 600f; // 观察范围
        private const float WaterDetectionRange = 400f; // 水检测范围
        private const float KillRange = 80f; // 杀戮范围
        #endregion

        #region 初始化
        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1; // 使用玩家绘制，不需要帧
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() {
                Hide = true // 不在怪物图鉴中显示
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
        }

        public override void SetDefaults() {
            NPC.width = 20;
            NPC.height = 42; // 玩家大小
            NPC.damage = 999999; // 触碰即死
            NPC.defense = 999999; // 无法被伤害
            NPC.lifeMax = 999999;
            NPC.life = 999999;
            NPC.HitSound = null; // 无击中音效
            NPC.DeathSound = null; // 无死亡音效
            NPC.knockBackResist = 0f; // 无击退
            NPC.noGravity = true; // 无重力（幽灵）
            NPC.noTileCollide = false; // 可穿墙，但有物理碰撞感
            NPC.aiStyle = -1; // 自定义AI
            NPC.immortal = true; // 不朽
            NPC.dontTakeDamage = true; // 不受伤害
            NPC.friendly = false; // 敌对
            NPC.netAlways = true; // 始终同步
        }

        public override void OnSpawn(IEntitySource source) {
            spawnPosition = NPC.Center;
            CurrentState = SpiritState.Dormant;
            StateTimer = 0;
            ghostOpacity = 0f;
            
            // 生成初始粒子效果
            for (int i = 0; i < 30; i++) {
                SpawnParticle(NPC.Center);
            }
        }
        #endregion

        #region 核心AI
        public override void AI() {
            // 生存时间检查
            NPC.timeLeft = 10; // 强制保持活跃
            CustomAI1++;
            
            if (CustomAI1 >= LifeTime) {
                DespawnWithEffect();
                return;
            }

            // 获取目标玩家
            UpdateTargetPlayer();
            
            // 淡入效果
            if (ghostOpacity < 1f) {
                ghostOpacity += 0.01f;
            }

            // 播放生成音效（仅一次）
            if (!hasPlayedSpawnSound && ghostOpacity > 0.5f) {
                PlaySpawnSound();
                hasPlayedSpawnSound = true;
            }

            // 状态机更新
            StateTimer++;
            UpdateState();

            // 环境音效
            UpdateAmbientSound();

            // 更新粒子
            UpdateParticles();

            // 更新视觉效果
            UpdateVisualEffects();

            // 同步网络
            if (Main.netMode == NetmodeID.Server && NPC.netSpam <= 0) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                NPC.netSpam = 10;
            }
        }

        /// <summary>
        /// 更新目标玩家
        /// </summary>
        private void UpdateTargetPlayer() {
            if (TargetPlayerIndex >= 0 && TargetPlayerIndex < Main.maxPlayers) {
                targetPlayer = Main.player[(int)TargetPlayerIndex];
                if (!targetPlayer.active || targetPlayer.dead) {
                    targetPlayer = null;
                    TargetPlayerIndex = -1;
                }
            }

            if (targetPlayer == null) {
                // 寻找最近的玩家
                float nearestDist = float.MaxValue;
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player.active && !player.dead) {
                        float dist = Vector2.Distance(NPC.Center, player.Center);
                        if (dist < nearestDist) {
                            nearestDist = dist;
                            targetPlayer = player;
                            TargetPlayerIndex = i;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 状态机更新
        /// </summary>
        private void UpdateState() {
            switch (CurrentState) {
                case SpiritState.Dormant:
                    UpdateDormant();
                    break;
                case SpiritState.Wandering:
                    UpdateWandering();
                    break;
                case SpiritState.Observing:
                    UpdateObserving();
                    break;
                case SpiritState.Hunting:
                    UpdateHunting();
                    break;
                case SpiritState.Killing:
                    UpdateKilling();
                    break;
            }
        }

        /// <summary>
        /// 休眠状态 - 站立不动，偶尔轻微晃动
        /// </summary>
        private void UpdateDormant() {
            NPC.velocity *= 0.95f; // 逐渐停止
            distortionIntensity = 0.3f;
            eyeGlowIntensity = 0.5f;

            // 轻微的头部倾斜动画（增加不安感）
            headTilt.X = (float)Math.Sin(StateTimer * 0.02f) * 0.1f;
            headTilt.Y = (float)Math.Cos(StateTimer * 0.03f) * 0.05f;

            // 偶尔生成粒子
            if (StateTimer % 20 == 0) {
                SpawnParticle(NPC.Center);
            }

            // 检查是否有玩家接近
            if (targetPlayer != null) {
                float dist = Vector2.Distance(NPC.Center, targetPlayer.Center);
                
                // 检查水触发条件
                if (IsPlayerInWater(targetPlayer) && dist < WaterDetectionRange) {
                    TransitionToState(SpiritState.Hunting);
                    return;
                }

                // 进入观察状态
                if (dist < ObservationRange) {
                    if (StateTimer > DormantDuration) {
                        TransitionToState(SpiritState.Observing);
                    }
                } else {
                    // 开始游荡
                    if (StateTimer > DormantDuration * 2) {
                        TransitionToState(SpiritState.Wandering);
                    }
                }
            }
        }

        /// <summary>
        /// 游荡状态 - 缓慢移动，无目的性
        /// </summary>
        private void UpdateWandering() {
            distortionIntensity = 0.4f;
            eyeGlowIntensity = 0.6f;

            // 缓慢的随机移动
            if (StateTimer % 60 == 0) {
                Vector2 wanderDirection = new Vector2(
                    Main.rand.NextFloat(-1f, 1f),
                    Main.rand.NextFloat(-0.5f, 0.5f)
                ).SafeNormalize(Vector2.Zero);
                
                NPC.velocity = wanderDirection * WanderSpeed;
            }

            // 头部跟随移动方向
            if (NPC.velocity.Length() > 0.1f) {
                headTilt = NPC.velocity.SafeNormalize(Vector2.Zero) * 0.15f;
            }

            // 保持在生成点附近
            float distFromSpawn = Vector2.Distance(NPC.Center, spawnPosition);
            if (distFromSpawn > 300f) {
                Vector2 toSpawn = (spawnPosition - NPC.Center).SafeNormalize(Vector2.Zero);
                NPC.velocity += toSpawn * 0.5f;
            }

            // 偶尔生成粒子
            if (StateTimer % 30 == 0) {
                SpawnParticle(NPC.Center);
            }

            // 检查玩家
            if (targetPlayer != null) {
                float dist = Vector2.Distance(NPC.Center, targetPlayer.Center);
                
                if (IsPlayerInWater(targetPlayer) && dist < WaterDetectionRange) {
                    TransitionToState(SpiritState.Hunting);
                    return;
                }

                if (dist < ObservationRange) {
                    TransitionToState(SpiritState.Observing);
                }
            }

            // 定期返回休眠
            if (StateTimer > 300) {
                TransitionToState(SpiritState.Dormant);
            }
        }

        /// <summary>
        /// 观察状态 - 盯着玩家，缓慢转向，高度压迫感
        /// </summary>
        private void UpdateObserving() {
            NPC.velocity *= 0.98f; // 几乎静止
            distortionIntensity = 0.6f;
            eyeGlowIntensity = MathHelper.Lerp(0.7f, 1f, 
                (float)Math.Sin(StateTimer * 0.1f) * 0.5f + 0.5f); // 眼睛脉动发光

            if (targetPlayer != null) {
                // 头部始终朝向玩家（恐怖效果）
                Vector2 directionToPlayer = (targetPlayer.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                headTilt = Vector2.Lerp(headTilt, directionToPlayer * 0.2f, 0.1f);

                // 缓慢靠近（几乎察觉不到）
                if (StateTimer % 10 == 0) {
                    NPC.velocity += directionToPlayer * 0.05f;
                }

                float dist = Vector2.Distance(NPC.Center, targetPlayer.Center);

                // 检查水触发
                if (IsPlayerInWater(targetPlayer) && dist < WaterDetectionRange) {
                    TransitionToState(SpiritState.Hunting);
                    return;
                }

                // 玩家离开范围
                if (dist > ObservationRange * 1.5f) {
                    TransitionToState(SpiritState.Wandering);
                }

                // 持续粒子效果
                if (StateTimer % 15 == 0) {
                    SpawnParticle(NPC.Center);
                }
            } else {
                TransitionToState(SpiritState.Dormant);
            }
        }

        /// <summary>
        /// 狩猎状态 - 极速接近玩家
        /// </summary>
        private void UpdateHunting() {
            distortionIntensity = 1.2f;
            eyeGlowIntensity = 1.5f; // 眼睛极亮

            if (targetPlayer != null) {
                Vector2 directionToPlayer = (targetPlayer.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                
                // 极速冲刺
                NPC.velocity = Vector2.Lerp(NPC.velocity, directionToPlayer * HuntingSpeed, 0.2f);

                // 头部锁定目标
                headTilt = directionToPlayer * 0.3f;

                // 大量粒子效果
                if (StateTimer % 2 == 0) {
                    SpawnParticle(NPC.Center);
                }

                // 播放狩猎音效
                if (StateTimer % 30 == 0 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie53 with { 
                        Volume = 0.8f, 
                        Pitch = -0.6f 
                    }, NPC.Center);
                }

                float dist = Vector2.Distance(NPC.Center, targetPlayer.Center);

                // 进入杀戮范围
                if (dist < KillRange) {
                    TransitionToState(SpiritState.Killing);
                }

                // 如果玩家离开水或距离太远，返回观察
                if ((!IsPlayerInWater(targetPlayer) || dist > WaterDetectionRange * 2f) && StateTimer > 120) {
                    TransitionToState(SpiritState.Observing);
                }
            } else {
                TransitionToState(SpiritState.Dormant);
            }
        }

        /// <summary>
        /// 杀戮状态 - 执行处决
        /// </summary>
        private void UpdateKilling() {
            NPC.velocity *= 0.9f;
            distortionIntensity = 2f;
            eyeGlowIntensity = 2f;

            if (targetPlayer != null && StateTimer < 60) {
                // 锁定玩家位置
                Vector2 offset = (targetPlayer.Center - NPC.Center) * 0.1f;
                NPC.velocity += offset;

                // 生成大量粒子
                SpawnParticle(NPC.Center);

                // 在第30帧执行杀戮
                if (StateTimer == 30) {
                    ExecuteKill(targetPlayer);
                }
            } else {
                // 杀戮完成，返回休眠
                TransitionToState(SpiritState.Dormant);
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 状态转换
        /// </summary>
        private void TransitionToState(SpiritState newState) {
            if (CurrentState == newState) return;

            CurrentState = newState;
            StateTimer = 0;

            // 状态转换音效
            if (!VaultUtils.isServer) {
                if (newState == SpiritState.Hunting) {
                    SoundEngine.PlaySound(SoundID.Roar with { 
                        Volume = 1.2f, 
                        Pitch = -0.8f 
                    }, NPC.Center);
                } else if (newState == SpiritState.Observing) {
                    SoundEngine.PlaySound(SoundID.Zombie2 with { 
                        Volume = 0.5f, 
                        Pitch = -1f 
                    }, NPC.Center);
                }
            }

            // 生成转换粒子
            for (int i = 0; i < 10; i++) {
                SpawnParticle(NPC.Center);
            }
        }

        /// <summary>
        /// 检查玩家是否在水中
        /// </summary>
        private static bool IsPlayerInWater(Player player) {
            if (player == null) return false;
            return player.wet && !player.lavaWet && !player.honeyWet;
        }

        /// <summary>
        /// 执行杀戮
        /// </summary>
        private static void ExecuteKill(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 使用深渊主题死亡原因
            PlayerDeathReason damageSource = PlayerDeathReason.ByCustomReason(
                CWRLocText.Instance.BloodAltar_Text3.ToNetworkText(player.name)
            );

            player.Hurt(damageSource, player.statLife + 1, 0);

            // 生成大量特效
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 50; i++) {
                    Dust dust = Dust.NewDustDirect(player.position, player.width, player.height,
                        DustID.DungeonWater, 0f, 0f, 100, Color.Black, 3f);
                    dust.velocity = Main.rand.NextVector2Circular(10f, 10f);
                    dust.noGravity = true;
                }

                // 播放杀戮音效
                SoundEngine.PlaySound(SoundID.NPCDeath59 with { 
                    Volume = 1.5f, 
                    Pitch = -0.9f 
                }, player.Center);
            }
        }

        /// <summary>
        /// 消失特效
        /// </summary>
        private void DespawnWithEffect() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 40; i++) {
                    SpawnParticle(NPC.Center);
                }
                
                SoundEngine.PlaySound(SoundID.Item8 with { 
                    Volume = 0.8f, 
                    Pitch = -0.5f 
                }, NPC.Center);
            }
            
            NPC.active = false;
        }

        /// <summary>
        /// 播放生成音效
        /// </summary>
        private void PlaySpawnSound() {
            if (VaultUtils.isServer) return;
            
            SoundEngine.PlaySound(SoundID.Zombie104 with { 
                Volume = 1.2f, 
                Pitch = -0.8f 
            }, NPC.Center);
        }

        /// <summary>
        /// 更新环境音效
        /// </summary>
        private void UpdateAmbientSound() {
            if (VaultUtils.isServer) return;

            ambientSoundTimer++;
            if (ambientSoundTimer >= AmbientSoundCooldown) {
                ambientSoundTimer = 0;

                // 根据状态播放不同音效
                if (CurrentState == SpiritState.Observing && targetPlayer != null) {
                    float dist = Vector2.Distance(NPC.Center, targetPlayer.Center);
                    if (dist < ObservationRange) {
                        SoundEngine.PlaySound(SoundID.Zombie53 with { 
                            Volume = 0.4f, 
                            Pitch = -0.9f 
                        }, NPC.Center);
                    }
                }
            }
        }

        /// <summary>
        /// 生成粒子
        /// </summary>
        private void SpawnParticle(Vector2 position) {
            Vector2 velocity = Main.rand.NextVector2Circular(2f, 2f);
            particles.Add(new AbyssGhostParticle(position, velocity));
        }

        /// <summary>
        /// 更新粒子
        /// </summary>
        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].IsDead) {
                    particles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 更新视觉效果
        /// </summary>
        private void UpdateVisualEffects() {
            frameCounter++;
            if (frameCounter >= 6) {
                frameCounter = 0;
                currentFrame = (currentFrame + 1) % 20; // 20帧动画循环
            }
        }
        #endregion

        #region 碰撞和伤害
        public override bool? CanBeHitByItem(Player player, Item item) => false;
        public override bool? CanBeHitByProjectile(Projectile projectile) => false;
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false; // 不通过碰撞伤害
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone) { }
        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone) { }
        #endregion

        #region 网络同步
        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(ghostOpacity);
            writer.Write(hasPlayedSpawnSound);
            writer.Write(spawnPosition.X);
            writer.Write(spawnPosition.Y);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            ghostOpacity = reader.ReadSingle();
            hasPlayedSpawnSound = reader.ReadBoolean();
            spawnPosition.X = reader.ReadSingle();
            spawnPosition.Y = reader.ReadSingle();
        }
        #endregion

        #region 绘制
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (ghostOpacity <= 0f) return false;

            // 准备玩家快照数据
            Player ghostPlayer = GetGhostPlayerData();
            if (ghostPlayer == null) return false;

            // 深渊主题颜色
            Color ghostColor = new Color(10, 30, 60) * ghostOpacity;
            Color glowColor = new Color(100, 150, 255) * eyeGlowIntensity * ghostOpacity;

            // 结束当前批次，准备绘制玩家
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, 
                SamplerState.PointClamp, null, Main.Rasterizer, null, 
                Main.GameViewMatrix.ZoomMatrix);

            // 绘制扭曲的残影层
            for (int i = 0; i < 3; i++) {
                Vector2 offset = new Vector2(
                    (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i) * distortionIntensity * 3f,
                    (float)Math.Cos(Main.GlobalTimeWrappedHourly * 3f + i) * distortionIntensity * 2f
                );

                DrawGhostPlayer(ghostPlayer, offset + headTilt * 10f, 
                    ghostColor * (0.3f / (i + 1)));
            }

            // 绘制主体玩家
            DrawGhostPlayer(ghostPlayer, headTilt * 10f, ghostColor * 0.8f);

            // 切换到发光混合模式绘制眼睛
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, 
                SamplerState.PointClamp, null, Main.Rasterizer, null, 
                Main.GameViewMatrix.ZoomMatrix);

            // 绘制眼睛发光
            DrawGhostEyes(spriteBatch, glowColor);

            // 绘制粒子
            foreach (var particle in particles) {
                particle.Draw(spriteBatch);
            }

            // 恢复正常绘制模式
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, 
                SamplerState.PointClamp, null, Main.Rasterizer, null, 
                Main.GameViewMatrix.ZoomMatrix);

            return false;
        }

        /// <summary>
        /// 获取用于渲染的幽灵玩家数据
        /// </summary>
        private static Player ghostRenderPlayer;
        private Player GetGhostPlayerData() {
            if (targetPlayer == null) return null;

            ghostRenderPlayer ??= new Player();
            Player ghost = ghostRenderPlayer;

            // 重置并复制玩家数据
            ghost.ResetEffects();
            ghost.CopyVisuals(targetPlayer);

            // 设置位置和动画
            ghost.position = NPC.position;
            ghost.velocity = NPC.velocity;
            ghost.direction = NPC.velocity.X < 0 ? -1 : 1; // 根据移动方向
            ghost.whoAmI = targetPlayer.whoAmI;

            // 设置动画帧（根据状态）
            switch (CurrentState) {
                case SpiritState.Dormant:
                    // 站立姿势
                    ghost.bodyFrame = new Rectangle(0, 56 * 0, 40, 56);
                    ghost.legFrame = new Rectangle(0, 56 * 0, 40, 56);
                    break;

                case SpiritState.Wandering:
                    // 行走动画
                    int walkFrame = (int)(Main.GlobalTimeWrappedHourly * 10f) % 20;
                    ghost.bodyFrame = new Rectangle(0, 56 * (walkFrame / 4), 40, 56);
                    ghost.legFrame = new Rectangle(0, 56 * (walkFrame / 4), 40, 56);
                    break;

                case SpiritState.Observing:
                    // 凝视姿势（类似站立）
                    ghost.bodyFrame = new Rectangle(0, 56 * 0, 40, 56);
                    ghost.legFrame = new Rectangle(0, 56 * 0, 40, 56);
                    break;

                case SpiritState.Hunting:
                    // 冲刺姿势
                    ghost.bodyFrame = new Rectangle(0, 56 * 14, 40, 56);
                    ghost.legFrame = new Rectangle(0, 56 * 6, 40, 56);
                    break;

                case SpiritState.Killing:
                    // 攻击姿势
                    ghost.bodyFrame = new Rectangle(0, 56 * 2, 40, 56);
                    ghost.legFrame = new Rectangle(0, 56 * 2, 40, 56);
                    break;
            }

            // 应用头部倾斜
            ghost.fullRotation = headTilt.X * 0.3f;

            return ghost;
        }

        /// <summary>
        /// 绘制幽灵玩家
        /// </summary>
        private void DrawGhostPlayer(Player ghost, Vector2 offset, Color tintColor) {
            if (ghost == null) return;

            // 保存原始颜色
            Color originalSkinColor = ghost.skinColor;
            Color originalHairColor = ghost.hairColor;
            Color originalEyeColor = ghost.eyeColor;
            Color originalShirtColor = ghost.shirtColor;
            Color originalUnderShirtColor = ghost.underShirtColor;
            Color originalPantsColor = ghost.pantsColor;
            Color originalShoeColor = ghost.shoeColor;

            // 应用深渊幽灵配色
            ghost.skinColor = tintColor;
            ghost.hairColor = tintColor;
            ghost.eyeColor = new Color(0, 0, 0, 0); // 眼睛单独绘制
            ghost.shirtColor = tintColor;
            ghost.underShirtColor = tintColor;
            ghost.pantsColor = tintColor;
            ghost.shoeColor = tintColor;

            // 临时调整位置
            Vector2 originalPosition = ghost.position;
            ghost.position += offset;

            try {
                // 使用游戏的玩家渲染器绘制
                Main.PlayerRenderer.DrawPlayer(
                    Main.Camera,
                    ghost,
                    ghost.position,
                    0f,
                    ghost.fullRotationOrigin
                );
            }
            catch (Exception) {
                // 渲染失败时静默处理
            }

            // 恢复原始状态
            ghost.position = originalPosition;
            ghost.skinColor = originalSkinColor;
            ghost.hairColor = originalHairColor;
            ghost.eyeColor = originalEyeColor;
            ghost.shirtColor = originalShirtColor;
            ghost.underShirtColor = originalUnderShirtColor;
            ghost.pantsColor = originalPantsColor;
            ghost.shoeColor = originalShoeColor;
        }

        /// <summary>
        /// 绘制幽灵眼睛发光效果
        /// </summary>
        private void DrawGhostEyes(SpriteBatch spriteBatch, Color glowColor) {
            Texture2D glowTexture = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            Vector2 drawPosition = NPC.Center - Main.screenPosition;

            // 眼睛位置偏移
            Vector2 eyeOffset = headTilt * 10f + new Vector2(0, -18);

            // 左眼
            spriteBatch.Draw(glowTexture,
                drawPosition + eyeOffset + new Vector2(-5, 0),
                null,
                glowColor,
                0f,
                glowTexture.Size() / 2f,
                0.2f,
                SpriteEffects.None,
                0f);

            // 右眼
            spriteBatch.Draw(glowTexture,
                drawPosition + eyeOffset + new Vector2(5, 0),
                null,
                glowColor,
                0f,
                glowTexture.Size() / 2f,
                0.2f,
                SpriteEffects.None,
                0f);

            // 眼睛光晕（更大的发光）
            if (eyeGlowIntensity > 0.8f) {
                spriteBatch.Draw(glowTexture,
                    drawPosition + eyeOffset + new Vector2(-5, 0),
                    null,
                    glowColor * 0.5f,
                    0f,
                    glowTexture.Size() / 2f,
                    0.4f * eyeGlowIntensity,
                    SpriteEffects.None,
                    0f);

                spriteBatch.Draw(glowTexture,
                    drawPosition + eyeOffset + new Vector2(5, 0),
                    null,
                    glowColor * 0.5f,
                    0f,
                    glowTexture.Size() / 2f,
                    0.4f * eyeGlowIntensity,
                    SpriteEffects.None,
                    0f);
            }
        }
        #endregion

    }

    #region 粒子类
    /// <summary>
    /// 深渊厉鬼粒子
    /// </summary>
    internal class AbyssGhostParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public Color Color;

        public bool IsDead => Life >= MaxLife;

        public AbyssGhostParticle(Vector2 position, Vector2 velocity) {
            Position = position;
            Velocity = velocity;
            Scale = Main.rand.NextFloat(0.5f, 1.2f);
            Alpha = 1f;
            Life = 0;
            MaxLife = Main.rand.Next(40, 80);
            
            Color = Main.rand.Next(3) switch {
                0 => new Color(10, 20, 40),
                1 => new Color(20, 40, 80),
                _ => new Color(40, 80, 120)
            };
        }

        public void Update() {
            Life++;
            Position += Velocity;
            Velocity *= 0.98f;
            Velocity.Y -= 0.05f; // 向上飘

            float lifeRatio = Life / (float)MaxLife;
            Alpha = 1f - lifeRatio;
            Scale *= 0.99f;
        }

        public void Draw(SpriteBatch spriteBatch) {
            if (IsDead) return;

            Texture2D texture = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            spriteBatch.Draw(texture,
                Position - Main.screenPosition,
                null,
                Color * Alpha,
                0f,
                texture.Size() / 2f,
                Scale,
                SpriteEffects.None,
                0f);
        }
    }
    #endregion
}
