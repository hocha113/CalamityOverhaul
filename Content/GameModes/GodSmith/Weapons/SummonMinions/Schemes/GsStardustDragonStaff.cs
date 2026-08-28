using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 星尘之龙法杖「星穹撕裂」（A 档）：龙是链体蠕虫，不入阵型系统，
    /// 增强层绝不碰任何节体速度（只加自绘星辉与头节尾迹，链体交给原版跟随 AI）；
    /// 签名 = 突击令下龙首 60 帧内两次凿中焦点目标，在凿点撕开星穹裂隙
    /// （<see cref="GsStardustDragonRiftProj"/>，每段 0.6× 共约 3 段星涌，冷却 150 帧）；
    /// 判定只挂头节（625），节体命中不计数
    /// </summary>
    internal class GsStardustDragonStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.StardustDragonStaff;

        public override string GsFamily => "SummonMinionsB";

        protected override string GsDescFallback =>
            "Skyrift Piercer: under the assault order, two drills of the dragon's head into the marked foe tear open a star rift at the wound that gushes falling stardust";

        private static readonly Color StarCyan = new(110, 220, 255);
        private static readonly Color StarPale = new(224, 248, 255);

        /// <summary>蠕虫链体不入阵型：任何速度牵引都会扯散节间跟随</summary>
        protected override GsMinionKit Kit => null;

        protected override int[] MinionProjTypes
            => [ProjectileID.StardustDragon1, ProjectileID.StardustDragon2,
                ProjectileID.StardustDragon3, ProjectileID.StardustDragon4];

        /// <summary>凿击计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint riftReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;

        //==================== 唤令动画 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GsMinionCastMotion.ApplyRaise(player);

        public override void GsUseAnimation(Item item, Player player)
            => GsMinionCastMotion.CastBurst(player, StarCyan, StarPale);

        //==================== 增强层：头节星尾 + 全节星辉（不碰链体运动） ====================

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            //只有头节高速冲刺时甩星尾粒子，节体零干预
            if (VaultUtils.isServer || proj.type != ProjectileID.StardustDragon1
                || proj.velocity.Length() < 7f || proj.timeLeft % 2 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                -proj.velocity * 0.03f, StarCyan, 0.13f)?.Configure(11, 0.75f);
        }

        /// <summary>
        /// 全节追加星辉涂层：速度越快辉光越拉长（原版贴图之上的加色残影，
        /// 绘制输入只有位置/速度/identity，禁随机）
        /// </summary>
        public override void GsProjPostDraw(Projectile proj, Color lightColor,
            GodSmithProjRouter router) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float speed = proj.velocity.Length();
            float stretch = 1f + MathHelper.Clamp(speed * 0.06f, 0f, 0.9f);
            float breathe = 0.8f + 0.2f * (float)Math.Sin(
                Main.GlobalTimeWrappedHourly * 4f + proj.identity * 0.7f);
            Vector2 pos = proj.Center - Main.screenPosition;
            float rot = proj.velocity.ToRotation();
            Main.EntitySpriteDraw(glow, pos, null,
                (StarCyan with { A = 0 }) * (0.3f * breathe), rot, glow.Size() / 2f,
                new Vector2(0.5f * stretch, 0.34f), SpriteEffects.None, 0);
            //头节独享星冠闪
            if (proj.type == ProjectileID.StardustDragon1) {
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null) {
                    Main.EntitySpriteDraw(star, pos + proj.velocity.SafeNormalize(Vector2.Zero) * 14f,
                        null, (StarPale with { A = 0 }) * (0.55f * breathe),
                        proj.identity * 0.61f + Main.GlobalTimeWrappedHourly * 2f,
                        star.Size() / 2f, 0.2f + 0.05f * stretch, SpriteEffects.None, 0);
                }
            }
        }

        //==================== 签名：星穹撕裂（只认头节） ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.StardustDragon1
                || !MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                || focus.whoAmI != target.whoAmI) {
                return;
            }
            int count = tally.Bump(target, proj, 60, out _);
            if (count >= 2 && Main.GameUpdateCount >= riftReadyTick) {
                riftReadyTick = Main.GameUpdateCount + 150;
                tally.Reset(target);
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsStardustDragonRiftProj>(),
                    (int)(proj.damage * 0.6f), 3f, proj.owner);
            }
        }
    }
}
