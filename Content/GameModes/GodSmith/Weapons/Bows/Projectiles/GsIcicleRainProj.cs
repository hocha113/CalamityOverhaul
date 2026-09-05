using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles
{
    /// <summary>
    /// 寒冰弓齐射冰凌：扇形上抛、过顶点后锁向落点区俯冲成凌。
    /// ai[0] = 落点世界 X（生成端按准星 ±140px 散布算好），命中挂霜火并参与冻标；
    /// 冻标叠满 3 层再中一凌，触发碎冰爆（伤害由本凌折算，跨端可见）。
    /// 上升相穿墙防低顶撞天，俯冲相恢复碰撞
    /// </summary>
    internal class GsIcicleRainProj : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.FrostArrow}";

        private ref float FallX => ref Projectile.ai[0];

        /// <summary>0 上抛（穿墙），1 俯冲（恢复碰撞）</summary>
        private ref float State => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 320;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.arrow = true;
        }

        public override void AI() {
            if (State == 0f) {
                //上抛相：重力减速，登顶转俯冲
                Projectile.velocity.Y += 0.26f;
                Projectile.velocity.X *= 0.985f;
                if (Projectile.velocity.Y >= -0.5f) {
                    State = 1f;
                    Projectile.tileCollide = true;
                    //俯冲初速：直落起步，横向朝落点收
                    Projectile.velocity = new Vector2(Projectile.velocity.X * 0.4f, 4f);
                }
            }
            else {
                //俯冲相：加速直坠，横速缓收向落点 X
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.62f, 19f);
                float wantX = MathHelper.Clamp((FallX - Projectile.Center.X) * 0.05f, -3.2f, 3.2f);
                Projectile.velocity.X = MathHelper.Lerp(Projectile.velocity.X, wantX, 0.12f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 120);
            //冻标：owner 端钩子，标记是攻击方本地量；碎冰爆真弹幕跨端可见
            if (!GsHuntMarkNPC.CanMark(target)) {
                return;
            }
            GsHuntMarkNPC mark = target.GetGlobalNPC<GsHuntMarkNPC>();
            if (mark.Stacks >= 3) {
                mark.Stacks = 0;
                mark.Timer = 0;
                //碎冰爆 80% 基伤：本凌 each 0.55，折算 ×1.45
                Player owner = Main.player[Projectile.owner];
                Projectile.NewProjectile(owner.GetSource_Misc("GsVolleyBurst"), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsVolleyBurstProj>(), (int)(Projectile.damage * 1.45f), 3f,
                    owner.whoAmI, 100f, GsVolleyBurstProj.ThemeFrost);
            }
            else {
                mark.Stacks++;
                mark.Timer = 240;
            }
        }
    }
}
