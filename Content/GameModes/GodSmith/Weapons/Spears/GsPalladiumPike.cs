using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Spears
{
    /// <summary>
    /// 钯金长枪重铸：生血枪锋。<br/>
    /// 材质：温血钯金枪刃。签名行为：①命中积攒「生机」层，枪杆暖金光珠逐层点亮
    /// ②叠满 5 层触发生机迸发——回复 5 点生命并清层，橙粉治愈光尘自伤口涌回持枪手
    /// ③生机迸发的那一击带治愈钟音，与普通命中的暖金火花明确区分
    /// </summary>
    internal class GsPalladiumPike : GsSpearScheme
    {
        public override int TargetItemID => ItemID.PalladiumPike;

        protected override string GsDescFallback =>
            "Reforged: each hit stores a charge of vigor in the warm palladium edge;" +
            "\nat five charges the spear bursts them, restoring 5 life to its wielder";

        protected override int HeldProjType => ModContent.ProjectileType<GsPalladiumPikeHeld>();

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;//回复收益温和，底伤补零头，综合 DPS 落在原版 108%~118%
    }

    /// <summary>每玩家生机层数（跨攻击持久，故不放共享单例方案里）</summary>
    internal class GsPalladiumPikePlayer : ModPlayer
    {
        /// <summary>生机层数 0~5</summary>
        internal int vigor;
        /// <summary>断层倒计时：5 秒没有新命中则清层</summary>
        internal int vigorDecay;

        public override void PostUpdate() {
            if (vigorDecay > 0 && --vigorDecay == 0) {
                vigor = 0;
            }
        }
    }

    /// <summary>
    /// 钯金长枪手持突刺：命中积生机层（owner 端守门写 ModPlayer），
    /// 叠满 5 层触发回复并清层；层数可视化 = 枪身辉光升温 + 杆上暖金光珠
    /// </summary>
    internal class GsPalladiumPikeHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.PalladiumPike;

        //温血钯金色板
        internal static readonly Color PalladiumGold = new(255, 196, 120);  //暖金亮
        internal static readonly Color PalladiumRose = new(255, 138, 110);  //橙粉血光
        internal static readonly Color PalladiumDeep = new(150, 78, 52);    //深铜底

        protected override float WindupFrames => 4f;
        protected override float ThrustFrames => 4f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 8f;
        protected override float RestHoldout => 10f;
        protected override float PullbackDist => 14f;
        protected override float StabReach => 62f;
        protected override float BladeLength => 90f;
        protected override float CollisionWidth => 28f;
        protected override float TipGreedRadius => 26f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.038f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => 2;
        protected override float ThrustPitch => -0.16f;

        protected override Color EdgeColor => PalladiumGold;
        protected override Color CoreColor => PalladiumRose;

        private int Vigor => Owner.GetModPlayer<GsPalladiumPikePlayer>().vigor;

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //生机层只在 owner 端结算（ModPlayer 是每玩家状态，写入守 IsOwnedByLocalPlayer）
            if (!firstOnTarget || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            GsPalladiumPikePlayer mp = Owner.GetModPlayer<GsPalladiumPikePlayer>();
            mp.vigor++;
            mp.vigorDecay = 300;
            if (mp.vigor < 5) {
                return;
            }
            //生机迸发：回复 5 点生命并清层（Heal 自带治疗数字与联机同步）
            mp.vigor = 0;
            Owner.Heal(5);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = 0.35f }, Owner.Center);
            //治愈光尘自伤口涌回持枪手
            for (int i = 0; i < 8; i++) {
                Vector2 from = target.Center + Main.rand.NextVector2Circular(16f, 16f);
                Vector2 vel = (Owner.Center - from).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(3f, 6f);
                Color c = Main.rand.NextBool(3) ? PalladiumRose : PalladiumGold;
                PRTLoader.NewParticle<PRT_Light>(from, vel, c, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 18), 0.6f, 1.3f);
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, PalladiumRose, 0.05f)
                ?.Configure(0.06f, 0.4f, 14);
        }

        /// <summary>命中反馈：暖金火花 + 橙粉滋养光点，音色偏软（与钴钢脆响区分）</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, PalladiumRose, 0.18f)?.Configure(9, 0.7f);
            for (int i = 0; i < 6; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3f, 7f);
                Color c = Main.rand.NextBool(3) ? PalladiumRose : PalladiumGold;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>生机层可视化其一：层数越高枪身辉光越暖</summary>
        protected override float ExtraGlowStrength() => Vigor * 0.07f;

        /// <summary>生机层可视化其二：杆上逐层点亮的暖金光珠（定值布点，无随机）</summary>
        protected override void DrawOverBlade(SpriteBatch sb) {
            int vigor = Vigor;
            if (vigor <= 0 || FanFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.StarGlow01?.Value;
            if (glow == null) {
                return;
            }
            for (int i = 0; i < vigor; i++) {
                float along = holdout + 10f + i * 13f;
                Vector2 at = Hand + stabUnit * along - Main.screenPosition;
                float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 1.4f);
                Color c = (i == 4 ? PalladiumRose : PalladiumGold) with { A = 0 } * (0.5f * FanFade * pulse);
                sb.Draw(glow, at, null, c, 0f, glow.Size() / 2f, 0.14f * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}
