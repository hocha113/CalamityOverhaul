using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>
    /// 自体目标：光标落在本机玩家自己身上时命中。<br/>
    /// 悬停优先级刻意压到全场最低，只有指着自己且指不到任何别的东西时才出现，
    /// 绝不与 NPC/物块/掉落物抢选中（设计稿原案是最高优先，此处按 2026-08
    /// 扩展批的裁决取反：站在敌群里想选自己的场合远少于误选自己的场合）。<br/>
    /// 已知取舍：玩家泡在液体里时液体目标（-10）仍会盖过自机
    /// </summary>
    internal class SelfRigTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.SelfRig;

        public override int HoverPriority => -100;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            //悬停探测只跑在本机（HackTimeTargeting 已按 myPlayer 闸），
            //这里再守一遍，远端玩家不可作为悬停产物
            Player player = Main.LocalPlayer;
            if (player?.active != true || player.dead || player.ghost) return null;

            Rectangle hitbox = player.Hitbox;
            hitbox.Inflate(8, 8);
            if (!hitbox.Contains(mouseWorld.ToPoint())) return null;

            return new SelfRigScannable(player.whoAmI);
        }
    }
}
