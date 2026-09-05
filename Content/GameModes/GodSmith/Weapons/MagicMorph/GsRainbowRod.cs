using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 彩虹魔杖重铸：引导弹命中迸散 3 枚彩虹碎屑（承签子弹幕，直线衰减）
    /// </summary>
    internal class GsRainbowRod : GsMorphScheme
    {
        public override int TargetItemID => ItemID.RainbowRod;

        protected override string GsDescFallback =>
            "Reforged: guided bolts burst into prismatic shards on hit.\nrelease to raise a rainbow bridge that hurts foes crossing it and hastens allies standing on it";
        protected override float BaseDamageMult => 1.08f;

        /// <summary>MarkData 形态：彩虹碎屑</summary>
        private const int KindShard = 10;

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.RainbowRodBullet || KindOf(router) == KindShard) {
                return;
            }
            //A 形态命中：迸散 3 枚碎屑（owner 端生成，Parent 源承签，方向 owner 掷定随生成包过线）
            for (int i = 0; i < 3; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(6f, 6f) - Vector2.UnitY * 2f;
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, vel,
                    ProjectileID.RainbowRodBullet, (int)(proj.damage * 0.25f), 0f, proj.owner);
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (proj.type != ProjectileID.RainbowRodBullet) {
                return;
            }
            //碎屑定型：改档位、缩短寿命（owner 端 Kill 会广播，远端不靠 timeLeft 对齐）
            router.MarkData = KindShard;
            router.MarkData2 = 0f;
            proj.timeLeft = 40;
            proj.scale = 0.6f;
            proj.alpha = 0;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.RainbowRodBullet || KindOf(router) != KindShard) {
                return true;
            }
            //碎屑压掉原版引导 AI（防止跟随光标），改直线衰减 + 微重力；全端同式确定运动
            proj.alpha = 0;
            proj.velocity *= 0.975f;
            proj.velocity.Y += 0.06f;
            proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;
            return false;
        }
    }
}
