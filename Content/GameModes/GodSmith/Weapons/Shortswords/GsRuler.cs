using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 直尺重铸「丈量惩戒」。<br/>
    /// 材质：教务处硬木直尺，红漆刻度。签名行为：①刺线最后两成是 sweet spot——用尺头丈量到位
    /// 伤害 ×1.6，刻度闪光加升级音 ②量歪了（非尺头命中）只有 ×0.85 ③刺击期刃身浮现五格红刻度
    /// </summary>
    internal class GsRuler : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.Ruler;

        protected override string GsDescFallback =>
            "Reforged: discipline demands exact measurement, land the very tip for a 1.6x graded strike;" +
            "\nsloppy hits closer to the hand are marked down to 0.85x";

        protected override int HeldProjType => ModContent.ProjectileType<GsRulerHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.30f;//joke 弱势武器（原版是 noMelee 的量地工具），sweet spot 期望约 ×1.0，按弱势条款补三成底伤
    }

    /// <summary>
    /// 直尺手持突刺：轻而长的丈量刺（StabReach 全族最远、刺线最细）。
    /// sweet spot 判定：target 碰撞箱到 TipPos 的距离 ≤ 刺线全长两成
    /// </summary>
    internal class GsRulerHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.Ruler;

        //教具木尺色板
        internal static readonly Color RulerWood = new(240, 216, 142);
        internal static readonly Color RulerMain = new(224, 188, 98);
        internal static readonly Color MarkRed = new(232, 78, 58);

        protected override float WindupFrames => 2f;
        protected override float ThrustFrames => 3f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 5f;
        protected override float PullbackDist => 7f;
        protected override float StabReach => 40f;//量程全族最远
        protected override float BladeLength => 44f;
        protected override float CollisionWidth => 18f;//尺薄，刺线最细
        protected override float TipGreedRadius => 20f;
        protected override float ThrustEasePower => 5f;
        protected override int HitstopFrames => 1;
        protected override float LeanAmp => 0.024f;
        protected override float ThrustPitch => 0.35f;//木尺破空的轻脆高音

        protected override Color EdgeColor => RulerWood;
        protected override Color CoreColor => MarkRed;

        /// <summary>本次命中是否量到了尺头（ModifyHit 与 OnHit 同链，供反馈分流）</summary>
        private bool sweetHit;

        /// <summary>丈量判定：目标碰撞箱到刺尖距离 ≤ 刺线全长两成 = 尺头命中</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            float lineLen = holdout + BladeLength;
            sweetHit = target.Hitbox.Distance(TipPos) <= lineLen * 0.20f;
            modifiers.FinalDamage *= sweetHit ? 1.6f : 0.85f;
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            if (!sweetHit || !firstOnTarget || VaultUtils.isServer) {
                return;
            }
            //丈量到位的升级反馈：升级音 + 刻度闪光沿尺头两成段排开
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = 0.35f }, target.Center);
            float lineLen = holdout + BladeLength;
            for (int i = 0; i < 3; i++) {
                Vector2 at = Hand + stabUnit * (lineLen * (0.82f + 0.08f * i));
                PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, MarkRed, 0.20f)?.Configure(10, 0.85f);
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(TipPos, Vector2.Zero, MarkRed, 0f)
                ?.Configure(0.04f, 0.30f, 11);
        }

        /// <summary>命中反馈：量到位红漆刻度屑四溅，量歪了只有干瘪木屑</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            int count = sweetHit ? 8 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(sweetHit ? 0.7 : 0.4) * Main.rand.NextFloat(2.5f, sweetHit ? 8f : 5f);
                Color c = sweetHit
                    ? (Main.rand.NextBool() ? MarkRed : RulerWood)
                    : (Main.rand.NextBool() ? RulerMain : RulerWood);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.3f, sweetHit ? 0.6f : 0.45f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            if (!sweetHit) {
                //量歪的钝响：挨了一下但不痛快
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.3f, Pitch = -0.4f }, target.Center);
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.2f, 2.5f), 100, default, Main.rand.NextFloat(0.8f, 1f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>刻度可视化：刺击期尺身浮现五格刻度灯，最末一格（sweet 区）红漆常亮（定值，无随机）</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            int phase = CurrentPhase;
            if (phase is not PhaseThrust and not PhaseDwell || FanFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float lineLen = holdout + BladeLength;
            for (int i = 0; i < 5; i++) {
                float along = 0.28f + 0.18f * i;//0.28~1.0，末格正是 sweet 段
                bool sweetMark = i == 4;
                Vector2 at = Hand + stabUnit * (lineLen * along) - Main.screenPosition;
                float pulse = sweetMark
                    ? 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.whoAmI)
                    : 1f;
                Color c = (sweetMark ? MarkRed : RulerWood) with { A = 0 }
                    * ((sweetMark ? 0.55f : 0.28f) * FanFade * pulse);
                sb.Draw(glow, at, null, c, 0f, glow.Size() / 2f,
                    (sweetMark ? 0.10f : 0.06f) * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}
