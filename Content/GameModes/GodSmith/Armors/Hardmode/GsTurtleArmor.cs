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
    /// 【龟甲套·棘壳圣域】巨龟之壳锻成的堡垒甲：①命中攒一格甲怒、受击攒两格
    /// ②满六格自动罩起龟壳圣域四秒 ③域内每次受击立即迸射八向棘刺环反击（棘刺以甲为锋，随防御变强）；
    /// 圣域全程未被打破一次，谢幕时主动迸发一轮棘刺。原版套装奖励（荆棘反伤）保留，神赋叠加
    /// </summary>
    internal class GsTurtleArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.TurtleHelmet];

        public override int BodyID => ItemID.TurtleScaleMail;

        public override int LegsID => ItemID.TurtleLeggings;

        protected override string EndowLineFallback =>
            "Spiked Sanctum: strikes build fury by 1, taking hits by 2; at 6 fury a turtle-shell dome rises for 4s, and every hit taken inside erupts an eight-way spike ring scaling with your defense";

        //龟甲绿 + 棘白色板
        internal static readonly Color TurtleBright = new(222, 255, 212);
        internal static readonly Color TurtleMain = new(112, 190, 112);
        internal static readonly Color TurtleDeep = new(52, 102, 62);
        internal static readonly Color SpikeBone = new(240, 236, 214);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => TurtleMain;

        protected override Color ThemeBright => TurtleBright;

        protected override bool ChargeOnHurt => true;

        protected override int ChargePerHurt => 2;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsTurtleBulwarkDomainProj>()
            || proj.type == ModContent.ProjectileType<GsTurtleBulwarkSpikeProj>();

        private static Projectile FindDome(Player player) {
            int type = ModContent.ProjectileType<GsTurtleBulwarkDomainProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>棘刺环：以佩戴者为心八向迸发（佩戴者端裁定）</summary>
        internal static void SpikeBurst(Player player, int hurtDamage) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.3f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //刺随甲坚：防御与所受一击共同折算
            int spikeDamage = Math.Clamp(player.statDefense + hurtDamage / 2, 20, 150);
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f + MathHelper.PiOver4 * 0.5f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithTurtleEndow"),
                    player.Center + ang.ToRotationVector2() * 20f,
                    ang.ToRotationVector2() * 11f,
                    ModContent.ProjectileType<GsTurtleBulwarkSpikeProj>(),
                    spikeDamage, 3f, player.whoAmI);
            }
        }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (state.EndowCharge < FullCharge) {
                return;
            }
            //满怒自动成罩；域已在场则持怒等待
            if (FindDome(player) != null) {
                return;
            }
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = -0.5f }, player.Center);
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + ang.ToRotationVector2() * 30f,
                        ang.ToRotationVector2() * 1.5f, TurtleMain, 0.4f)?.Configure(false, 16);
                }
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithTurtleEndow"),
                    player.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsTurtleBulwarkDomainProj>(),
                    0, 0f, player.whoAmI);
            }
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            //命中恰逢满怒的时序兜底：交还层数，由 UpdateEndowment 统一成罩
            state.EndowCharge = FullCharge;
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            Projectile dome = FindDome(player);
            if (dome != null) {
                //域内受击：立即棘刺环反击（30 帧内不重复触发）
                if (Main.GameUpdateCount >= state.EndowTimer) {
                    state.EndowTimer = Main.GameUpdateCount + 30;
                    dome.ai[1] = 1f;
                    dome.netUpdate = true;
                    SpikeBurst(player, info.Damage);
                }
                return;
            }
            base.OnEndowHurt(player, state, info);
        }
    }

    /// <summary>
    /// 龟壳圣域：罩住佩戴者的一层甲片能量壳，龟纹网格缓旋 + 域界呼吸 + 顶脊高光；
    /// 全程未被打破一次，则谢幕时主动迸发一轮棘刺（ai[1] 标记是否已反击过）
    /// </summary>
    internal class GsTurtleBulwarkDomainProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_193";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>1=本次圣域已触发过反击</summary>
        private ref float Retaliated => ref Projectile.ai[1];

        private float Seed => Projectile.identity * 0.6323f % 3.03f;

        /// <summary>壳罩半径</summary>
        private const float Radius = 96f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 12f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>壳罩本体不伤人，棘刺才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.3f);
            Projectile.velocity = Vector2.Zero;

            //壳面碎光（客户端装饰）
            if (!Main.dedServ && Main.rand.NextBool(8)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + ang.ToRotationVector2() * Radius * Main.rand.NextFloat(0.9f, 1f),
                    (ang + MathHelper.PiOver2).ToRotationVector2() * 0.8f,
                    GsTurtleArmor.TurtleBright, 0.28f)?.Configure(false, 14);
            }
            Lighting.AddLight(Projectile.Center, GsTurtleArmor.TurtleMain.ToVector3() * (0.25f * VisualFade));
        }

        public override void OnKill(int timeLeft) {
            Player owner = Main.player[Projectile.owner];
            //全程未破防：谢幕主动迸发（佩戴者端裁定）
            if (Retaliated == 0f && owner.active && !owner.dead && Projectile.owner == Main.myPlayer) {
                GsTurtleArmor.SpikeBurst(owner, 0);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2CircularEdge(Radius * 0.8f, Radius * 0.8f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f),
                    GsTurtleArmor.TurtleMain, Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制：龟纹网格壳 + 域界环 + 顶脊高光 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D grid = CWRAsset.Extra_193?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (grid == null || ring == null || glow == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float breathe = 1f + MathF.Sin(Life * 0.055f + Seed * 2f) * 0.025f;
            float scale = Radius * 2f * breathe / grid.Width;

            //龟纹甲片网格（缓旋，加色成能量壳）
            Main.EntitySpriteDraw(grid, pos, null,
                (GsTurtleArmor.TurtleMain with { A = 0 }) * (0.38f * fade), Life * 0.006f + Seed, grid.Size() * 0.5f,
                scale, SpriteEffects.None, 0);
            //反相细纹（逆旋一层，网格交叠出甲片感）
            Main.EntitySpriteDraw(grid, pos, null,
                (GsTurtleArmor.TurtleDeep with { A = 0 }) * (0.3f * fade), -Life * 0.004f - Seed, grid.Size() * 0.5f,
                scale * 0.9f, SpriteEffects.None, 0);
            //域界环
            Main.EntitySpriteDraw(ring, pos, null,
                (GsTurtleArmor.TurtleBright with { A = 0 }) * (0.4f * fade), 0f, ring.Size() * 0.5f,
                Radius * 2f * breathe / ring.Width, SpriteEffects.None, 0);
            //顶脊高光
            Main.EntitySpriteDraw(glow, pos - new Vector2(0f, Radius * 0.7f), null,
                (GsTurtleArmor.TurtleBright with { A = 0 }) * (0.35f * fade), 0f, glow.Size() * 0.5f,
                new Vector2(1.4f, 0.5f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 龟棘：壳缘迸出的骨白棘刺，出手快、途中缓（先声后坠），命中掷出甲屑
    /// </summary>
    internal class GsTurtleBulwarkSpikeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 26;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //出手快、途中骤缓
            if (Life > 8f) {
                Projectile.velocity *= 0.9f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsTurtleArmor.TurtleBright.ToVector3() * 0.1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsTurtleArmor.SpikeBone : GsTurtleArmor.TurtleMain,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制：骨白短棘 + 速度拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = shot.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.025f, 0.02f, 0.35f);

            Main.EntitySpriteDraw(shot, pos, null,
                (GsTurtleArmor.TurtleDeep with { A = 0 }) * (0.8f * fade), Projectile.rotation, origin,
                new Vector2(0.22f + stretch, 0.075f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, pos, null,
                (GsTurtleArmor.SpikeBone with { A = 0 }) * fade, Projectile.rotation, origin,
                new Vector2(0.17f + stretch * 0.7f, 0.045f), SpriteEffects.None, 0);
            return false;
        }
    }
}
