using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 死光扫描线：头部逐束点射，扫描角横越战场推进，种子挖出一个安全缺口，
    /// 终拍双束封边留中央走廊。头部全程睁眼（承诺兑换弱点）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.DeathrayScan, typeof(MLordContext))]
    internal class MLordDeathrayScanState : MLordStateBase
    {
        public override string StateName => "DeathrayScan";
        public override MLordStateIndex StateIndex => MLordStateIndex.DeathrayScan;

        internal const int PassInterval = 54;
        internal const int FirstPass = 44;

        private int passCount;
        private int gapIndex;
        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            passCount = context.CoreExposed ? 7 : 5;
            stateLength = FirstPass + passCount * PassInterval + Frames(context, 120);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                context.Npc.netUpdate = true;
            }
            gapIndex = 1 + (int)(MLordConstellationProj.Hash01(
                (int)context.Owner.ai[MLordAiSlots.OvAttackSeed], 11) * (passCount - 2));
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //扫描期收油慢移（可读性阀门）
            HoverTo(npc, target.Center + MLordDirector.CoreHoverOffset + new Vector2(0f, -60f), 4.2f, 0.035f);
            npc.velocity *= 0.96f;
            UpdateLean(context);
            context.SetChargeState(MathHelper.Clamp(Timer / (float)FirstPass, 0f, 1f));

            //种子在 OnEnter 后到达客户端，缺口本地补算
            gapIndex = 1 + (int)(MLordConstellationProj.Hash01(
                (int)context.Owner.ai[MLordAiSlots.OvAttackSeed], 11) * (passCount - 2));

            if (!VaultUtils.isClient) {
                RunServerPasses(context);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        private void RunServerPasses(MLordContext context) {
            NPC origin = context.Parts.Head >= 0 ? Main.npc[context.Parts.Head] : context.Npc;
            int damage = ScaleDamage(context, MLordDirector.ScanRayDamage);

            for (int i = 0; i < passCount; i++) {
                if (Timer != FirstPass + i * PassInterval) {
                    continue;
                }
                //缺口束跳过——安全走廊
                if (i == gapIndex) {
                    continue;
                }
                //扫描角自左向右推进（以下方扇面横越战场）
                float march = MathHelper.Lerp(MathHelper.PiOver2 + 1.05f, MathHelper.PiOver2 - 1.05f, i / (float)(passCount - 1));
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordScanRayProj>(), damage, 0f, Main.myPlayer,
                    origin.whoAmI, march, 34);
            }

            //终拍双束封边：留出玩家当前位置附近的中央走廊
            if (Timer == FirstPass + passCount * PassInterval + Frames(context, 26)) {
                Vector2 toTarget = context.Target.Center - origin.Center;
                float centerAngle = toTarget.ToRotation();
                foreach (float offset in stackalloc float[] { -0.42f, 0.42f }) {
                    Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center, Vector2.Zero,
                        ModContent.ProjectileType<MLordScanRayProj>(), damage, 0f, Main.myPlayer,
                        origin.whoAmI, centerAngle + offset, 42);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie100 with { Volume = 0.85f, Pitch = -0.3f }, origin.Center);
                }
            }

            //扫描间隙手部缓速眼弹压位
            if (Timer % Frames(context, 46) == 20) {
                SpawnHandEyeStream(context);
            }
        }

        private void SpawnHandEyeStream(MLordContext context) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.EyeDamage);
            for (int side = 0; side < 2; side++) {
                int index = side == 0 ? parts.LeftHand : parts.RightHand;
                bool alive = side == 0 ? parts.LeftHandAlive : parts.RightHandAlive;
                if (!alive || index < 0) {
                    continue;
                }
                NPC hand = Main.npc[index];
                Vector2 aim = (context.Target.Center - hand.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, aim * 4.6f,
                    ProjectileID.PhantasmalEye, damage, 0f, Main.myPlayer);
            }
        }
    }
}
