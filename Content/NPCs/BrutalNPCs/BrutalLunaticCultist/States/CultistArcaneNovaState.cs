using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 奥术新星(全阶段):司祭聚拢符文短咏唱,自身连放数道扩散符环脉冲,缺口弧逐环小步进成可跟的螺旋门<br/>
    /// 公平阀:首环缺口正对玩家方位(先教走位);后环步进须满足 所需切向速度=GapStep×半径/PulseGap 低于跑速,<br/>
    /// 且相邻门扇区有重叠(2×GapHalfAngle&gt;GapStep)——旧值 1.9 rad 一步把门甩到 109° 外,等于没门(已判死刑勿回退);<br/>
    /// 缺口/扩速常量声明于 CultistArcanePulse,判定与绘制同参
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.ArcaneNova, typeof(CultistStateContext))]
    internal class CultistArcaneNovaState : CultistStateBase
    {
        public override string StateName => "CultistArcaneNova";
        public override CultistStateIndex StateIndex => CultistStateIndex.ArcaneNova;

        private const int Windup = 24;
        /// <summary>脉冲间拍:环距=Speed×PulseGap≈286px,同时是同半径处两门之间的换位时间</summary>
        private const int PulseGap = 26;
        /// <summary>相邻脉冲缺口转步(rad):0.42×半径465/26帧≈7.5px/f 走位可跟;与 0.55 半角门重叠 0.68 rad,站重叠区可白过两环</summary>
        private const float GapStep = 0.42f;
        private const int Timeout = 176;

        /// <summary>首环缺口基角(权威端出手时定,后环由步进推)</summary>
        private float gapBase;

        private static int PulseCount(CultistStateContext context) =>
            context.Phase >= 3 || context.IsDeathMode ? 4 : 3;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 12);
            FaceTarget(npc, player.Center);
            context.PushAura(0.8f, CultistMotion.PhaseCore(context.Phase));

            //中距侧位站桩施法:环从他身上荡开,玩家读得出圆心
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 420f, -200f)
                + CultistMotion.BreathingOffset(seed: 13.7f, 9f);
            CultistMotion.SpringHover(npc, hover, 0.013f, 0.09f, 17f);

            //咏唱:符文向心汇聚+爬调
            if (Timer < Windup) {
                if (Timer % 6 == 0) {
                    CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Unit() * 120f,
                        CultistMotion.PhaseCore(context.Phase), 1, -6f);
                }
                if ((Timer == 6 || Timer == 16) && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with {
                        Volume = 0.6f,
                        Pitch = -0.3f + Timer / (float)Windup * 0.7f
                    }, npc.Center);
                }
            }

            //脉冲拍:每环一记顿点,缺口逐环转步
            int count = PulseCount(context);
            for (int i = 0; i < count; i++) {
                if (Timer != Windup + i * PulseGap) {
                    continue;
                }
                CultistMotion.CastFlash(npc.Center, CultistMotion.PhaseCore(context.Phase), 1.2f);
                CultistMotion.Shake(npc.Center, 3.2f, 8);
                context.ScalePulse = 1.10f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.85f, Pitch = -0.2f + i * 0.1f }, npc.Center);
                }
                if (VaultUtils.isClient) {
                    continue;
                }
                //首环缺口正对玩家(先给活路),后环按声明步进转
                if (i == 0) {
                    gapBase = (player.Center - npc.Center).ToRotation();
                }
                float gapCenter = gapBase + i * GapStep;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<CultistArcanePulse>(), 40, 0f, Main.myPlayer,
                    npc.whoAmI, gapCenter, context.Phase);
                npc.netUpdate = true;
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:放完收势(脉冲自扩自灭),或超时兜底
            if (Timer >= Windup + count * PulseGap + 24) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }
    }
}
