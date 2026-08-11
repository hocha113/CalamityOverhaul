using Terraria;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 坐标广播（默认档）：十五秒内防守方位置对攻击方全队穿墙透明。<br/>
    /// 位置数据原版本来就全员同步，本协议卖的是 UI 显性化——把"理论上知道"
    /// 变成"抬眼就看到"。防守方侧无任何数值变化，帐本条目明示"位置已暴露"
    /// （知道自己被点亮，才有反制决策）。<br/>
    /// 标记只画给攻击方队伍：表现由 PlayerEffectState 镜像驱动，
    /// <c>PlayerHackLinkRender</c> 按"观众 == 施加者 或 同其非零队"过滤，
    /// 防守方与无关旁观者不画（信息不对称是攻防博弈的一部分）
    /// </summary>
    internal class PositionCast : PlayerHackDef
    {
        /// <summary>观众是否属于该施加者的标记受益面（施加者本人或其非零同队）</summary>
        internal static bool ViewerBenefits(int casterIndex) {
            if (casterIndex < 0 || casterIndex >= Main.maxPlayers) return false;
            if (casterIndex == Main.myPlayer) return true;
            Player caster = Main.player[casterIndex];
            Player viewer = Main.LocalPlayer;
            return caster?.active == true && viewer?.active == true
                && caster.team != 0 && caster.team == viewer.team;
        }

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 2;
            Category = QuickHackCategory.Covert;
            //默认档：启用 PvP 骇入的服务器里人人都有的基本盘，无芯片
            UnlockedByDefault = true;
        }

        public override int GetDuration() => 900;

        //防守方侧刻意零逻辑：帐本条目本身就是"位置已暴露"的告知，
        //标记绘制在表现层按镜像走，不读防守方本机任何值

        public override string GlyphDiePath =>
            //晶粒纹：定位十字 + 外扩波圈（Q 弧），读作"被点亮的信标"
            "M 0 -0.42 L 0 0.42 M -0.42 0 L 0.42 0 "
            + "M -0.18 -0.62 Q 0 -0.78 0.18 -0.62 "
            + "M -0.34 -0.5 Q 0 -0.82 0.34 -0.5 "
            + "M 0 -0.1 L 0.1 0 L 0 0.1 L -0.1 0 Z";
    }
}
