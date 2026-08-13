using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States
{
    /// <summary>脱战离场：俯身潜回海里，风暴散去</summary>
    [InnoVault.StateMachines.VaultState((int)FishronStateIndex.Despawn, typeof(FishronStateContext))]
    internal class FishronDespawnState : FishronStateBase
    {
        public override string StateName => "Despawn";
        public override FishronStateIndex StateIndex => FishronStateIndex.Despawn;
        public override bool AllowFarSnap => false;

        private const int DespawnTime = 110;
        private bool splashed;

        public override void OnEnter(FishronStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            splashed = false;
            SoundEngine.PlaySound(SoundID.Zombie20 with { Volume = 0.8f, Pitch = -0.45f, MaxInstances = 3 }, context.Npc.Center);
        }

        public override IFishronState OnUpdate(FishronStateContext context) {
            NPC npc = context.Npc;

            npc.damage = 0;
            npc.dontTakeDamage = true;

            //风暴撤场
            float t = MathHelper.Clamp(Timer / (float)DespawnTime, 0f, 1f);
            context.StormBoost = -context.PhaseStormGrade * t;

            //先仰头蓄势，再俯冲入水
            if (Timer < 26) {
                npc.velocity = Vector2.Lerp(npc.velocity, -Vector2.UnitY * 3f, 0.12f);
            }
            else {
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.UnitY * 30f, 0.09f);
                AimBodyAlongVelocity(npc);
                context.FrameCommand = 2;

                //贴近水/地表面时炸一次水花后隐去
                if (!splashed) {
                    Vector2 surface = FishronMotionFX.FindSurfaceBelow(npc.Center, out _);
                    if (npc.Center.Y > surface.Y - 60f) {
                        splashed = true;
                        FishronMotionFX.SpawnSplashBurst(surface, 1.7f);
                        npc.alpha = 255;
                    }
                }
            }

            if (splashed || Timer >= DespawnTime) {
                if (!VaultUtils.isClient) {
                    DukeFishronAI.ClearMinions(alsoTornado: true);
                    npc.active = false;
                    npc.netUpdate = true;
                }
            }

            Timer++;
            return null;
        }
    }
}
