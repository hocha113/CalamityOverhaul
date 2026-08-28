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
    /// 【化石套·骨龄尘暴】（P10a 移交，键族归 ArmorsB）亿年骨层的苏醒：
    /// ①命中积攒骨尘，满六层后下一击唤醒地底骨龄 ②自目标脚下依次拱起三根骨刺串刺，
    /// 先冒尘预告再破土 ③骨刺渐远渐高，扫出一条骨墙走廊。原版套装奖励（投掷强化）保留，神赋叠加
    /// </summary>
    internal class GsFossilArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.FossilHelm];

        public override int BodyID => ItemID.FossilShirt;

        public override int LegsID => ItemID.FossilPants;

        protected override string EndowLineFallback =>
            "Boneage Wake: strikes build bone dust; at 6 stacks the next strike wakes the strata, thrusting three bone spurs up from the ground in a marching row";

        //化石骨白 + 琥珀色板
        internal static readonly Color BoneWhite = new(236, 226, 200);
        internal static readonly Color FossilAmber = new(212, 172, 92);
        internal static readonly Color FossilDeep = new(112, 86, 52);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => FossilAmber;

        protected override Color ThemeBright => BoneWhite;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsFossilBoneSpurProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.6f }, target.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int spurDamage = Math.Clamp((int)(damageDone * 0.40f), 8, 80);
            float march = target.Center.X >= player.Center.X ? 1f : -1f;
            for (int i = 0; i < 3; i++) {
                //逐根远去：目标脚下起，沿走向排开
                Vector2 probe = target.Bottom + new Vector2(march * i * 56f, -4f);
                Point tile = probe.ToTileCoordinates();
                bool grounded = false;
                for (int dy = -2; dy < 10; dy++) {
                    Point at = new(tile.X, tile.Y + dy);
                    if (!WorldGen.InWorld(at.X, at.Y, 10)) {
                        break;
                    }
                    Tile t = Framing.GetTileSafely(at.X, at.Y);
                    if (t.HasTile && Main.tileSolid[t.TileType]) {
                        probe = new Vector2(at.X * 16f + 8f, at.Y * 16f);
                        grounded = true;
                        break;
                    }
                }
                if (!grounded) {
                    continue;
                }
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithFossilEndow"),
                    probe, Vector2.Zero,
                    ModContent.ProjectileType<GsFossilBoneSpurProj>(),
                    spurDamage, 4f, player.whoAmI, 0f, 0f, i * 8f);
            }
        }
    }

    /// <summary>
    /// 骨刺：地底拱起的亿年椎骨，先冒尘预告十帧、再破土猛刺、驻留后缩回；
    /// 刺身骨白三层 + 琥珀纹理，破土时掀起骨尘
    /// </summary>
    internal class GsFossilBoneSpurProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>错拍延迟（随生成参数过线）</summary>
        private ref float Delay => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.7451f % 3.47f;

        /// <summary>预告帧数（冒尘）</summary>
        private const int TelegraphFrames = 10;

        /// <summary>破土帧数</summary>
        private const int ThrustFrames = 6;

        /// <summary>驻留帧数</summary>
        private const int HoldFrames = 22;

        /// <summary>刺高</summary>
        private const float SpurHeight = 96f;

        /// <summary>0~1 的出土进度</summary>
        private float Emergence {
            get {
                float t = Life - Delay - TelegraphFrames;
                if (t <= 0f) {
                    return 0f;
                }
                if (t <= ThrustFrames) {
                    float k = t / ThrustFrames;
                    return 1f - (1f - k) * (1f - k);
                }
                if (t <= ThrustFrames + HoldFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (t - ThrustFrames - HoldFrames) / 10f, 0f, 1f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
        }

        /// <summary>只在破土猛刺段判定</summary>
        public override bool? CanDamage() {
            float t = Life - Delay - TelegraphFrames;
            return t > 0f && t <= ThrustFrames + 4f;
        }

        /// <summary>命中盒跟随出土高度（自基点向上）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = SpurHeight * Emergence;
            Rectangle spur = new((int)(Projectile.Center.X - 13f), (int)(Projectile.Center.Y - h), 26, (int)h);
            return spur.Intersects(targetHitbox);
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            float t = Life - Delay;
            if (t < 0f) {
                return;
            }
            //预告：地表冒骨尘
            if (!Main.dedServ && t <= TelegraphFrames && Life % 2 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                    DustID.Sand, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1f, 2.5f)),
                    100, GsFossilArmor.FossilAmber, 1.1f);
                d.noGravity = false;
            }
            //破土瞬间：掀尘 + 闷响
            if (!Main.dedServ && (int)t == TelegraphFrames + 1) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 7; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 5f)),
                        Main.rand.NextBool() ? GsFossilArmor.BoneWhite : GsFossilArmor.FossilAmber,
                        Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(14, 24));
                }
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, -Vector2.UnitY * 1.2f,
                    GsFossilArmor.FossilDeep, 0.45f)?.Configure(18, 0.4f, 0.04f);
            }
            if (Emergence > 0f) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, 40f * Emergence),
                    GsFossilArmor.FossilAmber.ToVector3() * 0.14f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1.5f, 4f)),
                    Main.rand.NextBool() ? GsFossilArmor.BoneWhite : GsFossilArmor.FossilAmber,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制：破土椎骨（主刺 + 两根侧棘），出土高度驱动 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            if (shot == null) {
                return false;
            }
            float rise = Emergence;
            if (rise <= 0f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float lean = MathF.Sin(Seed * 4f) * 0.12f;
            float upRot = -MathHelper.PiOver2 + lean;

            //主刺：骨白三层，自基点向上（origin 挪到尾端使其从地里长出）
            Main.EntitySpriteDraw(shot, basePos, null,
                GsFossilArmor.FossilDeep * 0.9f, upRot, new Vector2(0f, shot.Height * 0.5f),
                new Vector2(SpurHeight / shot.Width * 1.1f * rise, 0.12f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, basePos, null,
                (GsFossilArmor.FossilAmber with { A = 0 }) * 0.85f, upRot, new Vector2(0f, shot.Height * 0.5f),
                new Vector2(SpurHeight / shot.Width * rise, 0.085f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(shot, basePos, null,
                (GsFossilArmor.BoneWhite with { A = 0 }) * 0.9f, upRot, new Vector2(0f, shot.Height * 0.5f),
                new Vector2(SpurHeight / shot.Width * 0.9f * rise, 0.045f), SpriteEffects.None, 0);
            //两根侧棘（矮，外倾）
            for (int i = -1; i <= 1; i += 2) {
                Main.EntitySpriteDraw(shot, basePos + new Vector2(i * 10f, 0f), null,
                    (GsFossilArmor.BoneWhite with { A = 0 }) * 0.7f, upRot + i * 0.32f, new Vector2(0f, shot.Height * 0.5f),
                    new Vector2(SpurHeight * 0.45f / shot.Width * rise, 0.05f), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
