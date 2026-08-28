using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 【长矛】剑鱼重铸：破浪突进。<br/>
    /// 材质：银蓝剑鱼吻骨，流线滑水。签名行为：①刺出爆发帧沿刺向鱼跃前冲一小段，
    /// 水中冲距翻倍——人枪一体的突进矛 ②冲刺期身后拖气泡尾流 ③命中水花四溅带湿滑水声，
    /// 身处水中时如鱼得水，伤害 +15%
    /// </summary>
    internal class GsSwordfish : GsSpearScheme
    {
        public override int TargetItemID => ItemID.Swordfish;

        protected override string GsDescFallback =>
            "Reforged: every thrust leaps you forward along the strike, twice as far underwater;" +
            "\nwhile wet the swordfish is in its element and deals 15% more damage";

        protected override int HeldProjType => ModContent.ProjectileType<GsSwordfishHeld>();

        //突进机动是核心收益，水中另有 +15% 条件增伤，底伤中补，综合 DPS 落在原版 108%~120%（水中）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;
    }

    /// <summary>
    /// 剑鱼手持突刺：六把里最快最轻的矛；
    /// 爆发帧 owner 沿刺向前冲（水中翻倍），冲刺期气泡尾流，水中命中 +15%
    /// </summary>
    internal class GsSwordfishHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Swordfish;

        //剑鱼银蓝色板
        internal static readonly Color FishSilver = new(218, 232, 242); //鱼身银
        internal static readonly Color OceanBlue = new(74, 144, 214);   //浅海蓝
        internal static readonly Color WaveDeep = new(26, 62, 104);     //深浪底
        internal static readonly Color FoamWhite = new(240, 250, 252);  //泡沫白

        //六把里最轻快的时间线：破浪讲究一气呵成
        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 8f;
        protected override float RestHoldout => 11f;
        protected override float PullbackDist => 12f;
        protected override float StabReach => 58f;
        protected override float BladeLength => 82f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 26f;
        protected override float ThrustEasePower => 6f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.045f;
        protected override int HitboxSize => 48;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.10f;

        protected override Color EdgeColor => FishSilver;
        protected override Color CoreColor => OceanBlue;

        /// <summary>本次突进是否在水中起跳（爆发帧快照，尾流与音效随它走）</summary>
        private bool leapWet;

        protected override void OnThrustBurst() {
            leapWet = Owner.wet;
            //破浪突进：owner 沿刺向鱼跃前冲，水中翻倍（owner 端权威，位置随原版同步）
            if (Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                Owner.velocity += stabUnit * (leapWet ? 8f : 4f);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = ThrustPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = leapWet ? 0.6f : 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Owner.Center);
            //起跳水花
            int count = leapWet ? 5 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.4f, 0.9f));
                Color c = Main.rand.NextBool(3) ? FoamWhite : OceanBlue;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit.RotatedByRandom(0.4) * Main.rand.NextFloat(3f, 6f), c,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        /// <summary>冲刺期气泡尾流：刺出与驻相期身后拖气泡与水尘</summary>
        protected override void OnTick(int phase) {
            if (VaultUtils.isServer || phase is not PhaseThrust and not PhaseDwell) {
                return;
            }
            //身后尾流：气泡从身后升起（水中更盛）
            Vector2 wakeAt = Owner.Center - stabUnit * Main.rand.NextFloat(10f, 30f)
                + Main.rand.NextVector2Circular(8f, 8f);
            if (Main.rand.NextBool(leapWet ? 1 : 2)) {
                PRTLoader.NewParticle<PRT_Light>(wakeAt,
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.3f)) - stabUnit * 0.4f,
                    Main.rand.NextBool(3) ? FoamWhite : OceanBlue,
                    Main.rand.NextFloat(0.2f, 0.4f))?.Configure(Main.rand.Next(10, 16), 0.55f);
            }
            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(wakeAt, DustID.Water,
                    -stabUnit * Main.rand.NextFloat(0.5f, 1.5f), 80, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = true;
            }
        }

        /// <summary>如鱼得水：水中银鳞泛光 + 命中增伤 15%</summary>
        protected override float ExtraGlowStrength() => Owner.wet ? 0.14f : 0f;

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (Owner.wet) {
                modifiers.FinalDamage *= 1.15f;
            }
        }

        /// <summary>命中反馈：水花四溅 + 湿滑水声，水中命中加倍水量</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.1f, MaxInstances = 3 }, pos);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, FishSilver, 0.17f)?.Configure(9, 0.75f);
            int sparks = Owner.wet ? 8 : 5;
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.6) * Main.rand.NextFloat(3f, 7f);
                Color c = Main.rand.NextBool(3) ? FoamWhite : OceanBlue;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
            int drops = Owner.wet ? 8 : 4;
            for (int i = 0; i < drops; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Water,
                    stabUnit.RotatedByRandom(1.1) * Main.rand.NextFloat(2f, 5.5f), 60, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }
    }
}
