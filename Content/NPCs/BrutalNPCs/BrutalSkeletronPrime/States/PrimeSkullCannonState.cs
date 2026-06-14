using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>颅骨主炮：90帧蓄力(72%静默) → 锁定 → PrimeSkullBeamProj 大半圈扫射</summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.SkullCannon, typeof(PrimeStateContext))]
    internal class PrimeSkullCannonState : PrimeStateBase
    {
        public override string StateName => "SkullCannon";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.SkullCannon;

        internal static int ChargeFrames => 90;
        /// <summary>蓄力末段锁定瞄准帧数（锁定后走位不再跟踪，公平阀）</summary>
        internal static int LockLead => 24;
        internal static int SilenceFrames => 6;
        /// <summary>扫射半弧（大师模式）：全弧 ≈ 252°，转一个大半圈</summary>
        internal static float ArcHalfMaster => 2.2f;
        /// <summary>扫射半弧（普通/专家）：全弧 ≈ 218°</summary>
        internal static float ArcHalfNormal => 1.9f;

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
                //静默拍：充能视觉骤停，下一刻巨炮
                context.ResetChargeState();
                npc.velocity *= 0.85f;
            }
            else if (Timer == ChargeFrames + SilenceFrames) {
                FireBeam(context);
            }
            else {
                //扫射期：机体沿光束当前角度反向缓慢后坐，随扫射转动（mass is reaction）
                float sweepSpeed = arcHalf * 2f / PrimeSkullBeamProj.SweepFrames;
                float sweepT = MathHelper.Clamp(
                    Timer - (ChargeFrames + SilenceFrames) - PrimeSkullBeamProj.ExpandTime,
                    0f, PrimeSkullBeamProj.SweepFrames);
                float beamAngle = aimAngle - arcHalf + sweepSpeed * sweepT;
                Vector2 recoil = -beamAngle.ToRotationVector2() * 0.45f;
                npc.velocity *= 0.88f;
                npc.velocity = Vector2.Lerp(npc.velocity, recoil, 0.035f);
                float maxSweepSpeed = 2f;
                if (npc.velocity.Length() > maxSweepSpeed) {
                    npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSweepSpeed;
                }
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
                arcHalf = context.MasterMode ? ArcHalfMaster : ArcHalfNormal;
                if (!VaultUtils.isClient) {
                    //大半圈的弧无法用单个扇形如实预告：改为"起点射线 + 起始扇区"，
                    //告知光束出生位置与旋转方向，后续走位靠光束本身的慢角速度阅读
                    float startAngle = aimAngle - arcHalf;
                    PrimeTelegraphLine.SpawnLine(npc, npc.Center, startAngle, LockLead + SilenceFrames);
                    PrimeTelegraphLine.SpawnFan(npc, npc.Center, startAngle + 0.5f,
                        0.55f, LockLead + SilenceFrames, true);
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
