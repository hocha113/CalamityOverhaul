using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>幽灵臂环猎：灵体手环绕凝聚，错拍连环扑抓</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.GhostArmCircle, typeof(SkeletronStateContext))]
    internal class SkeletronGhostArmCircleState : SkeletronStateBase
    {
        public override string StateName => "GhostArmCircle";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.GhostArmCircle;

        internal const int Duration = 164;

        /// <summary>缺口（契约3）：臂环永空 RingGapSlots 个槽（随机朝向的开口扇区），
        /// 且环心在布阵瞬间锁死不追踪——顺开口撤出即安全，布阵循环直接跳过该槽</summary>
        private const int RingGapSlots = 1;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            bool p2 = (int)npc.ai[SkeletronAiSlots.HeadPhase] >= SkeletronPhase.Unbound;

            //头保持中距斜上位游弋
            HoverMovement(context, 0.045f, 4.2f, 0.1f, 8.6f, 0.95f, 260);
            LeanByVelocity(npc);

            //召唤幽灵臂环（服务端一次性布阵，环心锁死在此刻站位）
            if (Timer == 8 && !VaultUtils.isClient) {
                int count = p2 ? 6 : 4;
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                int damage = SkullDamage(context);
                for (int i = 0; i < count - RingGapSlots; i++) {
                    float angle = baseAngle + MathHelper.TwoPi * i / count;
                    Vector2 pos = context.Target.Center + angle.ToRotationVector2() * SkeletronGhostArmProj.LungeRingRadius;
                    //交叉错拍：对角先后错开，读作连环而非齐射
                    int launchDelay = 34 + (i % 2 == 0 ? i / 2 : count / 2 + i / 2) * (p2 ? 11 : 15);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<SkeletronGhostArmProj>(), damage, 0f, Main.myPlayer,
                        (float)SkeletronGhostArmProj.ArmMode.CircleLunge, angle, launchDelay);
                }
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer && Timer == 8) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.55f }, context.Target.Center);
            }

            //中段头颅补一发正压颅火
            if (!VaultUtils.isClient && Timer == 96
                && Collision.CanHitLine(npc.Center, 1, 1, context.Target.position, context.Target.width, context.Target.height)) {
                Vector2 vel = DirectionToTarget(context) * 7f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel * 6f, vel,
                    ModContent.ProjectileType<SkeletronCursedSkull>(), SkullDamage(context), 0f, Main.myPlayer, 1f, 0f);
                npc.netUpdate = true;
            }

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                return new SkeletronHubState();
            }
            return null;
        }
    }
}
