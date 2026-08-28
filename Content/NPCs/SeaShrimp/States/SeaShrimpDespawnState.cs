using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.States
{
    /// <summary>脱战离场：钻回海底方向缓行淡出，90 帧后放行原版脱战</summary>
    [InnoVault.StateMachines.VaultState((int)SeaShrimpStateIndex.Despawn, typeof(SeaShrimpStateContext))]
    internal class SeaShrimpDespawnState : SeaShrimpStateBase
    {
        public override string StateName => "Despawn";
        public override SeaShrimpStateIndex StateIndex => SeaShrimpStateIndex.Despawn;

        public override ISeaShrimpState OnUpdate(SeaShrimpStateContext ctx) {
            NPC npc = ctx.Npc;
            Timer++;

            ctx.Owner.Locomotion.RequestCrawlTo(npc.Center + new Vector2(0f, 400f), 0.6f);
            ctx.BodyAlpha = MathHelper.Clamp(1f - Timer / 90f, 0f, 1f);
            ctx.WaveGain = 0.6f;

            if (Timer >= 90f) {
                npc.EncourageDespawn(10);
                //实体移除只由权威端裁决，客户端等服务器包
                if (!VaultUtils.isClient) {
                    npc.active = false;
                }
            }
            return null;
        }
    }
}
