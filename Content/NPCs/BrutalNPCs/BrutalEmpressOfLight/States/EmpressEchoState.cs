using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 光痕圆舞：六小节里每一拍在每位玩家脚下留一道光痕（两小节后凝成琉璃剑），她绕着你慢慢走圆场，
    /// 每两小节第一拍再从身上放一圈慢光球逼你挪位。规则：别回到两秒前站过的地方，场地会被你自己的过去填满
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Echo, typeof(EmpressStateContext))]
    internal class EmpressEchoState : EmpressStateBase
    {
        public override string StateName => "EmpressEcho";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Echo;

        /// <summary>留痕小节数</summary>
        private const int SpawnBars = 6;
        /// <summary>同一玩家两道光痕的最小间距：站着不动就会踩到自己的剑</summary>
        internal const float MinSpacing = 60f;
        /// <summary>绕场半径</summary>
        private const float OrbitRadius = 380f;

        private EmpressStateContext Context;
        private int spawnTick = -1;
        private float orbitAngle;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            spawnTick = -1;
            orbitAngle = context.Target.Alives() ? context.Npc.AngleFrom(context.Target.Center) : 0f;
            PlayLocal(SoundID.Item165 with { Volume = 0.7f, Pitch = 0.15f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            context.Pose = EmpressPose.CastBoth;
            context.PoseTimer = 20f + context.BarFrame * 0.5f;
            context.SetChargeState(3, 0.3f + 0.4f * (context.BarFrame / (float)EmpressTempo.BarFrames));

            //绕场：以你为心慢慢走圆，每小节转 1/6 圈，高度略高于你
            if (target.Alives()) {
                orbitAngle += MathHelper.TwoPi / (6f * EmpressTempo.BarFrames);
                Vector2 dest = target.Center + orbitAngle.ToRotationVector2() * OrbitRadius + new Vector2(0f, -120f);
                npc.velocity = npc.velocity * 0.8f + (dest - npc.Center) * 0.03f;
            }
            else {
                npc.velocity *= 0.9f;
            }

            //起手等第一拍
            if (spawnTick < 0) {
                if (context.Downbeat && Timer > 4) {
                    spawnTick = 0;
                }
                else {
                    return null;
                }
            }
            else {
                spawnTick++;
            }

            bool spawning = spawnTick < SpawnBars * EmpressTempo.BarFrames;
            if (spawning && context.OnBeat) {
                LeaveEchoes(context, npc);
                if (context.Downbeat && spawnTick / EmpressTempo.BarFrames % 2 == 0) {
                    RingPush(context, npc);
                }
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            //最后一道光痕碎掉后再收半小节
            if (spawnTick >= SpawnBars * EmpressTempo.BarFrames + EmpressEcho.TotalFrames + 30) {
                return new EmpressConnectorState();
            }
            return null;
        }

        /// <summary>每拍在每位活着的玩家脚下留一道光痕；60px 内已有光痕则不重复（那是他自己的问题）。终章复用</summary>
        internal static void LeaveEchoes(EmpressStateContext context, NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<EmpressEcho>();
            foreach (Player player in Main.ActivePlayers) {
                if (!player.Alives() || player.Distance(npc.Center) > 3200f) {
                    continue;
                }
                bool crowded = false;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == type && (int)p.ai[0] == player.whoAmI && p.Distance(player.Center) < MinSpacing) {
                        crowded = true;
                        break;
                    }
                }
                if (crowded) {
                    continue;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, Vector2.Zero, type,
                    context.EchoDamage, 0f, Main.myPlayer, player.whoAmI, npc.whoAmI);
            }
        }

        /// <summary>每两小节一圈慢光球（六缺口留得很宽），逼你从站位里走出来</summary>
        private void RingPush(EmpressStateContext context, NPC npc) {
            PlayLocal(SoundID.Item164 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center);
            if (VaultUtils.isClient) {
                return;
            }
            int count = 12;
            for (int i = 0; i < count; i++) {
                if (i % 2 == 1) {
                    continue;
                }
                float angle = MathHelper.TwoPi / count * i + orbitAngle;
                Vector2 dir = angle.ToRotationVector2();
                EmpressCast.Bolt(npc, npc.Center + dir * 40f, dir * 3.2f, context.BoltDamage, EmpressBoltMode.Straight);
            }
        }
    }
}
