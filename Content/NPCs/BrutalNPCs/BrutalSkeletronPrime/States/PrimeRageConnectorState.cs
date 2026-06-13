using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>狂暴 connector：60~90帧换弹排气标点；第3手固定 SkullCannon，&lt;35% 表尾追加第二发</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.RageConnector, typeof(PrimeStateContext))]
    internal class PrimeRageConnectorState : PrimeStateBase
    {
        public override string StateName => "RageConnector";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.RageConnector;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            int duration = PrimeDirector.RageConnectorFrames;
            if (context.BossRush) {
                duration -= 12;
            }

            Movement(context);
            LeanByVelocity(npc);
            context.SetChargeState(2, Timer / (float)duration);

            if (!VaultUtils.isServer && Timer % 18 == 0) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.3f, Volume = 0.5f }, npc.Center);
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(40f, 40f);
                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.Smoke, 0, -1.2f, 100, Color.Gray, 1.2f);
                dust.noGravity = false;
            }

            Timer++;
            if (Timer >= duration && !VaultUtils.isClient) {
                return ChooseNextAttack(context);
            }
            return null;
        }

        private static IPrimeState ChooseNextAttack(PrimeStateContext context) {
            bool lowHp = context.Npc.life < context.Npc.lifeMax * 0.35f;
            int index = context.RageAttackIndex % 6;
            context.RageAttackIndex++;

            return index switch {
                0 => new PrimeRageDashState(),
                1 => new PrimeIonOverloadState(),
                2 => new PrimeSkullCannonState(),
                3 => new PrimeGuillotineSpinState(),
                4 => new PrimeRocketCurtainState(),
                _ => lowHp ? new PrimeSkullCannonState() : new PrimeRageDashState(),
            };
        }

        private void Movement(PrimeStateContext context) {
            float vAccel = Main.masterMode ? 0.045f : 0.04f;
            float vMax = Main.masterMode ? 4.5f : 4f;
            float hAccel = Main.masterMode ? 0.1f : 0.09f;
            float hMax = Main.masterMode ? 10f : 9f;
            float decel = Main.masterMode ? 0.9f : 0.8f;
            HoverMovement(context, vAccel, vMax, hAccel, hMax, decel, 150, 380);
        }
    }
}
