using CalamityOverhaul.Common;
using InnoVault.Actors;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦技能核心管理器
    /// 管理激活/冷却状态、残影生成节奏、屏幕效果强度
    /// 冷却参数从装备的斯安威斯坦义体物品读取，支持不同型号
    /// </summary>
    internal class Sandevistan : ModSystem
    {
        /// <summary>
        /// 技能是否处于激活状态
        /// </summary>
        public static bool IsActive { get; private set; }

        /// <summary>
        /// 屏幕后处理效果强度（0~1），带渐入渐出过渡
        /// </summary>
        public static float ScreenEffectIntensity { get; private set; }

        /// <summary>
        /// 当前冷却值，激活时消耗，未激活时恢复
        /// </summary>
        public static float CurrentCooldown { get; set; }

        /// <summary>
        /// 最大冷却值，由装备的义体物品决定
        /// </summary>
        public static float MaxCooldown { get; private set; }

        /// <summary>
        /// 每帧冷却消耗量
        /// </summary>
        public static float ConsumptionRate { get; private set; }

        /// <summary>
        /// 每帧冷却恢复量
        /// </summary>
        public static float RecoveryRate { get; private set; }

        /// <summary>
        /// 冷却值比例（0~1），供HUD使用
        /// </summary>
        public static float CooldownRatio => MaxCooldown > 0 ? CurrentCooldown / MaxCooldown : 0f;

        private static int spawnTimer;
        private static bool wasActiveLastFrame;
        //用于检测装备变化，首次装备或切换型号时初始化冷却
        private static int trackedEquipType = -1;
        //停用后恢复延迟计时器，防止反复按键卡加速
        private static int recoveryDelay;

        private const float FadeInSpeed = 0.05f;
        private const float FadeOutSpeed = 0.01f;
        //停用后需要等多少帧才能开始恢复冷却（2秒 = 120帧）
        private const int RecoveryDelayTicks = 120;

        /// <summary>
        /// 每隔多少帧生成一个残影（越小残影越密集）
        /// </summary>
        public const int SpawnInterval = 4;

        /// <summary>
        /// 获取玩家当前装备的斯安威斯坦义体，未装备返回null
        /// </summary>
        public static SandevistansItem GetEquipped(Player player) {
            var cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is SandevistansItem sandy) {
                    return sandy;
                }
            }
            return null;
        }

        /// <summary>
        /// 尝试激活斯安威斯坦
        /// </summary>
        public static void TryActivate() {
            if (!IsActive && CurrentCooldown > 0) {
                IsActive = true;
            }
        }

        /// <summary>
        /// 强制停用斯安威斯坦
        /// </summary>
        public static void ForceDeactivate() {
            IsActive = false;
        }

        /// <summary>
        /// 在玩家逻辑更新中调用，驱动整个斯安威斯坦系统
        /// </summary>
        public static void Update(Player player) {
            SandevistansItem equipped = GetEquipped(player);

            //没有装备斯安威斯坦时的处理
            if (equipped == null) {
                if (IsActive) {
                    IsActive = false;
                }
                trackedEquipType = -1;
                HandleScreenEffect();
                SyncTimeSlow();
                HandleSoundTransition();
                wasActiveLastFrame = IsActive;
                return;
            }

            //同步冷却参数
            MaxCooldown = equipped.MaxCooldownTime;
            ConsumptionRate = equipped.ConsumptionPerFrame;
            RecoveryRate = equipped.RecoveryPerFrame;

            //检测装备变化（包括游戏加载后首次检测），初始化冷却值
            if (equipped.Item.type != trackedEquipType) {
                trackedEquipType = equipped.Item.type;
                CurrentCooldown = MaxCooldown;
                if (IsActive) {
                    IsActive = false;
                }
            }

            //技能输入由 CyberwareSkillRadialUI 通过 SandevistanSkill 桥接，
            //本系统不再直接监听 CyberwareSkill_Key，避免与其他义体技能产生按键冲突

            //冷却值消耗与恢复
            if (IsActive) {
                CurrentCooldown -= ConsumptionRate;
                if (CurrentCooldown <= 0) {
                    CurrentCooldown = 0;
                    IsActive = false;
                }
                //激活期间重置恢复延迟
                recoveryDelay = RecoveryDelayTicks;
            }
            else {
                //停用后需要等待延迟结束才能开始恢复
                if (recoveryDelay > 0) {
                    recoveryDelay--;
                }
                else {
                    CurrentCooldown = MathHelper.Min(CurrentCooldown + RecoveryRate, MaxCooldown);
                }
            }

            //音效触发（基于状态变化边沿检测）
            HandleSoundTransition();

            //屏幕效果渐变
            HandleScreenEffect();

            //同步时缓系统
            SyncTimeSlow();

            wasActiveLastFrame = IsActive;

            if (!IsActive) {
                spawnTimer = 0;
                return;
            }

            //玩家基本静止时不产生残影
            if (player.velocity.LengthSquared() < 1f) {
                return;
            }

            spawnTimer++;
            if (spawnTimer >= SpawnInterval) {
                spawnTimer = 0;
                SpawnGhost(player);
            }
        }

        private static void HandleSoundTransition() {
            if (IsActive && !wasActiveLastFrame) {
                SoundEngine.PlaySound(CWRSound.SandevistanStart);
            }
            else if (!IsActive && wasActiveLastFrame) {
                SoundEngine.PlaySound(CWRSound.SandevistanEnd);
            }
        }

        private static void HandleScreenEffect() {
            if (IsActive) {
                ScreenEffectIntensity = MathHelper.Min(ScreenEffectIntensity + FadeInSpeed, 1f);
            }
            else {
                ScreenEffectIntensity = MathHelper.Max(ScreenEffectIntensity - FadeOutSpeed, 0f);
            }
        }

        private static void SyncTimeSlow() {
            if (IsActive && !SandevistanTimeSlow.IsActive) {
                SandevistanTimeSlow.Activate();
            }
            else if (!IsActive && SandevistanTimeSlow.IsActive) {
                SandevistanTimeSlow.Deactivate();
            }
        }

        public override void PostUpdatePlayers() {
            Update(Main.LocalPlayer);
        }

        /// <summary>
        /// 在玩家当前位置生成一个残影实体
        /// </summary>
        public static void SpawnGhost(Player player) {
            if (Main.dedServ) {
                return;
            }

            int index = ActorLoader.NewActor<SandevistanGhostActor>(player.Center, Vector2.Zero);
            if (index >= 0) {
                ActorLoader.Actors[index].OnSpawn(player);
            }
        }
    }
}
