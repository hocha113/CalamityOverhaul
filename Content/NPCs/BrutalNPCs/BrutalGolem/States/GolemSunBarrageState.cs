using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.States
{
    /// <summary>太阳宝石弹幕：胸口宝石充能后连发弧线臼炮，空爆成余烬雨；附着头眼弹补线</summary>
    [InnoVault.StateMachines.VaultState((int)GolemStateIndex.SunBarrage, typeof(GolemStateContext))]
    internal class GolemSunBarrageState : GolemStateBase
    {
        public override string StateName => "SunBarrage";
        public override GolemStateIndex StateIndex => GolemStateIndex.SunBarrage;

        internal static int ChargeTime => 64;
        /// <summary>连发间隔拍</summary>
        internal static int VolleyInterval => 40;
        /// <summary>臼炮弹重力，与 GolemSunMortar.AI 的加速度一致（弧顶解算依赖）</summary>
        private const float MortarGravity = 0.32f;

        private int volleyTimer;

        public override IGolemState OnUpdate(GolemStateContext context) {
            NPC npc = context.Npc;
            context.FrameMode = 0;
            GroundBrake(npc);
            //站桩状态强制恢复地形碰撞
            npc.noTileCollide = false;

            int charge = Tempo(context, ChargeTime);

            //充能段：宝石汇聚（末1/4静默，尖啸前的吸气）
            if (Timer < charge) {
                float t = Timer / (float)charge;
                context.SetChargeState(1, t);
                context.VeinGlow = Math.Max(context.VeinGlow, t);

                if (!VaultUtils.isServer && t < 0.75f && Timer % 3 == 0) {
                    Vector2 gem = npc.Center + new Vector2(0f, -6f);
                    Vector2 from = gem + Main.rand.NextVector2CircularEdge(120f, 120f);
                    Dust dust = Dust.NewDustPerfect(from, DustID.SolarFlare, (gem - from) * 0.08f, 0, default, 1.2f);
                    dust.noGravity = true;
                }
                if (!VaultUtils.isServer && Timer == (int)(charge * 0.75f)) {
                    SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.3f, Volume = 0.85f }, npc.Center);
                }
            }
            else {
                context.SetChargeState(1, 1f);
                context.VeinGlow = Math.Max(context.VeinGlow, 0.8f);

                //连发段
                int volleys = context.DeathMode ? 4 : 3;
                int volleyInterval = Tempo(context, VolleyInterval);
                if (!VaultUtils.isClient && Counter < volleys) {
                    if (++volleyTimer >= volleyInterval) {
                        volleyTimer = 0;
                        FireVolley(context);
                        Counter++;
                    }
                }
            }

            Timer++;
            int endTime = charge + Tempo(context, VolleyInterval) * (context.DeathMode ? 4 : 3) + 70;
            if (Timer >= endTime && !VaultUtils.isClient) {
                return new GolemConnectorState();
            }
            return null;
        }

        /// <summary>一轮臼炮：横向按预读位置散布，弧顶解算到目标高度上方空爆（高飞也被余烬雨罩住）</summary>
        private void FireVolley(GolemStateContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            Vector2 gem = npc.Center + new Vector2(0f, -6f);

            int shots = (context.Sundered ? 4 : 3) + (context.DeathMode ? 1 : 0);
            int damage = ScaleDamage(context, GolemDirector.MortarDamage);

            for (int i = 0; i < shots; i++) {
                //解算抛物弧：横向按玩家预读位置分布落点
                float spread = (i - (shots - 1) * 0.5f) * 150f;
                Vector2 aimPoint = target.Center + target.velocity * 20f + new Vector2(spread, 0f);
                float dx = aimPoint.X - gem.X;
                float vx = MathHelper.Clamp(dx / 52f, -13f, 13f);
                //弧顶解算：空爆点压在目标上方约140px，余烬自上而下罩落
                float apexRise = gem.Y - (aimPoint.Y - 140f);
                float vy = apexRise > 60f
                    ? -MathF.Sqrt(2f * MortarGravity * apexRise)
                    : Main.rand.NextFloat(-15.5f, -13.5f);
                vy = MathHelper.Clamp(vy + Main.rand.NextFloat(-0.6f, 0.6f), -27f, -12.5f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), gem, new Vector2(vx, vy),
                    ModContent.ProjectileType<GolemSunMortar>(), damage, 0f, Main.myPlayer, 0f, 0f);
            }
            npc.netUpdate = true;
        }
    }
}
