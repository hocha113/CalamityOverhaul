using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 海啸（重铸 ~108%）：深渊骨脊制的潮弓。
    /// 身份宣言：①五矢成浪列，箭列作正弦浪涌推进、破浪后坠②满充巨浪：七矢双列加穿透，
    /// 浪冠白沫横扫开路③拍浪处决，潮柱自标记敌足下拔起（复用族内 GsTideSpoutProj）。
    /// 原版「五矢弧列同速平推」重编舞为相位浪涌，接管飞行相 46 帧后交还原版箭坠。
    /// 期望：普通 5×0.92=92%；巨浪每 7 发（7×1.0+浪冠≈8.4）→ 周期 ≈103%；追潮 +2%，拍浪 ≈+4%
    /// </summary>
    internal class GsTsunami : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.Tsunami;

        protected override string GsDescFallback =>
            "Reforged: each draw looses a rank of 5 arrows that surge forward in a rolling wave, one ammo per draw\nShots build tide charge; at full charge the next draw becomes a great wave: 7 piercing arrows in two ranks led by a sweeping foam crest\nWave arrows stack tide brands; branding a foe thrice sends a water spout erupting beneath it\nWhile a branded foe stands, every 4th arrow splits off a tide-chaser that bites toward it";

        //==================== 家族参数 ====================

        protected override int VolleyCount => 7;
        protected override float ChargePerShot => 100f / 7f;
        protected override int MarksPerVolleyHit => 1;
        protected override int PursuitEvery => 4;
        protected override float PursuitDamageMul => 0.35f;
        protected override Color TrailColor => TideMain;

        //==================== 本弓角色与色板 ====================

        /// <summary>浪矢（MarkData2：0~9 普通浪列相位；100+ 巨浪列，带穿透）</summary>
        internal const int RoleWave = GsVolleyRole.CustomBase;

        internal static readonly Color TideDeep = new(18, 78, 132);
        internal static readonly Color TideMain = new(58, 172, 224);
        internal static readonly Color TideBright = new(198, 244, 255);

        //==================== 射击流 ====================

        /// <summary>放一列浪矢：沿弹道纵向错位成箭列，相位随索引铺开成浪</summary>
        private void FireWaveRank(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback, int count, bool great) {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < count; i++) {
                //巨浪双列：奇数矢侧移半波，两列交错成厚浪
                float side = great && i % 2 == 1 ? 14f : 0f;
                Vector2 pos = position - dir * (i * 24f) + perp * (side + MathF.Sin(i * 1.257f) * 6f);
                SpawnTagged(player, source, pos, velocity, type, damage, knockback,
                    RoleWave, great ? 100 + i : i);
            }
        }

        /// <summary>普通射击：原版五矢弧列改为五矢浪列（0.92 each，原版链已扣 1 发弹药）</summary>
        protected override bool? OnNormalShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            FireWaveRank(player, source, position, velocity, type, (int)(damage * 0.92f), knockback, 5, false);
            return false;
        }

        /// <summary>巨浪：七矢双列全伤加穿透，浪冠白沫在前横扫开路</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            count = Math.Clamp(count, 3, VolleyCount);
            FireWaveRank(player, source, position, velocity, type, damage, knockback, count, true);
            //浪冠：半速半伤的横扫体，替浪列开路（伤害计入齐射预算）
            Projectile.NewProjectile(player.GetSource_Misc("GsTsunamiCrest"), position,
                velocity * 0.82f, ModContent.ProjectileType<GsTsunamiCrestProj>(),
                (int)(damage * 0.5f * count / 7f), knockback, player.whoAmI);
        }

        /// <summary>巨浪列穿透 +1（>0 守卫，防 -1 无限穿被写坏）</summary>
        protected override void OnSpawnMarkedHook(Projectile proj, GodSmithProjRouter router) {
            if ((int)router.MarkData == RoleWave && router.MarkData2 >= 100f && proj.penetrate > 0) {
                proj.penetrate++;
            }
        }

        //==================== 浪涌飞行（接管 46 帧后交还原版） ====================

        private class WaveState
        {
            public int T;
            public Vector2 BaseVel;
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if ((int)router.MarkData != RoleWave) {
                return true;
            }
            WaveState st = router.GetOrCreateState<WaveState>();
            if (st.T == 0) {
                st.BaseVel = proj.velocity;
            }
            st.T++;
            //破浪期结束：交还原版箭 AI，浪尽而坠
            if (st.T > 46) {
                return true;
            }
            //正弦浪涌：基速 + 横向摆，相位由 MarkData2 过线，各端同式推演
            float phase = router.MarkData2 % 100f;
            Vector2 perp = st.BaseVel.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            proj.velocity = st.BaseVel + perp * (MathF.Sin(st.T * 0.21f + phase * 1.257f) * 2.3f);
            proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver2;
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if ((int)router.MarkData != RoleWave || VaultUtils.isServer) {
                return;
            }
            bool great = router.MarkData2 >= 100f;
            Lighting.AddLight(proj.Center, TideMain.ToVector3() * (great ? 0.3f : 0.18f));
            if (proj.timeLeft % (great ? 3 : 5) == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.6f,
                    -proj.velocity * 0.05f, TideMain, great ? 0.11f : 0.08f)?.Configure(10, 0.75f);
            }
            if (proj.timeLeft % 7 == 0) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(proj.Center - proj.velocity * 0.4f,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.4f)),
                    TideBright, great ? 0.45f : 0.32f)?.Configure(20);
            }
        }

        /// <summary>浪矢自绘：青潮速度重影 + 箭头白沫星光（identity 定相，无随机）</summary>
        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if ((int)router.MarkData != RoleWave) {
                return null;
            }
            bool great = router.MarkData2 >= 100f;
            DrawSpeedGhost(proj, TideMain, great ? 0.5f : 0.4f);
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (star != null) {
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + proj.identity * 0.77f);
                Vector2 tip = proj.Center + proj.velocity.SafeNormalize(Vector2.UnitX) * 8f;
                Main.EntitySpriteDraw(star, tip - Main.screenPosition, null,
                    (TideBright with { A = 0 }) * (0.4f * pulse), proj.rotation,
                    star.Size() * 0.5f, (great ? 0.075f : 0.05f) * pulse, SpriteEffects.None, 0);
            }
            return null;
        }

        //==================== 命中与处决 ====================

        protected override bool IsMarkingHit(Projectile proj, int role) => role == RoleWave;

        /// <summary>浪矢命中：水花与浮沫（与原版箭簇命中明显区分）</summary>
        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool()) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.2f)),
                    TideBright, 0.4f)?.Configure(22);
            }
        }

        /// <summary>处决「拍浪」：潮柱自标记敌足下拔起（Center 定在敌脚上方 55px，族资产复用）</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            Vector2 at = new(target.Center.X, target.Bottom.Y - 55f);
            Projectile.NewProjectile(player.GetSource_Misc("GsTsunamiSpout"), at, Vector2.Zero,
                ModContent.ProjectileType<GsTideSpoutProj>(), (int)(proj.damage * 1.3f), 4f, player.whoAmI);
        }

        //==================== 动画：浪涌后坐 ====================

        /// <summary>浪涌后坐：先猛拉后回送、带一次前越，如弓身随涌浪起伏（仅位移，确定性输入）</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float back = 4.4f * MathF.Exp(-5f * elapsed);
            float surge = 1.2f * MathF.Sin(MathHelper.Clamp((elapsed - 0.3f) / 0.5f, 0f, 1f) * MathF.PI);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (back - surge);
            player.itemLocation.Y += 0.9f * MathF.Sin(elapsed * MathF.PI) * player.gravDir;
        }

        /// <summary>出手水花：口部浪环 + 浮沫（各端可见的出手相）</summary>
        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 muzzle = player.MountedCenter + new Vector2(player.direction * 20f, -2f);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = 0.35f }, muzzle);
            PRTLoader.NewParticle<PRT_DWave>(muzzle, new Vector2(player.direction * 1.2f, 0f), TideMain, 0.1f)
                ?.Configure(new Vector2(1f, 0.62f), 0f, 0.42f, 12);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(muzzle + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(player.direction * Main.rand.NextFloat(0.5f, 1.5f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    TideBright, 0.4f)?.Configure(18);
            }
        }

        /// <summary>潮势满时持弓待机：弓身滴落浮沫（本机个人读数）</summary>
        public override void GsHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GsVolleyPlayer>().Charge < 100f || !Main.rand.NextBool(7)) {
                return;
            }
            Vector2 at = player.MountedCenter + new Vector2(player.direction * Main.rand.NextFloat(8f, 20f),
                Main.rand.NextFloat(-10f, 4f));
            PRTLoader.NewParticle<PRT_CampfireBubble>(at, new Vector2(0f, -0.8f), TideBright, 0.32f)?.Configure(20);
        }
    }

    /// <summary>
    /// 海啸巨浪浪冠：随巨浪列推进的横扫水墙。速度衰减（浪愈行愈缓），
    /// 浪面 Extra_98（真 alpha）三层叠出深水鞘、主浪与亮芯，顶端卷沫收口、
    /// 前缘亮棱收口，不做两端平切贴条；绘制 identity 定相，无随机
    /// </summary>
    internal class GsTsunamiCrestProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 58;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            if (Life == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.9f, Pitch = -0.1f }, Projectile.Center);
            }
            Life++;
            //浪愈行愈缓，收尾加速消力（非匀速）
            Projectile.velocity *= Projectile.timeLeft < 12 ? 0.9f : 0.988f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!VaultUtils.isServer && Life % 2 == 0) {
                Vector2 perp = TopPerp();
                float h = Projectile.height * 0.5f;
                PRTLoader.NewParticle<PRT_CampfireBubble>(
                    Projectile.Center + perp * Main.rand.NextFloat(-h * 0.4f, h),
                    perp * Main.rand.NextFloat(0.5f, 1.5f) + Projectile.velocity * 0.15f,
                    GsTsunami.TideBright, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(20);
            }
            Lighting.AddLight(Projectile.Center, GsTsunami.TideMain.ToVector3() * 0.4f);
        }

        /// <summary>浪顶方向：垂直于行进方向、偏世界上方的一侧</summary>
        private Vector2 TopPerp() {
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            return perp.Y > 0f ? -perp : perp;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float grow = MathHelper.Clamp(Life / 5f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float alpha = grow * fade;
            float breathe = 1f + 0.07f * MathF.Sin(Life * 0.4f + Projectile.identity * 0.9f);

            Vector2 origin = tex.Size() * 0.5f;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 top = TopPerp();
            float faceRot = Projectile.rotation + MathHelper.PiOver2;

            //深水鞘（拖后）→ 主浪 → 亮芯（加色），浪面竖立于行进方向
            Main.EntitySpriteDraw(tex, center - dir * 9f, null, GsTsunami.TideDeep * (0.8f * alpha), faceRot,
                origin, new Vector2(0.52f, 1.32f * breathe), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, center, null, GsTsunami.TideMain * (0.88f * alpha), faceRot,
                origin, new Vector2(0.38f, 1.06f * breathe), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, center + dir * 3f, null,
                (GsTsunami.TideBright with { A = 0 }) * (0.5f * alpha), faceRot,
                origin, new Vector2(0.18f, 0.8f), SpriteEffects.None, 0);

            //顶端卷沫帽：浪头向前倾折，收掉上端平切
            Vector2 capAt = center + top * (Projectile.height * 0.42f * breathe) + dir * 7f;
            Main.EntitySpriteDraw(tex, capAt, null, GsTsunami.TideBright * (0.75f * alpha),
                faceRot + 0.5f * MathF.Sign(dir.X == 0f ? 1f : dir.X), origin,
                new Vector2(0.5f, 0.24f), SpriteEffects.None, 0);

            //前缘亮棱：行进面一条窄亮片，收掉前端平切
            Main.EntitySpriteDraw(tex, center + dir * 12f, null,
                (Color.White with { A = 0 }) * (0.28f * alpha), faceRot,
                origin, new Vector2(0.07f, 0.9f * breathe), SpriteEffects.None, 0);

            //底缘拖沫：下端一层矮浪脚，收掉下端平切
            Main.EntitySpriteDraw(tex, center - top * (Projectile.height * 0.4f), null,
                GsTsunami.TideMain * (0.55f * alpha), faceRot, origin,
                new Vector2(0.6f, 0.2f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(Projectile.Center + Main.rand.NextVector2Circular(20f, 30f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.8f, 2f)),
                    GsTsunami.TideBright, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(24);
            }
        }
    }
}
