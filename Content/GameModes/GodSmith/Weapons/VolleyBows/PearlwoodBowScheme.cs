using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 珍珠木弓（公认垫底位，整体重铸 135%；P13 返工 C+→B）：珍珠虹漆的入门弓。
    /// 身份宣言：①齐射成「虹桥」：五矢自左至右错帧展开成扇，逐矢换相虹色
    /// ②同一敌在拱内连吃 3 矢，绽珍珠棱光爆③满充弓身淌虹光、出手带轻后坐。
    /// 入门位不设标记与处决。期望：+4×0.5/6 ≈ +33%，棱光爆小额附加
    /// </summary>
    internal class GsPearlwoodBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.PearlwoodBow;

        protected override string GsDescFallback =>
            "Reforged: every 5 shots charge a rainbow arc volley, 5 arrows sweeping out one by one, one ammo per volley\nLanding 3 arc arrows on the same foe pops a pearl prism burst\nRight click releases the volley early at 60%+ charge";

        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 20f;
        protected override float SideArrowMul => 0.5f;
        protected override int MarksPerVolleyHit => 0;
        protected override int PursuitEvery => 0;
        protected override Color TrailColor => RainbowNow(0f);

        //==================== 棱光爆窗口（owner 端命中钩子消费，本机契约） ====================

        /// <summary>窗口内被拱矢命中的目标</summary>
        private int prismTarget = -1;

        /// <summary>窗口内命中计数</summary>
        private int prismCount;

        /// <summary>窗口起始帧（30 帧内算同一轮拱）</summary>
        private uint prismTick;

        /// <summary>珍珠虹相位色（确定性时间输入，绘制路径零随机）</summary>
        internal static Color RainbowNow(float shift) {
            float hue = (Main.GlobalTimeWrappedHourly * 0.35f + shift) % 1f;
            return Main.hslToRgb(hue, 0.75f, 0.72f);
        }

        //==================== 虹桥齐射：五矢错帧成扇 ====================

        /// <summary>虹桥：五矢按 ±10° 扇角自左至右错帧离弦，读作虹拱依次展开</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            GsVolleyPlayer vp = player.GetModPlayer<GsVolleyPlayer>();
            int mainIndex = FormationLib.MainIndex(count);
            for (int i = 0; i < count; i++) {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float rotOff = MathHelper.ToRadians(MathHelper.Lerp(-10f, 10f, t));
                bool isMain = i == mainIndex;
                vp.Enqueue(new GsPendingShot {
                    Delay = 1 + i * 2,
                    WeaponType = item.type,
                    ProjType = VolleyProjType(type),
                    Velocity = velocity.RotatedBy(rotOff),
                    Damage = isMain ? damage : (int)(damage * SideArrowMul),
                    Knockback = isMain ? knockback : knockback * 0.7f,
                    Role = isMain ? GsVolleyRole.VolleyMain : GsVolleyRole.VolleySide,
                    Param = i,
                });
            }
        }

        //==================== 动画法：轻后坐 + 满充淌虹 ====================

        /// <summary>入门弓的轻后坐：1.8px 随动画回弹（确定性输入，各端一致）</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (1.8f * progress);
        }

        /// <summary>满充持弓：弓身淌珍珠虹光（本机个人读数）</summary>
        public override void GsHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GsVolleyPlayer>().Charge < 100f || !Main.rand.NextBool(6)) {
                return;
            }
            Vector2 at = player.MountedCenter
                + new Vector2(player.direction * Main.rand.NextFloat(8f, 18f), -Main.rand.NextFloat(2f, 10f));
            PRTLoader.NewParticle<PRT_Sparkle>(at, new Vector2(0f, -0.5f),
                RainbowNow(Main.rand.NextFloat()), 0.3f)?.Configure(RainbowNow(0.3f), 16, 0.05f, 0.8f);
        }

        //==================== 飞行与命中 ====================

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.VolleyMain || role == GsVolleyRole.VolleySide) {
                //珍珠虹重影：相位随编队索引错开
                DrawSpeedGhost(proj, RainbowNow(router.MarkData2 * 0.13f + proj.identity * 0.021f), 0.4f);
                return null;
            }
            return base.GsProjPreDraw(proj, ref lightColor, router);
        }

        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            //同一轮拱内（30 帧）对同一敌计数，第 3 矢绽珍珠棱光爆
            if (prismTarget != target.whoAmI || Main.GameUpdateCount - prismTick > 30) {
                prismTarget = target.whoAmI;
                prismCount = 0;
                prismTick = Main.GameUpdateCount;
            }
            prismCount++;
            if (prismCount < 3) {
                return;
            }
            prismCount = 0;
            prismTarget = -1;
            Player owner = Main.player[proj.owner];
            SpawnBurst(owner, target.Center, (int)(proj.damage * 0.3f), 60f, Projectiles.GsVolleyBurstProj.ThemePearl);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.5f, Pitch = 0.4f }, target.Center);
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.6f),
                        RainbowNow(i * 0.25f), Main.rand.NextFloat(0.32f, 0.5f))
                        ?.Configure(RainbowNow(i * 0.25f + 0.1f), Main.rand.Next(16, 26), 0.06f, 0.9f);
                }
            }
        }
    }
}
