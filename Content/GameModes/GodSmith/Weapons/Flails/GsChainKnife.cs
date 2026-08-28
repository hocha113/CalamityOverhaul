using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·链刀】链刀重铸：粗铁快链屠刀。签名行为：①命中回手后 7 秒内再掷提速一层，至多三层
    /// ②层数越高充能与出手越快，刀身暗红辉光随层增亮 ③刀面咬住速度方向不自旋，快掷快收
    /// </summary>
    internal class GsChainKnife : GsFlailScheme
    {
        public override int TargetItemID => ItemID.ChainKnife;

        protected override int FlailProjType => ModContent.ProjectileType<GsChainKnifeHead>();

        protected override string GsDescFallback =>
            "Reforged: landing a blade hit grants Momentum for 7 seconds" +
            "\nEach stack spins up and throws faster, up to 3 stacks";

        /// <summary>连掷层数上限</summary>
        internal const int MaxStacks = 3;
        /// <summary>层数保鲜帧数（7 秒）</summary>
        private const int StackKeepFrames = 420;

        /// <summary>连掷层数；方案单例跨玩家共享，只在 myPlayer 守门路径读写</summary>
        private int stacks;
        /// <summary>层数衰减倒计时，只在 myPlayer 守门路径读写</summary>
        private int stackTimer;

        //层数经 ai[2] 随生成包过线，远端也能看到提速与辉光
        protected override float LaunchAi2(Player player, int index)
            => player.whoAmI == Main.myPlayer ? stacks : 0f;

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (stackTimer > 0 && --stackTimer == 0) {
                stacks = 0;
            }
        }

        /// <summary>锤头命中回调：加一层并续时（调用方已守 myPlayer）</summary>
        internal void AddMomentum() {
            stacks = Math.Min(MaxStacks, stacks + 1);
            stackTimer = StackKeepFrames;
        }

        //快小锤定位，签名收益是节奏提速：底伤补一成，综合 DPS 落在原版 110%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;
    }

    /// <summary>
    /// 链刀锤头。快小锤参数；层数（ai[2]）换算充能与出手提速，
    /// 命中经方案注册连掷层数，暗红辉光随层增亮
    /// </summary>
    internal class GsChainKnifeHead : GsFlailHeadProj
    {
        /// <summary>暗红刃光</summary>
        internal static readonly Color BladeRed = new(198, 64, 58);
        /// <summary>铁灰</summary>
        internal static readonly Color IronGray = new(150, 154, 162);

        public override int SourceItemID => ItemID.ChainKnife;
        public override int VanillaProjID => ProjectileID.ChainKnife;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain23;
        public override Color GlowColor => BladeRed;

        public override int HeadSize => 22;
        public override float MaxChainLength => 260f;
        /// <summary>刀面咬住速度方向，不做球锤自旋</summary>
        public override bool SelfSpinHead => false;

        /// <summary>当前连掷层数（ai[2] 随生成包过线，各端一致）</summary>
        private int Stacks => (int)MathHelper.Clamp(WeaponAi2, 0f, GsChainKnife.MaxStacks);

        //每层充能更快约 +12%、出手速度 +8%
        public override int ChargeFrames => Math.Max(12, (int)(30f / (1f + 0.12f * Stacks)));
        public override float LaunchSpeed => 18f * (1f + 0.08f * Stacks);

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //强转回方案加层（方案字段只在 myPlayer 守门路径消费）
            if (GodSmithScheme.TryGetScheme(SourceItemID, out GodSmithScheme scheme)
                && scheme is GsChainKnife knife) {
                knife.AddMomentum();
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //层数越高命中闪越亮：连掷节奏的即时反馈
            if (Stacks > 0) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    BladeRed, 0.10f + Stacks * 0.05f)?.Configure(8, 0.8f);
            }
            //铁灰刃屑补一撮，与族默认火花区分出金属屠刀质感
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(4f, 4f), IronGray,
                    Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>层数可视：刀身暗红辉光随层增亮，identity 播种呼吸不掷 Main.rand</summary>
        protected override void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) {
            int stacks = Stacks;
            if (stacks <= 0) {
                return;
            }
            Texture2D tex = TextureAssets.Projectile[VanillaProjID].Value;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GameUpdateCount * 0.17f + Projectile.identity * 0.917f);
            Color glow = BladeRed * (0.16f * stacks * pulse);
            glow.A = 0;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, glow,
                headRotation, origin, Projectile.scale * (1.05f + 0.02f * stacks), SpriteEffects.None, 0);
        }
    }
}
