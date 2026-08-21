using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 火·印记狩猎：印记逐张落在玩家预判位，定形 24 帧后喷焰扇<br/>
    /// 公平阀：印记描绘 64 帧全程可见；焰弹出膛慢速；追踪仅前 20 帧
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.FlameHunt, typeof(CultistStateContext))]
    internal class CultistFlameHuntState : CultistStateBase
    {
        public override string StateName => "CultistFlameHunt";
        public override CultistStateIndex StateIndex => CultistStateIndex.FlameHunt;

        private int SigilCount(CultistStateContext context) => context.Phase >= 2 ? 4 : 3;
        private int SigilInterval(CultistStateContext context) => context.Phase >= 2 ? 46 : 55;
        private const int SigilCharge = 64;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 11);
            FaceTarget(npc, player.Center);

            //绕行压位：环绕玩家缓慢换位，制造侧向火力角
            float orbitAngle = Timer * 0.017f + (npc.whoAmI % 7);
            Vector2 orbitPos = player.Center + orbitAngle.ToRotationVector2() * 430f - Vector2.UnitY * 120f;
            CultistMotion.SpringHover(npc, orbitPos, 0.011f, 0.08f, 17f);

            int interval = SigilInterval(context);
            int count = SigilCount(context);

            //逐张落印（权威端）
            if (!VaultUtils.isClient && Timer >= 12 && (Timer - 12) % interval == 0) {
                int placed = (int)(Timer - 12) / interval;
                if (placed < count) {
                    //印记落在玩家预判位：30 帧位移量的提前量
                    Vector2 predicted = player.Center + player.velocity * 26f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), predicted, Vector2.Zero,
                        ModContent.ProjectileType<CultistSigilProj>(), 0, 0f, Main.myPlayer,
                        0f, 0f, SigilCharge);
                }
            }

            //落印手势联动（各端由同步的印记出生推导，这里只推本体辉光）
            if ((Timer - 12) % interval == 0 && Timer >= 12) {
                context.PushAura(0.9f, CultistMotion.FlameCore);
                context.ScalePulse = 1.06f;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            //收尾：最后一张印记喷完 + 余韵
            int total = 12 + (count - 1) * interval + SigilCharge + 34;
            if (Timer >= total) {
                return new CultistWeaveState();
            }
            return null;
        }
    }
}
