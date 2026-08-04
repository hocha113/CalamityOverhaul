using System;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary><see cref="WorldFreezeSystem"/> 玩家侧快照与输入锁定</summary>
    internal class WorldFreezePlayer : ModPlayer
    {
        //位置快照
        private Vector2 frozenPosition;
        private Vector2 frozenVelocity;
        private bool positionCaptured;
        //朝向快照
        private int frozenDirection;
        //动画帧快照
        private Rectangle frozenBodyFrame;
        private Rectangle frozenLegFrame;
        private Rectangle frozenHeadFrame;
        //计时器快照，防冷却/无敌/呼吸推进
        private int frozenPotionDelay;
        private int frozenRestorationDelayTime;
        private int frozenItemAnimation;
        private int frozenItemAnimationMax;
        private int frozenItemTime;
        private int frozenImmuneTime;
        private bool frozenImmune;
        private int frozenBreath;
        private int frozenBreathCD;
        //怒气/肾上腺素快照
        private float frozenRage;
        private float frozenAdrenaline;
        private int frozenRageGainCooldown;
        private int frozenRageCombatFrames;
        private int frozenAdrenalinePauseTimer;
        //HP/魔力回复快照
        private int frozenStatLife;
        private float frozenLifeRegenTime;
        private int frozenStatMana;
        private float frozenManaRegenDelay;
        //buff 时长快照
        private int[] frozenBuffTime;
        private int[] frozenBuffType;
        //飞行时间快照，防开关冻结重置
        //internal 供 HackTime 预填
        internal float frozenWingTime;
        internal int frozenRocketTime;
        private bool snapshotInventoryOpen;
        //手持栏快照
        private int frozenSelectedItem;

        //ProcessTriggers 在 CopyInto 尾，control/QuickBuff 已消费，
        //数字键/滚轮/径向轮盘在其后读，这里清掉才对本帧有效
        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (!WorldFreezeSystem.IsActive) {
                return;
            }
            //数字键直改 selectedItem，触发器层吞
            triggersSet.Hotbar1 = false;
            triggersSet.Hotbar2 = false;
            triggersSet.Hotbar3 = false;
            triggersSet.Hotbar4 = false;
            triggersSet.Hotbar5 = false;
            triggersSet.Hotbar6 = false;
            triggersSet.Hotbar7 = false;
            triggersSet.Hotbar8 = false;
            triggersSet.Hotbar9 = false;
            triggersSet.Hotbar10 = false;
            //手柄快捷栏/径向轮盘同样屏蔽
            triggersSet.HotbarPlus = false;
            triggersSet.HotbarMinus = false;
            triggersSet.RadialHotbar = false;
            triggersSet.RadialQuickbar = false;
        }

        //本地 control 在 CopyInto(晚于 PreUpdate) 重写，
        //须在 SetControls 清；PreUpdate 只兜远程
        public override void SetControls() {
            if (!WorldFreezeSystem.IsActive) {
                return;
            }
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlThrow = false;
            //智能选火把也是换物品
            Player.controlTorch = false;
            Player.controlSmart = false;
            //掐开背包，防闪烁
            Player.controlInv = false;
        }

        public override void PreUpdate() {
            if (!WorldFreezeSystem.IsActive) {
                if (positionCaptured) {
                    Main.playerInventory = snapshotInventoryOpen;
                    Player.wingTime = frozenWingTime;
                    Player.rocketTime = frozenRocketTime;
                    Player.velocity = EntityFreezeState.IsFinite(frozenVelocity)
                        ? frozenVelocity
                        : Vector2.Zero;
                }
                positionCaptured = false;
                return;
            }

            //首次冻结快照
            if (!positionCaptured) {
                frozenPosition = Player.position;
                frozenVelocity = EntityFreezeState.IsFinite(Player.velocity)
                    ? Player.velocity
                    : Vector2.Zero;
                frozenDirection = Player.direction;
                frozenBodyFrame = Player.bodyFrame;
                frozenLegFrame = Player.legFrame;
                frozenHeadFrame = Player.headFrame;
                frozenPotionDelay = Player.potionDelay;
                frozenRestorationDelayTime = Player.restorationDelayTime;
                frozenItemAnimation = Player.itemAnimation;
                frozenItemAnimationMax = Player.itemAnimationMax;
                frozenItemTime = Player.itemTime;
                frozenImmuneTime = Player.immuneTime;
                frozenImmune = Player.immune;
                frozenBreath = Player.breath;
                frozenBreathCD = Player.breathCD;
                CWRRef.SnapshotRippers(Player, ref frozenRage, ref frozenAdrenaline
                    , ref frozenRageGainCooldown, ref frozenRageCombatFrames, ref frozenAdrenalinePauseTimer);
                frozenStatLife = Player.statLife;
                frozenLifeRegenTime = Player.lifeRegenTime;
                frozenStatMana = Player.statMana;
                frozenManaRegenDelay = Player.manaRegenDelay;
                frozenBuffTime ??= new int[Player.MaxBuffs];
                frozenBuffType ??= new int[Player.MaxBuffs];
                Array.Copy(Player.buffTime, frozenBuffTime, Player.MaxBuffs);
                Array.Copy(Player.buffType, frozenBuffType, Player.MaxBuffs);
                frozenWingTime = Player.wingTime;
                frozenRocketTime = Player.rocketTime;
                positionCaptured = true;
                snapshotInventoryOpen = Main.playerInventory;
                frozenSelectedItem = Player.selectedItem;
            }

            Player.position = frozenPosition;
            Player.velocity = Vector2.Zero;
            Player.direction = frozenDirection;
            //防解冻摔伤
            Player.fallStart = (int)(Player.position.Y / 16f);
            //手持栏兜底
            Player.selectedItem = frozenSelectedItem;
            //清滚轮/点击暂存
            Player.HotbarOffset = 0;
            Player.changeItem = -1;

            //禁移动，留鼠标给 UI
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlThrow = false;
            Player.controlSmart = false;
            Player.controlTorch = false;
        }

        public override void PostUpdate() {
            if (!WorldFreezeSystem.IsActive || !positionCaptured) return;
            //PostUpdate 再锁，防他处改位向
            Player.position = frozenPosition;
            Player.velocity = Vector2.Zero;
            Player.direction = frozenDirection;
            Player.selectedItem = frozenSelectedItem;
            //还原冷却计时
            Player.potionDelay = frozenPotionDelay;
            Player.restorationDelayTime = frozenRestorationDelayTime;
            Player.itemAnimation = frozenItemAnimation;
            Player.itemAnimationMax = frozenItemAnimationMax;
            Player.itemTime = frozenItemTime;
            Player.immuneTime = frozenImmuneTime;
            Player.immune = frozenImmune;
            Player.breath = frozenBreath;
            Player.breathCD = frozenBreathCD;
            //还原怒气/肾上腺素
            CWRRef.RestoreRippers(Player, frozenRage, frozenAdrenaline
                , frozenRageGainCooldown, frozenRageCombatFrames, frozenAdrenalinePauseTimer);
            //阻 HP/魔力自然回
            Player.statLife = frozenStatLife;
            Player.lifeRegenTime = frozenLifeRegenTime;
            Player.statMana = frozenStatMana;
            Player.manaRegenDelay = frozenManaRegenDelay;
            for (int i = 0; i < Player.MaxBuffs; i++) {
                if (Player.buffType[i] != 0 && Player.buffType[i] == frozenBuffType[i]) {
                    Player.buffTime[i] = frozenBuffTime[i];
                }
            }
            //还原飞行时间
            Player.wingTime = frozenWingTime;
            Player.rocketTime = frozenRocketTime;

            Main.playerInventory = false;
        }

        public override void FrameEffects() {
            if (!WorldFreezeSystem.IsActive || !positionCaptured) return;
            //锁动画帧
            Player.bodyFrame = frozenBodyFrame;
            Player.legFrame = frozenLegFrame;
            Player.headFrame = frozenHeadFrame;
        }

        public override bool PreItemCheck() {
            if (WorldFreezeSystem.IsActive) return false;
            return true;
        }

        /// <summary>死亡 DeactivateAll 兜底</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) return;
            if (WorldFreezeSystem.IsActive) {
                WorldFreezeSystem.DeactivateAll();
            }
        }
    }
}
