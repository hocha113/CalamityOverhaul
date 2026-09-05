using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 发射器族方案基类。族级三轴：弹药定制、引爆时机、区域压制。<br/>
    /// 引爆总架构：不替换原版火箭弹幕，owner 对自己已打标的弹幕走
    /// <see cref="GsDetonate"/>。爆炸类压 timeLeft=3 进原版爆窗（这正是原版碰撞起爆的
    /// 原生入口，Resize 判伤、置液、撒子雷全部保真），非爆炸类直接 Kill；
    /// 弹幕死亡由 tML 原生同步，联机零自建包
    /// </summary>
    internal abstract class GsLauncherScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Launchers";

        /// <summary>
        /// 子弹幕承签默认处理：第二打标槽在本族是「主弹私旗」（弹药 ID/落位序号/嵌入态等），
        /// 子弹幕一律清零防旗号误继承
        /// </summary>
        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) => router.MarkData2 = 0f;

        //==================== 引爆帮手 ====================

        /// <summary>
        /// 单弹引爆。爆炸类（aiStyle 16 或 Explosive 集）压 timeLeft=3 进原版爆窗：
        /// Resize 判伤、爆炸视觉、置液、集束撒子雷全走原版路径；速度清零并 netUpdate，
        /// 保证远端同步到定点后再收死亡包，爆点各端一致。非爆炸类直接 Kill 走死亡路径
        /// </summary>
        internal static void GsDetonate(Projectile proj) {
            if (proj.aiStyle == ProjAIStyleID.Explosive || ProjectileID.Sets.Explosive[proj.type]) {
                if (proj.timeLeft > 3) {
                    proj.velocity = Vector2.Zero;
                    proj.timeLeft = 3;
                    proj.netUpdate = true;
                }
                return;
            }
            proj.Kill();
        }

        //==================== 出手后坐 ====================

        /// <summary>
        /// 发射器出手后坐冲量。只应在 GsShoot（owner 端）调用；坐骑减半、空中加成 1.5 倍
        /// </summary>
        protected static void LaunchRecoil(Player player, Vector2 velocity, float recoil) {
            if (recoil <= 0f) {
                return;
            }
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
            float mul = player.mount?.Active == true ? 0.5f : 1f;
            if (player.velocity.Y != 0f) {
                mul *= 1.5f;
            }
            player.velocity -= aim * recoil * mul;
        }

        /// <summary>本地战斗文本提示（只在 myPlayer 端冒字）</summary>
        protected static void LocalTip(Player player, LocalizedText text, Color color) {
            if (player.whoAmI == Main.myPlayer && !VaultUtils.isServer) {
                CombatText.NewText(player.getRect(), color, text.Value);
            }
        }

        /// <summary>identity 确定性散列（0~1），绘制与散布路径禁 Main.rand 时用</summary>
        internal static float IdentityHash01(int identity, float salt = 0f)
            => MathF.Abs(MathF.Sin(identity * 12.9898f + salt * 78.233f) * 43758.5453f) % 1f;
    }

    /// <summary>
    /// 发射器族每玩家状态。全部字段只在 myPlayer 路径读写（射击链/PostUpdate
    /// 都发生在 owner 端逻辑流），联机零同步需求；远端玩家看到的引爆由弹幕死亡自然呈现
    /// </summary>
    internal class GsLaunchersPlayer : ModPlayer
    {
        /// <summary>粘附雷落位序号发号器（榴弹发射器）</summary>
        internal int grenadeSeq;
        /// <summary>地雷布设序号发号器</summary>
        internal int mineSeq;
        /// <summary>地雷回收返还预算（每分钟回满 8）</summary>
        internal int mineRecycleBudget = 8;
        private int mineRecycleTimer;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (++mineRecycleTimer >= 3600) {
                mineRecycleTimer = 0;
                mineRecycleBudget = 8;
            }
        }
    }
}
