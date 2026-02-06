using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 进场状态：从地下冲出，生成身体
    /// </summary>
    internal class DestroyerIntroState : DestroyerStateBase
    {
        public override string StateName => "Intro";
        private bool hasSpawned;

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            hasSpawned = false;
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            //第一帧AI时生成体节，确保NPC已在世界中完成定位
            if (!hasSpawned) {
                hasSpawned = true;
                if (!VaultUtils.isClient) {
                    DestroyerHeadAI.SpawnBodySegments(context.Npc);
                }
                SoundEngine.PlaySound(SoundID.Roar, context.Npc.Center);
                context.Npc.velocity = Vector2.UnitY * -20f;
            }

            Timer++;
            if (Timer > 120) {
                return new DestroyerPatrolState();
            }
            return null;
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
        }
    }
}
