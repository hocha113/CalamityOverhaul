using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Spazmatism
{
    /// <summary>
    /// 魔焰眼一阶段悬停射击状态
    /// </summary>
    internal class SpazmatismHoverShootState : TwinsStateBase
    {
        public override string StateName => "SpazmatismHoverShoot";

        private int ShootRate => Context.IsMachineRebellion ? 55 : (Context.IsDeathMode ? 60 : 80);
        private float MoveSpeed => Context.IsMachineRebellion ? 16f : (Context.IsDeathMode ? 14f : 12f);
        private int MaxShootCount => Context.IsDeathMode ? 2 : 3;

        private TwinsStateContext Context;

        public override void OnEnter(TwinsStateContext context) {
            base.OnEnter(context);
            Context = context;
        }

        public override ITwinsState OnUpdate(TwinsStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //计算悬停位置，在玩家侧边
            Vector2 hoverTarget = player.Center + new Vector2(npc.Center.X < player.Center.X ? -400 : 400, -200);
            MoveTo(npc, hoverTarget, MoveSpeed, 0.05f);
            FaceTarget(npc, player.Center);

            Timer++;
            if (Timer >= ShootRate) {
                //发射火球
                if (!VaultUtils.isClient) {
                    float shootSpeed = Context.IsDeathMode ? 14f : 12f;
                    Vector2 shootVel = GetDirectionToTarget(context) * shootSpeed;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVel,
                        ModContent.ProjectileType<Fireball>(),
                        30,
                        0f,
                        Main.myPlayer
                    );
                }
                SoundEngine.PlaySound(SoundID.Item34, npc.Center);
                Timer = 0;
                Counter++;
            }

            //射击次数后切换状态
            if (Counter >= MaxShootCount) {
                //50%概率使用火焰漩涡，50%概率冲刺
                if (Main.rand.NextBool()) {
                    return new SpazmatismFireVortexState();
                }
                else {
                    return new SpazmatismDashPrepareState();
                }
            }

            return null;
        }
    }
}
