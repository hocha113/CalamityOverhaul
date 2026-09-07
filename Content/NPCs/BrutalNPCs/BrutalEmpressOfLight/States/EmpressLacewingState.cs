using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 蝶群：第一小节从双手放出二十四只光蝶（每拍八只），之后八小节里每隔一小节合掌一次，
    /// 蝶群在合掌小节整节冻成琉璃。规则：别停下，她合掌时你不能在蝶群里。结束时挥手，蝶群升空散去
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Lacewing, typeof(EmpressStateContext))]
    internal class EmpressLacewingState : EmpressStateBase
    {
        public override string StateName => "EmpressLacewing";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Lacewing;

        private const int SwarmSize = 24;
        private const int ClapBars = 8;

        private EmpressStateContext Context;
        private int startBar = -1;
        private int released;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            startBar = -1;
            released = 0;
            PlayLocal(SoundID.Item165 with { Volume = 0.6f, Pitch = 0.4f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //起手等第一拍再开始放蝶；放蝶小节为 startBar，之后奇数小节合掌
            if (startBar < 0) {
                context.Pose = EmpressPose.Idle;
                if (target.Alives()) {
                    Vector2 dest = target.Center + new Vector2(target.Center.X > npc.Center.X ? -300f : 300f, -260f);
                    npc.velocity = npc.velocity * 0.8f + (dest - npc.Center) * 0.03f;
                }
                if (context.Downbeat && Timer > 4) {
                    startBar = context.BarIndex;
                }
                else {
                    return null;
                }
            }

            int bars = context.BarIndex - startBar;
            bool clapBar = bars >= 1 && bars % 2 == 1;
            bool preClap = bars % 2 == 0 && context.BarFrame >= EmpressTempo.BeatFrames * 2;

            //姿势：放蝶双手张开；合掌小节双手合拢；合掌前一拍手臂上抬
            context.Pose = clapBar ? EmpressPose.CastBoth : (preClap ? EmpressPose.CastBoth : EmpressPose.Idle);
            context.PoseTimer = clapBar ? 60f : (preClap ? (context.BarFrame - 40f) * 3f : 0f);
            if (preClap) {
                context.SetChargeState(3, (context.BarFrame - 40f) / 20f);
            }

            //她在你斜上方缓缓漂，随小节换边
            if (target.Alives()) {
                int side = bars % 4 < 2 ? 1 : -1;
                Vector2 dest = target.Center + new Vector2(side * 320f, -280f);
                npc.velocity = npc.velocity * 0.85f + (dest - npc.Center) * 0.02f;
            }
            else {
                npc.velocity *= 0.9f;
            }

            //第一小节：每拍放八只
            if (bars == 0 && context.OnBeat && released < SwarmSize && target.Alives()) {
                Release(context, npc, target, 8);
            }

            //合掌拍
            if (clapBar && context.Downbeat) {
                Clap(context, npc);
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (bars >= ClapBars + 1) {
                //挥手：光蝶自己发现她离开了此态，四散升空
                PlayLocal(SoundID.Item165 with { Volume = 0.5f, Pitch = 0.6f }, npc.Center);
                return new EmpressConnectorState();
            }
            return null;
        }

        private void Release(EmpressStateContext context, NPC npc, Player target, int count) {
            PlayLocal(SoundID.Item164 with { Volume = 0.45f, Pitch = 0.5f + released * 0.01f }, npc.Center);
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(context.LeftHand, Vector2.Zero, Color.White, 0.3f)?.Configure(10, 0.1f, context.DayFormBlend);
                PRTLoader.NewParticle<PRT_EmpressRipple>(context.RightHand, Vector2.Zero, Color.White, 0.3f)?.Configure(10, 0.6f, context.DayFormBlend);
            }
            if (VaultUtils.isClient) {
                released += count;
                return;
            }
            int type = ModContent.ProjectileType<EmpressLacewing>();
            for (int i = 0; i < count; i++) {
                Vector2 hand = i % 2 == 0 ? context.LeftHand : context.RightHand;
                float angle = -MathHelper.PiOver2 + (i - count / 2f + 0.5f) * 0.35f;
                Vector2 vel = angle.ToRotationVector2() * 4f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), hand, vel, type, context.LacewingDamage, 0f, Main.myPlayer,
                    target.whoAmI, npc.whoAmI, context.BarIndex);
            }
            released += count;
        }

        /// <summary>合掌：一声清响 + 全屏一记轻脉冲，蝶群自己按小节奇偶冻住</summary>
        private static void Clap(EmpressStateContext context, NPC npc) {
            PlayLocal(SoundID.Item29 with { Volume = 0.9f, Pitch = 0.1f }, npc.Center);
            PlayLocal(SoundID.Item35 with { Volume = 0.5f, Pitch = 0.6f }, npc.Center);
            EmpressMotion.Shake(npc.Center, 3f, 10);
            if (!VaultUtils.isServer) {
                EmpressScreenFX.PushPrismPulse(npc.Center, 0.3f, 18);
                PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center + new Vector2(0f, -30f), Vector2.Zero, Color.White, 0.8f)?.Configure(16, 0.12f, context.DayFormBlend);
            }
        }
    }
}
