using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 圣金水柱重铸（A 档）。材质身份：熔金灵液（黏稠灼热的猩红灵液金浆）。<br/>
    /// ①「蚀刻」：命中叠蚀层，五层灵液爆裂出八向飞溅并延长灵液侵蚀；<br/>
    /// ②「高压喷涌」：持续喷洒 90 帧不中断进入高压窗（弹速 +30%、金浆拉丝增密、蚀层每命中 +2）；<br/>
    /// ③爆裂点滞留灵液滴挂驻场；④施法有喷压后坐与出手金雾
    /// </summary>
    internal class GsGoldenShower : GsChantScheme
    {
        public override int TargetItemID => ItemID.GoldenShower;

        protected override string GsDescFallback =>
            "Reforged: hits etch molten ichor; the fifth layer bursts into an eight-way spray and a dripping ichor cluster" +
            "\nSpray without pause to build high pressure: faster bolts, denser streams, double etching";

        protected override float BaseDamageMult => 1.05f;

        /// <summary>持续流变体：节奏由喷洒时长自管，不走标准就绪窗</summary>
        protected override bool UsesStandardBeat => false;

        protected override Color ChantColor => IchorGold;

        internal static readonly Color IchorBright = new(255, 236, 150);
        internal static readonly Color IchorGold = new(255, 202, 64);
        internal static readonly Color IchorDeep = new(150, 96, 18);

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

        //==================== 动画法：喷压后坐 + 出手金雾 ====================

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

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //出手金雾：喷口一蓬灵液雾金（各端可见的起手光效）
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 18f, -4f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(tip + Main.rand.NextVector2Circular(4f, 4f),
                    new Vector2(player.direction * Main.rand.NextFloat(0.8f, 1.8f), -Main.rand.NextFloat(0.2f, 0.9f)),
                    IchorGold, Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 12));
            }
            PRTLoader.NewParticle<PRT_Light>(tip, Vector2.Zero, IchorBright, 0.1f)?.Configure(8, 0.7f);
            Lighting.AddLight(tip, IchorGold.ToVector3() * 0.3f);
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
                        PRTLoader.NewParticle<PRT_ProcRing>(player.MountedCenter + GsAimUnit(player) * 26f,
                            Vector2.Zero, IchorGold, 1f)?.Configure(20f, 6f, 12);
                    }
                }
            }
            else if (now - chant.TimerB > 8) {
                //断喷：蓄压清零（高压窗一旦开启不因断喷提前关闭）
                chant.CounterA = 0;
            }

            //高压窗内的个人读数：喷口金辉呼吸
            if (VaultUtils.isServer || !InPressure(chant)) {
                return;
            }
            if (now % 6 == 0) {
                Vector2 tip = player.MountedCenter + GsAimUnit(player) * 26f;
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(4f, 4f),
                    -Vector2.UnitY * 0.4f, IchorBright, 0.09f)?.Configure(10, 0.7f);
            }
        }

        protected override void ChantModifyShootStats(Item item, Player player, GsChantPlayer chant,
            ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            //把窗态写进拍型（各端按 MarkData 加密拉丝）；高压窗内弹速 +30%
            bool pressure = InPressure(chant);
            chant.CurrentBeat = pressure ? ChantBeat.OnBeat : ChantBeat.Straight;
            chant.ResonanceAtCast = 0;
            if (pressure) {
                velocity *= 1.3f;
            }
        }

        //==================== 飞行相：灵液拉丝 + 滴落细屑 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BoltType || VaultUtils.isServer) {
                return;
            }
            if (router.MarkData == FormSplash) {
                //飞溅弹：短程泄力，金珠迸散
                if (proj.timeLeft < 14) {
                    proj.velocity *= 0.92f;
                }
                if (proj.timeLeft % 3 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center, -proj.velocity * 0.1f,
                        IchorBright, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(6, 10));
                }
                Lighting.AddLight(proj.Center, IchorGold.ToVector3() * 0.16f);
                return;
            }
            //灵液拉丝：沿速度方向拖出黏稠金丝，高压窗弹更密
            int interval = IsOnBeatProj(router) ? 2 : 4;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center - proj.velocity * 0.5f,
                    proj.velocity * 0.12f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    IchorGold, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(10, 16));
            }
            //滴落细屑：黏稠液流偶尔坠下一滴金珠
            if (proj.timeLeft % 9 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(3f, 3f),
                    new Vector2(proj.velocity.X * 0.05f, Main.rand.NextFloat(0.6f, 1.4f)),
                    IchorBright, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(14, 24), 0.6f);
            }
            Lighting.AddLight(proj.Center, IchorGold.ToVector3() * 0.22f);
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //余痕相：金珠溅散坠落，活得比弹体久
            if (VaultUtils.isServer || proj.type != BoltType) {
                return;
            }
            int count = router.MarkData == FormSplash ? 2 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.4f, 1.2f)),
                    IchorGold, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(Main.rand.Next(16, 26), 0.6f);
            }
        }

        //==================== 命中：蚀刻与爆裂 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != BoltType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：金浆迸溅（高压窗更盛）
                int burst = IsOnBeatProj(router) ? 5 : 3;
                Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < burst; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        (-dir).RotatedByRandom(1.0) * Main.rand.NextFloat(2f, 4.5f),
                        i % 2 == 0 ? IchorGold : IchorBright,
                        Main.rand.NextFloat(0.22f, 0.38f))?.Configure(true, Main.rand.Next(10, 16));
                }
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

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //蚀层体表可见：金浆越积越亮，滴落越密（层数只在攻击方端存在，个人读数合法）
            if (EtchStacks <= 0 || Main.GameUpdateCount >= EtchUntil || Main.dedServ) {
                return;
            }
            drawColor = Color.Lerp(drawColor, GsGoldenShower.IchorGold,
                MathHelper.Clamp(EtchStacks * 0.05f, 0f, 0.25f));
            if (Main.rand.NextBool(Math.Max(2, 10 - EtchStacks * 2))) {
                PRTLoader.NewParticle<PRT_Light>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    new Vector2(0f, Main.rand.NextFloat(0.6f, 1.3f)),
                    GsGoldenShower.IchorBright, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(Main.rand.Next(12, 20), 0.65f);
            }
        }
    }

    /// <summary>
    /// 灵液滴挂：蚀层爆裂后滞留原地的金浆团，缓慢滴落、多跳低伤
    /// （判定圆与可见浆团同源；短寿驻场演出）
    /// </summary>
    internal class GsGoldenShowerDripProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
            if (VaultUtils.isServer) {
                return;
            }
            //滴挂：金珠不断从浆团坠下
            if (Projectile.timeLeft % 5 == 0) {
                float r = RadiusNow;
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-r, r) * 0.7f, Main.rand.NextFloat(-6f, 6f)),
                    new Vector2(0f, Main.rand.NextFloat(0.8f, 1.8f)),
                    Main.rand.NextBool() ? GsGoldenShower.IchorGold : GsGoldenShower.IchorBright,
                    Main.rand.NextFloat(0.06f, 0.11f))?.Configure(Main.rand.Next(16, 26), 0.7f);
            }
            Lighting.AddLight(Projectile.Center, GsGoldenShower.IchorGold.ToVector3() * 0.3f * (RadiusNow / Radius));
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
            //三层黏稠浆团（A=0 加色批），identity 定相的垂坠呼吸
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float r = RadiusNow;
            if (r < 4f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            for (int i = 0; i < 3; i++) {
                float phase = Main.GlobalTimeWrappedHourly * 3.4f + Projectile.identity * 0.61f + i * 2.2f;
                //垂坠形变：浆团下沉重、上收轻，像挂着的黏液
                Vector2 off = new(MathF.Sin(phase) * r * 0.22f, MathF.Abs(MathF.Cos(phase * 0.6f)) * 5f);
                float s = r / glow.Width * (2.0f - i * 0.45f);
                Color c = (i == 2 ? Color.White : i == 1 ? GsGoldenShower.IchorGold : GsGoldenShower.IchorDeep) with { A = 0 };
                Main.EntitySpriteDraw(glow, basePos + off, null, c * (0.32f + i * 0.1f), 0f,
                    glow.Size() / 2f, new Vector2(s, s * 1.25f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
