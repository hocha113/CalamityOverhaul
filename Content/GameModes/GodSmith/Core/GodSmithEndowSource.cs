using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神赋弹幕打标（逐实例 GlobalProjectile）：OnSpawn 从 ItemUse 类出生源
    /// 读取源武器的神赋并快照档位，供 <see cref="GodSmithPlayer.OnHitNPCWithProj"/> 回溯。<br/>
    /// 打标只存在于 owner 端且不上网：弹幕命中钩子本就只在 owner 端解算，无需同步；
    /// 若某条神赋要做远端可见的形态强化，在自己的文件里自建同步通道
    /// </summary>
    internal class GodSmithEndowSource : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>来源武器的神赋；null = 未打标</summary>
        internal GodSmithEndow Endow;

        /// <summary>出生时按来源武器词缀快照的档位缩放</summary>
        internal float TierScale = 1f;

        /// <summary>来源武器物品 ID（追溯与调试用）</summary>
        internal int SourceItemType;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //EntitySource_ItemUse_WithAmmo 派生自 EntitySource_ItemUse，一并覆盖
            if (source is not EntitySource_ItemUse itemUse || itemUse.Item == null) {
                return;
            }
            if (!itemUse.Item.TryGetGlobalItem(out GodSmithItem data) || data.Endow is not GodSmithEndow endow) {
                return;
            }
            Endow = endow;
            TierScale = endow.TierScaleFor(itemUse.Item.prefix);
            SourceItemType = itemUse.Item.type;
        }
    }
}
