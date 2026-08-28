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
    /// 【钛金套·镜面反刃】冷锻镜钛之甲，与金套镜像反向：①受击积攒冷锻（吃下的每一击都被记进镜面）
    /// ②满四层后下一次命中，自目标四方唤出十字四道镜刃错拍收束穿斩
    /// ③镜刃现身先驻定一瞬再合围，命中银芒交叉炸开。原版套装奖励（钛金屏障）保留，神赋叠加
    /// </summary>
    internal class GsTitaniumArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.TitaniumMask, ItemID.TitaniumHelmet, ItemID.TitaniumHeadgear];

        public override int BodyID => ItemID.TitaniumBreastplate;

        public override int LegsID => ItemID.TitaniumLeggings;

        protected override string EndowLineFallback =>
            "Mirror Riposte: taking hits builds cold-forge; at 4 stacks the next strike calls four mirror blades that converge on the target in a cross";

        //钛银冷光色板
        internal static readonly Color TitaniumBright = new(240, 246, 255);
        internal static readonly Color TitaniumMain = new(198, 210, 228);
        internal static readonly Color TitaniumDeep = new(88, 100, 124);

        protected override int FullCharge => 4;

        protected override Color ThemeMain => TitaniumMain;

        protected override Color ThemeBright => TitaniumBright;

        protected override bool ChargeOnHit => false;

        protected override bool ChargeOnHurt => true;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsTitaniumMirrorEdgeProj>();

        protected override void ReadyAura(Player player) {
            Lighting.AddLight(player.Center, TitaniumBright.ToVector3() * 0.18f);
            //镜光就绪：银芒偶闪
            if (Main.rand.NextBool(12)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    player.Center + Main.rand.NextVector2Circular(18f, 26f),
                    new Vector2(0f, -0.3f), TitaniumBright, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(TitaniumMain, 18, 0.05f, 0.8f);
            }
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int edgeDamage = Math.Clamp((int)(damageDone * 0.35f), 12, 150);
            //十字四方唤刃，错拍 4 帧依次收束
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.PiOver2 * i + MathHelper.PiOver4;
                Vector2 from = target.Center + ang.ToRotationVector2() * 130f;
                Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX) * 15f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithTitaniumEndow"),
                    from, vel, ModContent.ProjectileType<GsTitaniumMirrorEdgeProj>(),
                    edgeDamage, 2f, player.whoAmI, 0f, target.whoAmI, i * 4f);
            }
        }
    }

    /// <summary>
    /// 钛金镜刃：冷锻镜面唤出的细长银刃，现身驻定一瞬（蓄势微退），
    /// 随即错拍收束穿斩目标；刃身镜面流光扫掠，命中银芒交叉炸开
    /// </summary>
    internal class GsTitaniumMirrorEdgeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        private ref float TargetIndex => ref Projectile.ai[1];

        /// <summary>错拍延迟帧（随生成参数过线）</summary>
        private ref float HoldFrames => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.8863f % 3.79f;

        /// <summary>基础驻定帧数</summary>
        private const int BaseHold = 7;

        private bool Holding => Life < BaseHold + HoldFrames;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 4f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        /// <summary>驻定蓄势期不判定</summary>
        public override bool? CanDamage() => !Holding;

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Holding) {
                //驻定：钉在原位蓄势微退，刃口对准目标
                Projectile.position -= Projectile.velocity;
                Projectile.Center -= Projectile.velocity.SafeNormalize(Vector2.UnitX) * 0.8f;
                NPC aim = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
                if (aim != null && aim.active) {
                    Projectile.velocity = Projectile.velocity.Length() * (aim.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                }
                Lighting.AddLight(Projectile.Center, GsTitaniumArmor.TitaniumBright.ToVector3() * 0.15f);
                return;
            }

            //收束：微加速穿斩
            Projectile.velocity *= 1.06f;
            if (Projectile.velocity.Length() > 27f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 27f;
            }
            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    Projectile.velocity * 0.05f, GsTitaniumArmor.TitaniumMain,
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(false, Main.rand.Next(6, 10));
            }
            Lighting.AddLight(Projectile.Center, GsTitaniumArmor.TitaniumBright.ToVector3() * 0.25f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.4f, Pitch = 0.75f, MaxInstances = 4 }, target.Center);
            //银芒交叉炸开
            Texture2D cross = CWRAsset.RayCross01?.Value;
            if (cross != null) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    GsTitaniumArmor.TitaniumBright, 0.16f)?.Configure(8, 0.85f);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool() ? GsTitaniumArmor.TitaniumBright : GsTitaniumArmor.TitaniumMain,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        //==================== 绘制：细长镜刃 + 镜面流光 + 收束残影 ====================

        private void DrawEdge(Vector2 pos, float rotation, float alpha, float lengthScale) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot == null) {
                return;
            }
            Vector2 origin = shot.Size() * 0.5f;
            //冷钢衬
            Main.EntitySpriteDraw(shot, pos, null,
                (GsTitaniumArmor.TitaniumDeep with { A = 0 }) * (0.9f * alpha), rotation, origin,
                new Vector2(0.46f * lengthScale, 0.11f), SpriteEffects.None, 0);
            //镜面主刃
            Main.EntitySpriteDraw(shot, pos, null,
                (GsTitaniumArmor.TitaniumMain with { A = 0 }) * alpha, rotation, origin,
                new Vector2(0.38f * lengthScale, 0.07f), SpriteEffects.None, 0);
            //亮刃线
            Main.EntitySpriteDraw(shot, pos, null,
                (GsTitaniumArmor.TitaniumBright with { A = 0 }) * (0.9f * alpha), rotation, origin,
                new Vector2(0.30f * lengthScale, 0.032f), SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarGlow01?.Value;
            float fade = VisualFade;
            float lengthScale = Holding
                ? 0.85f + MathF.Sin(Life * 0.5f + Seed) * 0.04f
                : 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.022f, 0f, 0.55f);

            if (!Holding) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.3f * fade;
                    DrawEdge(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                        Projectile.rotation, ghost, lengthScale * (1f - i * 0.05f));
                }
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            DrawEdge(pos, Projectile.rotation, fade, lengthScale);

            //镜面流光：一点银辉沿刃身来回扫掠
            if (star != null) {
                float sweep = MathF.Sin(Life * 0.35f + Seed * 4f);
                Vector2 gleam = pos + Projectile.rotation.ToRotationVector2() * sweep * 30f * lengthScale;
                Main.EntitySpriteDraw(star, gleam, null,
                    (GsTitaniumArmor.TitaniumBright with { A = 0 }) * (0.75f * fade), 0f, star.Size() * 0.5f,
                    0.22f + (Holding ? 0.06f : 0f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
