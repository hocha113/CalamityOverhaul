using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 妖灵弓「双灵环绕」处决光灵：绕标记敌公转 2 秒的光珠（原版圣光星贴图默认绘制），每 30 帧一次接触伤。
    /// ai[0] = 目标 NPC whoAmI（跨端一致），ai[1] = 初相位（双灵对置）。
    /// 位置由目标位置 + 确定相位驱动（各端同步量），不走速度积分。
    /// 数量护栏：生成端按 ownedProjectileCounts 限 4/玩家
    /// </summary>
    internal class GsFaeOrbiterProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HallowStar;

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float PhaseOffset => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            int idx = (int)TargetIndex;
            NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (target == null || !target.active) {
                //目标失效：光灵散逸
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 8);
                Projectile.Center += new Vector2(0f, -1.2f);
                return;
            }
            //公转：角速度 + 呼吸半径，全部确定性输入
            float angle = PhaseOffset + Life * 0.085f;
            float radius = 46f + 7f * MathF.Sin(Life * 0.11f + Projectile.identity * 0.61f);
            Projectile.Center = target.Center + angle.ToRotationVector2() * radius;
        }
    }
}
