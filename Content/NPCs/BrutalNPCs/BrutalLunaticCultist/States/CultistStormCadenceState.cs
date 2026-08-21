using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 雷·雷律三拍：雷锚印记悬于玩家上方，三拍落雷按 0/44/82 帧递缩节拍<br/>
    /// 公平阀：每拍 26 帧细弧预告；预告线在起拍瞬间锁死不追人；只在 10 帧落雷窗内咬人
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.StormCadence, typeof(CultistStateContext))]
    internal class CultistStormCadenceState : CultistStateBase
    {
        public override string StateName => "CultistStormCadence";
        public override CultistStateIndex StateIndex => CultistStateIndex.StormCadence;

        private const int SigilCharge = 40;

        private int AnchorCount(CultistStateContext context) => context.Phase switch { 2 => 3, 1 => 2, _ => 1 };

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 11);
            FaceTarget(npc, player.Center);

            //高位威压驻停
            Vector2 hover = player.Center + new Vector2(0f, -360f)
                + CultistMotion.BreathingOffset(seed: 4.1f, 12f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            //落锚（权威端）：锚点悬于玩家预判位上方
            int anchors = AnchorCount(context);
            if (!VaultUtils.isClient && Timer >= 16 && (Timer - 16) % 62 == 0) {
                int placed = (int)(Timer - 16) / 62;
                if (placed < anchors) {
                    Vector2 anchorPos = player.Center + player.velocity * 20f + new Vector2(
                        Main.rand.NextFloat(-140f, 140f), -270f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), anchorPos, Vector2.Zero,
                        ModContent.ProjectileType<CultistSigilProj>(), 0, 0f, Main.myPlayer,
                        2f, 3f, SigilCharge);
                }
            }

            if (Timer >= 16 && (Timer - 16) % 62 == 0) {
                context.PushAura(0.9f, CultistMotion.StormCore);
                context.ScalePulse = 1.05f;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            //收尾：最后一锚 + 三拍打完（82+落雷+尾闪）
            int total = 16 + (anchors - 1) * 62 + SigilCharge + 82 + 50;
            if (Timer >= total) {
                return new CultistWeaveState();
            }
            return null;
        }
    }
}
