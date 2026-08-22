using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.States
{
    /// <summary>
    /// 孢子云播撒：三轮喷发把漂浮地雷铺进战场，
    /// 雷慢漂向玩家、被打爆连锁殉爆，清雷本身是风险决策
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PlanteraStateIndex.SporeSow, typeof(PlanteraStateContext))]
    internal class PlanteraSporeSowState : PlanteraStateBase
    {
        public override string StateName => "SporeSow";
        public override PlanteraStateIndex StateIndex => PlanteraStateIndex.SporeSow;

        private const int Vent1 = 40;
        private const int Vent2 = 120;
        private const int Vent3 = 200;
        private const int StateEnd = 268;

        public PlanteraSporeSowState() {
        }

        public override IPlanteraState OnUpdate(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //喷发前的鼓包蓄压(读得出下一口要吐)
            int nextVent = Timer <= Vent1 ? Vent1 : Timer <= Vent2 ? Vent2 : Vent3;
            int toVent = nextVent - Timer;
            if (toVent >= 0 && toVent < 20) {
                float squash = 1f - toVent / 20f;
                context.BodyScalePulse = squash * 0.07f;
                context.GlowPulse = 0.3f + squash * 0.4f;
                if (!VaultUtils.isServer && toVent < 12) {
                    PlanteraRenderHelper.SpawnChargeIntake(context, squash);
                }
            }

            //三轮喷发
            if (Timer == Vent1 || Timer == Vent2 || Timer == Vent3) {
                DoVent(context);
            }

            //喷发间本体换边(左右倒换站位)
            float side = Timer < Vent2 ? -1f : Timer < Vent3 ? 1f : -1f;
            SetSuspension(context, new Vector2(side * 200f, -60f),
                context.IsPhase2 ? PlanteraDirector.DriftSpeedP2 : PlanteraDirector.DriftSpeedP1, 0.06f);

            //轻种子压制
            if (Timer % 30 == 0 && Timer > 20 && !VaultUtils.isClient
                && Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                Vector2 aim = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + aim * 46f, aim * 17f,
                    ModContent.ProjectileType<PlanteraSeed>(), PlanteraSeed.GetDamage(npc), 0f, Main.myPlayer);
            }

            if (Timer >= StateEnd && !VaultUtils.isClient) {
                return new PlanteraCanopyState();
            }
            return null;
        }

        /// <summary>一轮喷发：朝玩家扇面撒雷</summary>
        private void DoVent(PlanteraStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            if (!VaultUtils.isClient) {
                int count = context.IsDeathMode ? 9 : 7;
                Vector2 baseDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                for (int i = 0; i < count; i++) {
                    float spread = MathHelper.Lerp(-1.1f, 1.1f, count <= 1 ? 0.5f : i / (float)(count - 1))
                        + Main.rand.NextFloat(-0.12f, 0.12f);
                    Vector2 vel = baseDir.RotatedBy(spread) * Main.rand.NextFloat(5f, 8.5f);
                    PlanteraSporeAI.SpawnSpore(npc, npc.Center + vel * 6f, vel);
                }
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.8f, Pitch = -0.15f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.7f, Pitch = -0.5f }, npc.Center);
                PlanteraRenderHelper.SpawnSporePuff(npc.Center, 1.5f);
                PlanteraScreenFX.CameraPunch(npc.Center, 3f, 10, "PlanteraSporeVent");
            }

            //喷发后坐
            Vector2 recoil = (npc.Center - player.Center).SafeNormalize(Vector2.UnitY);
            npc.velocity += recoil * 5f;
        }
    }
}
