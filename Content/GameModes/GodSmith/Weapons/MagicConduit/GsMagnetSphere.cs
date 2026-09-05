using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 磁球重铸（A 档）。材质身份：磁暴品红。<br/>
    /// ①热量=磁通：持杖与磁球之间架起「馈磁链」，链通时磁通持续上涨、不衰减；<br/>
    /// ②白热「过充」：磁球升格为过充态，额外向近敌泼洒重弧（每 0.4 秒一道）
    /// </summary>
    internal class GsMagnetSphere : GsHeatScheme
    {
        public override int TargetItemID => ItemID.MagnetSphere;

        protected override string GsDescFallback =>
            "Reforged: while you hold the staff a feed-tether links you to your sphere, and magnetic flux climbs as long as the link holds\nAt full flux the sphere overcharges, lashing heavy arcs at anything close";
        internal override float HeatPerShot => 10f;
        internal override float CoolRatePerTick => 1.1f;
        internal override float WhiteHotDamageMult => 1.12f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;

        /// <summary>馈磁链最大距离</summary>
        internal const float TetherRange = 620f;

        /// <summary>原版磁球弹类型</summary>
        internal static int SphereType => ContentSamples.ItemsByType[ItemID.MagnetSphere].shoot;

        //==================== 动画法：举杖 + 磁鸣 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举杖：杖头抬 4px 微颤（绝对剖面 (0.1+颤)·p，差分施加，数学见 GsMagicKickMath）
            float n = player.itemAnimationMax;
            int a = player.itemAnimation;
            float progress = a / n;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -4f) * progress;
            GsMagicKickMath.ApplyKickDiff(player,
                (0.1f + MathF.Sin(a * 1.4f) * 0.02f) * progress,
                (0.1f + MathF.Sin((a + 1) * 1.4f) * 0.02f) * ((a + 1) / n));
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //出手磁鸣：低嗡
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 }, player.Center);
        }

        //==================== 馈磁链：持杖喂磁通 ====================

        internal override void TickHold(Item item, Player player, GsHeatPlayer hp) {
            //owner 端：己方磁球在链距内即持续馈磁（AddHeat 顺带压住被动冷却，链通不衰减）
            Projectile sphere = FindOwnerSphere(player.whoAmI, player.MountedCenter);
            if (sphere == null) {
                return;
            }
            hp.AddHeat(this, 0.35f);

            //过充泼弧：白热且到拍（owner 端裁决，弧弹过线全端可见）
            if (hp.InWhiteHot && Main.GameUpdateCount % 24 == 0) {
                NPC prey = sphere.Center.FindClosestNPC(500f);
                if (prey != null) {
                    int arcDamage = Math.Max(1, (int)(player.GetWeaponDamage(item) * 0.7f));
                    Vector2 dir = (prey.Center - sphere.Center).SafeNormalize(Vector2.UnitX);
                    Projectile.NewProjectile(player.GetSource_Misc("GsMagnetOvercharge"), sphere.Center,
                        dir * 10f, ProjectileID.MagnetSphereBolt, arcDamage, 2f, player.whoAmI);
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, sphere.Center);
                }
            }
        }

        /// <summary>链距内最近的己方磁球（弹幕表各端同步，扫描结果一致）</summary>
        internal static Projectile FindOwnerSphere(int owner, Vector2 from) {
            Projectile best = null;
            float bestDist = TetherRange * TetherRange;
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type != SphereType || p.owner != owner) {
                    continue;
                }
                float d = Vector2.DistanceSquared(p.Center, from);
                if (d < bestDist) {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
        }
    }
}
