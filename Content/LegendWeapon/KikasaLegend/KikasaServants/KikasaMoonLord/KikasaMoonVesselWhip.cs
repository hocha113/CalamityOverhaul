using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaMoonLord
{
    /// <summary>
    /// 血管鞭：噬月心藏对远处目标的答话——一条血管从其脚下湖面暴起鞭抽。
    /// 湖面先鼓起一条血管脊线（涟漪+泡沫+水下血光的 tell）→ 一帧暴起上撩鞭抽
    /// （伤害窗严格对齐抽击帧）→ 张力散尽瘫回水里溶解。
    /// ai[0]=打击高度（目标 Y 快照）ai[1]=甩弧方向；几何全部是自身计时的
    /// 确定性函数，各端同画；它的攻击半径就是整个湖
    /// </summary>
    internal class KikasaMoonVesselWhip : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //节拍：脊线 tell → 暴起抽击 → 余势 → 瘫回溶解
        private const int TellEnd = 34;
        private const int SnapEnd = 44;
        private const int HoldEnd = 58;
        private const int SlumpEnd = 96;
        private const int KillAt = 102;

        //伤害窗：与可见的抽击严格对齐
        private const int DamageStart = 35;
        private const int DamageEnd = 52;

        private const int Samples = 14;

        private ref float StrikeY => ref Projectile.ai[0];
        private ref float Side => ref Projectile.ai[1];
        private ref float Life => ref Projectile.localAI[0];

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        //节拍闩：暴起拍/瘫回过线拍只放一次
        private bool snapDone;
        private bool slumpSplashed;

        private Player OwnerPlayer => Main.player[Projectile.owner];

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = KillAt + 20;
        }

        public override void AI() {
            Life++;
            int t = (int)Life;

            Player owner = OwnerPlayer;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }
            KikasaDomainPlayer domain = owner.GetModPlayer<KikasaDomainPlayer>();
            //湖没了：快进瘫回——只有 owner 裁决，服务器领域恒 Closed、
            //远端快照未到，别处判会把刚出鞘的鞭当场掐死
            if (Main.myPlayer == Projectile.owner
                && (!domain.AnyActive || domain.RiseT < 0.5f) && t < SlumpEnd) {
                Life = SlumpEnd;
            }
            float lakeY = domain.LakeWorldY;
            bool viewed = ViewedOwner;

            if (t <= TellEnd) {
                //脊线 tell：涟漪自两侧向暴起点收拢、泡沫翻涌、闷咕声——湖面在鼓一条脊
                float ridge = t / (float)TellEnd;
                if (t == 6) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.6f, Pitch = -0.7f, MaxInstances = 2 }, Projectile.Center);
                }
                if (t == 22) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.8f, MaxInstances = 2 }, Projectile.Center);
                }
                if (viewed) {
                    if (t % 4 == 1) {
                        float conv = 1f - ridge;
                        float side = t / 4 % 2 == 0 ? 1f : -1f;
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X + side * conv * 90f, lakeY),
                            0.4f + ridge * 0.7f);
                    }
                    //脊线泡沫：越临近越密
                    if (t % 3 == 0 && ridge > 0.3f) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            new Vector2(Projectile.Center.X + Main.rand.NextFloat(-46f, 46f) * (1f - ridge * 0.6f), lakeY - 2f),
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2.4f + ridge * 2f)),
                            FoamGlow * Main.rand.NextFloat(0.35f, 0.55f),
                            Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24), 0f);
                    }
                }
                return;
            }

            if (!snapDone) {
                //暴起拍：一声鞭响带一蓬破水
                snapDone = true;
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.85f, Pitch = -0.15f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.9f, Pitch = -0.4f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 2 }, Projectile.Center);
                if (viewed) {
                    Vector2 hit = new(Projectile.Center.X, lakeY);
                    KikasaDomainDeco.RippleAt(hit, 2.4f);
                    KikasaDomainDeco.SplashAt(hit + new Vector2(-14f, 0f), 12);
                    KikasaDomainDeco.SplashAt(hit + new Vector2(14f, 0f), 12);
                    KikasaDomainDeco.BloodBurst(hit, 14, 1.2f);
                    Main.LocalPlayer?.CWR()?.GetScreenShake(4f);
                }
            }

            //抽击期沿鞭身甩血（tell 期鞭还没出水，不白费）
            if (t > TellEnd && t < HoldEnd && !Main.dedServ && t % 2 == 0) {
                float ts = Main.rand.NextFloat(0.35f, 1f);
                Vector2 pos = WhipPoint(ts, lakeY, out _);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos,
                    Main.rand.NextVector2Circular(2.2f, 2.2f) - new Vector2(0f, 1.4f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 26), 0.3f);
            }

            //瘫回过线拍：鞭身砸回水面
            if (t >= SlumpEnd - 20 && !slumpSplashed) {
                Vector2 mid = WhipPoint(0.6f, lakeY, out _);
                if (mid.Y >= lakeY - 6f) {
                    slumpSplashed = true;
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, Projectile.Center);
                    if (viewed) {
                        KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X - 30f, lakeY), 8);
                        KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X + 40f, lakeY), 8);
                        KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, lakeY), 1.5f);
                    }
                }
            }

            //鞭身补光
            if (t > TellEnd && t < SlumpEnd) {
                Vector2 mid = WhipPoint(0.7f, lakeY, out _);
                Lighting.AddLight(mid, 0.3f, 0.08f, 0.07f);
            }

            if (t >= KillAt) {
                Projectile.Kill();
            }
        }

        //==================== 鞭形几何（自身计时的确定性函数）====================

        /// <summary>暴起进度：poly(6) 极锐出鞭</summary>
        private float SnapT() {
            float t = ((int)Life - TellEnd) / (float)(SnapEnd - TellEnd);
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - MathF.Pow(1f - t, 6f);
        }

        /// <summary>瘫回进度 0~1</summary>
        private float SlumpT() {
            float t = ((int)Life - HoldEnd) / (float)(SlumpEnd - HoldEnd);
            return MathHelper.Clamp(t, 0f, 1f);
        }

        /// <summary>鞭中线采样：根钉在湖面，鞭头暴起穿过打击高度，
        /// 弓弧在抽击中途翻面（鞭花），瘫回按节权重坠水</summary>
        private Vector2 WhipPoint(float t, float lakeY, out float width) {
            Vector2 root = new(Projectile.Center.X, lakeY + 12f);
            float side = Side == 0f ? 1f : Side;
            float snap = SnapT();
            float slump = SlumpT();

            //鞭头行程：水下→打击点上方过冲；行程封顶，别对着高空目标抻成一根线
            float reachY = MathF.Max(MathF.Min(StrikeY - 66f, lakeY - 120f), lakeY - 860f);
            Vector2 headStart = new(root.X, lakeY + 34f);
            Vector2 headApex = new(root.X + side * 96f, reachY);
            Vector2 head = Vector2.Lerp(headStart, headApex, snap);

            //弓弧翻面：蓄时正弓、抽出后反弓——鞭花的来源
            float bowAmp = MathHelper.Lerp(52f, -44f, snap);
            //余势摆动
            if ((int)Life > SnapEnd) {
                bowAmp += MathF.Sin(((int)Life - SnapEnd) * 0.42f + Seed) * 14f * (1f - slump);
            }
            Vector2 chord = head - root;
            Vector2 normal = new Vector2(-chord.Y, chord.X).SafeNormalize(Vector2.UnitX) * side;
            Vector2 pos = root + chord * t + normal * (bowAmp * MathF.Sin(t * MathHelper.Pi));

            //鞭身波：抽击期一道行波从根跑向鞭头
            if (snap > 0.05f && slump < 0.9f) {
                float wavePos = MathHelper.Clamp(snap * 1.3f, 0f, 1f);
                float wave = MathF.Exp(-(t - wavePos) * (t - wavePos) / 0.02f);
                pos += normal * wave * 16f * (1f - slump);
            }

            //瘫回：节权重坠水，越靠鞭头坠得越狠，落到水下缓冲
            if (slump > 0f) {
                float drop = slump * slump * (60f + 340f * t * t);
                pos.Y += drop;
                if (pos.Y > lakeY + 30f) {
                    pos.Y = MathHelper.Lerp(pos.Y, lakeY + 30f, 0.7f);
                }
            }

            //宽度：根粗鞭头细，鞭头一段更利
            width = MathHelper.Lerp(16f, 6.5f, t) * (1f - slump * 0.3f);
            return pos;
        }

        //==================== 判定 ====================

        /// <summary>伤害窗严格对齐抽击帧</summary>
        public override bool? CanDamage() {
            int t = (int)Life;
            return t >= DamageStart && t <= DamageEnd ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Player owner = OwnerPlayer;
            if (owner == null || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                return false;
            }
            float lakeY = domain.LakeWorldY;
            float _ = 0f;
            Vector2 prev = WhipPoint(0f, lakeY, out float w);
            for (int i = 1; i < Samples; i++) {
                Vector2 cur = WhipPoint(i / (float)(Samples - 1), lakeY, out w);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    prev, cur, 26f, ref _)) {
                    return true;
                }
                prev = cur;
            }
            return false;
        }

        public override bool? CanCutTiles() => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //鞭中：重水花甩溅
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(22f, 22f),
                    new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 5f)),
                    BloodMain * 0.65f, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.75f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 3 }, target.Center);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Player owner = OwnerPlayer;
            if (owner == null || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)) {
                return false;
            }
            int t = (int)Life;
            float lakeY = domain.LakeWorldY;
            SpriteBatch sb = Main.spriteBatch;

            //tell 期：水下血光沿脊线蓄起
            if (t <= TellEnd) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float ridge = t / (float)TellEnd;
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Vector2 pos = new Vector2(Projectile.Center.X, lakeY + MathHelper.Lerp(40f, 8f, ridge)) - Main.screenPosition;
                    float r = 30f + 46f * ridge;
                    sb.Draw(glow, pos, null, BloodDeep * (0.5f * ridge), 0f, glow.Size() * 0.5f,
                        new Vector2(r * 2.6f / glow.Width, r * 0.8f / glow.Height), SpriteEffects.None, 0f);
                    sb.Draw(glow, pos, null, FoamGlow * (0.3f * ridge * ridge), 0f, glow.Size() * 0.5f,
                        new Vector2(r * 1.4f / glow.Width, r * 0.5f / glow.Height), SpriteEffects.None, 0f);
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                }
                return false;
            }

            //鞭身条带：血水臂材质
            Effect fx = EffectLoader.KikasaHand?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            float slump = SlumpT();
            float opacity = MathHelper.Clamp((KillAt - t) / 14f, 0f, 1f);

            sb.End();
            if (fx != null && noise != null) {
                GraphicsDevice device = Main.instance.GraphicsDevice;
                BlendState prevBlend = device.BlendState;
                RasterizerState prevRaster = device.RasterizerState;
                device.BlendState = BlendState.AlphaBlend;
                device.RasterizerState = RasterizerState.CullNone;

                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
                fx.Parameters["uOpacity"]?.SetValue(opacity);
                //抽击帧绷到最紧，瘫回全松
                float grip = (int)Life <= HoldEnd ? SnapT() : MathHelper.Clamp(1f - slump * 1.4f, 0f, 1f);
                fx.Parameters["uGrip"]?.SetValue(grip);
                fx.Parameters["uSeed"]?.SetValue(Seed);
                fx.Parameters["uFoam"]?.SetValue((int)Life < SnapEnd + 8 ? 1f : 0.5f);
                fx.Parameters["uDrain"]?.SetValue(MathHelper.Clamp((slump - 0.55f) / 0.45f, 0f, 1f));

                var verts = BuildWhipStrip(lakeY);
                foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
                }

                device.BlendState = prevBlend;
                device.RasterizerState = prevRaster;
            }
            else {
                //CPU 回退：折线鞭
                Texture2D pixel = VaultAsset.placeholder2?.Value;
                if (pixel != null) {
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Vector2 prev = WhipPoint(0f, lakeY, out _);
                    for (int i = 1; i < Samples; i++) {
                        Vector2 cur = WhipPoint(i / (float)(Samples - 1), lakeY, out float w);
                        Vector2 d = cur - prev;
                        float len = d.Length();
                        if (len > 0.5f) {
                            sb.Draw(pixel, prev - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                                BloodDeep * (0.85f * opacity), MathF.Atan2(d.Y, d.X),
                                new Vector2(0f, 0.5f), new Vector2(len, w * 1.4f), SpriteEffects.None, 0f);
                        }
                        prev = cur;
                    }
                    sb.End();
                }
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>鞭条带装配：u=0 融水根 → 0.84 鞭身 → 1.0 鞭头（爪面提亮）</summary>
        private VertexPositionColorTexture[] BuildWhipStrip(float lakeY) {
            var verts = new VertexPositionColorTexture[Samples * 2];
            Vector2 prev = WhipPoint(0f, lakeY, out _);
            for (int i = 0; i < Samples; i++) {
                float t = i / (float)(Samples - 1);
                Vector2 pos = WhipPoint(t, lakeY, out float width);
                Vector2 next = i < Samples - 1
                    ? WhipPoint((i + 1) / (float)(Samples - 1), lakeY, out _)
                    : pos;
                Vector2 tangent = (i < Samples - 1 ? next - pos : pos - prev).SafeNormalize(-Vector2.UnitY);
                Vector2 normal = new(-tangent.Y, tangent.X);
                //鞭头 16% 段走爪面掩码：水膜提亮，鞭梢要读得出"利"
                float claw = MathHelper.Clamp((t - 0.84f) / 0.16f, 0f, 1f);
                Color vCol = new(0.5f, claw, 0f);
                float u = t < 0.84f ? t * 0.833f : 0.7f + (t - 0.84f) / 0.16f * 0.3f;
                verts[i * 2] = new VertexPositionColorTexture((pos + normal * width).ToVector3(),
                    vCol, new Vector2(u, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pos - normal * width).ToVector3(),
                    vCol, new Vector2(u, 1f));
                prev = pos;
            }
            return verts;
        }

        public override void OnKill(int timeLeft) {
            //瘫尽的残珠
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(-10f, 6f)),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.5f, 2f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(12, 22), 0.3f);
            }
        }
    }
}
