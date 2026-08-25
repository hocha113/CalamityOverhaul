using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 闪电·雷律三拍：雷锚印记悬于玩家上方，三拍落雷按 0/44/82 帧递缩节拍<br/>
    /// 星旋主场强化：闪电失去施法者变成天气——额外的天落雷从场地上空随机砸下,锚数+1<br/>
    /// 公平阀：每拍 26 帧细弧预告；预告线起拍锁死不追人；只在 10 帧落雷窗内咬人；天落雷同一套预告常量
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.BoltRite, typeof(CultistStateContext))]
    internal class CultistBoltRiteState : CultistStateBase
    {
        public override string StateName => "CultistBoltRite";
        public override CultistStateIndex StateIndex => CultistStateIndex.BoltRite;

        private const int SigilCharge = 40;

        private static bool IsHome(CultistStateContext context) => context.Phase == 0 || context.Phase >= 4;

        private int AnchorCount(CultistStateContext context) => IsHome(context) ? 3 : 2;

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
                        context.Phase, 3f, SigilCharge);
                }
            }

            //星旋主场:它成了天气——天落雷从上空砸场地随机点,不锁玩家(权威端)
            if (!VaultUtils.isClient && IsHome(context) && Timer >= 40 && Timer % 46 == 0) {
                float x = context.ArenaCenter.X + Main.rand.NextFloat(-0.8f, 0.8f) * CultistStateContext.ArenaRadius;
                Vector2 skyAnchor = new(x, context.ArenaCenter.Y - 560f);
                //拍点直落:ArcBolt 起拍即快照,预告常量与锚雷同一套
                int idx = Projectile.NewProjectile(npc.GetSource_FromAI(), skyAnchor, Vector2.Zero,
                    ModContent.ProjectileType<CultistArcBolt>(), 48, 0f, Main.myPlayer,
                    x, context.ArenaCenter.Y + 420f, 0f);
                if (idx < Main.maxProjectiles) {
                    Main.projectile[idx].netUpdate = true;
                }
            }

            if (Timer >= 16 && (Timer - 16) % 62 == 0) {
                context.PushAura(0.9f, CultistMotion.PhaseCore(context.Phase));
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
