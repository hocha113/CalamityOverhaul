using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// MagicMorph 族玩家状态：蓄力进度、右键闩锁、模式窗、回血预算与雨云偏好。<br/>
    /// 全部为实例字段（每玩家一份）；蓄力/模式只在本地玩家路径写入与消费，
    /// 联机时远端玩家的这些字段保持默认值，跨端可见的行为由真弹幕承载
    /// </summary>
    internal class GsMorphPlayer : ModPlayer
    {
        /// <summary>蓄力中的武器物品 ID，0=未蓄力</summary>
        public int ChargingItem;

        /// <summary>当前蓄力帧数</summary>
        public int ChargeTicks;

        /// <summary>右键按住期间的防重触发闩锁（松开右键复位）</summary>
        public bool AltLatch;

        /// <summary>模式切换型 B 形态（吹叶机/滋滋橙）的持续截止帧</summary>
        public uint ModeUntil;

        /// <summary>模式窗绑定的武器物品 ID</summary>
        public int ModeItem;

        /// <summary>血荆棘回血预算：窗口计时（60t 滚动）</summary>
        public int HealBudgetTick;

        /// <summary>血荆棘回血预算：窗内已回血量（上限 3HP/s）</summary>
        public int HealBudget;

        /// <summary>雨云魔棒形态偏好：false=温雨（伤害），true=雷雨（落雷）</summary>
        public bool NimbusStorm;

        public void BeginCharge(int itemType) {
            if (ChargingItem != itemType) {
                ChargingItem = itemType;
                ChargeTicks = 0;
            }
        }

        public void EndCharge() {
            ChargingItem = 0;
            ChargeTicks = 0;
        }

        public bool ModeActive(int itemType) => ModeItem == itemType && Main.GameUpdateCount < ModeUntil;

        public void OpenMode(int itemType, int durationTicks) {
            ModeItem = itemType;
            ModeUntil = Main.GameUpdateCount + (uint)durationTicks;
        }

        public void ClearMode() {
            ModeItem = 0;
            ModeUntil = 0;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!PlayerInput.Triggers.Current.MouseRight) {
                AltLatch = false;
            }
            //兜底清理：关模式/换武器/死亡时终止蓄力与模式窗（GsHoldItem 在这些情形下不再被分发）
            if (ChargingItem != 0 && (!GameModeSystem.GodSmithActive || Player.dead
                || Player.HeldItem == null || Player.HeldItem.type != ChargingItem)) {
                EndCharge();
            }
            if (ModeItem != 0 && (!GameModeSystem.GodSmithActive || Player.dead)) {
                ClearMode();
            }
            //回血预算窗滚动
            if (++HealBudgetTick >= 60) {
                HealBudgetTick = 0;
                HealBudget = 0;
            }
        }

        public override void PostUpdateRunSpeeds() {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //蓄力减速：重咏唱体感（蓄力状态只存在于本地玩家端）
            if (ChargingItem != 0
                && GodSmithScheme.TryGetScheme(ChargingItem, out GodSmithScheme scheme)
                && scheme is GsMorphScheme morph) {
                Player.maxRunSpeed *= morph.ChargeSlowdown;
                Player.accRunSpeed *= morph.ChargeSlowdown;
            }
            //虹桥友方增益：本端玩家踩在任意虹桥带内 +8% 移速（各端本地自查，桥为全端可见真弹幕）
            if (Player.whoAmI == Main.myPlayer && GsRainbowBridgeProj.LocalPlayerOnAnyBridge(Player)) {
                Player.maxRunSpeed *= 1.08f;
                Player.accRunSpeed *= 1.08f;
            }
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //彩虹枪棱彩领域：本玩家弹幕在彩虹弧上方增益带内命中时 +8% 伤害（攻击方端天然正确）
            GsRainbowGun.TryBandBonus(Player, proj, ref modifiers);
        }
    }
}
