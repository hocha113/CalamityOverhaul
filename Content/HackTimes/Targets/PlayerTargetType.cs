using CalamityOverhaul.Content.HackTimes.Scannables;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Targets
{
    /// <summary>
    /// 敌对玩家目标（PvP）。<br/>
    /// 悬停优先级 140：高于 BossPart(120) 与 Npc(100)——玩家骑坐骑/被小怪叠住时
    /// 必须能选到人。设计稿原案的排序假设"SelfRig &gt; Player"已随 2026-08 扩展批
    /// 对 SelfRig 的取反裁决（自体压到 -100 全场最低）失效：SelfRig 只探测本机玩家、
    /// 本类只探测其他玩家，两者永不竞争同一具身体；他人叠在自己身上时按现行哲学
    /// 让他人赢（想选自己得指着空处的自己）。<br/>
    /// 单人模式没有第二个玩家，本类天然无产出，不需要模式闸
    /// </summary>
    internal class PlayerTargetType : HackTargetType
    {
        public override HackTargetKind Kind => HackTargetKind.Player;

        public override int HoverPriority => 140;

        public override IHackTarget TryDetectHovered(Vector2 mouseWorld) {
            //悬停探测只跑在本机（HackTimeTargeting 已按 myPlayer 闸）
            if (Main.netMode == NetmodeID.SinglePlayer) return null;

            for (int i = 0; i < Main.maxPlayers; i++) {
                if (i == Main.myPlayer) continue;
                Player player = Main.player[i];
                if (player?.active != true || player.dead || player.ghost) continue;

                Rectangle hitbox = player.Hitbox;
                hitbox.Inflate(8, 8);
                if (!hitbox.Contains(mouseWorld.ToPoint())) continue;

                //不满足准入的玩家仍可选中——扫描面板降级灰态但行仍可读，
                //侦察价值保留（IsHackable 才是上传闸）
                return new PlayerScannable(i);
            }
            return null;
        }
    }
}
