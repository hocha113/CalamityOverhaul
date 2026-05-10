using CalamityOverhaul.Content.HackTimes;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Cyberwares.Implementation.CstmVisualEyes
{
    /// <summary>
    /// 网络型义眼 —— "CSTM 视像义眼"
    /// <br/>挂载于光学槽位的高阶网络义体，本身具备一定的辅助/信息处理能力（待扩展）
    /// <br/>核心系统级特性：
    /// <list type="bullet">
    ///   <item>装备后通过 <see cref="HackTimeAccess"/> 授予骇客时间使用资格（网络义体接入认证）</item>
    ///   <item>通过 <see cref="CstmVisualEyeRamProvider"/> 永久性地为玩家提供 +<see cref="RamCapacityBonus"/> RAM 上限</item>
    ///   <item>装备时启用 <see cref="CstmVisualEyeHUD"/>，在屏幕左下角呈现独立 RAM 弧形条；手持 SHPC 时由 SHPC HUD 接管，避免重叠</item>
    /// </list>
    /// 设计上严格保持解耦：
    /// <list type="bullet">
    ///   <item>HackTime 授权通过谓词条件查询玩家装备状态，无需在装备/卸载事件里显式注册</item>
    ///   <item>RAM 修饰器通过 <see cref="CstmVisualEyePlayer"/> 在世界进入时一次性挂入，IsActive 内部回查装备状态，自动开关</item>
    ///   <item>HUD 通过自身 Active 属性判定显隐，不与 SHPCUI 共享任何运行时状态</item>
    /// </list>
    /// </summary>
    internal class CstmVisualEye : BaseCyberware
    {
        /// <summary>
        /// 该义眼提供的 RAM 上限加成（永久生效，仅在装备期间生效）
        /// </summary>
        public const int RamCapacityBonus = 4;

        public override CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.OcularSystem;

        public override int CapacityCost => 4;

        /// <summary>
        /// 在 ModItem 静态初始化阶段一次性注册"装备本义体即可启用骇客时间"的访问条件
        /// <br/>谓词内部动态查询玩家装备，因此不需要在装备/卸载事件里反复注册——这避免了 Item.Clone 造成的实例错位
        /// </summary>
        public override void SetStaticDefaults() {
            HackTimeAccess.Register(player => GetEquipped(player) != null, "Cyberware:CstmVisualEye");
        }

        public override void SetDefaults() {
            base.SetDefaults();
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(0, 12, 0, 0);
        }

        /// <summary>
        /// 查询指定玩家是否装备了 <see cref="CstmVisualEye"/>，未装备返回 null
        /// </summary>
        public static CstmVisualEye GetEquipped(Player player) {
            if (player == null || !player.active) {
                return null;
            }
            CyberwarePlayer cyberPlayer = player.GetModPlayer<CyberwarePlayer>();
            if (cyberPlayer?.EquippedCyberwares == null) {
                return null;
            }
            for (int i = 0; i < CyberwarePlayer.SlotCount; i++) {
                if (cyberPlayer.EquippedCyberwares[i]?.ModItem is CstmVisualEye eye) {
                    return eye;
                }
            }
            return null;
        }
    }
}
