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
    /// 【长矛】黑曜剑鱼重铸：淬火迸屑。<br/>
    /// 材质：熔岩湖里淬出的黑曜石吻骨，芯里锁着余温。签名行为：①命中点燃目标，
    /// 伤口迸出双层黑曜石屑——近黑尖角碎屑混橙芯火花 ②淬火状态（自身着火或浸岩浆）
    /// 矛芯回温，伤害 +15% 且刃面透出熔橙 ③命中带岩石脆响与火燎声，重矛慢而狠
    /// </summary>
    internal class GsObsidianSwordfish : GsSpearScheme
    {
        public override int TargetItemID => ItemID.ObsidianSwordfish;

        protected override string GsDescFallback =>
            "Reforged: strikes set enemies on fire and burst obsidian shards from the wound;" +
            "\nwhile you are burning or soaked in lava the core reheats, dealing 15% more damage";

        protected override int HeldProjType => ModContent.ProjectileType<GsObsidianSwordfishHeld>();

        //点燃+淬火条件增伤吃掉大半预算，底伤只补零头，综合 DPS 落在原版 104%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 黑曜剑鱼手持突刺：六把里最沉的矛；命中点燃 + 黑曜石屑迸溅，
    /// 淬火状态（Owner 着火/岩浆湿身）伤害 +15% 且辉光升温
    /// </summary>
    internal class GsObsidianSwordfishHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.ObsidianSwordfish;

        //黑曜+熔橙色板
        internal static readonly Color ObsidianDark = new(36, 28, 46);   //黑曜近黑
        internal static readonly Color ObsidianSheen = new(122, 102, 148);//黑曜冷紫泽
        internal static readonly Color MoltenOrange = new(255, 138, 52); //熔橙
        internal static readonly Color EmberYellow = new(255, 212, 120); //余烬黄

        //六把里最沉的时间线：黑曜石又重又狠
        protected override float WindupFrames => 6f;
        protected override float ThrustFrames => 5f;
        protected override float DwellFrames => 4f;
        protected override float RecoverFrames => 9f;
        protected override float RestHoldout => 12f;
        protected override float PullbackDist => 16f;
        protected override float StabReach => 60f;
        protected override float BladeLength => 84f;
        protected override float CollisionWidth => 30f;
        protected override float TipGreedRadius => 27f;
        protected override float ThrustEasePower => 5f;
        protected override bool TwoHanded => true;
        protected override float LeanAmp => 0.05f;
        protected override int HitboxSize => 50;
        protected override int HitstopFrames => 3;
        protected override float ThrustPitch => -0.32f;

        protected override Color EdgeColor => ObsidianSheen;
        protected override Color CoreColor => MoltenOrange;

        /// <summary>淬火状态：自身着火或浸岩浆，矛芯回温</summary>
        private bool Quenched => Owner.lavaWet || Owner.HasBuff(BuffID.OnFire);

        protected override void OnThrustBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = ThrustPitch }, Owner.Center);
            //爆发帧火星拖尾（淬火时加量）
            int count = Quenched ? 5 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 at = Vector2.Lerp(Hand, TipPos, Main.rand.NextFloat(0.5f, 1f));
                Color c = Main.rand.NextBool(3) ? EmberYellow : MoltenOrange;
                PRTLoader.NewParticle<PRT_Spark>(at, stabUnit * Main.rand.NextFloat(3.5f, 7f), c,
                    Main.rand.NextFloat(0.32f, 0.52f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>淬火期矛尖余烬呼吸：驻相时缓慢吐余烬</summary>
        protected override void OnTick(int phase) {
            if (VaultUtils.isServer || !Quenched || phase != PhaseDwell) {
                return;
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(TipPos + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    Main.rand.NextBool(3) ? EmberYellow : MoltenOrange,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(8, 14), 0.5f, 1.3f);
            }
        }

        /// <summary>淬火可视化：刃面透出熔橙，微微呼吸</summary>
        protected override float ExtraGlowStrength() {
            if (!Quenched) {
                return 0f;
            }
            return 0.20f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.whoAmI);
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            //淬火回温：+15% 伤害
            if (Quenched) {
                modifiers.FinalDamage *= 1.15f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone, bool firstOnTarget) {
            //淬火迸屑的机制面：命中点燃
            target.AddBuff(BuffID.OnFire, Quenched ? 300 : 180);
        }

        /// <summary>命中反馈：双层黑曜石屑——近黑尖角碎屑（真 alpha 火花）+ 熔橙芯火花，
        /// 岩石脆响与火燎声</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = -0.4f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.35f, Pitch = 0.1f, MaxInstances = 3 }, pos);
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, MoltenOrange, 0.18f + (Quenched ? 0.06f : 0f))
                ?.Configure(9, 0.8f);
            //外层：近黑黑曜碎屑（加色物理上压不暗，走真 alpha 火花）
            int chips = Quenched ? 7 : 5;
            for (int i = 0; i < chips; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.75) * Main.rand.NextFloat(3f, 7.5f);
                Color c = Main.rand.NextBool(3) ? ObsidianSheen : ObsidianDark;
                PRTLoader.NewParticle<PRT_SparkAlpha>(pos, vel, c, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            //内层：熔橙芯火花
            int embers = Quenched ? 6 : 4;
            for (int i = 0; i < embers; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.5) * Main.rand.NextFloat(3.5f, 8f);
                Color c = Main.rand.NextBool(3) ? EmberYellow : MoltenOrange;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(12, 18));
            }
        }

        /// <summary>淬火自绘：矛尖后方一枚黑曜暗核裹熔橙晕（真 alpha 压暗 + 加色芯，无随机）</summary>
        protected override void DrawUnderBlade(SpriteBatch sb) {
            if (!Quenched || FanFade <= 0.05f) {
                return;
            }
            Texture2D dark = CWRAsset.Extra_98?.Value;
            if (dark == null) {
                return;
            }
            float flick = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI * 1.3f);
            Vector2 at = Hand + stabUnit * (holdout + BladeLength * 0.78f) - Main.screenPosition;
            sb.Draw(dark, at, null, ObsidianDark * (0.5f * FanFade), 0f,
                dark.Size() / 2f, 0.18f * flick, SpriteEffects.None, 0f);
            sb.Draw(dark, at, null, MoltenOrange with { A = 0 } * (0.4f * FanFade * flick), 0f,
                dark.Size() / 2f, 0.11f, SpriteEffects.None, 0f);
        }
    }
}
