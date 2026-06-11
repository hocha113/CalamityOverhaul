using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 弹幕墙（高难限定）：头颅升空号令，自下而上与自左而右的两面火箭洪流横扫战场，
    /// 期间授予所有玩家无限飞行——空中走位是唯一的生路。
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.LaserWall, typeof(PrimeStateContext))]
    internal class PrimeLaserWallState : PrimeStateBase
    {
        public override string StateName => "LaserWall";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.LaserWall;

        private const int WallTick = 30;
        private const int StateDuration = 150;
        private const int WallShootNum = 40;

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 2;

            //定点压阵
            Vector2 anchor = context.Target.Center + new Vector2(0, -320);
            npc.velocity = Vector2.Lerp(npc.velocity, (anchor - npc.Center) * 0.035f, 0.2f);
            LeanByVelocity(npc);

            if (Timer < WallTick) {
                context.SetChargeState(3, Timer / (float)WallTick);
            }

            if (Timer == WallTick) {
                context.ResetChargeState();
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    npc.TargetClosest();
                    SpawnWalls(context);
                    npc.netUpdate = true;
                }
            }

            //全程授予无限飞行，确保弹幕墙是"考走位"而不是"考蓝量"
            if (!VaultUtils.isServer) {
                foreach (Player p in Main.player) {
                    if (p.dead || !p.active) {
                        continue;
                    }
                    p.SetPlayerInfiniteFlight(true);
                }
            }

            Timer++;
            if (Timer >= StateDuration) {
                npc.damage = npc.defDamage * 2;
                if (!VaultUtils.isClient) {
                    return new PrimeRageHoverState();
                }
            }
            return null;
        }

        private void SpawnWalls(PrimeStateContext context) {
            NPC npc = context.Npc;
            Vector2 origin = context.Target.Center;
            int damage = ScaleDamage(CWRRef.GetProjectileDamage(npc, ProjectileID.RocketSkeleton));
            const int spacing = 200;
            int half = spacing * WallShootNum / -2;

            //自下而上的火箭墙
            for (int i = 0; i < WallShootNum; i++) {
                Vector2 spawnPos = origin + new Vector2(half + spacing * i, 1800);
                Vector2 velocity = new Vector2(0, -6);
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, velocity,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, -1, -1, velocity.ToRotation());
            }
            //自左而右的火箭墙
            for (int i = 0; i < WallShootNum; i++) {
                Vector2 spawnPos = origin + new Vector2(-1800, half + spacing * i);
                Vector2 velocity = new Vector2(6, 0);
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, velocity,
                    ModContent.ProjectileType<PrimeCannonOnSpan>(), damage, 0f,
                    Main.myPlayer, -1, -1, velocity.ToRotation());
            }
        }
    }
}
