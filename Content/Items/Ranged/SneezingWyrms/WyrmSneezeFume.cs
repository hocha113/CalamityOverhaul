using CalamityOverhaul.Content.Items.Magic.WheezingWyrms;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.SneezingWyrms
{
    /// <summary>
    /// 龙嚏烟云。三段生命：浓烟漂浮(无伤)→内芯阴燃透红→自燃成龙焰(才有伤害)，
    /// 烧尽散作余烬。烟相缓浮，燃相热浮加速；撞墙贴停不消散。
    /// 烟体三瓣雾叠绘防贴纸感，火从烟心内部先透光再破壳。<br/>
    /// ai0=点燃温度(0~1)，ai1=扰动种子
    /// </summary>
    internal class WyrmSneezeFume : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "Fog")]
        private static Asset<Texture2D> FogTex = null;
        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        private static Asset<Texture2D> FlameTex = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;

        private const int LifeTime = 126;
        /// <summary>阴燃起点(经过帧数)</summary>
        private const int SmolderTick = 42;
        /// <summary>自燃点(经过帧数)</summary>
        private const int IgniteTick = 68;

        /// <summary>点燃一拍的本地一次性闩</summary>
        private bool ignitePlayed;

        private float Temp => Projectile.ai[0];
        private float Seed => Projectile.ai[1];
        private int Elapsed => LifeTime - Projectile.timeLeft;
        private bool Ignited => Elapsed >= IgniteTick;
        /// <summary>阴燃进度0~1</summary>
        private float Smolder => MathHelper.Clamp((Elapsed - SmolderTick) / (float)(IgniteTick - SmolderTick), 0f, 1f);
        /// <summary>燃相进度0~1</summary>
        private float BurnLc => MathHelper.Clamp((Elapsed - IgniteTick) / (float)(LifeTime - IgniteTick), 0f, 1f);
        /// <summary>燃相温度：点燃后随烧尽轻微回落</summary>
        private float BurnTemp => Temp * (1f - BurnLc * 0.3f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        /// <summary>烟相不伤人，着了火才烫</summary>
        public override bool? CanDamage() => Ignited ? null : false;

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //烟团撞面贴停，不消散
            Projectile.velocity = Vector2.Zero;
            return false;
        }

        public override void AI() {
            int elapsed = Elapsed;
            //减速漂移+轻微湍流摆动
            Projectile.velocity *= 0.94f;
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(elapsed * 0.11f + Seed) * 0.02f);
            //烟相缓浮，燃相热浮加速
            Projectile.velocity.Y -= Ignited ? 0.05f : 0.008f;
            Projectile.rotation += (Seed % 1f - 0.5f) * 0.024f;
            Projectile.scale = 0.9f + elapsed * 0.006f;

            if (Ignited) {
                if (!ignitePlayed) {
                    IgniteFX();
                }
                float burnTemp = BurnTemp;
                if (!VaultUtils.isServer) {
                    //火舌上舔+甩烬
                    if (elapsed % 2 == 0) {
                        Vector2 od = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f));
                        PRTLoader.NewParticle<PRT_WyrmTongue>(Projectile.Center + Main.rand.NextVector2Circular(12f, 8f)
                            , od * 1.2f, default, Main.rand.NextFloat(0.8f, 1.3f))
                            ?.Configure(od, Main.rand.NextFloat(0.9f, 1.5f), Main.rand.Next(4, 8), burnTemp);
                    }
                    if (Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                            , Main.rand.NextVector2Circular(0.9f, 0.9f) - Vector2.UnitY * 0.7f
                            , default, Main.rand.NextFloat(0.6f, 1.1f))
                            ?.Configure(Main.rand.Next(14, 26), burnTemp);
                    }
                }
                Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(burnTemp).ToVector3() * (0.55f + 0.35f * (1f - BurnLc)));
            }
            else if (elapsed >= SmolderTick) {
                //阴燃：内烬偶发火星，微光渐起
                float smolder = Smolder;
                if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                        , Main.rand.NextVector2Circular(0.4f, 0.4f), default, Main.rand.NextFloat(0.4f, 0.7f))
                        ?.Configure(Main.rand.Next(10, 16), 0.15f + smolder * 0.25f);
                }
                Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(0.15f + smolder * 0.3f).ToVector3() * (0.1f + 0.3f * smolder));
            }
            else if (!VaultUtils.isServer && Main.rand.NextBool(7)) {
                //烟相：偶尔脱落一丝薄烟
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Projectile.velocity * 0.3f - Vector2.UnitY * 0.25f
                    , new Color(96, 88, 82) * 0.45f, Main.rand.NextFloat(0.12f, 0.2f))
                    ?.Configure(Main.rand.Next(18, 30), 0.05f);
            }
        }

        /// <summary>自燃一拍：冲击波环+火从烟心炸开，把裹着的烟壳顶散</summary>
        private void IgniteFX() {
            ignitePlayed = true;
            Projectile.Resize(60, 60);
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);

            PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, Wyrmfire.TempColor(Temp), 0.5f);
            wave?.Configure(new Vector2(1f, 1f), 0f, 0.85f, 14);

            for (int i = 0; i < 6; i++) {
                Vector2 od = (MathHelper.TwoPi / 6f * i + Seed).ToRotationVector2();
                PRTLoader.NewParticle<PRT_WyrmTongue>(Projectile.Center + od * 6f, od * 1.8f, default, Main.rand.NextFloat(1f, 1.5f))
                    ?.Configure(od, Main.rand.NextFloat(0.9f, 1.4f), Main.rand.Next(6, 10), Temp);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center
                    , Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * Main.rand.NextFloat(1f, 2.5f)
                    , Wyrmfire.TempColor(Temp + 0.1f), Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(false, Main.rand.Next(10, 16));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center, Main.rand.NextVector2Circular(2.5f, 2.5f) - Vector2.UnitY * 1.2f
                    , default, Main.rand.NextFloat(0.6f, 1.1f))
                    ?.Configure(Main.rand.Next(16, 26), Temp);
            }
            //烟壳被顶散
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center, Main.rand.NextVector2CircularEdge(2f, 2f) - Vector2.UnitY * 0.6f
                    , new Color(104, 94, 86) * 0.55f, Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(22, 36), 0.07f);
            }
            Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(Temp).ToVector3() * 1.1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //温度决定点燃档位，与龙焰同一套叙事
            if (Temp >= 0.8f) {
                target.AddBuff(BuffID.OnFire3, 360);
            }
            else if (Temp >= 0.5f) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
            else {
                target.AddBuff(BuffID.OnFire, 300);
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                Vector2 od = Main.rand.NextVector2Unit();
                PRTLoader.NewParticle<PRT_WyrmTongue>(target.Center + Main.rand.NextVector2Circular(target.width * 0.25f, target.height * 0.25f)
                    , od * 1.2f, default, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(od, Main.rand.NextFloat(0.8f, 1.2f), Main.rand.Next(8, 13), BurnTemp);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //烧尽：余烬上飘，残一撮灰烟，余韵活得比弹体久
            int emberN = Ignited ? 5 : 3;
            for (int i = 0; i < emberN; i++) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(1.2f, 1.2f) - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f)
                    , default, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(14, 26), Temp * 0.8f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f)
                    , new Color(112, 102, 94) * 0.5f, Main.rand.NextFloat(0.14f, 0.22f))
                    ?.Configure(Main.rand.Next(26, 42), 0.07f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = FogTex?.Value;
            Texture2D flame = FlameTex?.Value;
            Texture2D glow = GlowTex?.Value;
            if (fog == null || flame == null || glow == null) {
                return false;
            }

            int elapsed = Elapsed;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float smolder = Smolder;
            float burnLc = BurnLc;
            float burnTemp = BurnTemp;
            SpriteEffects fx = (int)Seed % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //底层内火透光：阴燃从烟心里往外亮，燃相成为大焰光
            float underA = smolder * 0.4f + (Ignited ? 0.5f * (1f - burnLc * 0.6f) : 0f);
            if (underA > 0f) {
                float underPulse = 0.8f + 0.2f * MathF.Sin(elapsed * 0.31f + Seed);
                Color underCol = Wyrmfire.TempColor(Ignited ? burnTemp : 0.12f + smolder * 0.28f) with { A = 0 };
                Main.EntitySpriteDraw(glow, pos, null, underCol * (underA * underPulse), 0f
                    , glow.Size() * 0.5f, Projectile.scale * (0.7f + smolder * 0.5f + burnLc * 0.4f), SpriteEffects.None, 0);
            }

            //烟体三瓣雾叠绘：中心浓两翼淡，随弹体慢旋防贴纸感；燃相被火吃掉
            float fogA = MathF.Min(elapsed / 4f, 1f) * (0.8f - smolder * 0.12f);
            if (Ignited) {
                fogA *= MathF.Max(1f - burnLc * 1.6f, 0f);
            }
            if (fogA > 0f) {
                //阴燃时烟被内火从灰照成暗红
                Color fogCol = Color.Lerp(new Color(98, 90, 84), new Color(132, 60, 32), smolder * 0.85f);
                float fogScale = Projectile.scale * 0.38f;
                for (int i = 0; i < 3; i++) {
                    float ph = Seed * 1.3f + i * 2.09f;
                    Vector2 off = (ph + Projectile.rotation * (i % 2 == 0 ? 1f : -1f)).ToRotationVector2() * (4f + i * 9f) * Projectile.scale;
                    Main.EntitySpriteDraw(fog, pos + off, null, fogCol * (fogA * (1f - i * 0.2f))
                        , Projectile.rotation + ph, fog.Size() * 0.5f, fogScale * (1f - i * 0.22f), fx, 0);
                }
            }

            //烟面上的阴燃亮斑：暗处也读得出"要着了"
            if (smolder > 0f && !Ignited) {
                Color rimCol = Wyrmfire.TempColor(0.2f + smolder * 0.3f) with { A = 0 };
                float rimPulse = 0.7f + 0.3f * MathF.Sin(elapsed * 0.47f + Seed * 2f);
                Main.EntitySpriteDraw(glow, pos + new Vector2(MathF.Sin(Seed * 3f) * 8f, MathF.Cos(Seed * 2f) * 6f), null
                    , rimCol * (smolder * 0.5f * rimPulse), 0f
                    , glow.Size() * 0.5f, Projectile.scale * 0.34f, SpriteEffects.None, 0);
            }

            //燃相火舌：根锚烟心向上舔，五外舌+双热芯，逐帧长度抖动是火的时域签名
            if (Ignited) {
                float bright = Wyrmfire.Brightness(burnTemp);
                float flameA = MathF.Min((elapsed - IgniteTick) / 4f, 1f) * (1f - burnLc * burnLc) * bright;
                Color body = Wyrmfire.TempColor(burnTemp) with { A = 0 };
                Color hotCore = Wyrmfire.TempColor(burnTemp + 0.3f) with { A = 0 };
                var origin = new Vector2(flame.Width * 0.5f, flame.Height);
                float baseScale = 92f / flame.Height * Projectile.scale;

                for (int i = 0; i < 5; i++) {
                    float ph = Seed + i * 1.7f;
                    float sway = MathF.Sin(elapsed * 0.21f + ph) * 0.34f;
                    float jitter = 0.78f + 0.34f * MathF.Sin((elapsed * 2.1f + ph) * 3.3f);
                    float lobe = 0.62f + 0.38f * MathF.Sin(ph * 2.3f);
                    var stretch = new Vector2(0.42f, lobe * jitter) * baseScale;
                    Vector2 root = pos + new Vector2(MathF.Sin(ph * 1.9f) * 14f * Projectile.scale, 8f);
                    Main.EntitySpriteDraw(flame, root, null, body * (flameA * 0.75f), sway
                        , origin, stretch, SpriteEffects.None, 0);
                }
                //热芯双层窄舌
                float coreJit = 0.85f + 0.25f * MathF.Sin(elapsed * 2.6f + Seed);
                float coreSway = MathF.Sin(elapsed * 0.3f + Seed) * 0.18f;
                Vector2 coreRoot = pos + new Vector2(0f, 8f);
                Main.EntitySpriteDraw(flame, coreRoot, null, hotCore * (flameA * 0.85f)
                    , coreSway, origin, new Vector2(0.3f, 0.82f * coreJit) * baseScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(flame, coreRoot, null, hotCore * (flameA * 0.6f)
                    , -coreSway * 0.7f, origin, new Vector2(0.18f, 0.6f * coreJit) * baseScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
