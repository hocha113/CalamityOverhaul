using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 掷环:浑天仪空转蓄势(后撤反向蓄力)→三环逐一离体,侧立刃线锁向(预告即承诺)→掷完末环即交棒下一手,环自行回旋归位<br/>
    /// 浑天仪显形不归本态管:环全数归位后由 CultistBossAI 帧检复位 OrreryMode,离体期(可能横跨后续状态)法器恒隐
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.RingHurl, typeof(CultistStateContext))]
    internal class CultistRingHurlState : CultistStateBase
    {
        public override string StateName => "CultistRingHurl";
        public override CultistStateIndex StateIndex => CultistStateIndex.RingHurl;

        private const int SpinUp = 30;
        /// <summary>各环离体拍(掷出间隔 14f)</summary>
        private static readonly int[] DetachBeats = [30, 44, 58];
        /// <summary>末环离体后的短收势拍,收完立刻交棒</summary>
        private const int RecoveryBeat = 12;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, Timer < SpinUp ? 12 : 11);
            FaceTarget(npc, player.Center);
            context.OrreryGlow = 1f;

            if (Timer < SpinUp) {
                //蓄势:环加速空转,末段反向猛缩(pow8 迟滞反冲)
                float t = Timer / (float)SpinUp;
                Vector2 away = (npc.Center - player.Center).SafeNormalize(Vector2.UnitX);
                npc.velocity = away * MathF.Pow(t, 8f) * 9f;
                context.PushAura(0.5f + t * 0.5f, CultistMotion.PhaseCore(context.Phase));
                //爬调链音
                if ((Timer == 8 || Timer == 17 || Timer == 26) && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item101 with {
                        Volume = 0.6f,
                        Pitch = -0.3f + Timer / (float)SpinUp * 0.8f
                    }, npc.Center);
                }
            }
            else {
                //离体期:防御性慢漂移,别追人
                Vector2 hover = player.Center + new Vector2(npc.Center.X < player.Center.X ? -520f : 520f, -240f);
                CultistMotion.SpringHover(npc, hover, 0.008f, 0.10f, 12f);
            }

            //逐环离体(权威端):环序 2→0(外环先走),各自侧立锁向
            for (int i = 0; i < DetachBeats.Length; i++) {
                if (Timer == DetachBeats[i] && !VaultUtils.isClient) {
                    context.OrreryMode = 1;
                    npc.ai[1] = 1;
                    int ringIdx = 2 - i;
                    //锁向:出手预判,各环小幅错角
                    Vector2 predicted = CultistMotion.PredictTarget(player, npc.Center, 25f, 0.6f);
                    Vector2 aim = (predicted - npc.Center).SafeNormalize(Vector2.UnitY)
                        .RotatedBy((i - 1) * 0.20f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, aim * 0.01f,
                        ModContent.ProjectileType<CultistOrreryRingProj>(), 44, 0f, Main.myPlayer,
                        ringIdx, npc.whoAmI);
                    npc.netUpdate = true;
                }
                if (Timer == DetachBeats[i]) {
                    CultistMotion.RuneBurst(npc.Center, CultistMotion.PhaseCore(context.Phase), 6, 5f);
                    context.ScalePulse = 1.08f;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //掷完即走:末环离体+短收势拍就回连接态选下一招,不等环返(环的往返与显形恢复由弹幕/AI 帧检自理)
            if (Timer >= DetachBeats[^1] + RecoveryBeat) {
                return new CultistCoilState();
            }
            return null;
        }

        /// <summary>本 BOSS 的离体环是否仍在场(AI 帧检与选招守卫共用)</summary>
        internal static bool AnyRingAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistOrreryRingProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho) {
                    return true;
                }
            }
            return false;
        }
    }
}
