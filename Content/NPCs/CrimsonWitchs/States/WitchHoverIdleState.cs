using CalamityOverhaul.Content.NPCs.CrimsonWitchs.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.CrimsonWitchs.States
{
    /// <summary>悬浮待机：弹簧悬停在玩家侧上方带呼吸浮动；
    /// 兼任开放地形阀门（远遁礼仪离场），M3 起作为洗牌袋选招的连接拍</summary>
    [InnoVault.StateMachines.VaultState((int)WitchStateIndex.HoverIdle, typeof(WitchStateContext))]
    internal class WitchHoverIdleState : WitchStateBase
    {
        public override string StateName => "HoverIdle";
        public override WitchStateIndex StateIndex => WitchStateIndex.HoverIdle;

        /// <summary>远遁计时：目标持续超出 LeaveDistance 的帧数</summary>
        private int leaveTimer;

        public override void OnEnter(WitchStateContext context) {
            base.OnEnter(context);
            leaveTimer = 0;
            DisableContactDamage(context.Npc);
        }

        public override IWitchState OnUpdate(WitchStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //目标失效：控制器兜底之外的本地出口
            if (!context.TargetValid) {
                return new WitchDespawnState();
            }

            Player player = context.Target;

            //远遁礼仪：追不上就体面离场，不做狂暴
            if (context.DistanceToTarget > WitchBattleConst.LeaveDistance) {
                leaveTimer++;
                if (leaveTimer >= WitchBattleConst.LeaveGraceTime) {
                    return new WitchDespawnState();
                }
            }
            else {
                leaveTimer = 0;
            }

            //弹簧悬停在玩家侧上方，保持当前侧不来回横跳
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 breathing = new((float)Math.Sin(Timer * 0.024f) * 14f, (float)Math.Sin(Timer * 0.041f) * 9f);
            Vector2 anchor = player.Center + new Vector2(side * 380f, -150f) + breathing;

            //距离越远追得越快，近处只余轻微漂浮（静是她的排面）
            float distance = npc.Center.Distance(anchor);
            float speed = MathHelper.Clamp(distance * 0.02f, 2f, 17f);
            MoveTo(npc, anchor, speed, 0.06f);
            FaceTarget(npc, player.Center);

            return null;
        }
    }
}
