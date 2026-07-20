using System;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary><see cref="WorldFreezeSystem"/> 玩家侧快照与输入锁定</summary>
    internal class WorldFreezePlayer : ModPlayer
    {
        //冻结时的玩家位置快照
        private Vector2 frozenPosition;
        //是否已记录冻结位置
        private bool positionCaptured;
        //冻结时的朝向快照
        private int frozenDirection;
        //冻结时的动画帧快照
        private Rectangle frozenBodyFrame;
        private Rectangle frozenLegFrame;
        private Rectangle frozenHeadFrame;
        //各类计时器快照，避免冷却条/无敌帧/呼吸条等持续推进
        private int frozenPotionDelay;
        private int frozenRestorationDelayTime;
        private int frozenItemAnimation;
        private int frozenItemAnimationMax;
        private int frozenItemTime;
        private int frozenImmuneTime;
        private bool frozenImmune;
        private int frozenBreath;
        private int frozenBreathCD;
        //Calamity 怒气与肾上腺素快照
        private float frozenRage;
        private float frozenAdrenaline;
        private int frozenRageGainCooldown;
        private int frozenRageCombatFrames;
        private int frozenAdrenalinePauseTimer;
        //HP、魔力及回复计时器快照
        private int frozenStatLife;
        private float frozenLifeRegenTime;
        private int frozenStatMana;
        private float frozenManaRegenDelay;
        //buff 持续时间快照，阻止药水病 / 闪避冷却等 buff 在冻结期间流逝
        private int[] frozenBuffTime;
        private int[] frozenBuffType;
        //飞行 / 翅膀时间快照，防止开启或关闭冻结时飞行时间被重置
        //字段保持 internal 以便上层系统（如 HackTime）在合适时机预填，避免初次进入冻结被覆盖
        internal float frozenWingTime;
        internal int frozenRocketTime;
        //背包开启状态
        private bool snapshotInventoryOpen;
        //冻结时的手持栏选择快照
        private int frozenSelectedItem;

        //ProcessTriggers 在 TriggersSet.CopyInto 尾部触发：control 拷贝与 QuickBuff/Loadout 消费已经发生，
        //但数字键 1~0、快捷栏加减、径向轮盘都在 CopyInto 返回之后才被读取，此处清掉对本帧有效
        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (!WorldFreezeSystem.IsActive) {
                return;
            }
            //数字键 1~0 直改 selectedItem 不经过 ScrollHotbar，在触发器层吞掉
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
            //手柄快捷栏加减与径向轮盘（轮盘会调 QuickMount/QuickHeal 等）同样屏蔽
            triggersSet.HotbarPlus = false;
            triggersSet.HotbarMinus = false;
            triggersSet.RadialHotbar = false;
            triggersSet.RadialQuickbar = false;
        }

        //本地玩家的 control 在 CopyInto（晚于 PreUpdate）里被重新写入，
        //只有 SetControls 这个时机清理才真正生效；PreUpdate 里那份清理只对远程玩家实例兜底
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
            //丢弃物品
            Player.controlThrow = false;
            //智能选择会临时把手持换成火把，也属于换物品路径
            Player.controlTorch = false;
            Player.controlSmart = false;
            //冻结期间背包本就被强制关闭，掐掉开关键避免开合闪烁
            Player.controlInv = false;
        }

        public override void PreUpdate() {
            if (!WorldFreezeSystem.IsActive) {
                if (positionCaptured) {
                    //还原背包
                    Main.playerInventory = snapshotInventoryOpen;
                    //还原飞行时间
                    Player.wingTime = frozenWingTime;
                    Player.rocketTime = frozenRocketTime;
                }
                positionCaptured = false;
                return;
            }

            //首次冻结时快照位置、朝向、动画帧及各类计时器
            if (!positionCaptured) {
                frozenPosition = Player.position;
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
                //快照飞行时间
                frozenWingTime = Player.wingTime;
                frozenRocketTime = Player.rocketTime;
                positionCaptured = true;
                //背包开启状态
                snapshotInventoryOpen = Main.playerInventory;
                //手持栏选择快照
                frozenSelectedItem = Player.selectedItem;
            }

            //锁定位置和速度
            Player.position = frozenPosition;
            Player.velocity = Vector2.Zero;
            //锁定朝向
            Player.direction = frozenDirection;
            //防止解冻后摔落伤害
            Player.fallStart = (int)(Player.position.Y / 16f);
            //锁定手持选择，滚轮/数字键/点击快捷栏在入口已拦，这里兜底直改路径
            Player.selectedItem = frozenSelectedItem;
            //清掉滚轮偏移与点击暂存，防止解冻瞬间补切
            Player.HotbarOffset = 0;
            Player.changeItem = -1;

            //禁移动键，保留鼠标给 UI
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
            //PostUpdate 后再次锁定，防止其他系统在更新中修改朝向和位置
            Player.position = frozenPosition;
            Player.velocity = Vector2.Zero;
            Player.direction = frozenDirection;
            //二次锁定手持选择
            Player.selectedItem = frozenSelectedItem;
            //还原各类冷却计时器，使其在冻结期间不流逝
            Player.potionDelay = frozenPotionDelay;
            Player.restorationDelayTime = frozenRestorationDelayTime;
            Player.itemAnimation = frozenItemAnimation;
            Player.itemAnimationMax = frozenItemAnimationMax;
            Player.itemTime = frozenItemTime;
            Player.immuneTime = frozenImmuneTime;
            Player.immune = frozenImmune;
            Player.breath = frozenBreath;
            Player.breathCD = frozenBreathCD;
            //还原 Calamity 怒气与肾上腺素，阻止冻结期间充能或衰减
            CWRRef.RestoreRippers(Player, frozenRage, frozenAdrenaline
                , frozenRageGainCooldown, frozenRageCombatFrames, frozenAdrenalinePauseTimer);
            //阻止 HP 和魔力在冻结期间自然恢复
            Player.statLife = frozenStatLife;
            Player.lifeRegenTime = frozenLifeRegenTime;
            Player.statMana = frozenStatMana;
            Player.manaRegenDelay = frozenManaRegenDelay;
            //还原 buff 计时
            for (int i = 0; i < Player.MaxBuffs; i++) {
                if (Player.buffType[i] != 0 && Player.buffType[i] == frozenBuffType[i]) {
                    Player.buffTime[i] = frozenBuffTime[i];
                }
            }
            //还原飞行时间，阻止翅膀耐久在冻结期间消耗或被系统归零
            Player.wingTime = frozenWingTime;
            Player.rocketTime = frozenRocketTime;

            //关闭背包
            Main.playerInventory = false;
        }

        public override void FrameEffects() {
            if (!WorldFreezeSystem.IsActive || !positionCaptured) return;
            //锁定动画帧，阻止任何帧变化
            Player.bodyFrame = frozenBodyFrame;
            Player.legFrame = frozenLegFrame;
            Player.headFrame = frozenHeadFrame;
        }

        public override bool PreItemCheck() {
            if (WorldFreezeSystem.IsActive) return false;
            return true;
        }

        /// <summary>死亡时 DeactivateAll，异常路径兜底</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) return;
            if (WorldFreezeSystem.IsActive) {
                WorldFreezeSystem.DeactivateAll();
            }
        }
    }
}
