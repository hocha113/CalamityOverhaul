using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 铅短剑重铸「铅蚀」。<br/>
    /// 材质：钝重铅刃，刃口泛病绿毒锈。签名行为：①命中挂中毒 ②对已中毒目标伤害 ×1.25，
    /// 补刀时毒锈绿闪 ③钝重低音的迟滞刺击手感，命中拖出灰绿毒雾
    /// </summary>
    internal class GsLeadShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.LeadShortsword;

        protected override string GsDescFallback =>
            "Reforged: the corroded lead edge poisons whatever it touches;" +
            "\nstriking an already poisoned foe bites 25% deeper into the rot";

        protected override int HeldProjType => ModContent.ProjectileType<GsLeadShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.20f;//弱势开局武器，毒蚀收益要先铺毒再兑现，底伤补两成

    }

    /// <summary>
    /// 铅短剑手持突刺：钝重迟滞的时间线（出 3 刺 4 驻 3 收 6）。
    /// 命中挂原版中毒；对已中毒目标（判定先于本次挂毒）伤害 ×1.25
    /// </summary>
    internal class GsLeadShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.LeadShortsword;

        //铅锈毒色板
        internal static readonly Color LeadPale = new(190, 200, 182);
        internal static readonly Color LeadMain = new(118, 130, 116);
        internal static readonly Color ToxGreen = new(138, 202, 88);
        internal static readonly Color LeadDeep = new(56, 64, 58);

        protected override float WindupFrames => 3f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 6f;
        protected override float PullbackDist => 11f;
        protected override float StabReach => 32f;
        protected override float BladeLength => 44f;
        protected override float ThrustEasePower => 4.5f;//钝头刺出没那么锐利
        protected override int HitstopFrames => 2;
        protected override float LeanAmp => 0.036f;
        protected override float ThrustPitch => -0.18f;//铅的钝重低音

        protected override Color EdgeColor => LeadPale;
        protected override Color CoreColor => ToxGreen;

        /// <summary>本次命中是否吃到了蚀毒增伤（ModifyHit 与 OnHit 同链，供反馈分流）</summary>
        private bool rotBite;

        /// <summary>蚀毒：对已中毒目标增伤（先判后挂，首击铺毒、次击兑现）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            rotBite = target.HasBuff(BuffID.Poisoned);
            if (rotBite) {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //铅锈铺毒：每次命中都续毒
            target.AddBuff(BuffID.Poisoned, 240);

            //蚀毒兑现的升级反馈：病绿闪 + 淤浊低音
            if (rotBite && firstOnTarget && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.45f, Pitch = -0.3f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(Vector2.Lerp(TipPos, target.Center, 0.5f), Vector2.Zero,
                    ToxGreen, 0.24f)?.Configure(12, 0.8f);
            }
        }

        /// <summary>命中反馈：灰绿毒雾（真 alpha 淤浊拉丝）+ 少量钝火花，蚀毒时毒雾更浓</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            //淤浊毒雾：带真 alpha 的暗拉丝往外淌
            int murk = rotBite ? 5 : 3;
            for (int i = 0; i < murk; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.9) * Main.rand.NextFloat(1.2f, 3.2f);
                Color c = Main.rand.NextBool(3) ? ToxGreen : LeadDeep;
                PRTLoader.NewParticle<PRT_SparkAlpha>(pos, vel, c, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(false, Main.rand.Next(14, 22));
            }
            for (int i = 0; i < 3; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.5) * Main.rand.NextFloat(2.5f, 6f);
                Color c = Main.rand.NextBool() ? LeadPale : LeadMain;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.2f, 2.8f), 100, default, Main.rand.NextFloat(0.9f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>驻相刃口渗毒：尖端偶发一缕病绿（表现层，各端自演）</summary>
        protected override void OnTick(int phase) {
            if (VaultUtils.isServer || phase != PhaseDwell) {
                return;
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(TipPos + Main.rand.NextVector2Circular(4f, 4f),
                    stabUnit * Main.rand.NextFloat(0.4f, 1.2f), ToxGreen,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(8, 12), 0.4f, 1.2f);
            }
        }
    }
}
