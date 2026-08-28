using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 霰弹枪重铸：喉径两态 + 铅弹质感。材质：胡桃木托猎用霰弹枪与灰橙铅弹。<br/>
    /// 签名行为：①宽喉弹粒带铅坠弧线与铅灰曳光，单次射击命中 3 次即在目标上空
    /// 炸开「铅幕」8 粒坠落铅屑 ②收颈 3 粒全中同一目标挂「铅蚀」1.5 秒（受本枪 +12%）
    /// ③宽喉大烟锥、收颈短促火舌的枪口分野。<br/>
    /// [宽喉]：6 粒 ±12 度宽扇；[收颈]：3 粒 ±3 度精束每粒 +30%，锥形瞄准线常亮。
    /// 两档一次 use 都只耗 1 发弹药，粒数由接管生成自控
    /// </summary>
    internal class GsShotgun : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Shotgun;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch choke\n" +
            "Wide Bore throws six drooping pellets in a broad fan; landing 3 pellet hits in one blast bursts a rain of lead over the target\n" +
            "Tight Choke fires three heavy pellets dead straight; all three into one foe corrodes it, taking 12% more from this gun\n" +
            "Every trigger pull still costs one shell";

        /// <summary>铅灰</summary>
        internal static readonly Color LeadGray = new(178, 172, 168);
        /// <summary>灼橙</summary>
        internal static readonly Color LeadEmber = new(255, 168, 90);

        /// <summary>本次 use 的命中记账（owner 攻击链独占）：总命中数 / 同目标命中数 / 铅幕闩</summary>
        private int useHitCount;
        private int useHitTarget = -1;
        private int useHitOnTarget;
        private bool leadRainFired;

        /// <summary>铅蚀窗口：NPC 编号 → (类型, 截止帧)。owner 本地量，攻击方端结算</summary>
        private readonly uint[] erodeUntil = new uint[Main.maxNPCs + 1];
        private readonly int[] erodeType = new int[Main.maxNPCs + 1];

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeWideBore", EnName = "Wide Bore",
                DamageMul = 0.73f,
            },
            new GsFireMode {
                Key = "ModeTightChoke", EnName = "Tight Choke",
                DamageMul = 1.30f,
                AimLine = GsAimLineKind.Cone, AimConeHalfAngle = MathHelper.ToRadians(3f),
            },
        ];

        //猎枪后坐：全族最重的一挫
        protected override float RecoilShift => 6f;
        protected override float RecoilKick => 0.08f;

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //新 use 开账
            useHitCount = 0;
            useHitTarget = -1;
            useHitOnTarget = 0;
            leadRainFired = false;
            //接管粒数：宽喉 6 粒宽扇 / 收颈 3 粒窄束（damage 已按档摊薄或增幅）
            int pellets = mp.ModeIndex == 0 ? 6 : 3;
            float halfSpread = mp.ModeIndex == 0 ? MathHelper.ToRadians(12f) : MathHelper.ToRadians(3f);
            for (int i = 0; i < pellets; i++) {
                Vector2 pelletVel = velocity.RotatedBy(Main.rand.NextFloat(-halfSpread, halfSpread))
                    * Main.rand.NextFloat(0.94f, 1.06f);
                Projectile.NewProjectile(source, position, pelletVel, type, damage, knockback, player.whoAmI);
            }
            if (!VaultUtils.isServer) {
                MuzzleVisual(position, velocity, wide: mp.ModeIndex == 0);
            }
            return false;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //弹粒记档：飞行相的坠弧与曳光按档分野
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            router.MarkData = PackMark(mp.ModeIndex);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type == ModContent.ProjectileType<GsShotgunLeadRainProj>()) {
                return;
            }
            //宽喉铅坠：恒定微重力（确定性输入，各端同弧）
            if (MarkModeOf(router.MarkData) == 0) {
                proj.velocity.Y += 0.045f;
            }
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type == ModContent.ProjectileType<GsShotgunLeadRainProj>()) {
                return;
            }
            //铅灰曳光：短促速度拉伸光带压在原版弹粒之上（原版贴图垫底）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float speed = proj.velocity.Length();
            if (speed < 2f) {
                return;
            }
            float stretch = MathHelper.Clamp(speed * 0.06f, 0.4f, 1.4f);
            Color c = Color.Lerp(LeadGray, LeadEmber, 0.3f) * 0.55f;
            c.A = 0;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null, c,
                proj.velocity.ToRotation(), glow.Size() / 2f,
                new Vector2(0.20f * stretch, 0.05f), SpriteEffects.None, 0);
        }

        /// <summary>攻击方端：弹粒命中记账，触发铅幕与铅蚀</summary>
        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type == ModContent.ProjectileType<GsShotgunLeadRainProj>()
                || target.friendly) {
                return;
            }
            useHitCount++;
            if (useHitTarget == target.whoAmI) {
                useHitOnTarget++;
            }
            else {
                useHitTarget = target.whoAmI;
                useHitOnTarget = 1;
            }
            int mode = MarkModeOf(router.MarkData);
            //宽喉铅幕：单次射击命中满 3 粒，目标上空炸开 8 粒坠落铅屑
            if (mode == 0 && !leadRainFired && useHitCount >= 3) {
                leadRainFired = true;
                Player player = Main.player[proj.owner];
                for (int i = 0; i < 8; i++) {
                    Vector2 pos = target.Center + new Vector2(Main.rand.NextFloat(-70f, 70f),
                        -Main.rand.NextFloat(90f, 150f));
                    Vector2 vel = new(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(4f, 7f));
                    Projectile.NewProjectile(proj.GetSource_FromAI(), pos, vel,
                        ModContent.ProjectileType<GsShotgunLeadRainProj>(),
                        Math.Max(1, (int)(proj.damage * 0.22f)), 0.5f, proj.owner);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.4f, Pitch = -0.2f }, target.Center);
                }
            }
            //收颈铅蚀：3 粒全中同一目标
            if (mode == 1 && useHitOnTarget >= 3 && target.whoAmI < erodeUntil.Length) {
                erodeUntil[target.whoAmI] = Main.GameUpdateCount + 90;
                erodeType[target.whoAmI] = target.type;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item52 with { Volume = 0.4f, Pitch = -0.3f }, target.Center);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                            Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f), LeadGray,
                            Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(12, 18));
                    }
                }
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //铅蚀：受本枪一切弹幕 +12%（owner 本地窗口，判定端即攻击方端）
            if (target.whoAmI < erodeUntil.Length
                && erodeUntil[target.whoAmI] > Main.GameUpdateCount
                && erodeType[target.whoAmI] == target.type) {
                modifiers.FinalDamage *= 1.12f;
            }
        }

        internal override void GsGunHeldReset(Player player) {
            useHitCount = 0;
            useHitTarget = -1;
            useHitOnTarget = 0;
            leadRainFired = false;
            Array.Clear(erodeUntil);
            Array.Clear(erodeType);
        }

        /// <summary>枪口演出：宽喉大烟锥、收颈短促火舌（owner 个人反馈）</summary>
        private static void MuzzleVisual(Vector2 muzzle, Vector2 velocity, bool wide) {
            Vector2 unit = velocity.SafeNormalize(Vector2.UnitX);
            int sparkCount = wide ? 4 : 2;
            float sparkSpread = wide ? 0.5f : 0.14f;
            for (int i = 0; i < sparkCount; i++) {
                PRTLoader.NewParticle<PRT_Spark>(muzzle + unit * 16f,
                    unit.RotatedByRandom(sparkSpread) * Main.rand.NextFloat(3f, 6.5f),
                    new Color(255, 196, 110), Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(muzzle + unit * Main.rand.NextFloat(8f, 20f),
                    unit * Main.rand.NextFloat(1f, 2.2f) - Vector2.UnitY * 0.4f,
                    new Color(120, 112, 100), Main.rand.NextFloat(0.35f, wide ? 0.6f : 0.45f))
                    ?.Configure(Main.rand.Next(18, 28), 0.42f, 0.02f);
            }
        }
    }

    /// <summary>
    /// 霰弹枪「铅幕」坠落铅屑：宽喉齐射的第二波打击。
    /// 重力直坠，自绘铅灰亮核 + 坠痕，落地/命中迸铅屑
    /// </summary>
    internal class GsShotgunLeadRainProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithGunsHard";

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.28f, 13f);
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.06f,
                    GsShotgun.LeadGray, Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(false, Main.rand.Next(6, 10));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //铅屑本体 = 拉伸铅灰光带 + 橙热尖（黑底贴图 A=0 加色）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color body = GsShotgun.LeadGray * 0.8f;
            body.A = 0;
            Color tip = GsShotgun.LeadEmber * 0.55f;
            tip.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, body, Projectile.rotation,
                glow.Size() / 2f, new Vector2(0.22f, 0.06f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, tip, Projectile.rotation,
                glow.Size() / 2f, new Vector2(0.10f, 0.04f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(1.8f, 1.2f) - Vector2.UnitY * 0.6f,
                    i == 0 ? GsShotgun.LeadEmber : GsShotgun.LeadGray,
                    Main.rand.NextFloat(0.18f, 0.32f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }
    }
}
