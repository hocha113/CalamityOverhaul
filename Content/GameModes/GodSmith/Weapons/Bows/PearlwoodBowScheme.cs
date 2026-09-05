using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    /// <summary>
    /// 珍珠木弓（公认垫底位，整体重铸 135%；P13 返工 C+→B）：珍珠虹漆的入门弓。
    /// 身份宣言：①齐射成「虹桥」：五矢自左至右错帧展开成扇
    /// ②同一敌在拱内连吃 3 矢，绽珍珠棱光爆③出手带轻后坐。
    /// 入门位不设标记与处决。期望：+4×0.5/6 ≈ +33%，棱光爆小额附加
    /// </summary>
    internal class GsPearlwoodBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.PearlwoodBow;

        protected override string GsDescFallback =>
            "Reforged: every 5 shots charge a rainbow arc volley, 5 arrows sweeping out one by one, one ammo per volley\nLanding 3 arc arrows on the same foe pops a pearl prism burst";
        protected override int VolleyCount => 5;
        protected override GsVolleyFormation Formation => GsVolleyFormation.Line;
        protected override float SpreadPx => 14f;
        protected override float ChargePerShot => 20f;
        protected override float SideArrowMul => 0.5f;
        protected override int MarksPerVolleyHit => 0;
        protected override int PursuitEvery => 0;

        //==================== 棱光爆窗口（owner 端命中钩子消费，本机契约） ====================

        /// <summary>窗口内被拱矢命中的目标</summary>
        private int prismTarget = -1;

        /// <summary>窗口内命中计数</summary>
        private int prismCount;

        /// <summary>窗口起始帧（30 帧内算同一轮拱）</summary>
        private uint prismTick;

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

        //==================== 动画法：轻后坐 ====================

        /// <summary>入门弓的轻后坐：1.8px 随动画回弹（确定性输入，各端一致）</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (1.8f * progress);
        }

        //==================== 命中 ====================

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
            }
        }
    }
}
