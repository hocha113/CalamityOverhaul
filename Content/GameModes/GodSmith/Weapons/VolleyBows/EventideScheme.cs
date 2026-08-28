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
    /// 黄昏 / Eventide（重铸 ~108%）：光之女皇的暮光月弧弓。
    /// 身份宣言：①五连扫弦，暮光之枪沿弧线依次离弦、向准星收束，弓身随扫弦划弧
    /// ②第三枪必为女王之枪，重挑一记③处决唤出双灵彩珠绕敌回旋啄击（复用族内 GsFaeOrbiterProj）。
    /// 原版「仅木箭化枪、中拍双倍」重制为全弹种化枪 + 确定性扫弦编舞 + 棱光扇齐射。
    /// 期望：普通 4×0.95+1.75=5.55/6≈92.5%；棱光扇每 20 发 ≈+4%；双灵驻场（0.18 each、上限 4）≈+13%
    /// </summary>
    internal class GsFairyQueenRangedItem : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.FairyQueenRangedItem;

        protected override string GsDescFallback =>
            "Reforged: all arrows become twilight lances; each sweep of the bow looses 5 lances along an arc, converging on the cursor\nThe 3rd lance of every sweep is the Queen's lance, striking nearly twice as hard\nShots build twilight; at full charge the next shot blooms into a prismatic fan of 5 lances\nLance hits stack brands; branding a foe thrice calls twin fae orbs to circle and peck it for 2 seconds";

        //==================== 家族参数（棱光扇直接吃基类编队） ====================

        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Cone;
        protected override float SpreadPx => 34f;
        protected override float SideArrowMul => 0.7f;
        protected override float VolleyVelMul => 2f;
        protected override float ChargePerShot => 5f;
        protected override int MarksPerVolleyHit => 1;
        protected override int PursuitEvery => 0;
        protected override Color TrailColor => new(238, 160, 232);

        protected override int VolleyProjType(int ammoProjType) => ProjectileID.FairyQueenRangedItemShot;

        //==================== 本弓角色 ====================

        /// <summary>扫弦暮光枪（MarkData2 = 弧线槽位 0~4）</summary>
        internal const int RoleLance = GsVolleyRole.CustomBase;
        /// <summary>女王之枪（每轮第三发，1.75 倍）</summary>
        internal const int RoleQueen = GsVolleyRole.CustomBase + 1;

        /// <summary>槽位 → 暮光谱段色（确定性，绘制与粒子共用）</summary>
        internal static Color LanceColor(float hue) => Main.hslToRgb(hue % 1f, 0.9f, 0.66f);

        //==================== 射击流：五连扫弦 ====================

        /// <summary>
        /// 扫弦：一次使用动画开 5 枪（原版 useLimitPerAnimation），发射原点沿 ±36° 弧线
        /// 逐发推移，枪头向准星收束；第三发升格女王之枪。镜像原版扫弦式，槽序确定性
        /// </summary>
        protected override bool? OnNormalShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int idx = Math.Clamp((player.itemAnimationMax - player.itemAnimation) / 2, 0, 4);
            bool queen = idx == 2;
            int slot = player.direction == 1 ? 4 - idx : idx;

            Vector2 fwd = velocity.SafeNormalize(Vector2.UnitX) * 40f;
            Vector2 origin = position + fwd.RotatedBy(MathHelper.Pi / 10f * (slot - 2));
            if (!Collision.CanHit(position, 0, 0, position + fwd, 0, 0)) {
                origin = position;
            }
            //向准星收束；准星贴身时退化为跟随弹道方向（镜像原版近距离保护）
            Vector2 mouse = Main.MouseWorld;
            Vector2 aim = (mouse - origin).SafeNormalize(velocity.SafeNormalize(-Vector2.UnitY));
            float nearLerp = Utils.GetLerpValue(100f, 40f, mouse.Distance(player.Center), clamped: true);
            if (nearLerp > 0f) {
                aim = Vector2.Lerp(aim, velocity.SafeNormalize(-Vector2.UnitY), nearLerp)
                    .SafeNormalize(-Vector2.UnitY);
            }

            float speed = velocity.Length() * 2f;
            int dmg = queen ? (int)(damage * 1.75f) : (int)(damage * 0.95f);
            SpawnTagged(player, source, origin, aim * speed, ProjectileID.FairyQueenRangedItemShot,
                dmg, queen ? knockback * 1.4f : knockback,
                queen ? RoleQueen : RoleLance, slot);

            if (!VaultUtils.isServer && queen) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.35f }, origin);
                PRTLoader.NewParticle<PRT_Light>(origin, aim * 2f, Color.White, 0.14f)?.Configure(8, 0.85f);
            }
            return false;
        }

        /// <summary>出生窗写入谱段：ai[1] 是暮光枪的原版彩相，按槽位确定性铺成谱带</summary>
        protected override void OnSpawnMarkedHook(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == RoleQueen) {
                proj.ai[1] = 0.87f;
                proj.scale = 1.18f;
            }
            else if (role == RoleLance) {
                proj.ai[1] = (0.58f + router.MarkData2 * 0.09f) % 1f;
            }
            else if (role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) {
                //棱光扇：五枪均分整条彩带
                proj.ai[1] = (0.5f + router.MarkData2 * 0.19f) % 1f;
            }
        }

        //==================== 飞行与自绘 ====================

        private class LanceState
        {
            public int T;
        }

        /// <summary>出膛 12 帧缓加速（1.02/帧），收掉匀速平推的贴纸感；各端同式推演</summary>
        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.None) {
                return;
            }
            LanceState st = router.GetOrCreateState<LanceState>();
            st.T++;
            if (st.T <= 12) {
                proj.velocity *= 1.02f;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Color hueColor = LanceColor(proj.ai[1]);
            Lighting.AddLight(proj.Center, hueColor.ToVector3() * (role == RoleQueen ? 0.4f : 0.24f));
            if (st.T % 5 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.4f,
                    Main.rand.NextVector2Circular(0.4f, 0.4f), hueColor, role == RoleQueen ? 0.62f : 0.45f)
                    ?.Configure(LanceColor(proj.ai[1] + 0.25f), 14);
            }
        }

        /// <summary>棱光残迹：按谱段色画三重后曳残影，女王枪另加白热芯（identity 定相）</summary>
        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.None) {
                return null;
            }
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = Terraria.GameContent.TextureAssets.Projectile[proj.type].Value;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + proj.identity * 0.61f);
            for (int i = 1; i <= 3; i++) {
                Color ghost = LanceColor(proj.ai[1] + i * 0.09f) with { A = 0 };
                Main.EntitySpriteDraw(tex, proj.Center - proj.velocity * (0.5f * i) - Main.screenPosition,
                    null, ghost * (0.36f * pulse / i), proj.rotation, tex.Size() * 0.5f,
                    proj.scale, SpriteEffects.None, 0);
            }
            if (role == RoleQueen) {
                Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null,
                    (Color.White with { A = 0 }) * (0.35f * pulse), proj.rotation,
                    tex.Size() * 0.5f, proj.scale * 1.12f, SpriteEffects.None, 0);
            }
            return null;
        }

        //==================== 命中与处决 ====================

        protected override bool IsMarkingHit(Projectile proj, int role)
            => role is RoleLance or RoleQueen || role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide;

        /// <summary>命中棱光迸散：谱段色星屑，女王枪升格为星辉爆点</summary>
        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            int role = (int)router.MarkData;
            int count = role == RoleQueen ? 6 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.2f),
                    LanceColor(proj.ai[1] + i * 0.13f), Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(LanceColor(proj.ai[1] + 0.5f), Main.rand.Next(12, 20));
            }
            if (role == RoleQueen) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, Color.White, 0.16f)?.Configure(9, 0.8f);
            }
        }

        /// <summary>处决「双灵环绕」：两枚妖灵光珠对置公转 2 秒啄击（0.18 each，全场上限 4 珠）</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GsFaeOrbiterProj>()] > 2) {
                return;
            }
            int dmg = (int)(proj.damage * 0.18f);
            for (int i = 0; i < 2; i++) {
                Projectile.NewProjectile(player.GetSource_Misc("GsEventideFae"), target.Center,
                    Vector2.Zero, ModContent.ProjectileType<GsFaeOrbiterProj>(), dmg, 1f,
                    player.whoAmI, target.whoAmI, i * MathF.PI);
            }
        }

        //==================== 动画：扫弦划弧 ====================

        /// <summary>
        /// 扫弦编舞：弓身沿垂直于弹道的弧线自下而上划过（对应五枪弧线原点），
        /// 前 10 帧每 2 帧一记扫弦顿挫，其后回弧收势（仅位移，确定性输入）
        /// </summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            int elapsed = player.itemAnimationMax - player.itemAnimation;
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            Vector2 arcUp = aimDir.RotatedBy(MathHelper.PiOver2);
            if (arcUp.Y > 0f) {
                arcUp = -arcUp;
            }
            //扫弧：0~14 帧从弧底扫到弧顶，其后带过冲回中
            float sweepP = MathHelper.Clamp(elapsed / 14f, 0f, 1f);
            float sweep = MathHelper.SmoothStep(-1f, 1f, sweepP);
            float settle = elapsed > 14 ? MathF.Sin(MathHelper.Clamp((elapsed - 14) / 16f, 0f, 1f) * MathF.PI) * -0.3f : 0f;
            player.itemLocation += arcUp * (5f * (sweep + settle));
            //扫弦顿挫：每一枪出膛的短促回拉
            if (elapsed <= 10) {
                float judder = elapsed % 2 == 0 ? 1.8f : 0.8f;
                player.itemLocation -= aimDir * judder;
            }
        }

        /// <summary>每一枪起手的棱光辉点（各端可见的出手相）</summary>
        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 muzzle = player.MountedCenter + new Vector2(player.direction * 18f, -4f);
            PRTLoader.NewParticle<PRT_Sparkle>(muzzle, new Vector2(player.direction * 1.2f, -0.4f),
                LanceColor(Main.GlobalTimeWrappedHourly * 0.4f), 0.5f)
                ?.Configure(Color.White, 12);
        }

        /// <summary>暮光蓄满待机：弓弧上流转谱段星尘（本机个人读数）</summary>
        public override void GsHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GsVolleyPlayer>().Charge < 100f || !Main.rand.NextBool(6)) {
                return;
            }
            Vector2 at = player.MountedCenter + new Vector2(player.direction * Main.rand.NextFloat(8f, 20f),
                Main.rand.NextFloat(-12f, 6f));
            PRTLoader.NewParticle<PRT_Sparkle>(at, new Vector2(0f, -0.5f),
                LanceColor(Main.GlobalTimeWrappedHourly * 0.6f), 0.4f)?.Configure(Color.White, 14);
        }
    }
}
