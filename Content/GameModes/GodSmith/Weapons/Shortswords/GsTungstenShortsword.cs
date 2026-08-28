using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 钨短剑重铸「钨芯蓄穿」。<br/>
    /// 材质：高密钨芯，剑身沉得压手。签名行为：①按住短蓄（约 26 帧即满），钨屑向刃身收拢
    /// ②满蓄刺出距离 ×1.5、刺线加宽、破甲提升，一条直线全穿 ③与黑暗长枪蓄力的区分：无弹幕、纯物理穿透、蓄得更快
    /// </summary>
    internal class GsTungstenShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.TungstenShortsword;

        protected override string GsDescFallback =>
            "Reforged: a dense tungsten core rewards a brief hold before the thrust;" +
            "\na full charge lunges half again as far, punching through armor and everything in line";

        protected override int HeldProjType => ModContent.ProjectileType<GsTungstenShortswordHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//蓄穿收益走机制端（距离/破甲/贯穿），底伤只小补
    }

    /// <summary>
    /// 钨短剑手持突刺：可选短蓄（26 帧满）。满蓄 reach ×1.5、刺线加宽、
    /// 破甲 +16×蓄力；penetrate 本就 -1，一线全穿无需额外弹幕
    /// </summary>
    internal class GsTungstenShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.TungstenShortsword;

        //钨芯色板（灰绿冷金属）
        internal static readonly Color TungstenBright = new(216, 226, 216);
        internal static readonly Color TungstenMain = new(136, 150, 140);
        internal static readonly Color CoreGreen = new(146, 214, 172);

        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 7f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 36f;
        protected override float BladeLength => 45f;
        /// <summary>蓄力加宽刺线：满蓄一条更粗的直线全穿</summary>
        protected override float CollisionWidth => 26f + ChargeT * 12f;
        protected override float TipGreedRadius => 24f + ChargeT * 8f;
        protected override float ThrustEasePower => 6f + ChargeT * 2f;
        protected override int HitboxSize => 44;
        protected override int HitstopFrames => ChargeT >= 0.8f ? 3 : 2;
        protected override float LeanAmp => 0.040f;
        protected override float ThrustPitch => -0.22f;//钨的沉重低音

        protected override Color EdgeColor => TungstenBright;
        protected override Color CoreColor => CoreGreen;

        /// <summary>短蓄即满：与黑暗长枪（32 帧、放弹幕）区分——更快、纯物理</summary>
        protected override float MaxChargeFrames => 26f;

        private bool FullyCharged => ChargeT >= 0.8f;
        private bool chargeCuePlayed;

        /// <summary>蓄力期：钨屑向刃身收拢（吸入向），满蓄一记金属定音</summary>
        protected override void OnChargingTick() {
            if (!chargeCuePlayed && ChargeT >= 1f) {
                chargeCuePlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.4f }, Owner.Center);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextFloat() < 0.30f + ChargeT * 0.35f) {
                Vector2 bladeAt = Hand + stabUnit * Main.rand.NextFloat(6f, holdout + BladeLength * 0.9f);
                Vector2 from = bladeAt + Main.rand.NextVector2Unit() * Main.rand.NextFloat(24f, 44f);
                Color c = Main.rand.NextBool(3) ? CoreGreen : TungstenBright;
                PRTLoader.NewParticle<PRT_Light>(from, (bladeAt - from) * 0.18f, c,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(Main.rand.Next(6, 10), 0.5f, 1.2f);
            }
        }

        /// <summary>放刺结算：距离与伤害随蓄力（满蓄 reach ×1.5）</summary>
        protected override void OnChargeRelease() {
            reachChargeMul = 1f + ChargeT * 0.5f;
            Projectile.damage = (int)(BaseDamage * (1f + ChargeT * 0.40f));
        }

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            if (FullyCharged) {
                //满蓄贯穿的重出手音
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = -0.35f }, Owner.Center);
            }
            int count = 2 + (int)(ChargeT * 3f);
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? CoreGreen : TungstenBright;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(5f, 10f), c,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        /// <summary>钨芯破甲：随蓄力最高 +16 穿甲</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (ChargeT > 0f) {
                modifiers.ArmorPenetration += 16f * ChargeT;
            }
        }

        /// <summary>命中反馈：致密金属钝响 + 沿刺线继续前冲的贯穿火花（穿透感）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = -0.25f }, target.Center);
            int sparks = 4 + (int)(ChargeT * 5f);
            for (int i = 0; i < sparks; i++) {
                //贯穿感：火花不四溅，压着刺向的窄扇继续前冲
                Vector2 vel = stabUnit.RotatedByRandom(0.25) * Main.rand.NextFloat(4f, 9f + ChargeT * 4f);
                Color c = Main.rand.NextBool(3) ? CoreGreen : TungstenBright;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.5) * Main.rand.NextFloat(1.5f, 3.5f), 100, default, Main.rand.NextFloat(0.9f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }
}
