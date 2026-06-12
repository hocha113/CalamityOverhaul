using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 武装阶段大招：四臂飞散四角，电弧链十字旋转收紧，结束时中心脉冲 + 冲击波。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.TetherSpin, typeof(PrimeStateContext))]
    internal class PrimeTetherSpinState : PrimeStateBase
    {
        public override string StateName => "TetherSpin";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.TetherSpin;

        private const int Telegraph = 36;
        private const int SpinDuration = 180;
        private const int Total = Telegraph + SpinDuration + 24;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            Vector2 anchor = context.Target.Center + new Vector2(0, -300);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.04f, 0.15f);
            npc.rotation += 0.08f;

            if (Timer < Telegraph) {
                context.SetChargeState(1, Timer / (float)Telegraph);
                if (!VaultUtils.isClient && Timer == 1) {
                    PrimeTelegraphLine.SpawnRing(npc.Center, Timer / (float)Telegraph, 0.9f, Telegraph);
                }
            }
            else if (Timer < Telegraph + SpinDuration) {
                context.SetChargeState(3, (Timer - Telegraph) / (float)SpinDuration);
                if (!VaultUtils.isServer && Timer % 8 == 0) {
                    Vector2 sparkPos = npc.Center + Main.rand.NextVector2CircularEdge(220f, 220f);
                    Dust dust = Dust.NewDustDirect(sparkPos, 1, 1, DustID.Electric, 0, 0, 100, Color.Cyan, 1.4f);
                    dust.noGravity = true;
                    dust.velocity = (npc.Center - sparkPos) * 0.08f;
                }
            }
            else if (Timer == Telegraph + SpinDuration && !VaultUtils.isServer) {
                PrimeScreenEffects.PushShockRing(npc.Center, 0.95f, 1f);
                PrimeDeathPerformancePlayer.RequestShake(12f, 18);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
            }

            Timer++;
            if (Timer >= Total && !VaultUtils.isClient) {
                return new PrimeCommandSequenceState();
            }
            return null;
        }
    }
}
