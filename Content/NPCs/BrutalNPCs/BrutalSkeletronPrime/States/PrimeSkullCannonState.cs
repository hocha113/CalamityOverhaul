using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 颅骨主炮（&lt;35% 扣留招）：90 帧蓄力（汇聚 → 72% 静默）→ 锁定瞄准 + 扇形预告
    /// → <see cref="PrimeSkullBeamProj"/> 巨型光束横扫，全场最华丽一招。
    /// <para>完整充能语法：粒子密度 ∝ √进度、72% 处声画双静默、静默拍后瞬间释放。</para>
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.SkullCannon, typeof(PrimeStateContext))]
    internal class PrimeSkullCannonState : PrimeStateBase
    {
        public override string StateName => "SkullCannon";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.SkullCannon;

        private const int ChargeFrames = 90;
        /// <summary>蓄力末段提前锁定瞄准的帧数（锁定后玩家走位不再被跟踪——公平阀）</summary>
        private const int LockLead = 24;
        private const int SilenceFrames = 6;

        private int TotalFrames => ChargeFrames + SilenceFrames + PrimeSkullBeamProj.TotalLife + 8;

        private float aimAngle;
        private float arcHalf;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            Vector2 anchor = context.Target.Center + new Vector2(0, -300);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.05f, 0.15f);
            LeanTowards(npc, context.Target.Center);

            if (Timer < ChargeFrames) {
                UpdateCharge(context);
            }
            else if (Timer < ChargeFrames + SilenceFrames) {
                //静默拍：充能视觉骤停、机体微滞——下一刻就是巨炮
                context.ResetChargeState();
                npc.velocity *= 0.85f;
            }
            else if (Timer == ChargeFrames + SilenceFrames) {
                FireBeam(context);
            }
            else {
                //扫射期：沿瞄准中轴缓慢反向后坐（mass is reaction）
                Vector2 recoil = -aimAngle.ToRotationVector2() * 1.4f;
                npc.velocity = Vector2.Lerp(npc.velocity, recoil, 0.06f);
            }

            Timer++;
            if (Timer >= TotalFrames && !VaultUtils.isClient) {
                return new PrimeRageConnectorState();
            }
            return null;
        }

        private void UpdateCharge(PrimeStateContext context) {
            NPC npc = context.Npc;
            float progress = Timer / (float)ChargeFrames;
            context.SetChargeState(3, progress);

            //锁定前持续跟踪玩家；锁定后瞄准冻结并打出扇形预告
            if (Timer < ChargeFrames - LockLead) {
                aimAngle = DirectionToTarget(context).ToRotation();
            }
            else if (Timer == ChargeFrames - LockLead) {
                arcHalf = context.MasterMode ? 0.62f : 0.55f;
                if (!VaultUtils.isClient) {
                    PrimeTelegraphLine.SpawnFan(npc, npc.Center, aimAngle,
                        arcHalf + 0.05f, LockLead + SilenceFrames);
                    npc.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            //汇聚流光：spawn ∝ √progress，72% 处静默（"吸气"拍）
            if (progress < 0.72f && Timer % 3 == 0) {
                int sparkCount = (int)(System.Math.Sqrt(progress) * 3f) + 1;
                for (int i = 0; i < sparkCount; i++) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2CircularEdge(130f, 130f);
                    Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.FireworkFountain_Red,
                        0, 0, 100, Color.Orange, Main.rand.NextFloat(1.1f, 1.7f));
                    dust.velocity = (npc.Center - pos) * 0.1f;
                    dust.noGravity = true;
                }
            }
            if (Timer == (int)(ChargeFrames * 0.72f)) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.8f, Volume = 0.7f }, npc.Center);
            }
        }

        private void FireBeam(PrimeStateContext context) {
            NPC npc = context.Npc;

            if (!VaultUtils.isClient) {
                float sweepSpeed = arcHalf * 2f / PrimeSkullBeamProj.SweepFrames;
                int damage = ScaleDamage((int)(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser) * 1.25f));
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<PrimeSkullBeamProj>(), damage, 0f, Main.myPlayer,
                    npc.whoAmI, aimAngle - arcHalf, sweepSpeed);
                npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                PrimeScreenEffects.PushShockRing(npc.Center, 1f, 640f);
                PrimeDeathPerformancePlayer.RequestShake(10f, 14);
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 1.2f, Pitch = -0.4f }, npc.Center);
                HeadPrimeAI.SpanFireLerterDustEffect(npc, 40);
            }
        }
    }
}
