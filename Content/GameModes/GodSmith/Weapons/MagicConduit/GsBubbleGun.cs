using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 泡泡枪重铸：泡压。材质身份：海沫水膜（Duke 血统的碧涛泡流）。<br/>
    /// ①热量=泡压：连喷积压；白热「沸泡」泡弹增大、命中裂两枚小泡；<br/>
    /// ②过载「爆管」：枪管堵塞进锁 90t；③施法有泡压后坐
    /// </summary>
    internal class GsBubbleGun : GsHeatScheme
    {
        public override int TargetItemID => ItemID.BubbleGun;

        protected override string GsDescFallback =>
            "Reforged: nonstop spray builds bubble pressure; at a boil the bubbles swell and burst into twin beads on impact\nCap the gauge and the barrel jams shut in a gush of foam";
        internal override float HeatPerShot => 3f;
        internal override float CoolRatePerTick => 0.7f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Lock;
        internal override int OverloadLockTicks => 90;

        /// <summary>原版泡弹类型</summary>
        private static int BubbleType => ContentSamples.ItemsByType[ItemID.BubbleGun].shoot;

        //==================== 动画法：泡压后坐 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //泡压后坐：出手瞬间水平后坐 2px 带轻抖，随动画进度回坐（绝对剖面 0.05·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation -= new Vector2(player.direction * 2f, MathF.Sin(player.itemAnimation * 1.7f) * 0.8f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.05f * progress, 0.05f * ((player.itemAnimation + 1) / n));
        }

        //==================== 沸泡：白热出生升格 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            base.GsProjOnSpawnMarked(proj, router);
            //白热出生的泡弹：增大（沸泡标随生成包过线，远端同拍渲染）
            if (proj.owner == Main.myPlayer && proj.type == BubbleType && router.MarkData >= 1f) {
                proj.scale *= 1.3f;
                proj.netUpdate = true;
            }
        }

        //==================== 命中：沸泡裂珠 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != BubbleType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：水膜破碎音
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 5 }, target.Center);
            }
            //沸泡裂珠：白热泡命中裂两枚小泡（源 Misc 不承签，防递归裂泡）
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData < 1f) {
                return;
            }
            int beadDamage = Math.Max(1, (int)(proj.damage * 0.3f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = dir.RotatedBy(i == 0 ? 0.6f : -0.6f) * 4.5f;
                int idx = Projectile.NewProjectile(Main.player[proj.owner].GetSource_Misc("GsConduitBoil"),
                    target.Center, vel, BubbleType, beadDamage, proj.knockBack * 0.3f, proj.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].scale *= 0.6f;
                    Main.projectile[idx].timeLeft = Math.Min(Main.projectile[idx].timeLeft, 40);
                    Main.projectile[idx].netUpdate = true;
                }
            }
        }

        //==================== 过载：爆管 ====================

        internal override void OnOverload(Player player, GsHeatPlayer hp) {
            base.OnOverload(player, hp);
            if (VaultUtils.isServer) {
                return;
            }
            //爆管音效
            SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.9f, Pitch = -0.4f }, player.Center);
        }
    }
}
