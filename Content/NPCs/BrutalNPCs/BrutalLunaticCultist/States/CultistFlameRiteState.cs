using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 火焰·印记狩猎：印记逐张落在玩家预判位，定形 24 帧后喷焰扇<br/>
    /// 日耀主场强化：印记+1,并追加日珥抛射——沿可见抛物线落地连成燃地,空间随时间收缩<br/>
    /// 公平阀：印记描绘 64 帧全程可见；焰弹出膛慢速；燃地有寿命会冷却,点燃循环保留走廊
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.FlameRite, typeof(CultistStateContext))]
    internal class CultistFlameRiteState : CultistStateBase
    {
        public override string StateName => "CultistFlameRite";
        public override CultistStateIndex StateIndex => CultistStateIndex.FlameRite;

        private const int SigilCharge = 64;

        private static bool IsHome(CultistStateContext context) => context.Phase == 3 || context.Phase >= 4;

        private int SigilCount(CultistStateContext context) => IsHome(context) ? 4 : 3;
        private int SigilInterval(CultistStateContext context) => IsHome(context) ? 46 : 55;

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
                    Vector2 predicted = player.Center + player.velocity * 26f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), predicted, Vector2.Zero,
                        ModContent.ProjectileType<CultistSigilProj>(), 0, 0f, Main.myPlayer,
                        context.Phase, 0f, SigilCharge);
                }
            }

            //日耀主场:日珥抛射,落地连燃地(权威端)
            if (!VaultUtils.isClient && IsHome(context) && Timer >= 30 && Timer % 58 == 0) {
                Vector2 dir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                Vector2 vel = dir * 6.5f - Vector2.UnitY * 5.5f;
                int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 30f, vel,
                    ModContent.ProjectileType<CultistFlameBolt>(), 38, 0f, Main.myPlayer, 0f, 1f);
                if (idx < Main.maxProjectiles) {
                    //主场燃地更持久
                    Main.projectile[idx].localAI[1] = 380f;
                    Main.projectile[idx].netUpdate = true;
                }
                CultistMotion.CastFlash(npc.Center + dir * 30f, CultistMotion.SolarCore, 0.9f);
                npc.velocity -= vel * 0.25f;
            }

            if ((Timer - 12) % interval == 0 && Timer >= 12) {
                context.PushAura(0.9f, CultistMotion.PhaseCore(context.Phase));
                context.ScalePulse = 1.06f;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            int total = 12 + (count - 1) * interval + SigilCharge + 34;
            if (Timer >= total) {
                return new CultistWeaveState();
            }
            return null;
        }
    }
}
