using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 集结场：集结指令下立于旗点的驻场判定弹幕，四形态共用一类。<br/>
    /// ai[0] = 形态（0 胶垛 / 1 盘旋鸟群 / 2 雪障 / 3 万剑门），
    /// ai[1] = 空闲，ai[2] = 绑定的仆从弹幕类型（续命条件）。全部初值经 NewProjectile 形参传入。<br/>
    /// 续命各端确定性判定：集结态 + 绑定仆从在场 + 模式开启，任一不满足即全端同步过期；
    /// 剑门（形态 3）为 60 帧一次性，不续命
    /// </summary>
    internal class GsRallyFieldProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        internal const int StanceGelMound = 0;
        internal const int StanceFlock = 1;
        internal const int StanceSnowDrift = 2;
        internal const int StanceBladeGate = 3;

        //胶垛蓝凝胶色板
        private static readonly Color GelBlue = new(96, 148, 255);
        private static readonly Color GelDeep = new(46, 76, 170);
        //雀羽暖光
        private static readonly Color FeatherWarm = new(255, 208, 128);
        //雪障霜色
        private static readonly Color SnowPale = new(214, 238, 255);
        //剑门金橙
        private static readonly Color BladeGold = new(255, 206, 110);

        private int Stance => (int)Projectile.ai[0];

        private int BoundMinionType => (int)Projectile.ai[2];

        private float Seed => Projectile.identity * 0.5813f % MathHelper.TwoPi;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            //首帧：节拍参数按形态落位（ai 随生成包，各端一致）+ 立场音（AI 各端都跑，远端也可闻）
            if (Life == 1f) {
                if (Stance is StanceBladeGate or StanceFlock) {
                    Projectile.localNPCHitCooldown = 20;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item46 with { Volume = 0.4f, Pitch = 0.1f },
                        Projectile.Center);
                }
            }

            //剑门为 60 帧一次性：owner 端到时收门（Kill 广播兜底远端），不走续命
            if (Stance == StanceBladeGate) {
                if (Life >= 60f && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }
            //续命：各端按同一条件判定
            else if (Projectile.timeLeft < 90
                && GameModeSystem.GodSmithActive
                && MinionDoctrine.GetCommand(Projectile.owner) == MinionDoctrine.CommandRally
                && OwnerKeepsMinion()) {
                Projectile.timeLeft = 150;
            }

            if (VaultUtils.isServer) {
                return;
            }
            //形态粒子（持续每帧 ≤2，identity 去同相）
            switch (Stance) {
                case StanceGelMound:
                    Lighting.AddLight(Projectile.Center, GelBlue.ToVector3() * 0.16f);
                    if (Main.rand.NextBool(11)) {
                        PRTLoader.NewParticle<PRT_FarmGelGlob>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-42f, 42f), 8f),
                            new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.8f)),
                            GelBlue, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 26));
                    }
                    break;
                case StanceFlock:
                    Lighting.AddLight(Projectile.Center, FeatherWarm.ToVector3() * 0.14f);
                    if (Main.rand.NextBool(5)) {
                        float orbitAng = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 at = Projectile.Center + orbitAng.ToRotationVector2() * 110f;
                        PRTLoader.NewParticle<PRT_Light>(at,
                            orbitAng.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2.6f,
                            FeatherWarm, Main.rand.NextFloat(0.09f, 0.15f))?.Configure(12, 0.7f);
                    }
                    break;
                case StanceSnowDrift:
                    Lighting.AddLight(Projectile.Center, SnowPale.ToVector3() * 0.12f);
                    if (Main.rand.NextBool(6)) {
                        Dust snow = Dust.NewDustDirect(Projectile.Center - new Vector2(50f, 20f),
                            100, 40, DustID.Snow, 0f, -0.6f, 60, default, 1.1f);
                        snow.noGravity = Main.rand.NextBool();
                    }
                    if (Main.rand.NextBool(18)) {
                        PRTLoader.NewParticle<PRT_DefFrostGlint>(
                            Projectile.Center + Main.rand.NextVector2Circular(46f, 24f),
                            new Vector2(0f, -0.4f), SnowPale,
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 22));
                    }
                    break;
                case StanceBladeGate:
                    Lighting.AddLight(Projectile.Center, BladeGold.ToVector3() * 0.2f);
                    if (Main.rand.NextBool(4)) {
                        PRTLoader.NewParticle<PRT_Spark>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-16f, 16f),
                                Main.rand.NextFloat(-55f, 55f)),
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.5f, 1.4f)),
                            BladeGold, Main.rand.NextFloat(0.2f, 0.38f))?.Configure(false, Main.rand.Next(8, 14));
                    }
                    break;
            }
        }

        /// <summary>绑定武器的仆从仍在场（ownedProjectileCounts 各端一致维护）</summary>
        private bool OwnerKeepsMinion() {
            Player owner = Main.player[Projectile.owner];
            return owner.active && BoundMinionType > 0
                && owner.ownedProjectileCounts[BoundMinionType] > 0;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle zone = Stance switch {
                //胶垛：旗点贴地的弹性胶堆
                StanceGelMound => CenteredRect(96, 52, 14),
                //鸟群：盘旋圈用大方形近似（视觉半径 120）
                StanceFlock => CenteredRect(220, 200, 0),
                //雪障：横向雪堆
                StanceSnowDrift => CenteredRect(110, 66, 6),
                //剑门：竖立门框
                _ => CenteredRect(34, 116, 0),
            };
            return zone.Intersects(targetHitbox);
        }

        private Rectangle CenteredRect(int width, int height, int sink)
            => new((int)(Projectile.Center.X - width / 2f),
                (int)(Projectile.Center.Y - height / 2f + sink), width, height);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch (Stance) {
                case StanceGelMound:
                    target.AddBuff(BuffID.Slimed, 120);
                    break;
                case StanceSnowDrift:
                    target.AddBuff(BuffID.Frostburn, 180);
                    break;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //命中反馈（≤6 粒）
            Color hue = Stance switch {
                StanceGelMound => GelBlue,
                StanceFlock => FeatherWarm,
                StanceSnowDrift => SnowPale,
                _ => BladeGold,
            };
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    hue, Main.rand.NextFloat(0.25f, 0.45f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制（identity 定相，禁 Main.rand） ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            float fadeIn = MathHelper.Clamp(Life / 10f, 0f, 1f);
            //剑门以本地 Life 收口（timeLeft 只作续命载体），持续场以 timeLeft 收口
            float fadeOut = Stance == StanceBladeGate
                ? MathHelper.Clamp((60f - Life) / 12f, 0f, 1f)
                : MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            switch (Stance) {
                case StanceGelMound: {
                    //弹性胶垛：三球堆叠 + 张力呼吸
                    float wob = 0.1f * (float)Math.Sin(Life * 0.11f + Seed);
                    Vector2 jiggle = new(1f + wob, 1f - wob);
                    Main.EntitySpriteDraw(soft, pos + new Vector2(0f, 14f), null,
                        GelDeep * (0.75f * fade), 0f, soft.Size() / 2f,
                        new Vector2(0.72f, 0.34f) * jiggle, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(soft, pos + new Vector2(-18f, 2f), null,
                        GelBlue * (0.6f * fade), 0.3f, soft.Size() / 2f,
                        new Vector2(0.36f, 0.3f) * jiggle, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(soft, pos + new Vector2(16f, 0f), null,
                        GelBlue * (0.6f * fade), -0.25f, soft.Size() / 2f,
                        new Vector2(0.4f, 0.32f) * jiggle, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, pos, null,
                        (GelBlue with { A = 0 }) * (0.3f * fade), 0f, glow.Size() / 2f,
                        new Vector2(1.1f, 0.6f), SpriteEffects.None, 0);
                    break;
                }
                case StanceFlock: {
                    //盘旋鸟群：三点环转暖光（粒子承载主体，这里画轨道暗示）
                    for (int i = 0; i < 3; i++) {
                        float ang = Seed + Life * 0.05f + MathHelper.TwoPi * i / 3f;
                        Vector2 orbit = pos + ang.ToRotationVector2() * 110f
                            * new Vector2(1f, 0.55f);
                        Main.EntitySpriteDraw(glow, orbit, null,
                            (FeatherWarm with { A = 0 }) * (0.5f * fade), 0f,
                            glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
                    }
                    Main.EntitySpriteDraw(glow, pos, null,
                        (FeatherWarm with { A = 0 }) * (0.22f * fade), 0f, glow.Size() / 2f,
                        new Vector2(2.4f, 1.4f), SpriteEffects.None, 0);
                    break;
                }
                case StanceSnowDrift: {
                    //雪堆：真 alpha 白堆双层
                    Main.EntitySpriteDraw(soft, pos + new Vector2(0f, 12f), null,
                        Color.White * (0.8f * fade), 0f, soft.Size() / 2f,
                        new Vector2(0.85f, 0.4f), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(soft, pos + new Vector2(6f, -4f), null,
                        SnowPale * (0.55f * fade), 0.2f, soft.Size() / 2f,
                        new Vector2(0.5f, 0.3f), SpriteEffects.None, 0);
                    break;
                }
                case StanceBladeGate: {
                    //万剑门：两列匕首立成门框，门楣星芒
                    Main.instance.LoadProjectile(ProjectileID.Smolstar);
                    Texture2D dagger = Terraria.GameContent.TextureAssets
                        .Projectile[ProjectileID.Smolstar].Value;
                    for (int i = 0; i < 4; i++) {
                        float y = -42f + i * 28f;
                        float sway = 0.08f * (float)Math.Sin(Life * 0.13f + Seed + i);
                        Main.EntitySpriteDraw(dagger, pos + new Vector2(-15f, y), null,
                            Color.White * fade, -MathHelper.PiOver2 * 0.06f + sway,
                            dagger.Size() / 2f, 0.9f, SpriteEffects.None, 0);
                        Main.EntitySpriteDraw(dagger, pos + new Vector2(15f, y), null,
                            Color.White * fade, MathHelper.PiOver2 * 0.06f - sway,
                            dagger.Size() / 2f, 0.9f, SpriteEffects.FlipHorizontally, 0);
                    }
                    Texture2D flare = CWRAsset.StarFlare01?.Value;
                    if (flare != null) {
                        float breathe = 0.85f + 0.15f * (float)Math.Sin(Life * 0.2f + Seed);
                        Main.EntitySpriteDraw(flare, pos + new Vector2(0f, -64f), null,
                            (BladeGold with { A = 0 }) * (0.85f * fade), Seed,
                            flare.Size() / 2f, 0.3f * breathe, SpriteEffects.None, 0);
                    }
                    Main.EntitySpriteDraw(glow, pos, null,
                        (BladeGold with { A = 0 }) * (0.3f * fade), 0f, glow.Size() / 2f,
                        new Vector2(0.5f, 1.2f), SpriteEffects.None, 0);
                    break;
                }
            }
            return false;
        }
    }
}
