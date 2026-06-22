using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Actors;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans
{
    /// <summary>
    /// 斯安威斯坦核心 ModSystem
    /// <br/>激活/冷却/残影/屏幕强度；参数读装备义体型号
    /// </summary>
    internal class Sandevistan : ModSystem
    {
        /// <summary>时缓激活中</summary>
        public static bool IsActive { get; private set; }

        /// <summary>屏幕后处理强度 0~1，渐入渐出</summary>
        public static float ScreenEffectIntensity { get; private set; }

        /// <summary>当前冷却，激活消耗/停用恢复</summary>
        public static float CurrentCooldown { get; set; }

        /// <summary>最大冷却，由装备义体决定</summary>
        public static float MaxCooldown { get; private set; }

        /// <summary>每帧消耗量</summary>
        public static float ConsumptionRate { get; private set; }

        /// <summary>每帧恢复量</summary>
        public static float RecoveryRate { get; private set; }

        /// <summary>冷却比例 0~1，HUD 用</summary>
        public static float CooldownRatio => MaxCooldown > 0 ? CurrentCooldown / MaxCooldown : 0f;

        private static int spawnTimer;
        private static bool wasActiveLastFrame;
        //装备变化检测，首次/换型号初始化冷却
        private static int trackedEquipType = -1;
        //停用后恢复延迟，防按键卡加速
        private static int recoveryDelay;

        private const float FadeInSpeed = 0.05f;
        private const float FadeOutSpeed = 0.01f;
        //停用后 120 帧才开始恢复
        private const int RecoveryDelayTicks = 120;

        /// <summary>残影生成间隔帧，越小越密</summary>
        public const int SpawnInterval = 4;

        /// <summary>当前装备斯安威斯坦，无则 null</summary>
        public static SandevistansItem GetEquipped(Player player) {
            var cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is SandevistansItem sandy) {
                    return sandy;
                }
            }
            return null;
        }

        /// <summary>尝试激活</summary>
        public static void TryActivate() {
            if (!IsActive && CurrentCooldown > 0) {
                IsActive = true;
            }
        }

        /// <summary>强制停用</summary>
        public static void ForceDeactivate() {
            IsActive = false;
        }

        /// <summary>每帧驱动冷却/音效/时缓/残影</summary>
        public static void Update(Player player) {
            SandevistansItem equipped = GetEquipped(player);

            //未装备：清状态
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

            //装备变化或首次加载→满冷却
            if (equipped.Item.type != trackedEquipType) {
                trackedEquipType = equipped.Item.type;
                CurrentCooldown = MaxCooldown;
                if (IsActive) {
                    IsActive = false;
                }
            }

            //输入走 SandevistanSkill 雷达桥接，不监听 CyberwareSkill_Key

            //外部时缓因子（排除自身源）：HackTime 等冻结期间为 0，冷却消耗/恢复随之暂停；本人自身的世界减速不计入
            float externalTimeScale = TimeGear.TimeScaleExcluding(SandevistanTimeSlow.TimeGearKey);

            //冷却值消耗与恢复，按外部时缓缩放
            if (IsActive) {
                CurrentCooldown -= ConsumptionRate * externalTimeScale;
                if (CurrentCooldown <= 0) {
                    CurrentCooldown = 0;
                    IsActive = false;
                }
                //激活期重置恢复延迟
                recoveryDelay = RecoveryDelayTicks;
            }
            else if (externalTimeScale > 0f) {
                //冻结期间恢复延迟与恢复一并暂停
                if (recoveryDelay > 0) {
                    recoveryDelay--;
                }
                else {
                    CurrentCooldown = MathHelper.Min(CurrentCooldown + RecoveryRate * externalTimeScale, MaxCooldown);
                }
            }

            //边沿检测播启停音
            HandleSoundTransition();

            //屏幕效果渐变
            HandleScreenEffect();

            //同步 TimeGear 时缓
            SyncTimeSlow();

            wasActiveLastFrame = IsActive;

            if (!IsActive) {
                spawnTimer = 0;
                return;
            }

            //外部时间冻结（HackTime 等）期间不推进残影节奏
            if (externalTimeScale <= 0f) {
                return;
            }

            //静止不产残影
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

        /// <summary>生成一帧玩家残影 Actor</summary>
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
