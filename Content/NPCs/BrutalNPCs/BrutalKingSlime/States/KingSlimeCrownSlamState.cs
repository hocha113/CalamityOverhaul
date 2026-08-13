using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 王冠天坠：王冠升空瞄准→天坠砸地(金环+喷泉)→飞回。
    /// 期间本体两次轻跳保持压迫；P2王冠落地时本体跟进一记大跳追压
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.CrownSlam, typeof(KingSlimeStateContext))]
    internal class KingSlimeCrownSlamState : KingSlimeStateBase
    {
        public override string StateName => "CrownSlam";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.CrownSlam;

        private const int Watchdog = 330;

        private bool crownLandedReacted;
        private int hopTimer;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            crownLandedReacted = false;
            hopTimer = 0;

            //默认态王冠扣在头顶(渲染层绘制，无弹幕)，招式开始时从扣冠锚点脱冕弹出
            if (!VaultUtils.isClient && context.FindCrown() == null) {
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(),
                    KingSlimeRenderer.CrownAnchorWorld(context.Npc, context),
                    Vector2.Zero, ModContent.ProjectileType<BKSCrownProj>(),
                    (int)(context.Npc.defDamage * 0.55f), 0f, Main.myPlayer,
                    context.Npc.whoAmI, BKSCrownProj.ModeLaunch);
            }
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            npc.direction = npc.spriteDirection = DirToTarget(context);

            Projectile crown = context.FindCrown();
            int crownMode = crown != null ? (int)crown.ai[1] : -1;

            //本体：轻跳保持压迫(小步逼近)
            hopTimer++;
            if (Grounded(npc)) {
                npc.velocity.X *= 0.8f;
                if (hopTimer > 46) {
                    hopTimer = 0;
                    float dx = player.Center.X - npc.Center.X;
                    LaunchHop(npc, MathHelper.Clamp(dx / 60f, -6.5f, 6.5f), -8.5f);
                    context.StretchImpulse(0.2f);
                    KingSlimeGelFX.SquishSound(npc.Bottom, -0.1f, 0.6f);
                }
            }

            //王冠落地拍：P2本体跟进大跳追压
            if (crownMode == BKSCrownProj.ModeLanded && !crownLandedReacted) {
                crownLandedReacted = true;
                if (context.IsPhase2 && Grounded(npc)) {
                    float dx = player.Center.X + player.velocity.X * 16f - npc.Center.X;
                    LaunchHop(npc, MathHelper.Clamp(dx / 30f, -13f, 13f), -15f);
                    context.StretchImpulse(0.35f);
                    context.PendingLandingShockwave = 1;
                    context.LandingSplashMul = 1.3f;
                }
            }

            //王冠蓄势期本体光环呼应
            if (crownMode == BKSCrownProj.ModeTelegraph) {
                context.AuraMode = 1;
                context.AuraProgress = 0.5f;
            }

            //收招：王冠归位砸扣完成(弹幕消亡、扣冠交还渲染层)
            bool crownDone = crown == null;
            if (Timer > 40 && crownDone && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            //看门狗
            if (Timer > Watchdog && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }
    }
}
