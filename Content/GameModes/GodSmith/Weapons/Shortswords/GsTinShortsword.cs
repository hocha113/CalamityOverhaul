using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Shortswords
{
    /// <summary>
    /// 锡短剑重铸「锡鸣」。<br/>
    /// 材质：薄锻锡刃，击之如磬。签名行为：①踩住断拍窗连刺，刺音音阶逐层上行（最多四层）
    /// ②满四层起每刺在尖端荡开一圈锡鸣涟漪，附小额增伤 ③层数可视为刃身冷银辉光
    /// </summary>
    internal class GsTinShortsword : GsShortswordScheme
    {
        public override int TargetItemID => ItemID.TinShortsword;

        protected override string GsDescFallback =>
            "Reforged: chain your thrusts to climb the tin scale, each note higher than the last;" +
            "\nat full chime every thrust rings out a rippling pulse and bites a little deeper";

        protected override int HeldProjType => ModContent.ProjectileType<GsTinShortswordHeld>();

        /// <summary>音阶层数 0~4，只在 myPlayer 路径消费（与 Gladius 的节奏加速区别：层数只走音高与满层涟漪增伤，不催快时间线）</summary>
        private int chimeStacks;

        protected override void SpawnHeld(Item item, Player player, int beat) {
            //断拍窗内接上一刺 = 音阶上行一层；首刺/断拍归零重起
            chimeStacks = comboCounter > 1 ? Math.Min(4, chimeStacks + 1) : 0;
            base.SpawnHeld(item, player, beat);
        }

        protected override float SpawnAi1(Item item, Player player) => chimeStacks;

        protected override void OnComboReset() => chimeStacks = 0;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.28f;//公认弱势开局武器（原版 6 伤），按公约弱势条款放宽至 135% 内取 128%
    }

    /// <summary>
    /// 锡短剑手持突刺。ai[1]=音阶层数 0~4：刺音 Pitch 逐层 +0.09；
    /// 满四层每刺在尖端定格瞬间荡开锡鸣涟漪（PRT_StarPulseRing）并小额增伤
    /// </summary>
    internal class GsTinShortswordHeld : GsThrustHeldBase
    {
        protected override int TargetItemType => ItemID.TinShortsword;

        //锡磬色板
        internal static readonly Color TinBright = new(228, 236, 244);
        internal static readonly Color TinMain = new(170, 184, 198);
        internal static readonly Color ChimeBlue = new(152, 202, 255);

        private int ChimeStacks => Math.Clamp((int)WeaponParam, 0, 4);
        private bool FullChime => ChimeStacks >= 4;

        protected override float WindupFrames => 3f;
        protected override float ThrustFrames => 3f;
        protected override float DwellFrames => 2f;
        protected override float RecoverFrames => 5f;
        protected override float PullbackDist => 9f;
        protected override float StabReach => 29f;
        protected override float BladeLength => 41f;
        protected override float ThrustEasePower => 5.2f;
        protected override int HitstopFrames => 1;
        protected override float LeanAmp => 0.026f;
        /// <summary>音阶上行：层数直接抬 Pitch</summary>
        protected override float ThrustPitch => 0.06f + ChimeStacks * 0.09f;

        protected override Color EdgeColor => TinBright;
        protected override Color CoreColor => FullChime ? ChimeBlue : TinMain;

        /// <summary>满层涟漪：尖端定格瞬间荡开一圈锡鸣 + 轻磬</summary>
        protected override void OnDwellStart() {
            if (!FullChime || VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(TipPos, Vector2.Zero, ChimeBlue, 0f)
                ?.Configure(0.05f, 0.42f, 13);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.32f, Pitch = 0.45f }, TipPos);
        }

        /// <summary>满层锡鸣共振：涟漪附着的刺小额增伤（机制端收益小，底伤已按弱势放宽）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (FullChime) {
                modifiers.FinalDamage *= 1.12f;
            }
        }

        /// <summary>命中反馈：冷银碎音火花，满层追加一圈小涟漪当作音波命中感</summary>
        protected override void SpawnHitEffects(NPC target, NPC.HitInfo hit) {
            Vector2 pos = Vector2.Lerp(TipPos, target.Center, 0.5f);
            int count = 4 + ChimeStacks;
            for (int i = 0; i < count; i++) {
                Vector2 vel = stabUnit.RotatedByRandom(0.55) * Main.rand.NextFloat(3f, 7.5f);
                Color c = Main.rand.NextBool(3) ? ChimeBlue : TinBright;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, c, Main.rand.NextFloat(0.32f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            if (FullChime) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, TinBright, 0f)
                    ?.Configure(0.04f, 0.28f, 10);
            }
            if (!CWRLoad.NPCValue.ISTheofSteel(target)) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    stabUnit.RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 3f), 100, default, Main.rand.NextFloat(0.8f, 1.1f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>音阶层数可视化：层数越高冷银辉光越亮</summary>
        protected override float ExtraGlowStrength() => ChimeStacks * 0.07f;
    }
}
