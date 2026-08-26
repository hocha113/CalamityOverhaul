using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 灾变计量每玩家状态。计量只在攻击方端积攒与消费（命中钩子只在攻击方端执行），
    /// 触发判定同样走本地玩家路径，天然联机安全；不做 UI，读数走杖尖金辉与就绪音
    /// </summary>
    internal class GsCataclysmPlayer : ModPlayer
    {
        /// <summary>灾变计量（攻击方本地量）</summary>
        public int Charge;
        /// <summary>计量绑定的武器物品 ID，换灾变武器时重绑清零</summary>
        public int BoundItemType;
        /// <summary>冷却截止帧（全族共享一条冷却，防多件轮放）</summary>
        public uint CooldownUntil;
        /// <summary>就绪音已响过（计量清空后复位）</summary>
        public bool ReadyChimed;
        /// <summary>上次触发失败反馈帧（防抖）</summary>
        public uint LastDenyTick;
        /// <summary>星籁演奏会状态（主演出期间由 director 每帧刷新）</summary>
        public bool StellarConcert;

        public bool OnCooldown => Main.GameUpdateCount < CooldownUntil;

        public override void ResetEffects() => StellarConcert = false;

        /// <summary>积攒计量；绑定武器不同则重绑清零后再积攒</summary>
        public void AddCharge(int amount, int max, int weaponItemType) {
            if (BoundItemType != weaponItemType) {
                BoundItemType = weaponItemType;
                Charge = 0;
                ReadyChimed = false;
            }
            if (Charge >= max) {
                return;
            }
            Charge += amount;
            if (Charge > max) {
                Charge = max;
            }
        }

        /// <summary>触发成功：清计量并进入冷却</summary>
        public void ConsumeAndCooldown(int cooldownTicks) {
            Charge = 0;
            ReadyChimed = false;
            CooldownUntil = Main.GameUpdateCount + (uint)cooldownTicks;
        }

        public override void PostUpdateRunSpeeds() {
            //星籁「星海终章」主演出期间的演奏会步伐
            if (StellarConcert) {
                Player.moveSpeed += 0.10f;
            }
        }
    }
}
