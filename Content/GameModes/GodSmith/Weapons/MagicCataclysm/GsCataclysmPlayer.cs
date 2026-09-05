using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 灾变计量每玩家状态。计量只在攻击方端积攒（命中钩子只在攻击方端执行），天然联机安全
    /// </summary>
    internal class GsCataclysmPlayer : ModPlayer
    {
        /// <summary>灾变计量（攻击方本地量）</summary>
        public int Charge;
        /// <summary>计量绑定的武器物品 ID，换灾变武器时重绑清零</summary>
        public int BoundItemType;

        /// <summary>积攒计量；绑定武器不同则重绑清零后再积攒</summary>
        public void AddCharge(int amount, int max, int weaponItemType) {
            if (BoundItemType != weaponItemType) {
                BoundItemType = weaponItemType;
                Charge = 0;
            }
            if (Charge >= max) {
                return;
            }
            Charge += amount;
            if (Charge > max) {
                Charge = max;
            }
        }
    }
}
