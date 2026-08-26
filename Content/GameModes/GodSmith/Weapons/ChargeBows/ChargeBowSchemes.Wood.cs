using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.ChargeBows
{
    /// <summary>
    /// 木弓组共享：三级蓄力标准档。T2 质变木灵箭（体量 ×1.25、穿透 +1、木系色拖带），
    /// T3 命中分裂扇形木箭（各 50% 伤）再加逐木个性 rider。公认最弱层，DPS 锚定取高
    /// </summary>
    internal abstract class GsWoodBowScheme : GsChargeBowScheme
    {
        internal override float DpsTarget => 1.06f;

        /// <summary>T3 命中分裂木箭数（红木弓 4）</summary>
        internal virtual int SplitCount => 3;

        internal override void ArrowPostAI(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            //木灵箭体量：各端在 PostAI 统一涨（scale 不进生成包，出生窗口写只有 owner 看得到）
            if (tier >= 2 && kind == KindMain && proj.scale < 1.25f) {
                proj.scale = 1.25f;
            }
        }

        internal sealed override void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) {
            if (tier >= 3) {
                SpawnSplitArrows(proj, target, tier);
            }
            WoodRiderHit(proj, target, hit, damageDone, tier);
        }

        /// <summary>T3 命中分裂：越过目标向前扇形撒出木箭，对原目标短免疫防体内秒结算（owner 端）</summary>
        private void SpawnSplitArrows(Projectile proj, NPC target, int tier) {
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            float speed = Math.Max(7f, proj.velocity.Length() * 0.85f);
            int dmg = Math.Max(1, (int)(proj.damage * 0.5f));
            int count = SplitCount;
            for (int i = 0; i < count; i++) {
                float lerp = count <= 1 ? 0.5f : i / (float)(count - 1);
                float rot = MathHelper.ToRadians(MathHelper.Lerp(-20f, 20f, lerp)) + Main.rand.NextFloat(-0.03f, 0.03f);
                Vector2 vel = dir.RotatedBy(rot) * speed;
                Vector2 pos = target.Center + dir * (target.width * 0.5f + 8f);
                StampNext(tier, KindSplit);
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), pos, vel,
                    ProjectileID.WoodenArrowFriendly, dmg, proj.knockBack * 0.5f, proj.owner);
                Projectile split = Main.projectile[idx];
                split.usesLocalNPCImmunity = true;
                split.localNPCHitCooldown = 8;
                split.localNPCImmunity[target.whoAmI] = 30;
            }
        }

        /// <summary>逐木个性 rider：T2+ 主箭命中时（攻击方端）</summary>
        internal virtual void WoodRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, int tier) { }
    }

    /// <summary>
    /// 木弓（基准弓，收编自基建范例）：无个性 rider，纯三级蓄力标准档。
    /// 保留范例全部视觉签名：琥珀火星拖尾、过满灼芯白光重影、命中余烬迸溅、箭亡处回落火星
    /// </summary>
    internal class GsWoodenBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.WoodenBow;
        protected override string GsDescFallback =>
            "Reforged: hold to draw, release to loose. A half draw hits harder, a full draw transmutes the arrow, an overdrawn shot splits on impact; hold too long and the string destabilizes";
        internal override Color TrailMain => new(255, 188, 96);
        internal override Color TrailHot => new(255, 236, 190);
        internal override Color TrailDeep => new(148, 92, 44);
    }

    /// <summary>北极木弓：T3 命中挂寒滞（非 boss 顿挫 + 霜火），霜晶闪点</summary>
    internal class GsBorealWoodBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.BorealWoodBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. An overdrawn arrow chills its mark, briefly numbing lesser foes with frost";
        internal override Color TrailMain => new(150, 214, 255);
        internal override Color TrailHot => new(224, 246, 255);
        internal override Color TrailDeep => new(70, 110, 160);

        internal override void WoodRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, int tier) {
            if (tier < 3 || !ValidRiderTarget(target)) {
                return;
            }
            target.AddBuff(BuffID.Frostburn, 60);
            if (!target.boss) {
                target.velocity *= 0.90f;
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_DefFrostGlint>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        Main.rand.NextVector2Circular(1f, 1f), TrailHot, Main.rand.NextFloat(0.8f, 1.3f))
                        ?.Configure(Main.rand.Next(16, 26));
                }
            }
        }
    }

    /// <summary>棕榈木弓：T3 箭干燥利落，追加 3% 暴击补正，沙金闪点</summary>
    internal class GsPalmWoodBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.PalmWoodBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Overdrawn arrows fly dry and true, striking critically more often";
        internal override Color TrailMain => new(255, 214, 120);
        internal override Color TrailHot => new(255, 240, 190);
        internal override Color TrailDeep => new(170, 120, 50);

        internal override void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) {
            //暴击补正：命中结算在攻击方端，掷点安全
            if (tier >= 3 && kind == KindMain && Main.rand.NextFloat() < 0.03f) {
                modifiers.SetCrit();
            }
        }

        internal override void WoodRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, int tier) {
            if (tier < 3 || VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Sparkle>(target.Center, Main.rand.NextVector2Circular(2f, 2f),
                TrailHot, 0.5f)?.Configure(TrailMain, Main.rand.Next(14, 20), 0.05f, 0.8f);
        }
    }

    /// <summary>红木弓：藤蔓多茎，T3 分裂箭多 1 支</summary>
    internal class GsRichMahoganyBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.RichMahoganyBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Overdrawn arrows split into one extra vine-fletched shaft on impact";
        internal override int SplitCount => 4;
        internal override Color TrailMain => new(170, 220, 110);
        internal override Color TrailHot => new(230, 255, 180);
        internal override Color TrailDeep => new(80, 120, 50);
    }

    /// <summary>乌木弓：T3 命中 25% 挂暗影焰（与恶魔弓区分：无追踪）</summary>
    internal class GsEbonwoodBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.EbonwoodBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Overdrawn arrows have a chance to ignite shadowflame on impact";
        internal override Color TrailMain => new(150, 90, 200);
        internal override Color TrailHot => new(220, 170, 255);
        internal override Color TrailDeep => new(60, 34, 90);

        internal override void WoodRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, int tier) {
            if (tier >= 3 && ValidRiderTarget(target) && Main.rand.NextFloat() < 0.25f) {
                target.AddBuff(BuffID.ShadowFlame, 90);
            }
        }
    }

    /// <summary>暗影木弓：T3 命中 25% 挂放血（族内失血减益）</summary>
    internal class GsShadewoodBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.ShadewoodBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Overdrawn arrows have a chance to open a bleeding wound";
        internal override Color TrailMain => new(220, 70, 80);
        internal override Color TrailHot => new(255, 150, 150);
        internal override Color TrailDeep => new(110, 24, 34);

        internal override void WoodRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, int tier) {
            if (tier >= 3 && ValidRiderTarget(target) && Main.rand.NextFloat() < 0.25f) {
                target.AddBuff(ModContent.BuffType<GsChargeBleedBuff>(), 180);
            }
        }
    }

    /// <summary>灰烬木弓：T3 命中挂着火，余烬燃屑</summary>
    internal class GsAshWoodBow : GsWoodBowScheme
    {
        public override int TargetItemID => ItemID.AshWoodBow;
        protected override string GsDescFallback =>
            "Reforged: three-stage draw. Overdrawn arrows set their mark on fire, scattering cinders";
        internal override Color TrailMain => new(255, 120, 60);
        internal override Color TrailHot => new(255, 200, 120);
        internal override Color TrailDeep => new(120, 50, 30);

        internal override void WoodRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, int tier) {
            if (tier < 3 || !ValidRiderTarget(target)) {
                return;
            }
            target.AddBuff(BuffID.OnFire, 120);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_DefEmber>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 2.5f)),
                        TrailMain, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(20, 32));
                }
            }
        }
    }
}
