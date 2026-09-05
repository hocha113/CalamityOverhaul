using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
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
        protected override int VolleyCount => 7;
        protected override float ChargePerShot => 100f / 7f;
        protected override int MarksPerVolleyHit => 1;
        protected override int PursuitEvery => 4;
        protected override float PursuitDamageMul => 0.35f;

        //==================== 本弓角色 ====================

        /// <summary>浪矢（MarkData2：0~9 普通浪列相位；100+ 巨浪列，带穿透）</summary>
        internal const int RoleWave = GsVolleyRole.CustomBase;

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

        //==================== 命中与处决 ====================

        protected override bool IsMarkingHit(Projectile proj, int role) => role == RoleWave;

        /// <summary>浪矢命中：水花声（与原版箭簇命中区分）</summary>
        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool()) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
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

        /// <summary>出手水花声（各端可见的出手相）</summary>
        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 muzzle = player.MountedCenter + new Vector2(player.direction * 20f, -2f);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = 0.35f }, muzzle);
        }
    }

    /// <summary>
    /// 海啸巨浪浪冠：随巨浪列推进的横扫水墙。速度衰减（浪愈行愈缓），
    /// 绘制只留原版水弹贴图按判定框拉伸、竖立于行进方向的一笔
    /// </summary>
    internal class GsTsunamiCrestProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WaterBolt;

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
        }

        /// <summary>范围提示：原版水弹贴图按判定框拉伸一笔，浪面竖立于行进方向，尾 12 帧渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            Vector2 scale = new(Projectile.width / (float)tex.Width, Projectile.height / (float)tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                Projectile.rotation + MathHelper.PiOver2, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
