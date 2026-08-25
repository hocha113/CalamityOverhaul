using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 幻象·真假弹幕扇：同拍放出多组一模一样的扇,只有一组是真的<br/>
    /// 识真线索恒定可学=材质法则本身:真弹是 vanilla 实体、会遮挡、会发光;幻象半透明、无光、透背景<br/>
    /// 星云主场强化：组数 3→5(真扇 2 组)<br/>
    /// 公平阀：真扇出膛速度同幻象完全一致,不靠速度骗;拍间隔恒定
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.PhantomRite, typeof(CultistStateContext))]
    internal class CultistPhantomRiteState : CultistStateBase
    {
        public override string StateName => "CultistPhantomRite";
        public override CultistStateIndex StateIndex => CultistStateIndex.PhantomRite;

        private const int VolleyInterval = 74;

        private static bool IsHome(CultistStateContext context) => context.Phase == 1 || context.Phase >= 4;

        private int VolleyCount(CultistStateContext context) => IsHome(context) ? 3 : 2;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 12);
            FaceTarget(npc, player.Center);

            //近距压场:幻象要糊脸才有辨认压力,但保底距离
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 340f, -170f)
                + CultistMotion.BreathingOffset(seed: 5.9f, 12f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            context.PushAura(0.55f, CultistMotion.PhaseCore(1));

            //齐射(权威端):扇形阵列,真假扇同拍同速同角距
            int volleys = VolleyCount(context);
            if (!VaultUtils.isClient && Timer >= 30 && (Timer - 30) % VolleyInterval == 0) {
                int volley = (int)(Timer - 30) / VolleyInterval;
                if (volley < volleys) {
                    FireMirrorFans(context, npc, player);
                }
            }
            if (Timer >= 30 && (Timer - 30) % VolleyInterval == 0) {
                context.ScalePulse = 1.06f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.35f }, npc.Center);
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }
            int total = 30 + volleys * VolleyInterval + 40;
            if (Timer >= total) {
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>放一拍真假扇:围绕玩家方位摆 3(主场5)个发射方位,随机一个(主场两个)是真的</summary>
        private void FireMirrorFans(CultistStateContext context, NPC npc, Player player) {
            bool home = IsHome(context);
            int fans = home ? 5 : 3;
            int trueCount = home ? 2 : 1;
            float baseAngle = (player.Center - npc.Center).ToRotation();
            //扇位:以指向玩家为中心的等距角阵
            float spread = 0.52f;
            int trueSlotA = Main.rand.Next(fans);
            int trueSlotB = trueCount > 1 ? (trueSlotA + 1 + Main.rand.Next(fans - 1)) % fans : -1;

            for (int fan = 0; fan < fans; fan++) {
                float fanAngle = baseAngle + (fan - (fans - 1) * 0.5f) * spread;
                bool isTrue = fan == trueSlotA || fan == trueSlotB;
                for (int i = -2; i <= 2; i++) {
                    Vector2 vel = (fanAngle + i * 0.17f).ToRotationVector2() * 7.2f;
                    if (isTrue) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel.SafeNormalize(Vector2.Zero) * 26f,
                            vel, ModContent.ProjectileType<CultistTrueBolt>(), 36, 0f, Main.myPlayer, context.Phase);
                    }
                    else {
                        //幻象:ai0=1,半透明无光永不咬人
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel.SafeNormalize(Vector2.Zero) * 26f,
                            vel, ModContent.ProjectileType<CultistPaleBolt>(), 30, 0f, Main.myPlayer, 1f);
                    }
                }
            }
        }
    }
}
