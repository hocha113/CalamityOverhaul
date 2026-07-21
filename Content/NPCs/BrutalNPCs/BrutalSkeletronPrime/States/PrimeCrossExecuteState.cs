using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>十字绞杀</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.CrossExecute, typeof(PrimeStateContext))]
    internal class PrimeCrossExecuteState : PrimeStateBase
    {
        public override string StateName => "CrossExecute";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.CrossExecute;

        internal static int Telegraph => 36;
        internal static int Execute => 90;
        private static int Total => Telegraph + Execute + 20;

        /// <summary>四臂封位槽</summary>
        internal static readonly Vector2[] ArmSlots = new Vector2[] {
            new(-320f, 0f), new(320f, 0f), new(0f, -280f), new(0f, 280f),
        };

        /// <summary>十字锚点，预警瞬间冻</summary>
        private Vector2 crossCenter;

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = (float)PrimeCommandKind.CrossExecute;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;

            if (Timer <= 1) {
                crossCenter = context.Target.Center;
            }

            //头退对角缝上方
            int side = npc.Center.X >= crossCenter.X ? 1 : -1;
            Vector2 anchor = crossCenter + new Vector2(330f * side, -400f);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.05f, 0.12f);
            LeanTowards(npc, crossCenter);

            if (Timer < Telegraph) {
                context.SetChargeState(1, Timer / (float)Telegraph);
                //十字灼烧预警
                if (!VaultUtils.isClient && Timer == 1) {
                    for (int i = 0; i < 4; i++) {
                        PrimeTelegraphLine.SpawnLine(npc, crossCenter, MathHelper.PiOver2 * i, Telegraph);
                    }
                }
            }
            else if (Timer == Telegraph) {
                FireCrossBeams(context);
            }

            Timer++;
            if (Timer >= Total && !VaultUtils.isClient) {
                npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        /// <summary>四道热射线向心</summary>
        private void FireCrossBeams(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (!VaultUtils.isClient) {
                int damage = ScaleDamage((int)(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser) * 1.15f));
                for (int i = 0; i < ArmSlots.Length; i++) {
                    float inwardAngle = (-ArmSlots[i]).ToRotation();
                    Projectile.NewProjectile(npc.GetSource_FromAI(), crossCenter + ArmSlots[i], Vector2.Zero,
                        ModContent.ProjectileType<PrimeCrossBeamProj>(), damage, 0f, Main.myPlayer,
                        npc.whoAmI, inwardAngle, 0f);
                }
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = 0.3f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.2f, MaxInstances = 3 }, crossCenter);
                PrimeScreenEffects.PushShockRing(crossCenter, 0.9f, 620f);
                PrimeDeathPerformancePlayer.RequestShake(8f, 12);
            }
        }

        public override void OnExit(PrimeStateContext context) {
            base.OnExit(context);
            context.Npc.ai[PrimeAiSlots.HeadCommandSlot] = 0f;
        }
    }
}
