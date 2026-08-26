using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 魔法导弹重铸：引导二形态。<br/>
    /// A 形态保留原版按住引导并提速 25%，星屑尾迹；
    /// B 形态（右键蓄 45t）「三联齐导」：一次射出 3 枚编队导弹，
    /// 以 ±40px 垂直偏置跟随光标（槽位经 MarkData2 过线，各端同形）
    /// </summary>
    internal class GsMagicMissile : GsMorphScheme
    {
        public override int TargetItemID => ItemID.MagicMissile;

        protected override string GsDescFallback =>
            "Reforged: guided flight steers 25% faster.\nHold right click to charge; release a triple volley that escorts your cursor in formation";

        protected override int ChargeTicksB => 45;
        protected override float ChargeManaMult => 1.8f;
        protected override Color ChargeColor => new(150, 140, 255);
        protected override float BaseDamageMult => 1.10f;

        private static readonly Color TrailViolet = new(168, 150, 255);

        protected override void FireMorphB(Item item, Player player) {
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.9f, Pitch = -0.1f }, player.Center);
            Vector2 dir = GsAimUnit(player);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int dmg = (int)(player.GetWeaponDamage(item) * 0.7f);
            float speed = MathHelper.Max(item.shootSpeed, 7f);
            for (int slot = -1; slot <= 1; slot++) {
                SpawnMorph(player, item, player.Center + dir * 16f + perp * slot * 22f,
                    dir * speed, ProjectileID.MagicMissile, dmg, item.knockBack, KindB, slot);
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            Player owner = Main.player[proj.owner];
            //引导修正只在 owner 端进行（目标点读的是本地鼠标），远端靠原版 netUpdate 同步速度
            if (proj.owner == Main.myPlayer && owner.channel) {
                float speed = proj.velocity.Length();
                if (speed > 0.01f) {
                    if (KindOf(router) == KindB) {
                        //编队：按槽位对光标做垂直偏置，三弹护送而不重叠
                        float slot = router.MarkData2;
                        Vector2 aim = Main.MouseWorld - owner.Center;
                        Vector2 perp = aim.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                        Vector2 target = Main.MouseWorld + perp * slot * 40f;
                        proj.velocity = (target - proj.Center).SafeNormalize(proj.velocity / speed) * speed * 1.25f;
                    }
                    else {
                        //A 形态：原版每帧重置速度基准，这里恒定放大 25% 不会逐帧累乘
                        proj.velocity *= 1.25f;
                    }
                }
            }
            //星屑尾迹（各端客户端，低频）
            if (!VaultUtils.isServer && proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(
                    proj.Center - proj.velocity * 0.3f, -proj.velocity * 0.05f,
                    TrailViolet, 0.4f)?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (VaultUtils.isServer || KindOf(router) != KindB) {
                return;
            }
            //B 命中：小簇星屑迸溅（攻击方个人反馈，预算 4 粒）
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Main.rand.NextVector2Circular(3f, 3f),
                    TrailViolet, 0.3f)?.Configure(TrailViolet, 12, 0.2f, 1f);
            }
        }
    }
}
