using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 星星炮重铸（借调发射器族）。材质：坠星。<br/>
    /// 签名：星星撞到物块不碎，而是「叮」一声弹开一次，弹开后更大、更快。<br/>
    /// 操作奖励：弹开过的星星伤害 +45%。对着地面、天花板或墙打跳弹，让星星拐进敌人，比直射痛得多。<br/>
    /// 预算：直射 ≈ 原版 100%；跳弹命中 ≈ 145%（弹开还快 15%，缩短飞行时间）
    /// </summary>
    internal class GsStarCannon : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.StarCannon;

        protected override string GsDescFallback =>
            "Reforged: stars bounce off blocks once instead of shattering, growing bigger and faster\nBank shots: a star that has bounced deals 45% more damage";
        /// <summary>每颗星最多弹开次数</summary>
        private const int MaxBounces = 1;
        /// <summary>弹开后的伤害倍率</summary>
        private const float BankShotDamage = 1.45f;
        /// <summary>弹开后提速</summary>
        private const float BounceSpeedUp = 1.15f;
        /// <summary>弹开后放大（原版贴图按 scale 默认绘制）</summary>
        private const float BounceScale = 1.3f;

        /// <summary>星星炮当前原版弹类型（1.4.4 为专用星弹，从物品读不硬编码）</summary>
        private static int StarType => ContentSamples.ItemsByType[ItemID.StarCannon].shoot;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 0.8f);
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != StarType) {
                return;
            }
            //MarkData = 已弹开次数（0 起），各端在各自的撞块回调里同步累加
            router.MarkData = 0f;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //撞块开关各端逐帧钉死为开，保证远端也走同一条弹开路径（原版星弹若本就撞块则恒等）
            if (proj.type == StarType) {
                proj.tileCollide = true;
            }
        }

        //==================== 撞块：弹开一次 ====================

        public override bool? GsProjOnTileCollide(Projectile proj, Vector2 oldVelocity, GodSmithProjRouter router) {
            if (proj.type != StarType || router.MarkData >= MaxBounces) {
                return null;
            }
            //被挡住的轴取反，另一轴保留；弹开后更大更快
            if (proj.velocity.X != oldVelocity.X) {
                proj.velocity.X = -oldVelocity.X;
            }
            if (proj.velocity.Y != oldVelocity.Y) {
                proj.velocity.Y = -oldVelocity.Y;
            }
            proj.velocity *= BounceSpeedUp;
            proj.scale = BounceScale;
            router.MarkData += 1f;
            proj.netUpdate = true;
            if (!VaultUtils.isServer) {
                //「叮」：星星的清脆撞击声，比原版碎星音高半截，玩家一听就知道这颗弹开了
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.7f, Pitch = 0.4f, MaxInstances = 4 }, proj.Center);
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(proj.Center, DustID.YellowStarDust,
                        proj.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.9f) * Main.rand.NextFloat(1f, 3f),
                        0, default, 1.2f);
                    d.noGravity = true;
                }
            }
            return false;
        }

        //==================== 命中：跳弹奖励 ====================

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (proj.type == StarType && router.MarkData >= 1f) {
                modifiers.FinalDamage *= BankShotDamage;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != StarType || router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            //跳弹命中：星屑多迸一把 + 高一截的星响
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.7f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.YellowStarDust,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f);
                d.noGravity = true;
            }
        }
    }
}
