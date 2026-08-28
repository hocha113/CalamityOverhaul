using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 转阶段演出:清场+旧星裂解(裂纹→坍缩→单帧引爆散尽)→仰首嘶吼+浑天仪调律→新星开金门降临<br/>
    /// 全程免伤但不出手,攻势不跨阶段(公平阀:清弹幕+新阶段起手有缓冲)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PhaseShift, typeof(CultistStateContext))]
    internal class CultistPhaseShiftState : CultistStateBase
    {
        public override string StateName => "CultistPhaseShift";
        public override CultistStateIndex StateIndex => CultistStateIndex.PhaseShift;

        private const int Duration = 150;
        /// <summary>旧星引爆落拍:清场令(8)+裂纹(40)+坍缩(14)</summary>
        private const int DetonationBeat = 62;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            context.IsInPhaseTransition = true;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;

            if (!VaultUtils.isClient) {
                context.Phase++;
                npc.ai[0] = context.Phase;
                context.ClearAttackBag();
                //转阶段折半充能:大祭不紧跟着转场压人
                context.AlignCharge *= 0.5f;
                npc.netUpdate = true;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 13);
            npc.velocity *= 0.9f;

            Color core = CultistMotion.PhaseCore(context.Phase);
            CultistScreenFX.SetVeil(0.65f, npc.Center, core, 680f);
            context.PushAura(0.9f, core);
            //调律:三环失稳抖动,随嘶吼渐定
            context.StaggerWobble = MathHelper.Max(context.StaggerWobble,
                MathHelper.Clamp(1f - Timer / 90f, 0f, 0.7f));
            context.OrreryGlow = 1f;

            //清场+旧星裂解:内核蓄力炸开,旧攻势不跨阶段(权威端)
            if (Timer == 8 && !VaultUtils.isClient) {
                CultistBossAI.ClearHostileKit(npc);
                CultistPlanetProj.CommandExplode(npc.whoAmI);
            }

            //嘶吼顿点
            if (Timer == 34) {
                CultistScreenFX.PushFlash(0.45f);
                CultistMotion.Shake(npc.Center, 6f, 14);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 18, 7.5f);
                context.ScalePulse = 1.12f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1.2f, Pitch = -0.15f }, npc.Center);
                }
            }

            //新星降临(权威端):旧星散尽尘埃落定后,新的推开金门而来(门闪演出在星球降临段自带)
            if (Timer == DetonationBeat + 14 && !VaultUtils.isClient) {
                int kind = System.Math.Clamp(context.Phase, 0, 4);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, -430f), Vector2.Zero,
                    ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                    kind, npc.whoAmI, 0f);
            }

            //嘶吼后符文持续涌
            if (Timer > 34 && Timer % 6 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(24f, 32f), core, 1, 3f);
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistCoilState(10);
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.IsInPhaseTransition = false;
            context.Npc.dontTakeDamage = false;
        }
    }
}
