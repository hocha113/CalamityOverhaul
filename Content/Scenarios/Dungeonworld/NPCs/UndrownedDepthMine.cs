using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 狱水沉雷：布水雷撒进水里的锈刺雷。借地牢刺球贴图重染，水下微沉浮，
    /// 充能读秒（膨胀+变亮），逐雷错相引爆（波浪不齐爆）。
    /// ai[0]=雷序号（错相拍=Fuse+序号×12，随 spawn 包原子过线），ai[1]=主人 whoAmI。
    /// 爆片只取罗盘 8 向的下半扇 5 向：朝上半扇恒空（水面方向永远是逃生向）。
    /// 引爆裁决只在服务器（爆片=服务器生成的敌对弹幕），各端读秒表现本地推进
    /// </summary>
    internal class UndrownedDepthMine : UndrownedModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>基础引信与逐雷错相</summary>
        internal const int MineFuse = 180;
        internal const int MineFuseStagger = 12;

        private int MineIndex => (int)Projectile.ai[0];
        private int OwnerIndex => (int)Projectile.ai[1];
        private int FuseTotal => MineFuse + MineIndex * MineFuseStagger;

        private ref float Life => ref Projectile.localAI[0];
        private float Seed => Projectile.identity * 0.7391f % 3.7f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MineFuse + 6 * MineFuseStagger + 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        /// <summary>入水降位帧数（纯视觉：从上方 70px 沉到位，判定框不动）</summary>
        private const int EntryFrames = 16;

        /// <summary>入水视觉偏移：前 16f 从上方坠沉到位（各端从本地 Life 确定性同放）</summary>
        private Vector2 EntryOffset() {
            float k = MathHelper.Clamp(Life / EntryFrames, 0f, 1f);
            k = 1f - (1f - k) * (1f - k);
            return new Vector2(0f, -70f * (1f - k));
        }

        public override void AI() {
            Life++;
            //水下微沉浮（确定性正弦，不掷随机）
            Projectile.velocity.X *= 0.98f;
            Projectile.velocity.Y = MathF.Sin(Life * 0.05f + Seed) * 0.3f;
            Projectile.rotation += 0.008f + Charge() * 0.03f;

            //入水拍：沉位起点冒一串气泡 + 闷响（撒网的"网"落进水里）
            if (!Main.dedServ && (int)Life == 2) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.35f, Pitch = -0.7f, MaxInstances = 4 }, Projectile.Center);
                for (int k = 0; k < 4; k++) {
                    PRTLoader.NewParticle<PRT_SumpSpray>(
                        Projectile.Center + EntryOffset() + Main.rand.NextVector2Circular(10f, 8f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.4f)),
                        Undrowned.FoamWhite * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
                }
            }

            //气泡鞘（客户端）：越临爆越密
            int bubbleGap = Charge() > 0.8f ? 4 : 9;
            if (!Main.dedServ && (int)Life % bubbleGap == 0) {
                PRTLoader.NewParticle<PRT_SumpSpray>(
                    Projectile.Center + EntryOffset() + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    Undrowned.FoamWhite * 0.5f, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(12, 20));
            }
            //读秒后段的滴答（各端同拍）
            if ((int)Life == FuseTotal - 40 || (int)Life == FuseTotal - 20) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            }

            //引爆裁决只在服务器：爆片随 spawn 包过线，客户端由弹幕消失+爆响得知
            if ((int)Life >= FuseTotal) {
                if (!VaultUtils.isClient) {
                    Detonate();
                    Projectile.Kill();
                    Projectile.netUpdate = true;
                }
            }
        }

        /// <summary>充能进度 0~1（膨胀+变亮读秒）</summary>
        private float Charge() => MathHelper.Clamp(Life / FuseTotal, 0f, 1f);

        /// <summary>入水沉位期判定关闭（伤害窗=视觉窗：雷还没落位不咬人）</summary>
        public override bool? CanDamage() => Life < EntryFrames ? false : null;

        /// <summary>下半扇 5 向爆片：0/45/90/135/180 度（屏幕系 +Y 向下），上半扇恒空</summary>
        private void Detonate() {
            int damage = Projectile.damage;
            for (int i = 0; i < 5; i++) {
                float angle = MathHelper.Pi * i / 4f;
                Vector2 vel = angle.ToRotationVector2() * 7f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    ModContent.ProjectileType<UndrownedMineShard>(), damage, 1f,
                    Main.myPlayer, 0f, OwnerIndex);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Drown with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 2 }, Projectile.Center);
            for (int k = 0; k < 10; k++) {
                PRTLoader.NewParticle<PRT_SumpSpray>(Projectile.Center,
                    Main.rand.NextVector2Circular(3.5f, 3.5f),
                    Color.Lerp(Undrowned.BogWater, Undrowned.FoamWhite, Main.rand.NextFloat(0.7f)),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(14, 26));
            }
            //双环冲击（内环快亮外环慢淡）+ 上浮泡柱余韵（活得比爆点久）
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, Undrowned.BogWater, 0.06f)
                ?.Configure(new Vector2(0.9f, 1f), 0f, 0.22f, 9);
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero, Undrowned.FoamWhite * 0.6f, 0.04f)
                ?.Configure(new Vector2(1f, 0.9f), 0f, 0.3f, 14);
            for (int k = 0; k < 5; k++) {
                PRTLoader.NewParticle<PRT_SumpSpray>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-6f, 6f)),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.5f, 3.2f)),
                    Undrowned.FoamWhite * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(24, 40));
            }
        }

        //==================== 绘制：刺球重染 + 充能加色芯 ====================

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadNPC(NPCID.SpikeBall);
            Texture2D tex = TextureAssets.Npc[NPCID.SpikeBall]?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null || glow == null) {
                return false;
            }
            int count = Math.Max(1, Main.npcFrameCount[NPCID.SpikeBall]);
            Rectangle frame = new(0, 0, tex.Width, tex.Height / count);
            Vector2 origin = new(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 drawWorld = Projectile.Center + EntryOffset();
            Vector2 pos = drawWorld - Main.screenPosition;
            float charge = Charge();
            float scale = 0.9f + charge * 0.28f;
            float fade = MathHelper.Clamp(Life / 6f, 0f, 1f);

            //幽光场（shader：焦散光壳+读秒收缩环+末段警闪；画在雷体之下）
            DrawGlowField(drawWorld, charge, fade);

            //锈染刺球：暗缘 + 锈橙乘色
            Main.spriteBatch.Draw(tex, pos, frame, Undrowned.RustDeep * (0.7f * fade),
                Projectile.rotation, origin, scale * 1.08f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pos, frame,
                lightColor.MultiplyRGB(Undrowned.RustOrange) * fade,
                Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

            //充能读秒芯（加色批：呼吸频率随充能爬升，强度写进色乘）
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            float pulse = 0.5f + 0.5f * MathF.Sin(Life * (0.12f + charge * 0.3f) + Seed);
            Color core = Color.Lerp(Undrowned.BogWater, Undrowned.RustOrange, charge);
            sb.Draw(glow, pos, null, core * ((0.25f + 0.45f * charge) * pulse * fade), 0f,
                glow.Size() * 0.5f, new Vector2((10f + charge * 8f) * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>水雷幽光：UndrownedMineGlow.fx（读秒环触雷即爆=可读倒计时）；缺编时只剩加色芯</summary>
        private void DrawGlowField(Vector2 worldPos, float charge, float fade) {
            Effect effect = EffectLoader.UndrownedMineGlow?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (effect == null || noise == null || pixel == null || fade <= 0.02f) {
                return;
            }
            const float FieldSize = 132f;
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Seed);
            effect.Parameters["uCharge"]?.SetValue(charge);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            //幽光色抬到亮尸青档：沼靛在低 alpha 下读灰（沙盒实测），警告色仍走锈橙
            effect.Parameters["uGlowColor"]?.SetValue(new Vector3(0.42f, 0.66f, 0.60f));
            effect.Parameters["uWarnColor"]?.SetValue(Undrowned.RustOrange.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(Undrowned.FoamWhite.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(pixel, worldPos - Main.screenPosition, null, Color.White * fade, 0f,
                pixel.Size() * 0.5f, new Vector2(FieldSize / pixel.Width), SpriteEffects.None, 0f);
            sb.End();
            gd.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// <summary>沉雷爆片：短命锈刺碎片，下半扇直线掠出，撞墙即灭</summary>
    internal class UndrownedMineShard : UndrownedModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 26;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Life++;
            Projectile.velocity *= 0.985f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && (int)Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_SumpSpray>(Projectile.Center,
                    -Projectile.velocity * 0.1f, Undrowned.BogWater * 0.7f,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blob == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = 1f - MathF.Pow(Life / 26f, 2f);
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.1f, 0.4f, 1.2f);
            //碎片体（实色）+ 亮头（A=0 加色点缀在预乘批内）
            Main.spriteBatch.Draw(blob, pos, null, Undrowned.RustDeep * (0.9f * fade),
                Projectile.rotation, blob.Size() * 0.5f,
                new Vector2(0.1f * (1f + stretch), 0.07f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, pos, null,
                (Undrowned.RustOrange with { A = 0 }) * (0.7f * fade),
                Projectile.rotation, glow.Size() * 0.5f,
                new Vector2(10f * 2f / glow.Width, 5f * 2f / glow.Height), SpriteEffects.None, 0f);
            return false;
        }
    }
}
