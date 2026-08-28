using CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode;
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
    /// 【角斗士套·凯旋标枪】（P10a 移交，键族归 ArmorsB）竞技场的喝彩化为投械：
    /// ①命中积攒喝彩，满六层后下一击自肩上依次掷出三支青铜标枪
    /// ②标枪走抛物弧线钉向目标，枪尖破空带啸 ③着弹钉入迸出青铜火星。
    /// 原版无套装奖励，神赋即是它的第一件套装奖励
    /// </summary>
    internal class GsGladiatorArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.GladiatorHelmet];

        public override int BodyID => ItemID.GladiatorBreastplate;

        public override int LegsID => ItemID.GladiatorLeggings;

        protected override string EndowLineFallback =>
            "Triumph Volley: strikes build acclaim; at 6 stacks the next strike hurls three bronze javelins over your shoulder in arcing volleys at the foe";

        //青铜 + 竞技红色板
        internal static readonly Color BronzeBright = new(255, 232, 172);
        internal static readonly Color BronzeGold = new(222, 172, 92);
        internal static readonly Color BronzeDeep = new(132, 92, 42);
        internal static readonly Color ArenaRed = new(202, 62, 52);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => BronzeGold;

        protected override Color ThemeBright => BronzeBright;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsGladiatorJavelinProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.25f }, player.Center);
                //掷前喝彩：青铜火星自肩迸起
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + new Vector2(0f, -14f),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 2.5f)),
                        i % 2 == 0 ? BronzeBright : ArenaRed, 0.35f)?.Configure(false, 14);
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int javelinDamage = Math.Clamp((int)(damageDone * 0.35f), 7, 75);
            Vector2 shoulder = player.Center + new Vector2(-player.direction * 8f, -18f);
            for (int i = 0; i < 3; i++) {
                //抛物弧：抬高出手角，重力自然落向目标；三支错拍不同弧
                Vector2 flat = target.Center - shoulder;
                float dist = MathHelper.Clamp(flat.Length(), 60f, 700f);
                Vector2 dir = flat.SafeNormalize(Vector2.UnitX);
                float speed = 10f + dist * 0.008f + i * 0.8f;
                //上抬量随距离/序号变化，三弧分层
                Vector2 vel = dir * speed - Vector2.UnitY * (3.2f + i * 1.1f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithGladiatorEndow"),
                    shoulder, vel, ModContent.ProjectileType<GsGladiatorJavelinProj>(),
                    javelinDamage, 3f, player.whoAmI, 0f, 0f, i * 6f);
            }
        }
    }

    /// <summary>
    /// 凯旋标枪：肩上掷出的青铜标枪，错拍出手、抛物坠向目标；
    /// 枪身青铜三层 + 缨穗红点 + 破空啸线，着弹钉入迸青铜火星
    /// </summary>
    internal class GsGladiatorJavelinProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>错拍延迟帧（随生成参数过线）</summary>
        private ref float HoldFrames => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.9421f % 4.11f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        /// <summary>错拍持枪期不判定</summary>
        public override bool? CanDamage() => Life >= HoldFrames;

        public override void AI() {
            Life++;
            //错拍：悬持蓄势
            if (Life < HoldFrames) {
                Projectile.position -= Projectile.velocity;
                Projectile.rotation = Projectile.velocity.ToRotation();
                return;
            }
            if ((int)Life == (int)HoldFrames && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 4 }, Projectile.Center);
            }
            //抛物坠落
            Projectile.velocity.Y += 0.24f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ && Life % 3 == 0) {
                //破空啸线
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.04f, GsGladiatorArmor.BronzeBright,
                    Main.rand.NextFloat(0.14f, 0.24f))?.Configure(false, Main.rand.Next(6, 10));
            }
            Lighting.AddLight(Projectile.Center, GsGladiatorArmor.BronzeGold.ToVector3() * (0.12f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = 0.35f, MaxInstances = 4 }, target.Center);
            //青铜火星钉入迸溅
            Vector2 inDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    (-inDir).RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsGladiatorArmor.BronzeBright : GsGladiatorArmor.BronzeGold,
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    GsGladiatorArmor.BronzeGold, Main.rand.NextFloat(0.2f, 0.32f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //==================== 绘制：青铜枪身三层 + 缨穗红点 + 飞行残影 ====================

        private void DrawJavelin(Vector2 pos, float rotation, float alpha) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (shot == null || star == null) {
                return;
            }
            Vector2 origin = shot.Size() * 0.5f;
            //枪杆深铜
            Main.EntitySpriteDraw(shot, pos, null,
                (GsGladiatorArmor.BronzeDeep with { A = 0 }) * (0.9f * alpha), rotation, origin,
                new Vector2(0.30f, 0.055f), SpriteEffects.None, 0);
            //枪身青铜
            Main.EntitySpriteDraw(shot, pos, null,
                (GsGladiatorArmor.BronzeGold with { A = 0 }) * alpha, rotation, origin,
                new Vector2(0.25f, 0.035f), SpriteEffects.None, 0);
            //枪尖亮芒
            Main.EntitySpriteDraw(shot, pos + rotation.ToRotationVector2() * 12f, null,
                (GsGladiatorArmor.BronzeBright with { A = 0 }) * (0.85f * alpha), rotation, origin,
                new Vector2(0.10f, 0.02f), SpriteEffects.None, 0);
            //缨穗红点（枪尾）
            Main.EntitySpriteDraw(star, pos - rotation.ToRotationVector2() * 15f, null,
                (GsGladiatorArmor.ArenaRed with { A = 0 }) * (0.8f * alpha), 0f, star.Size() * 0.5f,
                0.13f, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = VisualFade;
            if (Life >= HoldFrames) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.28f * fade;
                    DrawJavelin(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                        Projectile.rotation, ghost);
                }
            }
            else {
                //持枪蓄势微闪（identity 相位）
                fade *= 0.7f + MathF.Sin(Life * 0.8f + Seed * 5f) * 0.3f;
            }
            DrawJavelin(Projectile.Center - Main.screenPosition, Projectile.rotation, fade);
            return false;
        }
    }
}
