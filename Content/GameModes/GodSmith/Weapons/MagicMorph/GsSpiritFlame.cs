using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 灵焰法杖重铸。材质身份：蓝白灵焰（悬停锁猎的沙漠亡灵之火）。<br/>
    /// 灵焰命中时或迸出追灵火苗；施法有举杖响应
    /// </summary>
    internal class GsSpiritFlame : GsMorphScheme
    {
        public override int TargetItemID => ItemID.SpiritFlame;

        protected override string GsDescFallback =>
            "Reforged: spirit flames trail pale soulfire; half of their hits split off a hunting wisp\nrelease to lay a five-flame ring at your cursor that pounces on intruders";
        protected override float BaseDamageMult => 1.05f;

        /// <summary>原版灵焰弹类型</summary>
        private static int FlameType => ContentSamples.ItemsByType[ItemID.SpiritFlame].shoot;

        //==================== 动画法：举杖 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //施法举杖：出手瞬间杖身抬升 4px 再落回（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(0f, -4f * progress);
            player.itemRotation -= player.direction * 0.08f * progress;
        }

        //==================== rider：追灵火苗 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != FlameType) {
                return;
            }
            //追灵火苗：owner 端掷签，半数命中分出一缕（生成随原生链同步）
            if (proj.IsOwnedByLocalPlayer() && Main.rand.NextBool()) {
                int wispDamage = Math.Max(1, (int)(proj.damage * 0.3f));
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f, 4f) - new Vector2(0f, 2f);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    ModContent.ProjectileType<GsSpiritFlameWispProj>(), wispDamage, 0f, proj.owner);
            }
        }
    }

    /// <summary>追灵火苗：命中分出的小灵焰，弧线上飘后咬向近敌</summary>
    internal class GsSpiritFlameWispProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpiritFlame;

        public override string LocalizationCategory => "GodSmithMagicMorph";

        public override void SetStaticDefaults() {
            //原版魂火是竖排四帧，不声明帧数会整条竖图一起画
            Main.projFrames[Type] = Main.projFrames[ProjectileID.SpiritFlame];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //前 18t 上飘减速（出手弧线），随后锁定近敌俯冲
            if (Projectile.timeLeft > 72) {
                Projectile.velocity *= 0.95f;
            }
            else {
                NPC prey = Projectile.Center.FindClosestNPC(360f);
                if (prey != null) {
                    float wanted = (prey.Center - Projectile.Center).ToRotation();
                    float current = Projectile.velocity.ToRotation();
                    float speed = Math.Min(Projectile.velocity.Length() + 0.25f, 11f);
                    Projectile.velocity = Utils.AngleTowards(current, wanted, MathHelper.ToRadians(7f))
                        .ToRotationVector2() * speed;
                }
            }
        }
    }
}
