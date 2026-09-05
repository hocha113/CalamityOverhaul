using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 霰弹枪重铸：铅弹质感。<br/>
    /// [宽喉]：6 粒 ±12 度宽扇，弹粒带铅坠弧线；单次射击命中 3 次即在目标上空
    /// 炸开「铅幕」8 粒坠落铅屑。一次 use 只耗 1 发弹药，粒数由接管生成自控
    /// </summary>
    internal class GsShotgun : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Shotgun;

        public override string GsFamily => "Guns";

        protected override string GsDescFallback =>
            "Reforged: six drooping pellets in a broad fan; landing 3 pellet hits in one blast bursts a rain of lead over the target\nEvery trigger pull still costs one shell";
        /// <summary>本次 use 的命中记账（owner 攻击链独占）：总命中数 / 铅幕闩</summary>
        private int useHitCount;
        private bool leadRainFired;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeWideBore", EnName = "Wide Bore",
                DamageMul = 0.73f,
            },
        ];

        //猎枪后坐：全族最重的一挫
        protected override float RecoilShift => 6f;
        protected override float RecoilKick => 0.08f;

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //新 use 开账
            useHitCount = 0;
            leadRainFired = false;
            //接管粒数：6 粒宽扇（damage 已按档摊薄）
            float halfSpread = MathHelper.ToRadians(12f);
            for (int i = 0; i < 6; i++) {
                Vector2 pelletVel = velocity.RotatedBy(Main.rand.NextFloat(-halfSpread, halfSpread))
                    * Main.rand.NextFloat(0.94f, 1.06f);
                Projectile.NewProjectile(source, position, pelletVel, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type == ModContent.ProjectileType<GsShotgunLeadRainProj>()) {
                return;
            }
            //铅坠：恒定微重力（确定性输入，各端同弧）
            proj.velocity.Y += 0.045f;
        }

        /// <summary>攻击方端：弹粒命中记账，触发铅幕</summary>
        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type == ModContent.ProjectileType<GsShotgunLeadRainProj>()
                || target.friendly) {
                return;
            }
            useHitCount++;
            //铅幕：单次射击命中满 3 粒，目标上空炸开 8 粒坠落铅屑
            if (!leadRainFired && useHitCount >= 3) {
                leadRainFired = true;
                for (int i = 0; i < 8; i++) {
                    Vector2 pos = target.Center + new Vector2(Main.rand.NextFloat(-70f, 70f),
                        -Main.rand.NextFloat(90f, 150f));
                    Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(4f, 7f));
                    Projectile.NewProjectile(proj.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<GsShotgunLeadRainProj>(),
                        Math.Max(1, (int)(proj.damage * 0.22f)), 0.5f, proj.owner);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.4f, Pitch = -0.2f }, target.Center);
                }
            }
        }

        internal override void GsGunHeldReset(Player player) {
            useHitCount = 0;
            leadRainFired = false;
        }
    }

    /// <summary>
    /// 霰弹枪「铅幕」坠落铅屑：宽喉齐射的第二波打击，重力直坠
    /// </summary>
    internal class GsShotgunLeadRainProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        public override string LocalizationCategory => "GodSmithGuns";

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.28f, 13f);
            //原版子弹贴图朝上，转向补 PiOver2
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
