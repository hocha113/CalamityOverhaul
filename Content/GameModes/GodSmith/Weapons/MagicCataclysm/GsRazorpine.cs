using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 剃刀松重铸（P13 左键 rider）。材质身份：淬毒松脂与冷杉针（针叶风暴的余种）。<br/>
    /// ①左键 rider：松针命中叠「松脂」，同一敌积 5 层引发「针环收缩」，四支松针
    /// 自四周向心收拢刺入，呼应大招的针环收紧②松针曳松脂微光③满量右键「针叶风暴」照旧
    /// ④施法有前扫响应。针环 4×0.15/5 折算 ≈ +8%，计入包络
    /// </summary>
    internal class GsRazorpine : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.Razorpine;

        protected override string GsDescFallback =>
            "Reforged: hits build Resin; at full charge, right click to raise the Needle Storm\n" +
            "A ring of needles tightens around you, then settles into a needle mat underfoot\n" +
            "Needle hits smear resin; 5 smears on one foe draw a ring of 4 needles closing in on it";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 45;

        protected override float PassiveDamageBonus => 0.08f;

        protected override int DirectorType => ModContent.ProjectileType<GsPineStormDirector>();

        protected override Color AccentColor => new(122, 205, 118);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item66;

        /// <summary>原版松针弹类型</summary>
        private static int NeedleType => ContentSamples.ItemsByType[ItemID.Razorpine].shoot;

        /// <summary>针环出生窗旗标（打标继承窗口写角色，防环针再叠脂）</summary>
        private bool pendingRing;

        //==================== 动画法：松杖前扫 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //快节奏前扫：松杖前压 2px 带正旋（绝对剖面 −0.08·p 下压，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(player.direction * 2f, 0f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, -0.08f * progress, -0.08f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：松脂与针环 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != NeedleType || VaultUtils.isServer) {
                return;
            }
            //松脂微光：环针（MarkData=1）更稀
            int interval = router.MarkData >= 1f ? 8 : 5;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.03f, GsPineStormDirector.PineGreen, 0.07f)?.Configure(9, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != NeedleType) {
                return null;
            }
            //冷杉针流光：细而锐的翠影
            GsCataclysmRiderLib.DrawSpeedGhost(proj,
                router.MarkData >= 1f ? GsPineStormDirector.PineDeep : GsPineStormDirector.PineGreen, 0.32f);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积松脂计量
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != NeedleType) {
                return;
            }
            //命中反馈：松脂珠迸溅
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(7f, 7f),
                        Main.rand.NextVector2Circular(1.2f, 1.2f) + new Vector2(0f, 0.4f),
                        GsPineStormDirector.PineGreen, Main.rand.NextFloat(0.08f, 0.12f))?.Configure(14, 0.85f);
                }
            }
            //环针不再叠脂（防自喂）；本机玩家路径叠松脂
            if (router.MarkData >= 1f || !proj.IsOwnedByLocalPlayer() || !GsCataclysmRiderNPC.CanCarry(target)) {
                return;
            }
            GsCataclysmRiderNPC rider = target.GetGlobalNPC<GsCataclysmRiderNPC>();
            rider.ResinStacks++;
            rider.ResinTimer = 150;
            if (rider.ResinStacks < 5) {
                return;
            }
            rider.ResinStacks = 0;
            SpawnNeedleRing(proj, target);
        }

        /// <summary>针环收缩：四支松针自目标四周 84px 处向心刺入（各 0.15×，承签防再叠）</summary>
        private void SpawnNeedleRing(Projectile proj, NPC target) {
            int damage = Math.Max(1, (int)(proj.damage * 0.15f));
            float baseAngle = proj.identity * 0.61f;
            pendingRing = true;
            for (int i = 0; i < 4; i++) {
                float angle = baseAngle + MathHelper.PiOver2 * i;
                Vector2 from = target.Center + angle.ToRotationVector2() * 84f;
                Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitY) * 9f;
                Projectile.NewProjectile(proj.GetSource_FromThis(), from, vel,
                    NeedleType, damage, proj.knockBack * 0.4f, proj.owner);
            }
            pendingRing = false;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item66 with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //环针：出生窗打角色标（先于生成包），缩体 0.85
            if (pendingRing && proj.owner == Main.myPlayer) {
                router.MarkData = 1f;
                proj.scale *= 0.85f;
                proj.netUpdate = true;
            }
        }
    }
}
