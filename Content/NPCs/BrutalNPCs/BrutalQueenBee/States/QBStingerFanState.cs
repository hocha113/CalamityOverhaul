using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 毒刺扇：悬停侧上位连发扇形毒刺，机动压制型喘息招<br/>
    /// ai[0]=侧翼(服务端掷骰)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.StingerFan, typeof(QueenBeeStateContext))]
    internal class QBStingerFanState : QueenBeeStateBase
    {
        public override string StateName => "StingerFan";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.StingerFan;

        private const int MaxTime = 184;
        private const int MaxVolleys = 6;
        private const int BaseInterval = 24;
        /// <summary>公平阀：扇形半张角(一/二阶段)，毒刺条数有限且等角散布，射线之间恒有可穿行角缝</summary>
        private const float FanSpreadHalfP1 = 0.12f;
        private const float FanSpreadHalfP2 = 0.24f;
        /// <summary>公平阀：奇数序毒刺减速比，扇面拆成前后两层，纵深上也留穿越窗</summary>
        private const float LaggedRayScale = 0.88f;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                NPC npc = context.Npc;
                npc.ai[0] = npc.Center.X < context.Target.Center.X ? -1 : 1;
                npc.netUpdate = true;
            }
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = npc.ai[0] >= 0f ? 1 : -1;

            Timer++;

            //悬停侧上位，横向绕着走(压制而非站桩)
            float drift = (float)System.Math.Sin(Timer * 0.03f) * 90f;
            Vector2 hoverPos = player.Center + new Vector2(side * (340f + drift), -260f);
            QueenBeeMotion.SpringHover(npc, hoverPos, 0.017f, 0.09f, 26f);
            FaceTarget(npc, player.Center);

            //射击节拍：修罗模式/激怒加速
            int interval = BaseInterval;
            if (context.IsAsuraMode) {
                interval -= 5;
            }
            interval -= (int)(context.EnrageScale * 3f);
            if (interval < 14) {
                interval = 14;
            }

            if (Timer % interval == interval - 1 && Counter < MaxVolleys) {
                Counter++;
                FireFan(context);
            }

            if (Timer >= MaxTime || Counter >= MaxVolleys && Timer % interval == 0) {
                return new QBRepositionState();
            }
            return null;
        }

        /// <summary>腹部扇形毒刺+后坐+蜜雾</summary>
        private void FireFan(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Vector2 muzzle = npc.Center + new Vector2(0f, npc.height * 0.32f);
            Vector2 aim = (player.Center - muzzle).SafeNormalize(Vector2.UnitY);

            //射击后坐：发射器被顶回去
            npc.velocity -= aim * 2.8f;

            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = -0.05f }, muzzle);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_HoneyMist>(muzzle, aim * Main.rand.NextFloat(1f, 2.4f),
                        QueenBeeMotion.HoneyGold * 0.4f, Main.rand.NextFloat(0.4f, 0.7f));
                }
            }

            if (VaultUtils.isClient) {
                return;
            }

            int count = context.IsPhase2 ? 5 : 3;
            float spreadHalf = context.IsPhase2 ? FanSpreadHalfP2 : FanSpreadHalfP1;
            float speed = 8.5f + context.EnrageScale * 1.2f + (context.IsAsuraMode ? 1f : 0f);
            for (int i = 0; i < count; i++) {
                float t = count <= 1 ? 0f : i / (float)(count - 1) * 2f - 1f;
                Vector2 vel = aim.RotatedBy(t * spreadHalf) * speed;
                //扇缘略慢，形成层次
                if (i % 2 == 1) {
                    vel *= LaggedRayScale;
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                    ModContent.ProjectileType<BrutalBeeStinger>(), BrutalBeeStinger.BaseDamage, 0f, Main.myPlayer, 0f);
            }
        }
    }
}
