using CalamityOverhaul.Content.NPCs.ScrapCommanders.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.States
{
    /// <summary>脱战离场：垂链失神一拍，随后拖着链条加速升空淡出</summary>
    [InnoVault.StateMachines.VaultState((int)ScrapStateIndex.Despawn, typeof(ScrapStateContext))]
    internal class ScrapDespawnState : ScrapStateBase
    {
        public override string StateName => "Despawn";
        public override ScrapStateIndex StateIndex => ScrapStateIndex.Despawn;

        private const int LiftBeat = 20;
        private const int FadeStart = 34;
        private const int DespawnEnd = 84;

        private bool liftPlayed;

        public override IScrapState OnUpdate(ScrapStateContext ctx) {
            NPC npc = ctx.Npc;
            int t = (int)Timer;

            npc.dontTakeDamage = t > FadeStart;

            if (t < LiftBeat) {
                //失神一拍：链条彻底卸劲
                npc.velocity *= 0.9f;
            }
            else {
                if (!liftPlayed) {
                    liftPlayed = true;
                    SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 1 }, npc.Center);
                }
                npc.velocity.X *= 0.96f;
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y - 0.7f, -28f, 10f);
            }

            //拖链淡出
            float fade = 1f - MathHelper.Clamp((t - FadeStart) / 42f, 0f, 1f);
            ctx.HeadAlpha = fade;
            for (int i = 0; i < ScrapCommander.ArmCount; i++) {
                ctx.ToolAlpha[i] = fade;
            }

            Timer++;
            if (t >= DespawnEnd && !VaultUtils.isClient) {
                npc.active = false;
                //active=false 不再走常规 netUpdate 通道，显式广播下线
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
            }
            return null;
        }
    }
}
