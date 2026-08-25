using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.WheezingWyrms
{
    /// <summary>
    /// 龙焰团，一串团粒叠成喷流。出膛减速、越慢越受热浮上飘、体积膨胀、沿途冷却变色；
    /// 撞面舔焰留残舌，空中熄灭化烟(烧得越净烟越少)，入水淬熄。<br/>
    /// 绘制走七层异质叠加：辉光垫层/拖影/暗色外鞘/湍流帧序列/双股火舌/白热芯/化烟膜。<br/>
    /// ai0=出生温度(0~1)，ai1=扰动种子
    /// </summary>
    internal class WyrmFlame : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        private static Asset<Texture2D> FlameTex = null;
        [VaultLoaden(CWRConstant.Masking + "Fire")]
        private static Asset<Texture2D> FireSheet = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowTex = null;
        [VaultLoaden(CWRConstant.Masking + "Fog")]
        private static Asset<Texture2D> FogTex = null;

        private const int LifeTime = 32;

        /// <summary>撞上地形，OnKill 走舔面分支</summary>
        private bool hitSurface;
        private Vector2 surfaceVel;

        private float Temp0 => Projectile.ai[0];
        private float Seed => Projectile.ai[1];
        private float LifeCompletion => 1f - Projectile.timeLeft / (float)LifeTime;
        /// <summary>当前温度：出生温度沿途冷却</summary>
        private float CurTemp => MathF.Max(Temp0 * 1.12f - LifeCompletion * 0.5f, 0.02f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 3;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            float lc = LifeCompletion;
            //减速+热浮：气团慢下来后上飘，同时轻微湍流摆动，不给匀速平飞
            Projectile.velocity *= 0.952f;
            Projectile.velocity.Y -= 0.028f + 0.085f * lc;
            Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Projectile.timeLeft * 0.53f + Seed) * 0.016f);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale = 0.8f + lc * 1.15f;

            float temp = CurTemp;
            Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(temp).ToVector3() * (0.3f + 0.6f * Temp0));

            //沿途甩烬
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(0.9f, 0.9f)
                    , default, Main.rand.NextFloat(0.8f, 1.4f))
                    ?.Configure(Main.rand.Next(14, 24), temp);
            }

            //入水淬熄，腾一股白汽
            if (Collision.WetCollision(Projectile.position, Projectile.width, Projectile.height)) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.35f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center, -Vector2.UnitY * Main.rand.NextFloat(0.8f, 1.6f)
                            , new Color(196, 196, 192) * 0.55f, Main.rand.NextFloat(0.17f, 0.26f))
                            ?.Configure(Main.rand.Next(24, 36), 0.09f);
                    }
                }
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //温度决定点燃档位：蓝焰上地狱火长燃
            if (Temp0 >= 0.92f) {
                target.AddBuff(BuffID.OnFire3, 480);
            }
            else if (Temp0 >= 0.5f) {
                target.AddBuff(BuffID.OnFire3, 240);
            }
            else {
                target.AddBuff(BuffID.OnFire, 360);
            }

            if (VaultUtils.isServer) {
                return;
            }
            float temp = CurTemp;
            //火舔上目标
            for (int i = 0; i < 3; i++) {
                Vector2 od = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f));
                PRTLoader.NewParticle<PRT_WyrmTongue>(target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f)
                    , od * 1.5f, default, Main.rand.NextFloat(1f, 1.5f))
                    ?.Configure(od, Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(8, 14), temp);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(target.Center, Main.rand.NextVector2Circular(3.5f, 3.5f)
                    , default, Main.rand.NextFloat(0.8f, 1.4f))
                    ?.Configure(Main.rand.Next(16, 26), temp);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            hitSurface = true;
            surfaceVel = oldVelocity;
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            float temp = CurTemp;

            if (hitSurface) {
                //撞面舔焰：残舌沿面滑开+溅烬+压扁热浪圈+烟，余韵活得比弹体久
                Vector2 inDir = (surfaceVel == Vector2.Zero ? Projectile.velocity : surfaceVel).SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 5; i++) {
                    Vector2 od = (-inDir).RotatedBy(Main.rand.NextFloat(-1.15f, 1.15f));
                    PRTLoader.NewParticle<PRT_WyrmTongue>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                        , od * Main.rand.NextFloat(0.5f, 1.6f), default, Main.rand.NextFloat(0.9f, 1.7f))
                        ?.Configure(od, Main.rand.NextFloat(0.8f, 1.5f), Main.rand.Next(14, 26), temp);
                }
                for (int i = 0; i < 7; i++) {
                    Vector2 ev = (-inDir).RotatedBy(Main.rand.NextFloat(-0.95f, 0.95f)) * Main.rand.NextFloat(2f, 6f);
                    PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center, ev, default, Main.rand.NextFloat(0.8f, 1.3f))
                        ?.Configure(Main.rand.Next(16, 28), temp);
                }
                PRT_DWave wave = PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero
                    , Wyrmfire.TempColor(temp), 0.5f);
                wave?.Configure(new Vector2(1f, 0.5f), inDir.ToRotation(), 1f, 10);
                int smokeN = (int)MathF.Round((1f - Temp0) * 2f);
                for (int i = 0; i < smokeN; i++) {
                    PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center, -inDir * Main.rand.NextFloat(0.5f, 1.2f)
                        , new Color(88, 80, 74) * 0.6f, Main.rand.NextFloat(0.18f, 0.3f))
                        ?.Configure(Main.rand.Next(26, 40), 0.07f);
                }
                Lighting.AddLight(Projectile.Center, Wyrmfire.TempColor(temp).ToVector3() * 0.9f);
                return;
            }

            //空中熄灭：火化烟收尾；越热烧得越净，蓝焰几乎无烟
            int n = (int)MathF.Round((1f - Temp0) * 2.2f);
            for (int i = 0; i < n; i++) {
                PRTLoader.NewParticle<PRT_WyrmSmoke>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , Projectile.velocity * 0.3f - Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.7f)
                    , new Color(96, 88, 82) * 0.5f, Main.rand.NextFloat(0.17f, 0.26f))
                    ?.Configure(Main.rand.Next(24, 38), 0.08f);
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_WyrmEmber>(Projectile.Center, Projectile.velocity * 0.2f
                    , default, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(12, 20), CurTemp);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D flame = FlameTex?.Value;
            Texture2D fireSheet = FireSheet?.Value;
            Texture2D glow = GlowTex?.Value;
            if (flame == null || glow == null) {
                return false;
            }

            float temp = CurTemp;
            float lc = LifeCompletion;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float bright = Wyrmfire.Brightness(temp);
            //焰团整体透明度：出生快现，尾段随化烟衰减
            float fade = MathF.Min(Projectile.timeLeft / 6f, 1f) * MathF.Min((1f - lc) * 4f + 0.35f, 1f);

            Color body = Wyrmfire.TempColor(temp) with { A = 0 };
            Color mantle = Wyrmfire.MantleColor(temp) with { A = 0 };
            Color core = Wyrmfire.CoreColor(temp) with { A = 0 };

            float speed = Projectile.velocity.Length();
            float rot = Projectile.rotation + MathHelper.PiOver2;
            //逐帧长度抖动，火的时域签名
            float jitter = 0.84f + 0.3f * MathF.Sin((Projectile.timeLeft * 2.2f + Seed) * 3.1f);
            float baseScale = 56f / flame.Height * Projectile.scale;
            var stretch = new Vector2(0.62f, (0.85f + speed * 0.055f) * jitter) * baseScale;
            var origin = new Vector2(flame.Width * 0.5f, flame.Height);
            //根在尾、舌尖朝速度向：往后挪半截让贴图中段盖住弹体
            Vector2 rootPos = pos - Projectile.velocity * 0.6f;

            //①辉光垫层(低频光团,只垫底不当本体)
            Main.EntitySpriteDraw(glow, pos, null, body * (0.32f * fade * bright), 0f
                , glow.Size() * 0.5f, Projectile.scale * 1.6f, SpriteEffects.None, 0);

            //②双重拖影：补住团粒间隙,把一串团读成连续喷流
            Main.EntitySpriteDraw(flame, rootPos - Projectile.velocity * 1.1f, null, body * (0.22f * fade * bright)
                , rot + MathF.Sin(Seed + Projectile.timeLeft * 0.5f) * 0.18f
                , origin, stretch * 0.78f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flame, rootPos - Projectile.velocity * 0.55f, null, body * (0.42f * fade * bright)
                , rot + MathF.Sin(Seed + Projectile.timeLeft * 0.7f) * 0.14f
                , origin, stretch * 0.9f, SpriteEffects.None, 0);

            //③暗色外鞘：红黄相暗红外焰、蓝相深蓝外焰,慢相位摆动
            Main.EntitySpriteDraw(flame, rootPos, null, mantle * (0.5f * fade * bright)
                , rot + MathF.Sin(Seed * 2f + Projectile.timeLeft * 0.9f) * 0.1f
                , origin, stretch * new Vector2(1.34f, 1.16f), SpriteEffects.None, 0);

            //④湍流帧序列：Fire 图集逐帧翻动,给焰体内部结构(异质中层)
            if (fireSheet != null) {
                int frameIdx = (int)(Projectile.timeLeft / 3 + Seed * 7f) % 16;
                int fw = fireSheet.Width / 4;
                int fh = fireSheet.Height / 4;
                Rectangle frame = new(fw * (frameIdx % 4), fh * (frameIdx / 4), fw, fh);
                float fireScale = 56f * Projectile.scale / fh;
                Main.EntitySpriteDraw(fireSheet, rootPos, frame, body * (0.5f * fade * bright)
                    , rot, new Vector2(fw * 0.5f, fh * 0.86f)
                    , new Vector2(fireScale * 0.85f, fireScale * (0.9f + speed * 0.03f)), SpriteEffects.None, 0);
            }

            //⑤双股火舌：主舌+错相侧舔,火是一簇舌头不是一张贴纸
            Main.EntitySpriteDraw(flame, rootPos, null, body * (0.95f * fade * bright)
                , rot, origin, stretch, SpriteEffects.None, 0);
            float lickSway = MathF.Sin((Projectile.timeLeft * 1.7f + Seed * 3f) * 2.3f);
            Main.EntitySpriteDraw(flame, rootPos, null, body * (0.6f * fade * bright)
                , rot + 0.32f * lickSway
                , origin, stretch * new Vector2(0.55f, 0.72f + 0.1f * lickSway), SpriteEffects.None, 0);

            //⑥白热芯：暖相淡金、蓝相蓝白,永不给纯白常驻
            Main.EntitySpriteDraw(flame, rootPos, null, core * (0.85f * fade * bright)
                , rot, origin, stretch * new Vector2(0.42f, 0.6f), SpriteEffects.None, 0);

            //⑦尾段火转烟：低温焰才有烟膜(真alpha能压暗)
            Texture2D fog = FogTex?.Value;
            if (fog != null && lc > 0.62f && Temp0 < 0.6f) {
                float sootA = (lc - 0.62f) / 0.38f * (0.6f - Temp0) * 0.8f;
                Main.EntitySpriteDraw(fog, pos, null, new Color(90, 82, 76) * sootA
                    , Seed + lc * 2f, fog.Size() * 0.5f, Projectile.scale * 0.3f
                    , (int)Seed % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            }
            return false;
        }
    }
}
