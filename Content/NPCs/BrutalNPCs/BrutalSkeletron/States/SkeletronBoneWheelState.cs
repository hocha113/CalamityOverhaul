using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.States
{
    /// <summary>
    /// 旋骨罗盘（二阶段签名）：头颅升空默祷，两波旋骨轮从侧翼/天地钳杀碾场<br/>
    /// 缺口（契约3）：轮辐 90° 豁口可穿（SkeletronBoneWheel.SpokeGapSlots，碰撞绘制同源）；
    /// 头颅全程停火悬停，本招是纯输出窗，压力全在轮上
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)SkeletronStateIndex.BoneWheel, typeof(SkeletronStateContext))]
    internal class SkeletronBoneWheelState : SkeletronStateBase
    {
        public override string StateName => "BoneWheel";
        public override SkeletronStateIndex StateIndex => SkeletronStateIndex.BoneWheel;

        /// <summary>默祷帧（第一波召唤前的读秒）</summary>
        private const int WindupFrames = 30;
        /// <summary>第二波召唤帧</summary>
        private const int SecondWaveFrame = 100;
        /// <summary>状态总时长（轮体自走寿命，不等它死）</summary>
        private const int Duration = 176;
        /// <summary>侧翼出轮距离</summary>
        private const float FlankDistance = 680f;
        /// <summary>天/地出轮距离（地侧更近，读作破土）</summary>
        private const float SkyDistance = 540f;
        private const float GroundDistance = 440f;

        public override ISkeletronState OnUpdate(SkeletronStateContext context) {
            NPC npc = context.Npc;
            npc.damage = npc.defDamage;

            //默祷升空→全程高位缓浮（离轮阵远，画面让给轮）
            HoverMovement(context, 0.06f, 5f, 0.09f, 7f, 0.95f, 380);
            LeanByVelocity(npc);
            context.EyeFlame = Timer < WindupFrames ? 1f + Timer / (float)WindupFrames * 0.5f : 1.3f;

            if (Timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageCastHeal with { Volume = 0.9f, Pitch = -0.55f }, npc.Center);
            }

            //第一波：侧翼水平钳杀（滚进角出生锁死，指向此刻站位）
            if (Timer == WindupFrames && !VaultUtils.isClient) {
                SpawnWheelPair(context, Vector2.UnitX * FlankDistance, Vector2.UnitX * -FlankDistance);
            }
            //第二波：天坠地涌垂直钳杀
            if (Timer == SecondWaveFrame && !VaultUtils.isClient) {
                SpawnWheelPair(context, Vector2.UnitY * -SkyDistance, Vector2.UnitY * GroundDistance);
            }

            Timer++;
            if (Timer >= Duration && !VaultUtils.isClient) {
                npc.TargetClosest();
                npc.netUpdate = true;
                return new SkeletronHubState();
            }
            return null;
        }

        /// <summary>自目标两翼各出一轮，滚进线穿过召唤瞬间的站位（此后绝不转向）</summary>
        private static void SpawnWheelPair(SkeletronStateContext context, Vector2 offsetA, Vector2 offsetB) {
            NPC npc = context.Npc;
            Vector2 lockPoint = context.Target.Center;
            float speed = (context.AsuraMode ? 11.4f : 10.2f) + (context.BossRush ? 1.4f : 0f);
            int damage = SkullDamage(context);

            foreach (Vector2 offset in new[] { offsetA, offsetB }) {
                Vector2 spawn = lockPoint + offset;
                float rollAngle = (lockPoint - spawn).ToRotation();
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, Vector2.Zero,
                    ModContent.ProjectileType<SkeletronBoneWheel>(), damage, 0f, Main.myPlayer,
                    rollAngle, speed, Main.rand.NextFloat(MathHelper.TwoPi));
            }
            npc.netUpdate = true;
        }
    }
}
