using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】钨套「钨钉十字」：材质=最重的硬金属，钉是打进去的不是飞光。<br/>
    /// ①命中积攒星核压，满 5 层后下一击自目标迸出正十字四枚钨钉②钨钉贯穿两次、
    /// 速度取向拉伸如钉入③迸发瞬间金属脆响 + 目标处冲击小环④受击崩落 2 层星核压。<br/>
    /// 原版套装奖励保留，神赋是叠加层；层数是攻击方端本地量，跨端可见的部分是钨钉实体
    /// </summary>
    internal class GsTungstenArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.TungstenHelmet];

        public override int BodyID => ItemID.TungstenChainmail;

        public override int LegsID => ItemID.TungstenGreaves;

        protected override string EndowLineFallback =>
            "Tungsten Cross: strikes build core pressure; at 5 stacks the next strike erupts four piercing tungsten spikes from the target";

        //硬钨色板
        internal static readonly Color TungstenEdge = new(44, 66, 52);
        internal static readonly Color TungstenBody = new(120, 150, 128);
        internal static readonly Color TungstenCore = new(210, 225, 214);

        /// <summary>迸钉所需星核压层数</summary>
        private const int FullCharge = 5;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：灰绿金属微尘偶闪（个人读数）
            if (Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2CircularEdge(18f, 24f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    TungstenBody, Main.rand.NextFloat(0.2f, 0.3f))?.Configure(false, 12);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //钨钉自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsTungstenSpikeProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //满层：这一击自目标迸出正十字四枚钨钉
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.6f, Pitch = -0.2f }, target.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, TungstenBody, 0.35f)
                    ?.Configure(0.1f, 0.8f, 12);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                        Main.rand.NextBool() ? TungstenCore : TungstenBody, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(true, Main.rand.Next(14, 22));
                }
            }
            //proc 弹幕 owner 侧生成；钉伤 18% 封 8..90
            if (player.whoAmI == Main.myPlayer) {
                int spikeDamage = Math.Clamp((int)(damageDone * 0.18f), 8, 90);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.PiOver2 * i + Main.rand.NextFloat(-0.12f, 0.12f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithTungstenEndow"),
                        target.Center + ang.ToRotationVector2() * 12f,
                        ang.ToRotationVector2() * 14f,
                        ModContent.ProjectileType<GsTungstenSpikeProj>(), spikeDamage, 1.5f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击崩落两层星核压
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Tungsten, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 钨钉：一根被压力打出去的实心硬钉，不是飞光。直线贯穿两次，末段泄力减速；
    /// 长钉形三层叠色（墨绿边/灰绿体/金属亮芯）+ 钉头亮点 + 短残影，飞行偶发灰绿迸屑
    /// </summary>
    internal class GsTungstenSpikeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //末段泄力：钉入后动能耗散，不匀速飞满全程
            if (Life > 20f) {
                Projectile.velocity *= 0.96f;
            }

            //飞行相：偶发灰绿金属迸屑
            if (!Main.dedServ && Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Main.rand.NextBool(3) ? GsTungstenArmor.TungstenEdge : GsTungstenArmor.TungstenBody,
                    Main.rand.NextFloat(0.2f, 0.35f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsTungstenArmor.TungstenBody.ToVector3() * (0.18f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //钉入反馈：金属脆响 + 迸屑
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsTungstenArmor.TungstenCore : GsTungstenArmor.TungstenBody,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //钉尽余痕：几粒钨屑坠落
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Tungsten,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.5f),
                    100, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = false;
            }
        }

        //==================== 绘制：长钉形三层 + 钉头亮点 + 短残影 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D tip = CWRAsset.StarGlow01?.Value;
            if (tex == null || tip == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            //强速度拉伸 Y：越快钉形越长
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.34f);
            //钉身刚性微振，确定性相位（幅度极小，硬金属不软抖）
            float wob = MathF.Sin(Life * 0.8f + Seed * 6f) * 0.03f;
            Vector2 jiggle = new(1f + wob, 1f);

            //短残影：旧位置褪色钉影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.25f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, gpos, null, GsTungstenArmor.TungstenEdge * ghost,
                    Projectile.oldRot[i], origin, new Vector2(0.07f, 0.30f + stretch * 0.8f), SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            //墨绿压边
            Main.EntitySpriteDraw(tex, pos, null, GsTungstenArmor.TungstenEdge * (0.9f * fade), rotation, origin,
                new Vector2(0.10f, 0.40f + stretch) * jiggle, SpriteEffects.None, 0);
            //灰绿钉体
            Main.EntitySpriteDraw(tex, pos, null, GsTungstenArmor.TungstenBody * fade, rotation, origin,
                new Vector2(0.07f, 0.34f + stretch * 0.9f) * jiggle, SpriteEffects.None, 0);
            //金属亮芯：窄条冷反光
            Main.EntitySpriteDraw(tex, pos, null, (GsTungstenArmor.TungstenCore with { A = 0 }) * (0.55f * fade),
                rotation, origin, new Vector2(0.03f, 0.22f + stretch * 0.5f) * jiggle, SpriteEffects.None, 0);
            //钉头亮点：沿速度方向探出，加色小星
            Vector2 tipPos = pos + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 16f;
            Main.EntitySpriteDraw(tip, tipPos, null, (GsTungstenArmor.TungstenCore with { A = 0 }) * (0.6f * fade),
                rotation, tip.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
            return false;
        }
    }
}
