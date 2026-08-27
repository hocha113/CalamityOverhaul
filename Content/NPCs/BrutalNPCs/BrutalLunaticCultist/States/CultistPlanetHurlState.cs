using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 举星砸掷:他把神器当武器。主星被拽到头顶(举手姿态)→承重下沉→缓慢举升→反倾蓄势→顺势砸向玩家<br/>
    /// 预警线与咏唱音效按令删除:整段举星动作本身就是预告(动画即预警的豁免);<br/>
    /// 瞄准在反倾拍起锁死(预告即承诺,身体后倾=承诺拍),砸出后星球自行归位
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PlanetHurl, typeof(CultistStateContext))]
    internal class CultistPlanetHurlState : CultistStateBase
    {
        public override string StateName => "CultistPlanetHurl";
        public override CultistStateIndex StateIndex => CultistStateIndex.PlanetHurl;

        /// <summary>拽星入手:主星飞向头顶举持位</summary>
        private const int GrabFrames = 44;
        /// <summary>承重拍止:星到手,整个人被压下去</summary>
        private const int SettleEnd = GrabFrames + 12;
        /// <summary>举升拍止:扛着世界往上顶,臂在抖</summary>
        private const int HoistEnd = SettleEnd + 26;
        /// <summary>砸出帧:反倾蓄势后的爆发</summary>
        private const int SmashBeat = HoistEnd + 14;
        private const int Duration = SmashBeat + 36;

        /// <summary>没抓到可掷的星球时直接放弃(权威端置位)</summary>
        private bool aborted;
        /// <summary>举持桩位(各端入态自捕,身体语言都相对它演)</summary>
        private Vector2 holdPos;
        /// <summary>锁定瞄点(权威端,反倾拍捕获后不再追瞄)</summary>
        private Vector2 lockedAim;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            aborted = false;
            holdPos = context.Npc.Center;
            //桩位收进穹内:头顶还要举着最大 700+px 的星球,别把它顶出结界
            if (context.ArenaSpawned) {
                Vector2 delta = holdPos - context.ArenaCenter;
                float maxR = CultistStateContext.ArenaRadius - 850f;
                if (delta.Length() > maxR) {
                    holdPos = context.ArenaCenter + delta.SafeNormalize(Vector2.UnitY) * maxR;
                }
            }
            lockedAim = context.Target?.Center ?? holdPos;
            if (!VaultUtils.isClient) {
                //举星令:主星拽向头顶举持位
                aborted = !CultistPlanetProj.CommandRecede(context.Npc.whoAmI);
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            //举手托举全程,砸出瞬切施法(掷出的跟随手势)
            SetPose(npc, Timer < SmashBeat ? 11 : 12);
            FaceTarget(npc, player.Center);
            context.PushAura(0.85f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = 1f;

            Vector2 away = (holdPos - player.Center).SafeNormalize(Vector2.UnitX);

            //举星身体语言:站桩拽星→被星压沉→颤着举起→向后反倾→砸出前扑
            if (Timer <= GrabFrames) {
                Vector2 hover = holdPos + CultistMotion.BreathingOffset(seed: 7.7f, 8f);
                CultistMotion.SpringHover(npc, hover, 0.020f, 0.12f, 14f);
                //拽引符文:手上有力在收
                if (Timer % 12 == 0) {
                    CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -40f),
                        CultistMotion.PhaseCore(context.Phase), 2, 3.5f);
                }
            }
            else if (Timer <= SettleEnd) {
                CultistMotion.SpringHover(npc, holdPos + new Vector2(0f, 40f), 0.060f, 0.18f, 18f);
            }
            else if (Timer <= HoistEnd) {
                float tremble = (float)Math.Sin(Timer * 1.9f) * 2.6f;
                CultistMotion.SpringHover(npc, holdPos + new Vector2(tremble, -58f), 0.016f, 0.10f, 9f);
                context.ScalePulse = 1.04f;
            }
            else if (Timer < SmashBeat) {
                CultistMotion.SpringHover(npc, holdPos + away * 52f + new Vector2(0f, 14f), 0.050f, 0.14f, 16f);
            }
            else {
                npc.velocity *= 0.90f;
            }

            //承重拍:重量落在肩上
            if (Timer == GrabFrames + 1) {
                CultistMotion.Shake(npc.Center, 3f, 9);
                CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -70f),
                    CultistMotion.PhaseCore(context.Phase), 6, 5f);
                context.ScalePulse = 0.94f;
            }

            //反倾拍起锁瞄(权威端):预告即承诺,身体后倾后不再追人;
            //预判速度与砸出爆发同参,提前量 0.5=站桩/慢挪必中,全速跑位仍有欠预判的活路
            if (Timer == HoistEnd && !VaultUtils.isClient && !aborted) {
                Projectile planet = FindHeldPlanet(npc.whoAmI);
                Vector2 from = planet?.Center ?? npc.Center;
                lockedAim = CultistMotion.PredictTarget(player, from, 26f, 0.5f);
            }

            //砸出(权威端):沿锁定点爆发掷下,本体顺势前扑
            if (Timer == SmashBeat && !VaultUtils.isClient && !aborted) {
                CultistPlanetProj.CommandSmash(npc.whoAmI, lockedAim);
                npc.velocity += (lockedAim - npc.Center).SafeNormalize(Vector2.UnitY) * 12f;
                npc.netUpdate = true;
            }
            //砸出演出(各端,按令无音效):身体的爆发靠震屏与白闪落地
            if (Timer == SmashBeat) {
                CultistMotion.Shake(npc.Center, 8f, 14);
                CultistScreenFX.PushFlash(0.25f);
                context.ScalePulse = 1.12f;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (aborted || Timer >= Duration) {
                return new CultistCoilState();
            }
            return null;
        }

        /// <summary>找被举起待掷的非幻象主星(锁瞄起点)</summary>
        private static Projectile FindHeldPlanet(int ownerWho) {
            int type = ModContent.ProjectileType<CultistPlanetProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[1] == ownerWho
                    && (int)proj.ai[2] % 10 == 3 && (int)proj.ai[2] / 10 == 0) {
                    return proj;
                }
            }
            return null;
        }
    }
}
