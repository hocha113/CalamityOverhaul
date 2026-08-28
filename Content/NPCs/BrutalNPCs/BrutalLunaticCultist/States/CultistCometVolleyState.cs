using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 彗星潮(P1 起):司祭指划天穹,自身侧切向甩出数颗彗星,受场心引力拉成沿黄道内壁的大弧<br/>
    /// 蓄势期链音爬调+反向小后撤;彗星错拍出手,同一绕向,轨迹整体可预读<br/>
    /// 公平阀:彗星自带 20 帧出生无伤(CultistCometProj.WarmupFrames),弧线不追踪
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Comet, typeof(CultistStateContext))]
    internal class CultistCometVolleyState : CultistStateBase
    {
        public override string StateName => "CultistCometVolley";
        public override CultistStateIndex StateIndex => CultistStateIndex.Comet;

        private const int Windup = 28;
        /// <summary>彗星错拍间隔(帧)</summary>
        private const int BeatGap = 10;
        private const int Timeout = 130;

        /// <summary>绕向(权威端定,只有权威端发射用得到)</summary>
        private float orbitDir = 1f;

        private static int CometCount(CultistStateContext context) =>
            context.Phase >= 4 || context.IsAsuraMode ? 4 : 3;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                orbitDir = Main.rand.NextBool() ? 1f : -1f;
            }
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, Timer < Windup ? 12 : 11);
            FaceTarget(npc, player.Center);
            context.PushAura(0.75f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = MathHelper.Max(context.OrreryGlow, 0.7f);

            //站到场心外围高位:彗星从这里入轨
            float side = npc.Center.X < context.ArenaCenter.X ? -1f : 1f;
            Vector2 hover = context.ArenaCenter + new Vector2(side * 520f, -420f)
                + CultistMotion.BreathingOffset(seed: 6.3f, 10f);
            CultistMotion.SpringHover(npc, hover, 0.013f, 0.09f, 17f);

            //蓄势语调:链音爬调+向心符文
            if ((Timer == 6 || Timer == 15 || Timer == 24) && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item101 with {
                    Volume = 0.6f,
                    Pitch = -0.4f + Timer / (float)Windup * 0.9f
                }, npc.Center);
            }
            if (Timer % 8 == 0 && Timer < Windup) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Unit() * 90f,
                    CultistMotion.PhaseCore(context.Phase), 1, -5f);
            }

            //错拍甩星:切向出手,场心引力自会把它拉弯
            int count = CometCount(context);
            for (int i = 0; i < count; i++) {
                if (Timer != Windup + i * BeatGap) {
                    continue;
                }
                CultistMotion.CastFlash(npc.Center, CultistMotion.PhaseCore(context.Phase), 1.1f);
                CultistMotion.Shake(npc.Center, 3.5f, 8);
                context.ScalePulse = 1.08f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = -0.1f + i * 0.12f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    Vector2 radial = (npc.Center - context.ArenaCenter).SafeNormalize(Vector2.UnitY);
                    Vector2 tangent = radial.RotatedBy(orbitDir * MathHelper.PiOver2);
                    //各颗微错角,同绕向不同轨
                    Vector2 vel = tangent.RotatedBy((i - (count - 1) * 0.5f) * 0.12f) * 8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel.SafeNormalize(Vector2.Zero) * 46f,
                        vel, ModContent.ProjectileType<CultistCometProj>(), 42, 0f, Main.myPlayer,
                        npc.whoAmI, orbitDir);
                    //甩星反冲:身体语言
                    npc.velocity -= vel.SafeNormalize(Vector2.Zero) * 2.6f;
                    npc.netUpdate = true;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:甩完收势(彗星自巡自灭),或超时兜底
            if (Timer >= Windup + count * BeatGap + 18) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }
    }
}
