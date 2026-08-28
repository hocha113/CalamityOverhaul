using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 磁球重铸（A 档）。材质身份：磁暴品红（Magnet 色板已预留）。<br/>
    /// ①热量=磁通：持杖与磁球之间架起「馈磁链」，链通时磁通持续上涨、不衰减；<br/>
    /// ②白热「过充」：磁球升格为过充态，额外向近敌泼洒重弧（每 0.4 秒一道）；<br/>
    /// ③泄压「磁暴」：以磁球为心炸开向心磁暴环（拉拽非 Boss，威力随磁通）；<br/>
    /// ④A 档四相：出手磁鸣环/馈磁链弧光/磁弧命中溅弧/磁暴余韵
    /// </summary>
    internal class GsMagnetSphere : GsHeatScheme
    {
        public override int TargetItemID => ItemID.MagnetSphere;

        protected override string GsDescFallback =>
            "Reforged: while you hold the staff a feed-tether links you to your sphere, and magnetic flux climbs as long as the link holds" +
            "\nAt full flux the sphere overcharges, lashing heavy arcs at anything close" +
            "\nRight click to detonate a magnet storm around the sphere that drags lesser foes toward its heart";

        internal override float HeatPerShot => 10f;
        internal override float CoolRatePerTick => 1.1f;
        internal override float WhiteHotDamageMult => 1.12f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;
        internal override float VentMinHeat => 35f;
        internal override Color MuzzleTheme => GsConduitVFX.MagnetMain;

        /// <summary>馈磁链最大距离</summary>
        internal const float TetherRange = 620f;

        /// <summary>原版磁球弹类型</summary>
        internal static int SphereType => ContentSamples.ItemsByType[ItemID.MagnetSphere].shoot;

        //==================== 动画法：举杖 + 磁鸣环 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举杖：杖头抬 4px 微颤（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -4f) * progress;
            player.itemRotation -= player.direction * (0.1f + MathF.Sin(player.itemAnimation * 1.4f) * 0.02f) * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //出手磁鸣：低嗡 + 品红磁环
            SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = -0.5f, MaxInstances = 3 }, player.Center);
            PRTLoader.NewParticle<PRT_ProcRing>(player.MountedCenter + GsAimUnit(player) * 24f,
                Vector2.Zero, GsConduitVFX.MagnetMain, 1f)?.Configure(30f, 5f, 10);
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

        //==================== 磁球与磁弧的可见层 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            if (proj.type == SphereType) {
                Lighting.AddLight(proj.Center, GsConduitVFX.MagnetMain.ToVector3() * 0.4f);
                //馈磁链弧尘：持杖者与磁球间链上电尘（HeldItem 已同步，各端同判同演）
                Player owner = Main.player[proj.owner];
                if (owner.active && !owner.dead && owner.HeldItem.type == TargetItemID
                    && Vector2.DistanceSquared(owner.MountedCenter, proj.Center) < TetherRange * TetherRange
                    && proj.timeLeft % 4 == 0) {
                    float at = Main.rand.NextFloat();
                    Vector2 spot = Vector2.Lerp(owner.MountedCenter, proj.Center, at);
                    PRTLoader.NewParticle<PRT_Spark>(spot + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(0.8f, 0.8f), GsConduitVFX.MagnetBright,
                        Main.rand.NextFloat(0.14f, 0.24f))?.Configure(false, Main.rand.Next(6, 12));
                }
                return;
            }
            if (proj.type == ProjectileID.MagnetSphereBolt && proj.timeLeft % 3 == 0) {
                //磁弧尾迹：品红电尘
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.4f,
                    Main.rand.NextVector2Circular(0.5f, 0.5f), GsConduitVFX.MagnetMain,
                    Main.rand.NextFloat(0.14f, 0.24f))?.Configure(false, Main.rand.Next(6, 10));
            }
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != SphereType) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = proj.Center - Main.screenPosition;
            float t = Main.GlobalTimeWrappedHourly;
            float seed = proj.identity * 0.61f;
            float pulse = 0.82f + 0.18f * MathF.Sin(t * 6f + seed);

            //馈磁链：三层品红线束（HeldItem 已同步，各端同判同绘）
            Player owner = Main.player[proj.owner];
            if (owner.active && !owner.dead && owner.HeldItem.type == TargetItemID) {
                Vector2 hand = owner.MountedCenter;
                float dist = Vector2.Distance(hand, proj.Center);
                if (dist < TetherRange && dist > 24f) {
                    float sway = MathF.Sin(t * 5f + seed) * 0.04f;
                    GsConduitVFX.DrawBeam(Main.spriteBatch, hand,
                        (proj.Center - hand).ToRotation() + sway, dist,
                        5f * pulse, GsConduitVFX.MagnetMain, GsConduitVFX.MagnetBright, 0.55f);
                }
            }
            //磁球底辉：反向差速旋的双层磁光
            Main.EntitySpriteDraw(glow, pos, null,
                GsConduitVFX.MagnetDeep with { A = 0 } * (0.6f * pulse), 0f, glow.Size() / 2f, 0.5f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null,
                GsConduitVFX.MagnetMain with { A = 0 } * 0.5f, t * 4f + seed, star.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null,
                GsConduitVFX.MagnetBright with { A = 0 } * 0.4f, -t * 6.5f + seed, star.Size() / 2f, 0.2f, SpriteEffects.None, 0);
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.MagnetSphereBolt || VaultUtils.isServer) {
                return;
            }
            //磁弧命中：溅弧
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f),
                    i % 2 == 0 ? GsConduitVFX.MagnetMain : GsConduitVFX.MagnetBright,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //==================== 泄压：磁暴 ====================

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //以最近己方磁球为心（无球则以自身为心）炸开向心磁暴环：品红预设 + 拉拽
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * (0.8f + 1.6f * frac)));
            Projectile sphere = FindOwnerSphere(player.whoAmI, player.MountedCenter);
            Vector2 heart = sphere?.Center ?? player.MountedCenter;
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), heart, Vector2.Zero,
                ModContent.ProjectileType<GsConduitNovaProj>(), damage, 7f, player.whoAmI,
                (120f + 90f * frac) + 1 * 1024f, 1f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.9f, Pitch = -0.2f }, heart);
            }
        }
    }
}
