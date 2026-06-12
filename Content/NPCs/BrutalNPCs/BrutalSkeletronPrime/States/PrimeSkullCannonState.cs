using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 颅骨主炮（&lt;35% 扣留）：90 帧蓄力 → 0.7°/f 巨型光束横扫。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.SkullCannon, typeof(PrimeStateContext))]
    internal class PrimeSkullCannonState : PrimeStateBase
    {
        public override string StateName => "SkullCannon";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.SkullCannon;

        private const int ChargeFrames = 90;
        private const int SilenceFrames = 6;
        private const int SweepFrames = 90;
        private float sweepAngle;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 2;

            Vector2 anchor = context.Target.Center + new Vector2(0, -280);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.05f, 0.15f);

            if (Timer < ChargeFrames) {
                npc.damage = 0;
                context.SetChargeState(3, Timer / (float)ChargeFrames);
                if (!VaultUtils.isClient && Timer == ChargeFrames - 8) {
                    PrimeTelegraphLine.SpawnFan(npc.Center, sweepAngle, 0.9f, 1f, 24);
                }
            }
            else if (Timer < ChargeFrames + SilenceFrames) {
                npc.damage = 0;
                context.ResetChargeState();
            }
            else {
                int sweepT = Timer - ChargeFrames - SilenceFrames;
                sweepAngle += MathHelper.ToRadians(0.7f) * (context.MasterMode ? 1.15f : 1f);
                npc.rotation = sweepAngle;
                if (!VaultUtils.isClient && sweepT % 4 == 0) {
                    FireBeam(context, sweepAngle);
                }
                if (!VaultUtils.isServer && sweepT == 1) {
                    PrimeScreenEffects.PushShockRing(npc.Center, 1f, 1f, 24);
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
                }
            }

            Timer++;
            if (Timer >= ChargeFrames + SilenceFrames + SweepFrames && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private static void FireBeam(PrimeStateContext context, float angle) {
            NPC npc = context.Npc;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser));
            Vector2 vel = angle.ToRotationVector2() * 12f;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 40f, vel,
                ModContent.ProjectileType<DeadLaser>(), damage, 0f, Main.myPlayer, 2f, 0f);
            HeadPrimeAI.SpanFireLerterDustEffect(npc, 40);
        }
    }
}
