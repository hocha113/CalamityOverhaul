using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 钨短剑重铸「钨芯蓄穿」。<br/>
    /// 材质：高密钨芯，剑身沉得压手。签名行为：①按住短蓄（约 26 帧即满）
    /// ②满蓄刺出距离 ×1.5、刺线加宽、破甲提升，一条直线全穿 ③与黑暗长枪蓄力的区分：无弹幕、纯物理穿透、蓄得更快
    /// </summary>
    internal class GsTungstenShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.TungstenShortsword;

        protected override string GsDescFallback =>
            "Reforged: a dense tungsten core rewards a brief hold before the thrust;\na full charge lunges half again as far, punching through armor and everything in line";
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

        protected override float WindupFrames => 4f;
        /// <summary>满蓄 reach ×1.5：刺出帧数随行程等比放大（4→6），保持帧间矛体重叠</summary>
        protected override float ThrustFrames => 4f + ChargeT * 2f;
        protected override float DwellFrames => 3f;
        protected override float RecoverFrames => 7f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 36f;
        protected override float BladeLength => 45f;
        /// <summary>蓄力加宽刺线：满蓄一条更粗的直线全穿</summary>
        protected override float CollisionWidth => 26f + ChargeT * 12f;
        protected override float TipGreedRadius => 24f + ChargeT * 8f;
        protected override float ThrustEasePower => 2.6f + ChargeT * 0.4f;
        protected override int HitboxSize => 44;
        protected override int HitstopFrames => ChargeT >= 0.8f ? 3 : 2;
        protected override float LeanAmp => 0.040f;
        protected override float ThrustPitch => -0.22f;//钨的沉重低音

        /// <summary>短蓄即满：与黑暗长枪（32 帧、放弹幕）区分——更快、纯物理</summary>
        protected override float MaxChargeFrames => 26f;

        private bool FullyCharged => ChargeT >= 0.8f;
        private bool chargeCuePlayed;

        /// <summary>蓄力期：满蓄一记金属定音</summary>
        protected override void OnChargingTick() {
            if (!chargeCuePlayed && ChargeT >= 1f) {
                chargeCuePlayed = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.4f }, Owner.Center);
                }
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
        }

        /// <summary>钨芯破甲：随蓄力最高 +16 穿甲</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (ChargeT > 0f) {
                modifiers.ArmorPenetration += 16f * ChargeT;
            }
        }

        /// <summary>命中反馈：致密金属钝响</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.45f, Pitch = -0.25f }, target.Center);
            }
        }
    }
}
