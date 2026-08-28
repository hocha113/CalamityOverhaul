using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 月耀重铸（P13 抬档 B→A，终局件）。材质身份：月蚀冷辉（蚀盘边缘淌下的月焰）。<br/>
    /// ①左键 rider：「第四落」，每第 4 次施放自天穹补落一道 0.5× 月焰（复刻原版天降参数），
    /// 通体苍白，呼应大招的十二连焰瀑；月焰皆曳月弧青影，坠点残留月尘余晖（余痕相）
    /// ②咏唱层级可见：计量每过四分之一，书页月辉升一档密度③施法有举书响应与翻页月尘
    /// ④满量右键「月蚀审判」照旧。第四落 0.5×/(4×3) ≈ +4%，底伤加成保持 5%
    /// </summary>
    internal class GsLunarFlare : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.LunarFlareBook;

        protected override string GsDescFallback =>
            "Reforged: hits build Moonphase; at full charge, right click to raise the Lunar Eclipse\n" +
            "An eclipse disk pours twelve woven flare falls, every third one backed by a phantom moonbeam\n" +
            "Every 4th cast pours a pale fourth flare from the sky\n" +
            "Moonlight gathers on the book as the gauge fills";

        public override int ChargePerHit => 2;

        public override int CataclysmManaCost => 60;

        protected override float PassiveDamageBonus => 0.05f;

        protected override int DirectorType => ModContent.ProjectileType<GsLunarEclipseDirector>();

        protected override Color AccentColor => new(150, 220, 235);

        protected override SoundStyle TriggerSound => SoundID.Item4;

        /// <summary>原版月焰弹类型</summary>
        private static int FlareType => ContentSamples.ItemsByType[ItemID.LunarFlareBook].shoot;

        /// <summary>施放计数与第四落旗标（GsShoot 只在 owner 端执行，本机契约）</summary>
        private int castCounter;

        private bool pendingPale;

        protected override void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) {
            //审判区落在光标处，蚀盘由 director 悬于其上空
            anchor.Y -= 30f;
        }

        //==================== 动画法：举书 + 翻页月尘 + 咏唱层级读数 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举书诵月：书身抬升 4px 微后仰再缓落（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 2f, -4f) * progress;
            player.itemRotation -= player.direction * 0.11f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //翻页月尘：书页间洒出两粒冷辉
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 14f, -10f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(tip + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(player.direction * 0.2f, -Main.rand.NextFloat(0.5f, 1f)),
                    GsLunarEclipseDirector.MoonPale, Main.rand.NextFloat(0.22f, 0.34f))
                    ?.Configure(GsLunarEclipseDirector.MoonCyan, 18, 0.04f, 0.8f);
            }
            Lighting.AddLight(tip, GsLunarEclipseDirector.MoonCyan.ToVector3() * 0.3f);
        }

        /// <summary>咏唱层级读数：计量每过四分之一，书页月辉升一档密度（满档交给基类金辉）</summary>
        protected override void GsCataclysmHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            GsCataclysmPlayer state = player.GetModPlayer<GsCataclysmPlayer>();
            if (state.BoundItemType != TargetItemID || state.Charge >= ChargeMax) {
                return;
            }
            int tier = Math.Min(3, state.Charge * 4 / ChargeMax);
            if (tier <= 0 || Main.GameUpdateCount % (12 - tier * 3) != 0) {
                return;
            }
            Vector2 tip = player.itemLocation + new Vector2(player.direction * 10f, -8f)
                + Main.rand.NextVector2Circular(6f, 6f);
            PRTLoader.NewParticle<PRT_Light>(tip, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                tier >= 2 ? GsLunarEclipseDirector.MoonPale : GsLunarEclipseDirector.MoonCyan,
                0.06f + tier * 0.02f)?.Configure(14, 0.8f);
        }

        //==================== 左键 rider：第四落 + 月弧青影 ====================

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //每第 4 次施放补落一道苍白月焰：复刻原版天降参数（天窗高 600px、ai[1]=准星深度）
            castCounter++;
            if (castCounter % 4 != 0) {
                return null;
            }
            Vector2 aim = Main.MouseWorld;
            Vector2 sky = new((aim.X + player.Center.X) * 0.5f + Main.rand.Next(-200, 201),
                player.MountedCenter.Y - 600f);
            Vector2 delta = aim - sky;
            delta.Y = MathF.Max(MathF.Abs(delta.Y), 20f);
            Vector2 vel = delta.SafeNormalize(Vector2.UnitY) * velocity.Length() * 0.5f;
            pendingPale = true;
            Projectile.NewProjectile(source, sky, vel, type,
                Math.Max(1, damage / 2), knockback, player.whoAmI, 0f, aim.Y);
            pendingPale = false;
            //原版三落照常放行
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //苍白第四落：出生窗打角色标（先于生成包，远端同见）
            if (pendingPale && proj.type == FlareType) {
                router.MarkData = 1f;
            }
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != FlareType || VaultUtils.isServer) {
                return;
            }
            bool pale = router.MarkData >= 1f;
            Lighting.AddLight(proj.Center, GsLunarEclipseDirector.MoonCyan.ToVector3() * (pale ? 0.34f : 0.22f));
            //月尘缀行（月焰 extraUpdates=5，用低频防刷屏）
            if (proj.timeLeft % 18 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.03f,
                    pale ? GsLunarEclipseDirector.MoonPale : GsLunarEclipseDirector.MoonCyan,
                    Main.rand.NextFloat(0.22f, 0.34f))?.Configure(GsLunarEclipseDirector.MoonCyan, 18, 0.04f, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != FlareType) {
                return null;
            }
            //月弧青影：第四落用苍白重影
            GsCataclysmRiderLib.DrawSpeedGhost(proj,
                router.MarkData >= 1f ? GsLunarEclipseDirector.MoonPale : GsLunarEclipseDirector.MoonCyan,
                router.MarkData >= 1f ? 0.5f : 0.36f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积月相
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != FlareType || VaultUtils.isServer) {
                return;
            }
            //命中反馈：月屑迸散
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    (-proj.velocity.SafeNormalize(Vector2.UnitY)).RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3.5f),
                    Main.rand.NextBool() ? GsLunarEclipseDirector.MoonPale : GsLunarEclipseDirector.MoonCyan,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != FlareType || VaultUtils.isServer) {
                return;
            }
            //余痕相：坠点残留月尘余晖，飘起后缓落，活得比焰体久
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(proj.Center + Main.rand.NextVector2Circular(8f, 6f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.6f, 1.4f)),
                    Color.Lerp(GsLunarEclipseDirector.MoonCyan, GsLunarEclipseDirector.MoonPale, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.26f, 0.4f))?.Configure(false, 30);
            }
        }
    }
}
