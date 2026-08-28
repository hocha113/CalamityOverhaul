using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【钯金套·生机虹吸】温血活金之甲：①命中积攒生机，满八层后下一击在目标身上种下虹吸苞
    /// ②虹吸苞四秒内周期脉冲啃噬宿主，每次脉冲抽出一缕金血飞回佩戴者回血 ③宿主死亡苞体凋零飘散。
    /// 原版套装奖励（命中触发急速再生）保留，神赋叠加
    /// </summary>
    internal class GsPalladiumArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.PalladiumMask, ItemID.PalladiumHelmet, ItemID.PalladiumHeadgear];

        public override int BodyID => ItemID.PalladiumBreastplate;

        public override int LegsID => ItemID.PalladiumLeggings;

        protected override string EndowLineFallback =>
            "Vital Siphon: strikes build vigor; at 8 stacks the next strike plants a siphon bloom that gnaws the host and sends life motes back to you";

        //钯金暖橙色板
        internal static readonly Color PalladiumBright = new(255, 212, 160);
        internal static readonly Color PalladiumMain = new(236, 128, 58);
        internal static readonly Color PalladiumDeep = new(128, 58, 24);

        protected override int FullCharge => 8;

        protected override Color ThemeMain => PalladiumMain;

        protected override Color ThemeBright => PalladiumBright;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsPalladiumSiphonBloomProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.2f }, target.Center);
                //种苞瞬间：金血花粉自命中点绽开
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                        Main.rand.NextBool() ? PalladiumBright : PalladiumMain,
                        Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            if (player.whoAmI == Main.myPlayer) {
                int bloomDamage = Math.Clamp((int)(damageDone * 0.15f), 5, 80);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithPalladiumEndow"),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsPalladiumSiphonBloomProj>(),
                    bloomDamage, 0f, player.whoAmI, 0f, target.whoAmI);
            }
        }
    }

    /// <summary>
    /// 钯金虹吸苞：寄生在目标身上的活金花苞，三瓣金属花瓣缓旋，
    /// 每 30 帧收拢一次啃噬宿主并抽出金血光缕飞向佩戴者（佩戴者端回血 2 点）；
    /// 宿主消亡则苞体脱落凋零
    /// </summary>
    internal class GsPalladiumSiphonBloomProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private ref float HostIndex => ref Projectile.ai[1];

        /// <summary>脱落凋零态标记</summary>
        private ref float Withering => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.6173f % 3.31f;

        /// <summary>脉冲周期</summary>
        private const int PulseInterval = 30;

        private float VisualFade => Withering > 0f
            ? MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f)
            : MathHelper.Clamp(Life / 8f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = PulseInterval;
        }

        /// <summary>只在脉冲收拢的短窗内造成伤害，其余时间不判定</summary>
        public override bool? CanDamage() => Withering <= 0f && Life % PulseInterval < 4;

        public override void AI() {
            Life++;

            NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[(int)HostIndex] : null;
            if (Withering <= 0f && (host == null || !host.active)) {
                //宿主消亡：脱落凋零
                Withering = 1f;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 20);
                Projectile.velocity = new Vector2(0f, -0.4f);
                Projectile.netUpdate = true;
            }

            if (Withering > 0f) {
                Projectile.velocity *= 0.97f;
                Projectile.rotation += 0.02f;
                return;
            }

            //贴附宿主，缓慢呼吸浮动
            Projectile.Center = host.Center + new Vector2(0f, MathF.Sin(Life * 0.08f + Seed) * 4f - host.height * 0.2f);

            //脉冲拍：啃噬 + 金血光缕飞回佩戴者
            if (Life % PulseInterval == 0) {
                Player owner = Main.player[Projectile.owner];
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.25f, Pitch = 0.7f, MaxInstances = 3 }, Projectile.Center);
                    //金血光缕：初速朝佩戴者收束
                    Vector2 toOwner = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                            toOwner.RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f)) * Main.rand.NextFloat(4f, 7f),
                            i % 2 == 0 ? GsPalladiumArmor.PalladiumBright : GsPalladiumArmor.PalladiumMain,
                            Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, Main.rand.Next(12, 18));
                    }
                }
                //回血只在佩戴者自己端结算
                if (Projectile.owner == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                    owner.Heal(2);
                }
            }
            Lighting.AddLight(Projectile.Center, GsPalladiumArmor.PalladiumMain.ToVector3() * (0.25f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //凋零：花瓣散落
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Smoke, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.2f, 1.4f)),
                    140, GsPalladiumArmor.PalladiumMain, 1f);
                d.noGravity = false;
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsPalladiumArmor.PalladiumBright, 0.12f)?.Configure(8, 0.6f);
        }

        //==================== 绘制：三瓣活金花 + 脉冲收拢 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (star == null || core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //脉冲收拢：临近脉冲拍花瓣咬紧
            float pulsePhase = Life % PulseInterval / (float)PulseInterval;
            float clench = pulsePhase > 0.85f ? (pulsePhase - 0.85f) / 0.15f : 0f;
            float petalScale = (0.16f - clench * 0.05f) * (Withering > 0f ? fade : 1f);
            float spin = Life * 0.03f + Seed;

            //三瓣金属花瓣（四芒星旋转错相叠出花形）
            for (int i = 0; i < 3; i++) {
                float ang = spin + MathHelper.TwoPi * i / 3f;
                Vector2 off = ang.ToRotationVector2() * (7f - clench * 3f);
                Main.EntitySpriteDraw(star, pos + off, null,
                    (GsPalladiumArmor.PalladiumMain with { A = 0 }) * (0.55f * fade), ang, star.Size() * 0.5f,
                    petalScale, SpriteEffects.None, 0);
            }
            //焦金瓣底
            Main.EntitySpriteDraw(core, pos, null,
                GsPalladiumArmor.PalladiumDeep * (0.75f * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.24f, 0.26f), SpriteEffects.None, 0);
            //温血苞心，脉冲时增亮
            Main.EntitySpriteDraw(core, pos, null,
                (GsPalladiumArmor.PalladiumBright with { A = 0 }) * ((0.5f + clench * 0.5f) * fade), 0f, core.Size() * 0.5f,
                new Vector2(0.10f + clench * 0.04f, 0.11f + clench * 0.04f), SpriteEffects.None, 0);
            return false;
        }
    }
}
