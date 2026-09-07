using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 月影（昼）：第一小节每拍抛一枚月屑，第二小节她升起聚光、世界开始发白，
    /// 第三到第六小节世界全白，每拍不在任一月屑阴影锥里的玩家蒸发一截生命（不走灼痕），
    /// 第七小节释放，月屑碎，白光退。规则：躲进她的阴影里
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.MoonShadow, typeof(EmpressStateContext))]
    internal class EmpressMoonShadowState : EmpressStateBase
    {
        public override string StateName => "EmpressMoonShadow";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.MoonShadow;

        private const int ThrowBar = 0;
        private const int GatherBar = 1;
        private const int RadianceStartBar = 2;
        private const int RadianceEndBar = 6;
        private const int ReleaseBar = 6;
        private const int EndBar = 7;

        private EmpressStateContext Context;
        private int startBar = -1;
        private int thrown;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            startBar = -1;
            thrown = 0;
            PlayLocal(SoundID.Item165 with { Volume = 0.8f, Pitch = -0.5f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            if (startBar < 0) {
                context.Pose = EmpressPose.CastLeft;
                npc.velocity *= 0.9f;
                if (context.Downbeat && Timer > 4) {
                    startBar = context.BarIndex;
                }
                else {
                    return null;
                }
            }

            int bar = context.BarIndex - startBar;
            int bf = context.BarFrame;

            if (bar == ThrowBar) {
                //抛月屑：每拍一枚，先朝你那侧，再左右各一枚
                context.Pose = EmpressPose.CastLeft;
                context.PoseTimer = bf;
                if (context.OnBeat && thrown < 3 && target.Alives()) {
                    ThrowShard(context, npc, target, thrown);
                    thrown++;
                }
                if (target.Alives()) {
                    Vector2 dest = target.Center + new Vector2(0f, -420f);
                    npc.velocity = npc.velocity * 0.85f + (dest - npc.Center) * 0.02f;
                }
            }
            else if (bar == GatherBar) {
                //聚光：她升到你头顶 420，世界开始发白
                context.Pose = EmpressPose.Transform;
                context.PoseTimer = Math.Min(bf, 58);
                context.SetChargeState(3, bf / 60f);
                if (target.Alives()) {
                    Vector2 dest = target.Center + new Vector2(0f, -420f);
                    npc.velocity = npc.velocity * 0.85f + (dest - npc.Center) * 0.03f;
                }
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareWhiteout(0.5f * bf / 60f, npc.Center);
                    EmpressMotion.HandChargeDust(context.LeftHand, bf / 60f, context.DayFormBlend);
                    EmpressMotion.HandChargeDust(context.RightHand, bf / 60f, context.DayFormBlend);
                    if (bf % 6 == 0) {
                        EmpressMotion.Shake(npc.Center, bf / 60f * 2.5f, 6);
                    }
                    if (bf == 58) {
                        //最后两帧静默前的屏息拍：一记白闪把世界推到全白
                        EmpressScreenFX.PushPhaseFlash(0.5f);
                    }
                }
            }
            else if (bar >= RadianceStartBar && bar < RadianceEndBar) {
                RadianceUpdate(context, npc, target, bar, bf);
            }
            else if (bar == ReleaseBar) {
                //释放：白光退，月屑碎，她吐气
                context.Pose = EmpressPose.Idle;
                context.PoseTimer = 0f;
                npc.velocity *= 0.9f;
                if (bf == 0) {
                    ShatterShards(npc);
                    EmpressMotion.Shake(npc.Center, 6f, 16);
                    PlayLocal(SoundID.Item122 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
                }
                if (!VaultUtils.isServer) {
                    EmpressScreenFX.DeclareWhiteout(0.9f * (1f - bf / 60f), npc.Center);
                }
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            if (bar >= EndBar) {
                return new EmpressConnectorState();
            }
            return null;
        }

        private void ThrowShard(EmpressStateContext context, NPC npc, Player target, int idx) {
            PlayLocal(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.6f }, npc.Center);
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_EmpressRipple>(context.LeftHand, Vector2.Zero, new Color(150, 160, 230), 0.5f)?.Configure(14, 0.66f, 0f);
            }
            if (VaultUtils.isClient) {
                return;
            }
            //落点：0 号在你与她连线的你这一侧再往外 260px（阴影正好盖住你脚下），1/2 号在她两侧 110° 各 700px
            Vector2 pos;
            if (idx == 0) {
                Vector2 dir = npc.DirectionTo(target.Center);
                pos = target.Center + dir * 260f;
            }
            else {
                float side = idx == 1 ? 1f : -1f;
                float ang = npc.AngleTo(target.Center) + side * MathHelper.ToRadians(110f);
                pos = npc.Center + ang.ToRotationVector2() * 700f;
            }
            int life = (EndBar - ThrowBar) * EmpressTempo.BarFrames - context.BarFrame + 10;
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, npc.DirectionTo(pos) * 0.5f, ModContent.ProjectileType<EmpressMoonShard>(),
                0, 0f, Main.myPlayer, npc.whoAmI, life);
        }

        /// <summary>全白四小节：每拍不在阴影里的本地玩家蒸发一截；她悬着不动只随呼吸</summary>
        private void RadianceUpdate(EmpressStateContext context, NPC npc, Player target, int bar, int bf) {
            context.Pose = EmpressPose.Transform;
            context.PoseTimer = 40f + 18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
            context.SetChargeState(3, 1f);
            npc.velocity *= 0.9f;
            if (target.Alives()) {
                npc.velocity.Y += (target.Center.Y - 420f - npc.Center.Y) * 0.0006f;
            }

            if (VaultUtils.isServer) {
                return;
            }
            EmpressScreenFX.DeclareWhiteout(0.9f, npc.Center);
            EmpressScreenFX.DeclareAmbient(0.6f);
            //她身上不断迸出白光碎彩
            if (Main.rand.NextBool(2)) {
                EmpressMotion.SparkBurst(npc.Center + Main.rand.NextVector2Circular(30f, 40f), Main.rand.NextVector2Unit(), 1, 2f, 6f, context.DayFormBlend, MathHelper.Pi);
            }

            if (!context.OnBeat) {
                return;
            }
            Player me = Main.LocalPlayer;
            if (!me.Alives() || me.Distance(npc.Center) > 3200f) {
                return;
            }
            if (EmpressMoonShadow.InShadow(me.Center, npc.Center, EmpressMoonShard.Active)) {
                //在阴影里：一拍轻微的凉意提示
                if (context.Downbeat) {
                    PRTLoader.NewParticle<PRT_EmpressRipple>(me.Center, Vector2.Zero, new Color(150, 160, 230), 0.35f)?.Configure(10, 0.66f, 0f);
                }
                return;
            }
            int dmg = Math.Max((int)(me.statLifeMax2 * context.RadianceTickFraction), 1);
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(EmpressOfLightAI.RadianceDeathText(me.name));
            me.Hurt(reason, dmg, 0, false, false, -1, false, 0f, 1f, 0f);
            EmpressHitFeedback.Trigger(me.Center, Vector2.UnitY, 0.6f, true);
            EmpressScreenFX.PushHitDark(0.15f);
        }

        private static void ShatterShards(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            int type = ModContent.ProjectileType<EmpressMoonShard>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == npc.whoAmI) {
                    p.timeLeft = Math.Min(p.timeLeft, 20);
                    p.netUpdate = true;
                }
            }
        }
    }
}
