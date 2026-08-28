using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>悬吊巡航连接态：呼吸拍+轻压制，选下一招</summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.Canopy, typeof(PlanteraStateContext))]
    internal class PlanteraCanopyState : PlanteraStateBase
    {
        public override string StateName => "Canopy";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.Canopy;

        private int Duration(PlanteraStateContext ctx) {
            int baseTime = ctx.IsPhase2 ? 58 : 88;
            if (ctx.IsLowLife) {
                baseTime -= 12;
            }
            return (int)(baseTime * PlanteraDirector.TimeScale(ctx));
        }

        public PlanteraCanopyState() {
        }

        public override void OnEnter(PlanteraStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                //连接拍兜底补钩爪
                PlanteraAI.EnsureHooks(context.Npc);

                //二阶段缓慢补触手(一拍最多补一根，被打光的缺口不会立刻愈合)
                if (context.IsPhase2) {
                    int target = context.IsAsuraMode ? 10 : 8;
                    if (context.Tentacles.Count > 0 && context.Tentacles.Count < target) {
                        PlanteraTentacleAI.SpawnTentacle(context.Npc, Main.rand.NextFloat(MathHelper.TwoPi));
                    }
                }
            }
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //侧向漂移，绕着玩家换位
            float side = (float)System.Math.Sin(Timer * 0.02f + npc.whoAmI);
            Vector2 offset = new(side * 130f, -40f);
            SetSuspension(context, offset,
                context.IsPhase2 ? PlanteraDirector.DriftSpeedP2 : PlanteraDirector.DriftSpeedP1, 0.055f);

            //轻压制：稀疏单发种子(开场留静默窗)
            int fireGap = context.IsPhase2 ? 26 : 34;
            if (Timer > 24 && Timer % fireGap == 0 && !VaultUtils.isClient
                && Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 46f, aim * 17f,
                    ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
                npc.velocity -= aim * 1.4f;
            }
            if (Timer > 24 && Timer % fireGap == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = 0.1f, MaxInstances = 5 }, npc.Center);
            }

            Timer++;

            if (Timer >= Duration(context) && !VaultUtils.isClient) {
                return PlanteraDirector.CreateState(PlanteraDirector.NextAttack(context));
            }

            return null;
        }
    }
}
