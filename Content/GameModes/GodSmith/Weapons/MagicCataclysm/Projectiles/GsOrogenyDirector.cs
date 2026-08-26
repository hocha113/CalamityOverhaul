using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 大地法杖灾变「造山」：锚定光标处地面。蓄势 35t 地裂纹亮线蔓延；
    /// 爆发 160t 三波岩柱自地面顶升（每波 4 柱错帧 ×1.2）加波间碎石抛射（×0.4）；
    /// 余韵 150t 熔岩脉滩（0.3×/24t）
    /// </summary>
    internal class GsOrogenyDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 35;
        public override int MainTicks => 160;
        public override int AftermathTicks => 150;

        protected override int HitTickRate => 24;

        protected override float TickDamageMul => 0.3f;

        /// <summary>岩柱横向布位</summary>
        private static readonly float[] PillarOffsets = [-195f, -65f, 65f, 195f];
        /// <summary>熔岩脉滩半宽/半高</summary>
        private const float VeinHalfW = 220f;
        private const float VeinHalfH = 16f;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color MagmaOrange = new(255, 140, 52);
        internal static readonly Color MagmaDeep = new(150, 52, 20);

        private static int BoulderType => ContentSamples.ItemsByType[ItemID.StaffofEarth].shoot;

        /// <summary>贴地锚：蓄势首帧探地（tile 各端一致），全程钉在地面</summary>
        protected override void UpdateAnchor() {
            if (Projectile.localAI[2] == 0f) {
                Projectile.localAI[2] = 1f;
                Projectile.localAI[0] = Projectile.Center.X;
                Projectile.localAI[1] = FindGroundY(Projectile.Center);
            }
            Projectile.Center = new Vector2(Projectile.localAI[0], Projectile.localAI[1] - VeinHalfH);
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.9f, Pitch = -0.5f }, Projectile.Center);
            }
            //地裂纹亮线蔓延：火星沿地面自中心向两侧走
            if (!VaultUtils.isServer && t % 2 == 0) {
                float spread = t / (float)OmenTicks * VeinHalfW * 1.6f;
                float x = Main.rand.NextFloat(-spread, spread);
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + new Vector2(x, 4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)),
                    MagmaOrange, Main.rand.NextFloat(0.4f, 0.7f));
            }
            Lighting.AddLight(Projectile.Center, MagmaOrange.ToVector3() * 0.4f * (t / (float)OmenTicks));
        }

        protected override void MainUpdate(int t) {
            int wave = t / 55;
            int waveT = t % 55;
            //波起帧：轰鸣与震土
            if (waveT == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.75f, Pitch = -0.45f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-VeinHalfW, VeinHalfW), 0f),
                        new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(2f, 5f)),
                        MagmaDeep, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(28);
                }
            }
            if (!OwnerSide) {
                return;
            }
            //每波 4 柱错帧 3t 顶升
            if (waveT < PillarOffsets.Length * 3 && waveT % 3 == 0) {
                int idx = waveT / 3;
                float x = Projectile.Center.X + PillarOffsets[idx] + Main.rand.NextFloat(-16f, 16f);
                Vector2 probe = new(x, Projectile.localAI[1] - 60f);
                float groundY = FindGroundY(probe);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), new Vector2(x, groundY), Vector2.Zero,
                    ModContent.ProjectileType<GsEarthPillarProj>(), ScaledDamage(1.2f),
                    Projectile.knockBack, Projectile.owner, 0f, Main.rand.NextFloat(130f, 175f));
            }
            //波间碎石抛射
            if (waveT >= 18 && waveT < 38 && waveT % 5 == 3) {
                Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-VeinHalfW, VeinHalfW), -8f);
                Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), -Main.rand.NextFloat(9f, 12.5f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, vel,
                    BoulderType, ScaledDamage(0.4f), Projectile.knockBack * 0.5f, Projectile.owner);
            }
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.LiquidsHoneyLava with { Volume = 0.8f }, Projectile.Center);
            }
            //熔岩脉滩低频火星
            if (!VaultUtils.isServer && t % 6 == 0) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-VeinHalfW, VeinHalfW), Main.rand.NextFloat(-6f, 8f));
                PRTLoader.NewParticle<PRT_LavaFire>(pos, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)),
                    MagmaOrange, Main.rand.NextFloat(0.35f, 0.6f));
            }
            Lighting.AddLight(Projectile.Center, MagmaOrange.ToVector3() * 0.5f * (1f - t / (float)AftermathTicks));
        }

        /// <summary>爆发段伤害全在岩柱与碎石；余韵熔岩脉滩触碰判定</summary>
        public override bool? CanDamage() => Phase == 2 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 2) {
                return false;
            }
            Rectangle vein = new((int)(Projectile.Center.X - VeinHalfW), (int)(Projectile.Center.Y - VeinHalfH),
                (int)(VeinHalfW * 2f), (int)(VeinHalfH * 2f + 10f));
            return vein.Intersects(targetHitbox);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //熔岩脉滩附着灼烧
            if (Phase == 2) {
                target.AddBuff(BuffID.OnFire, 90);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GlowTex?.Value;
            if (glow == null) {
                return false;
            }
            //蓄势裂纹与余韵脉滩共用地面亮带：三段脉动橙红
            float env = Phase == 0
                ? VaultUtils.EaseOutQuad(Elapsed / (float)OmenTicks) * 0.6f
                : Phase == 2 ? MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f) : 0.35f;
            if (env <= 0.02f) {
                return false;
            }
            for (int i = -1; i <= 1; i++) {
                Vector2 pos = Projectile.Center + new Vector2(i * VeinHalfW * 0.6f, 2f) - Main.screenPosition;
                float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.1f + i * 2.3f + Projectile.identity * 0.41f);
                Main.EntitySpriteDraw(glow, pos, null, MagmaOrange with { A = 0 } * (0.5f * env * pulse), 0f,
                    glow.Size() * 0.5f, new Vector2(190f, 34f) / glow.Width * pulse, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, pos, null, MagmaDeep with { A = 0 } * (0.4f * env), 0f,
                    glow.Size() * 0.5f, new Vector2(280f, 18f) / glow.Width, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 造山岩柱：自地面顶升、驻留、沉降。判定矩形与可见柱高同源。
    /// ai[0]=相位计时（各端自增） ai[1]=满柱高 px
    /// </summary>
    internal class GsEarthPillarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        private const int RiseTicks = 12;
        private const int HoldTicks = 26;
        private const int SinkTicks = 16;
        private const int LifeTicks = RiseTicks + HoldTicks + SinkTicks;
        private const float HalfWidth = 23f;

        private ref float Timer => ref Projectile.ai[0];
        private float FullHeight => Projectile.ai[1] > 8f ? Projectile.ai[1] : 140f;

        /// <summary>当前柱高：顶升长满、沉降收回</summary>
        private float HeightNow {
            get {
                float grow = VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / RiseTicks, 0f, 1f));
                float sink = MathHelper.Clamp((LifeTicks - Timer) / (float)SinkTicks, 0f, 1f);
                return FullHeight * Math.Min(grow, VaultUtils.EaseOutQuad(sink));
            }
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeTicks + 6;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Timer == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_GraniteShard>(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2.5f, 6f)),
                        GsOrogenyDirector.MagmaDeep, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(26);
                }
            }
            Timer++;
            if (Timer >= LifeTicks) {
                Projectile.Kill();
                return;
            }
            //顶升期顶端迸碎屑
            if (!VaultUtils.isServer && Timer < RiseTicks && Timer % 3 == 0) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), -HeightNow),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(1.5f, 3.5f)),
                    GsOrogenyDirector.MagmaOrange, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(22);
            }
            Lighting.AddLight(Projectile.Center + new Vector2(0f, -HeightNow * 0.6f), GsOrogenyDirector.MagmaOrange.ToVector3() * 0.25f);
        }

        /// <summary>沉降段无伤</summary>
        public override bool? CanDamage() => Timer < RiseTicks + HoldTicks ? null : false;

        /// <summary>柱体判定与可见高度同源：自地面向上 HeightNow 的矩形</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = HeightNow;
            if (h < 4f) {
                return false;
            }
            Rectangle pillar = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - h),
                (int)(HalfWidth * 2f), (int)h);
            return pillar.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int boulderType = ContentSamples.ItemsByType[ItemID.StaffofEarth].shoot;
            Main.instance.LoadProjectile(boulderType);
            Texture2D rock = TextureAssets.Projectile[boulderType].Value;
            float h = HeightNow;
            if (h < 4f) {
                return false;
            }
            //圆石自地面向上堆叠成柱，底暗顶亮，identity 定相的旋转错位
            float blockH = rock.Height * 0.92f;
            int blocks = Math.Max(1, (int)Math.Ceiling(h / blockH));
            for (int i = 0; i < blocks; i++) {
                float yTop = Math.Min((i + 1) * blockH, h);
                Vector2 pos = Projectile.Center + new Vector2(
                    (float)Math.Sin(Projectile.identity * 1.37f + i * 2.1f) * 5f, -yTop + blockH * 0.5f) - Main.screenPosition;
                float shade = MathHelper.Lerp(0.55f, 1f, blocks <= 1 ? 1f : i / (float)(blocks - 1));
                Color tint = Color.Lerp(GsOrogenyDirector.MagmaDeep, new Color(210, 190, 175), shade) * shade;
                float rot = Projectile.identity * 0.61f + i * 1.13f;
                Main.EntitySpriteDraw(rock, pos, null, tint, rot, rock.Size() * 0.5f,
                    1.35f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
