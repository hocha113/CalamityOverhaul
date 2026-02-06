using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 脱战状态
    /// </summary>
    internal class DestroyerDespawnState : DestroyerStateBase
    {
        public override string StateName => "Despawn";

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.Y = 82f;
            npc.dontTakeDamage = true;

            Timer++;
            if (Timer > 180) {
                if (!VaultUtils.isClient) {
                    npc.active = false;
                    npc.netUpdate = true;
                    DestroyerHeadAI.HandleDespawn();
                    DestroyerHeadAI.SendDespawn();
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 死亡动画状态
    /// </summary>
    internal class DestroyerDeathState : DestroyerStateBase
    {
        public override string StateName => "Death";

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            npc.velocity *= 0.9f;
            npc.rotation += 0.05f;

            Timer++;

            if (Timer % 5 == 0) {
                Vector2 randomPos = npc.Center + Main.rand.NextVector2Circular(100, 100);
                SoundEngine.PlaySound(SoundID.Item14, randomPos);
                Dust.NewDust(randomPos, 0, 0, DustID.Smoke, 0, 0, 100, default, 3f);
            }

            if (Timer > 180) {
                npc.life = 0;
                npc.HitEffect();
                npc.checkDead();
            }

            return null;
        }
    }
}
