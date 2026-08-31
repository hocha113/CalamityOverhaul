using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Core
{
    /// <summary>
    /// 神赋弹幕打标（逐实例 GlobalProjectile）：OnSpawn 从 ItemUse 类出生源
    /// 读取源武器的神赋并快照档位，供 <see cref="GodSmithPlayer.OnHitNPCWithProj"/> 回溯；
    /// 父弹幕已打标时子弹幕承签传染（分裂/集束/派生弹，镜像 GodSmithProjRouter 的承签分支）。
    /// 神赋自产弹（proc 弹）一律用 GetSource_Misc 出生，Misc 源既不打标也不承签，自喂环天然断路。<br/>
    /// 打标只存在于 owner 端且不上网：唯一消费点（弹幕命中钩子）只在 owner 端解算，
    /// 武器子弹幕也在 owner 端出生，owner 本地即闭环；
    /// 若某条神赋要做远端可见的形态强化，在自己的文件里自建同步通道。<br/>
    /// 兼职「接管武器使用沿」补发：接管方案在 GsCanUseItem 里生成 held 并全端返回 false 压掉原版 use 流，
    /// tML 的 UseAnimation 钩子链因此永不触发（Player.cs L39130→L39151→L47062→L5017），
    /// 故在 held 出生沿补发 <see cref="GodSmithEndow.OnUseAnimation"/>，判据见 <see cref="OnSpawn"/>
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

        //==================== 接管武器使用沿窗口 ====================

        //窗口 = GodSmithScheme.CanUseItem 的同步调用栈（由其 try/finally 开合）。
        //tML 只在 itemAnimation==0 时查询使用许可（Player.cs L39120），接管方案在该栈内生成 held；
        //非接管武器的弹幕都出生在 Shoot 期（itemAnimation>0，Player.cs L39786 前置判据）
        //或神赋 OnUseAnimation 原链内（窗口外），据此与原版 UseAnimation 链互斥，不会双触发。
        //static 只承载同步栈内的瞬时窗口（帧戳 + whoAmI 双校验），不跨帧不跨玩家
        private static int useGatePlayer = -1;
        private static uint useGateFrame;

        /// <summary>打开使用沿窗口（仅 GodSmithScheme.CanUseItem 转发处调用，try/finally 保证闭合）</summary>
        internal static void OpenUseGate(Player player) {
            useGatePlayer = player.whoAmI;
            useGateFrame = Main.GameUpdateCount;
        }

        /// <summary>关闭使用沿窗口</summary>
        internal static void CloseUseGate() => useGatePlayer = -1;

        private static bool InUseGate(int owner)
            => useGatePlayer == owner && useGateFrame == Main.GameUpdateCount;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            //EntitySource_ItemUse_WithAmmo 派生自 EntitySource_ItemUse，一并覆盖
            if (source is EntitySource_ItemUse itemUse && itemUse.Item != null) {
                if (!itemUse.Item.TryGetGlobalItem(out GodSmithItem data) || data.Endow is not GodSmithEndow endow) {
                    return;
                }
                Endow = endow;
                TierScale = endow.TierScaleFor(itemUse.Item.prefix);
                SourceItemType = itemUse.Item.type;
                DispatchTakeoverUseAnimation(projectile, itemUse.Item, endow, TierScale);
                return;
            }
            //子弹幕承签：父弹幕已打标则整套打标传染（分裂/集束/派生弹）。
            //ItemUse 源的 Entity 是 Player，不会误入本分支；神赋 proc 弹是 Misc 源不带标，传染链在其身上断路
            if (source is EntitySource_Parent parentSource && parentSource.Entity is Projectile parentProj
                && parentProj.TryGetGlobalProjectile(out GodSmithEndowSource parentMark)
                && parentMark.Endow is GodSmithEndow) {
                Endow = parentMark.Endow;
                TierScale = parentMark.TierScale;
                SourceItemType = parentMark.SourceItemType;
            }
        }

        /// <summary>
        /// 接管武器的 OnUseAnimation 补发（使用沿 = 接管方案在 CanUseItem 栈内生成 held 的那一拍）。
        /// 三闸：使用沿窗口内（排除延迟补射/held 派生弹/神赋原链内生弹等一切非 use 栈生成）、
        /// 原版 use 流未启动（itemAnimation==0——接管路径独有，held 存活期每帧强撑 itemAnimation=2，
        /// 非接管武器出弹时动画必已启动）、每玩家每帧至多一次（一次点击多弹幕只算一次使用）。
        /// 只在生成端（owner）执行：六条 OnUseAnimation 消费者首行全部守 myPlayer，
        /// 原版链的远端模拟本就空转，行为等价
        /// </summary>
        private static void DispatchTakeoverUseAnimation(Projectile projectile, Item item, GodSmithEndow endow, float tierScale) {
            if (!InUseGate(projectile.owner)) {
                return;
            }
            Player player = Main.player[projectile.owner];
            if (player.ItemAnimationActive) {
                return;
            }
            GodSmithPlayer state = player.GetModPlayer<GodSmithPlayer>();
            if (state.LastTakeoverUseAnimFrame == Main.GameUpdateCount) {
                return;
            }
            state.LastTakeoverUseAnimFrame = Main.GameUpdateCount;
            endow.OnUseAnimation(item, player, tierScale);
        }
    }
}
