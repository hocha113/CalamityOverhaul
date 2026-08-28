using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>群臂万象：横竖交织的幽灵臂巷道扫掠，末段双臂对角扑抓</summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.GhostPandemonium, typeof(SkeletronStateContext))]
    internal class SkeletronGhostPandemoniumState : SkeletronStateBase
    {
        public override string StateName => "GhostPandemonium";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.GhostPandemonium;

        internal const int Duration = 236;

        private Vector2 latchCenter;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            //头织于上空，轻压制
            HoverMovement(context, 0.05f, 4.6f, 0.12f, 9.4f, 0.95f, 380);
            LeanByVelocity(npc);
            SkeletronScreenEffects.RequestDomain(0.28f);

            //布阵（服务端一次性）
            if (Timer == 10) {
                latchCenter = context.Target.Center;
                if (!VaultUtils.isClient) {
                    SpawnLaneArms(context, npc);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.4f }, context.Target.Center);
                }
            }

            //末段对角双扑
            if (Timer == 158 && !VaultUtils.isClient) {
                int damage = SkullDamage(context);
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int k = 0; k < 2; k++) {
                    float a = ang + k * MathHelper.Pi;
                    Vector2 pos = context.Target.Center + a.ToRotationVector2() * SkeletronGhostArmProj.LungeRingRadius;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                        ModContent.ProjectileType<SkeletronGhostArmProj>(), damage, 0f, Main.myPlayer,
                        (float)SkeletronGhostArmProj.ArmMode.CircleLunge, a, 26f + k * 9f);
                }
                npc.netUpdate = true;
            }

            //期间头颅两次点射
            if (!VaultUtils.isClient && (Timer == 70 || Timer == 120)
                && Collision.CanHitLine(npc.Center, 1, 1, context.Target.position, context.Target.width, context.Target.height)) {
                Vector2 vel = DirectionToTarget(context) * 6.8f;
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

        /// <summary>两横两竖巷道臂：错拍扫掠，缝隙可走</summary>
        private void SpawnLaneArms(SkeletronStateContext context, NPC npc) {
            int damage = SkullDamage(context);
            float sweep = SkeletronDirector.GhostSweepSpeed(context.AsuraMode);
            Vector2 c = latchCenter;

            //横巷道：上下两条，方向相对
            SpawnSweep(npc, new Vector2(c.X - 820f, c.Y - 150f), new Vector2(sweep, 0f), 30, 110, damage);
            SpawnSweep(npc, new Vector2(c.X + 820f, c.Y + 60f), new Vector2(-sweep, 0f), 62, 110, damage);
            //竖巷道：左右两条，自上而下
            SpawnSweep(npc, new Vector2(c.X - 190f, c.Y - 680f), new Vector2(0f, sweep), 94, 96, damage);
            SpawnSweep(npc, new Vector2(c.X + 190f, c.Y - 680f), new Vector2(0f, sweep), 120, 96, damage);
        }

        private static void SpawnSweep(NPC npc, Vector2 pos, Vector2 vel, int telegraph, int active, int damage) {
            //注意：不追加 netUpdate：生成包已带原始巷道速度，
            //冻结后再同步会把速度0覆写给远端，导致远端永不起扫
            Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel,
                ModContent.ProjectileType<SkeletronGhostArmProj>(), damage, 0f, Main.myPlayer,
                (float)SkeletronGhostArmProj.ArmMode.LaneSweep, telegraph, active);
        }
    }
}
