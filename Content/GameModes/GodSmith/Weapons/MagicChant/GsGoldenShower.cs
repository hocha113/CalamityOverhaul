using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 圣金水柱重铸（A 档）。材质身份：熔金灵液（黏稠灼热的猩红灵液金浆）。<br/>
    /// ①「蚀刻」：命中叠蚀层，五层灵液爆裂出八向飞溅并延长灵液侵蚀；<br/>
    /// ②「高压喷涌」：持续喷洒 90 帧不中断进入高压窗（弹速 +30%、蚀层每命中 +2）；<br/>
    /// ③爆裂点滞留灵液滴挂驻场；④施法有喷压后坐
    /// </summary>
    internal class GsGoldenShower : GsChantScheme
    {
        public override int TargetItemID => ItemID.GoldenShower;

        protected override string GsDescFallback =>
            "Reforged: hits etch molten ichor; the fifth layer bursts into an eight-way spray and a dripping ichor cluster\nSpray without pause to build high pressure: faster bolts, denser streams, double etching";
        protected override float BaseDamageMult => 1.05f;

        /// <summary>持续流变体：节奏由喷洒时长自管，不走标准就绪窗</summary>
        protected override bool UsesStandardBeat => false;

        /// <summary>私有形态：蚀层爆裂的八向灵液飞溅</summary>
        private const float FormSplash = 10f;

        /// <summary>进入高压窗所需连续喷洒帧数</summary>
        private const int PressureChargeTicks = 90;
        /// <summary>高压窗时长</summary>
        private const int PressureWindowTicks = 150;
        /// <summary>蚀层引爆阈值</summary>
        private const int EtchBurstStacks = 5;

        /// <summary>原版灵液弹类型</summary>
        private static int BoltType => ProjectileID.GoldenShowerFriendly;

        /// <summary>
        /// 高压窗是否在期。寄存器语义（绑定本武器，换绑清零）：
        /// CounterA = 连续喷洒帧计数，TimerA = 高压窗关闭时刻，TimerB = 最近一次喷洒帧时刻
        /// </summary>
        private static bool InPressure(GsChantPlayer chant) => Main.GameUpdateCount < chant.TimerA;

        //==================== 动画法：喷压后坐 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //喷压后坐：出手瞬间水平后坐 2px + 枪口微抬，随动画进度回坐（绝对剖面 0.07·p，差分施加防累积漂移；本书动画三喷，中途 snap 由差分清账）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation -= new Vector2(player.direction, 0f) * (2f * progress);
            GsMagicKickMath.ApplyKickDiff(player, 0.07f * progress, 0.07f * ((player.itemAnimation + 1) / n));
        }

        //==================== 高压喷涌：喷洒时长自管节奏 ====================

        protected override void ChantHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsChantPlayer chant = Chant(player);
            uint now = Main.GameUpdateCount;
            bool spraying = player.itemAnimation > 0;

            if (spraying) {
                chant.TimerB = now;
                if (!InPressure(chant)) {
                    chant.CounterA++;
                    if (chant.CounterA >= PressureChargeTicks) {
                        //蓄压完成：开高压窗，喷口一声爆压
                        chant.CounterA = 0;
                        chant.TimerA = now + PressureWindowTicks;
                        SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.8f, Pitch = 0.4f }, player.Center);
                    }
                }
            }
            else if (now - chant.TimerB > 8) {
                //断喷：蓄压清零（高压窗一旦开启不因断喷提前关闭）
                chant.CounterA = 0;
            }
        }

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //把窗态写进拍型（各端按 MarkData 判蚀层加成）；高压窗内弹速 +30%
            bool pressure = InPressure(chant);
            chant.CurrentBeat = pressure ? ChantBeat.OnBeat : ChantBeat.Straight;
            chant.ResonanceAtCast = 0;
            if (pressure) {
                velocity *= 1.3f;
            }
        }

        //==================== 飞行相：飞溅弹泄力 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BoltType) {
                return;
            }
            //飞溅弹：短程泄力
            if (router.MarkData == FormSplash && proj.timeLeft < 14) {
                proj.velocity *= 0.92f;
            }
        }

        //==================== 命中：蚀刻与爆裂 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != BoltType) {
                return;
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            if (router.MarkData == FormSplash) {
                //飞溅弹不再叠蚀，只延长灵液侵蚀
                target.AddBuff(BuffID.Ichor, 360);
                return;
            }
            //蚀刻：高压窗每命中 +2 层，五层爆裂
            GsGoldenShowerNPC etch = target.GetGlobalNPC<GsGoldenShowerNPC>();
            int stacks = etch.AddEtch(IsOnBeatProj(router) ? 2 : 1, 300);
            if (stacks < EtchBurstStacks) {
                return;
            }
            etch.ClearEtch();
            BurstEtch(proj, target);
        }

        /// <summary>五层蚀刻爆裂：八向灵液飞溅 + 灵液侵蚀延长 + 滞留滴挂（owner 端生成，全端可见）</summary>
        private void BurstEtch(Projectile proj, NPC target) {
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.7f, Pitch = 0.15f }, target.Center);
            target.AddBuff(BuffID.Ichor, 600);
            int splashDamage = Math.Max(1, (int)(proj.damage * 0.35f));
            float baseRot = proj.velocity.ToRotation();
            for (int i = 0; i < 8; i++) {
                Vector2 vel = (baseRot + MathHelper.TwoPi * i / 8f).ToRotationVector2() * 7.5f;
                QueueForm(Main.player[proj.owner], FormSplash);
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    BoltType, splashDamage, proj.knockBack * 0.3f, proj.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Projectile splash = Main.projectile[idx];
                    splash.scale *= 0.8f;
                    splash.timeLeft = 24;
                    splash.tileCollide = false;
                    splash.netUpdate = true;
                }
            }
            int dripDamage = Math.Max(1, (int)(proj.damage * 0.2f));
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsGoldenShowerDripProj>(), dripDamage, 0f, proj.owner);
        }
    }

    /// <summary>
    /// 蚀层标记（攻击方本地量：命中钩子只在攻击方端执行，爆裂裁决与可见结果经弹幕过线）
    /// </summary>
    internal class GsGoldenShowerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>蚀层层数（5 层爆裂）</summary>
        internal int EtchStacks;

        /// <summary>蚀层失效时刻</summary>
        internal uint EtchUntil;

        /// <summary>叠层并返回新层数；过期自动清零重计</summary>
        internal int AddEtch(int add, uint durationTicks) {
            if (EtchStacks > 0 && Main.GameUpdateCount >= EtchUntil) {
                EtchStacks = 0;
            }
            EtchStacks += add;
            EtchUntil = Main.GameUpdateCount + durationTicks;
            return EtchStacks;
        }

        internal void ClearEtch() {
            EtchStacks = 0;
            EtchUntil = 0;
        }
    }

    /// <summary>
    /// 灵液滴挂：蚀层爆裂后滞留原地的金浆团，多跳低伤
    /// （判定圆与可见尺寸同源；短寿驻场）
    /// </summary>
    internal class GsGoldenShowerDripProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GoldenShowerFriendly;

        public override string LocalizationCategory => "GodSmithMagicChant";

        private const int LifeTicks = 100;
        private const float Radius = 40f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.timeLeft = LifeTicks;
        }

        /// <summary>凝聚-滴挂-耗尽的半径生命周期</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / 8f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
                return Radius * VaultUtils.EaseOutQuad(grow) * fade;
            }
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Ichor, 120);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        public override bool PreDraw(ref Color lightColor) {
            //原版灵液弹贴图一笔按判定半径缩放（尺寸提示与判定同源）
            float r = RadiusNow;
            if (r < 4f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = r * 2f / Math.Max(tex.Width, tex.Height);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
