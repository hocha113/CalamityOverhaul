using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaPlantera
{
    /// <summary>
    /// 鬼奴世纪之花的湖面藤袭：在点名的 X 位先鼓涟漪与荆棘尖 tell，
    /// 随后一根带刺血藤破水暴起直上鞭笞（快、狠、带水花），抽完瘫软沉回。
    /// 与鹿角怪的行进冰刺列语义相反——这是"在目标位置点名冒出"。
    /// 全生命周期由 spawn 参数确定（ai0=鞭高 ai2=甩鞘侧向），各端本地同推；
    /// 伤害窗严格对齐暴起+鞭笞段，碰撞沿藤身当前曲线取线段
    /// </summary>
    internal class KikasaPlanteraVine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序 ====================

        private const int TellFrames = 24;
        private const int BurstEnd = TellFrames + 5;
        private const int HoldEnd = 43;
        private const int LimpEnd = 69;
        private const int DamageStart = TellFrames;
        private const int DamageEnd = 39;

        /// <summary>鞭击全高（spawn 参数，按目标高度钳制）</summary>
        private ref float StrikeHeight => ref Projectile.ai[0];
        private ref float Life => ref Projectile.ai[1];
        /// <summary>鞭梢甩向（±1，spawn 参数）</summary>
        private ref float LashDir => ref Projectile.ai[2];

        //本地表现闩（防快照回卷重播）
        private bool burstDone;
        private bool slumpDone;
        private int lastTellRipple = -1;

        private float Seed => Projectile.identity * 0.7391f % 4.13f;

        private static Color BloodMain => KikasaPlanteraServant.BloodMain;
        private static Color BloodDeep => KikasaPlanteraServant.BloodDeep;
        private static Color FoamGlow => KikasaPlanteraServant.FoamGlow;
        private static Color PetalPink => KikasaPlanteraServant.PetalPink;

        public override void SetStaticDefaults() {
            //藤体比 hitbox 高出最多 560px，基部出屏也要画
            ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 700;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 40;
            Projectile.timeLeft = LimpEnd + 8;
        }

        /// <summary>伤害窗严格对齐暴起与鞭笞前段；tell 与瘫软不带伤害</summary>
        public override bool? CanDamage()
            => Life >= DamageStart && Life <= DamageEnd ? null : false;

        public override bool? CanCutTiles() => false;

        /// <summary>沿藤身当前曲线取样，逐段线碰撞</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CurrentHeight() < 30f) {
                return false;
            }
            float _ = 0f;
            Vector2 prev = SamplePoint(0f);
            const int samples = 8;
            for (int s = 1; s <= samples; s++) {
                Vector2 p = SamplePoint(s / (float)samples);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    prev, p, 15f, ref _)) {
                    return true;
                }
                prev = p;
            }
            return false;
        }

        //==================== 推进 ====================

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            int t = (int)Life;
            Vector2 basePos = Projectile.Center;
            bool viewed = ViewedOwner;

            if (t <= TellFrames) {
                //预告：涟漪逐圈收拢变密，水面在这一点先"鼓"起来
                int rippleIdx = t / 5;
                if (viewed && t % 5 == 2 && lastTellRipple < rippleIdx) {
                    lastTellRipple = rippleIdx;
                    float p = t / (float)TellFrames;
                    KikasaDomainDeco.RippleAt(basePos, 0.3f + p * 0.42f);
                }
                if (t == 2) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.7f, MaxInstances = 3 }, basePos);
                }
                if (t == 14) {
                    SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.55f, Pitch = -0.35f, MaxInstances = 3 }, basePos);
                }
                //最后几帧荆棘尖探头 + 碎泡上冒
                if (!Main.dedServ && t > TellFrames - 8 && t % 2 == 0) {
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                        basePos + new Vector2(Main.rand.NextFloat(-10f, 10f), -2f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.2f, 2.4f)),
                        BloodDeep * 0.6f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 16));
                }
                return;
            }

            if (!burstDone) {
                //破水暴起拍：鞭响 + 浪花 + 震屏，一帧读满暴力
                burstDone = true;
                SoundEngine.PlaySound(SoundID.Item153 with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 3 }, basePos);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f, Pitch = -0.25f, MaxInstances = 3 }, basePos);
                if (viewed) {
                    KikasaDomainDeco.SplashAt(basePos, 10);
                    KikasaDomainDeco.RippleAt(basePos, 1.6f);
                    ShakeViewer(2.5f);
                }
                if (!Main.dedServ) {
                    //沿藤身向上抛的血珠帘
                    for (int k = 0; k < 10; k++) {
                        float h = Main.rand.NextFloat(0.15f, 1f);
                        PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                            basePos - new Vector2(Main.rand.NextFloat(-9f, 9f), StrikeHeight * h),
                            new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(1.5f, 4f)),
                            Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                            Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 28));
                    }
                }
            }

            if (t <= HoldEnd) {
                //鞭笞段：梢部前几帧甩出离心血珠
                if (!Main.dedServ && t <= BurstEnd + 4 && t % 2 == 0) {
                    Vector2 tip = SamplePoint(1f);
                    PRTLoader.NewParticle<PRT_KikasaBloodGlob>(tip + Main.rand.NextVector2Circular(6f, 6f),
                        new Vector2(LashDir * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(0.5f, 2f)),
                        BloodMain * 0.6f, Main.rand.NextFloat(0.35f, 0.55f))?.Configure(Main.rand.Next(14, 24));
                }
                float glowStrength = 0.5f;
                Lighting.AddLight(SamplePoint(0.7f), 0.36f * glowStrength, 0.09f * glowStrength, 0.08f * glowStrength);
                return;
            }

            if (!slumpDone && t >= HoldEnd + 3) {
                //瘫软拍：泄劲的湿响
                slumpDone = true;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.45f, Pitch = -0.6f, MaxInstances = 3 }, basePos);
            }

            //沉回：藤身边缩边化血珠
            if (!Main.dedServ && t % 3 == 0 && CurrentHeight() > 24f) {
                float u = Main.rand.NextFloat(0.2f, 1f);
                Vector2 p = SamplePoint(u);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(p + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1f, 2.4f)),
                    BloodMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
            if (viewed && t == LimpEnd - 10) {
                KikasaDomainDeco.RippleAt(basePos, 0.6f);
                KikasaDomainDeco.SplashAt(basePos, 4);
            }
        }

        //==================== 形体（绘制与碰撞共用同一条参数曲线）====================

        /// <summary>当前伸出高度：暴起急升、鞭笞持满、瘫软加速缩回</summary>
        private float CurrentHeight() {
            int t = (int)Life;
            float h = StrikeHeight;
            if (t <= TellFrames) {
                return 0f;
            }
            if (t <= BurstEnd) {
                float u = (t - TellFrames) / (float)(BurstEnd - TellFrames);
                return h * (1f - MathF.Pow(1f - u, 3f));
            }
            if (t <= HoldEnd) {
                //满高微颤：绷紧的鞭在抖
                return h * (1f + MathF.Sin(t * 1.7f + Seed) * 0.008f);
            }
            float v = MathHelper.Clamp((t - HoldEnd) / (float)(LimpEnd - HoldEnd), 0f, 1f);
            return h * (1f - v * v);
        }

        /// <summary>
        /// 藤身参数点：u=0 基部（水面）→ u=1 梢部。
        /// 暴起近直、鞭笞段梢部向 LashDir 甩弧、瘫软段整体垂坠打弯
        /// </summary>
        private Vector2 SamplePoint(float u) {
            int t = (int)Life;
            float height = CurrentHeight();
            Vector2 basePos = Projectile.Center;

            //鞭梢甩弧：hold 段一记从蓄到放的侧向曲线，上端权重大
            float lash = 0f;
            if (t > TellFrames && t <= HoldEnd) {
                float lp = MathHelper.Clamp((t - TellFrames) / (float)(HoldEnd - TellFrames), 0f, 1f);
                lash = MathF.Sin(lp * MathHelper.Pi) * 30f * LashDir;
            }
            //瘫软：越上越塌的横向失稳
            float limp = 0f;
            if (t > HoldEnd) {
                float v = MathHelper.Clamp((t - HoldEnd) / (float)(LimpEnd - HoldEnd), 0f, 1f);
                limp = (MathF.Sin(u * 9.4f + Life * 0.16f + Seed) * 16f + LashDir * 26f) * v;
            }
            //常态轻微 S 身条
            float sway = MathF.Sin(u * 5.2f + Seed * 3f) * 4f;

            float x = basePos.X + (sway + lash * MathF.Pow(u, 2.2f) + limp * u) * 1f;
            float y = basePos.Y - height * u;
            return new Vector2(x, y);
        }

        //==================== 命中 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(18f, 18f),
                    new Vector2(LashDir * Main.rand.NextFloat(1f, 3.5f), -Main.rand.NextFloat(1f, 4f)),
                    BloodMain * 0.6f, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.4f, 0.4f));
            }
            PRTLoader.NewParticle<PRT_DWave>(target.Center, Vector2.Zero, BloodDeep, 0.07f)
                ?.Configure(new Vector2(0.6f, 1f), -MathHelper.PiOver2, 0.2f, 8);
            SoundEngine.PlaySound(SoundID.NPCHit7 with { Volume = 0.55f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕：基部一圈余珠，鞭已沉回湖里
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), -4f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 1.6f)),
                    BloodDeep * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            int t = (int)Life;
            SpriteBatch sb = Main.spriteBatch;

            if (t <= TellFrames) {
                DrawTell(sb, t);
                return false;
            }

            float height = CurrentHeight();
            if (height < 6f) {
                return false;
            }

            Main.instance.LoadNPC(NPCID.PlanterasTentacle);
            Texture2D chain = TextureAssets.Chain27?.Value;
            Texture2D tipTex = TextureAssets.Npc[NPCID.PlanterasTentacle]?.Value;
            if (chain == null || tipTex == null) {
                return false;
            }

            float fade = t > LimpEnd - 12
                ? MathHelper.Clamp((LimpEnd - t) / 12f, 0f, 1f) : 1f;
            Color col = Color.Lerp(Color.White, BloodMain, 0.66f) * fade;
            Color deep = Color.Lerp(Color.White, BloodDeep, 0.78f) * (fade * 0.95f);

            //暴起余像：极速拉出时留 1~2 帧残影（速度门控）
            if (t > TellFrames && t <= BurstEnd + 2) {
                DrawVineStrip(sb, chain, tipTex, BloodMain * (0.25f * fade), 0.72f);
            }

            //本体藤条 + 梢部触头
            DrawVineStrip(sb, chain, tipTex, col, 1f, deep);

            //加色层：鞭笞窗内梢部湿亮 + 基部水光
            if (t <= HoldEnd) {
                Texture2D glowTex = CWRAsset.SoftGlow?.Value;
                if (glowTex != null) {
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Vector2 tip = SamplePoint(1f);
                    float a = MathHelper.Clamp((t - TellFrames) / 4f, 0f, 1f) * fade;
                    sb.Draw(glowTex, tip - Main.screenPosition, null, PetalPink * (0.4f * a), 0f,
                        glowTex.Size() * 0.5f, new Vector2(26f * 2f / glowTex.Width), SpriteEffects.None, 0f);
                    sb.Draw(glowTex, Projectile.Center - Main.screenPosition, null, FoamGlow * (0.3f * a), 0f,
                        glowTex.Size() * 0.5f, new Vector2(40f * 2.4f / glowTex.Width, 40f * 0.6f / glowTex.Height), SpriteEffects.None, 0f);
                    sb.End();
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                }
            }
            return false;
        }

        /// <summary>荆棘藤条带：原版触手细链 16px 逐段贴着参数曲线铺，梢端压触头贴图</summary>
        private void DrawVineStrip(SpriteBatch sb, Texture2D chain, Texture2D tipTex, Color col, float scaleMul, Color? deepOverride = null) {
            float height = CurrentHeight() * scaleMul;
            if (height < 6f) {
                return;
            }
            int steps = Math.Min((int)(height / 15f) + 1, 42);
            Vector2 prev = SamplePoint(0f);
            for (int s = 1; s <= steps; s++) {
                float u = s / (float)steps * scaleMul;
                Vector2 p = SamplePoint(u);
                Vector2 seg = p - prev;
                float len = seg.Length();
                if (len > 0.5f) {
                    int srcH = (int)MathF.Min(len + 1f, chain.Height);
                    //基部沉色、越梢越亮
                    Color c = deepOverride.HasValue && u < 0.3f ? deepOverride.Value : col;
                    sb.Draw(chain, (prev + p) * 0.5f - Main.screenPosition,
                        new Rectangle(0, 0, chain.Width, srcH), c,
                        seg.ToRotation() - MathHelper.PiOver2,
                        new Vector2(chain.Width * 0.5f, srcH * 0.5f), 1f, SpriteEffects.None, 0f);
                }
                prev = p;
            }

            //梢部触头：贴图爪朝上原生，沿末段切线摆头
            int tipFrames = Main.npcFrameCount[NPCID.PlanterasTentacle];
            int tipH = tipTex.Height / tipFrames;
            Rectangle tipFrame = new(0, tipH * ((int)Life / 5 % tipFrames), tipTex.Width, tipH);
            Vector2 tipPos = SamplePoint(scaleMul);
            Vector2 tangent = tipPos - SamplePoint(MathF.Max(scaleMul - 0.12f, 0f));
            float rot = tangent.ToRotation() + MathHelper.PiOver2;
            sb.Draw(tipTex, tipPos - Main.screenPosition, tipFrame, col, rot,
                tipFrame.Size() * 0.5f, 0.95f, SpriteEffects.None, 0f);
        }

        /// <summary>tell 的水面鼓包：一枚贴水扁光斑随预告涨大，最后几帧透出荆棘暗尖</summary>
        private void DrawTell(SpriteBatch sb, int t) {
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex == null || t < 2) {
                return;
            }
            float p = MathHelper.Clamp(t / (float)TellFrames, 0f, 1f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            float w = MathHelper.Lerp(34f, 62f, p);
            float h = MathHelper.Lerp(6f, 14f, p * p);
            sb.Draw(glowTex, Projectile.Center - new Vector2(0f, 2f) - Main.screenPosition, null,
                BloodMain with { A = 0 } * (0.2f + 0.3f * p), 0f, glowTex.Size() * 0.5f,
                new Vector2(w * 2f / glowTex.Width, h * 2f / glowTex.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //荆棘暗尖：破水前最后的实体预告
            if (t > TellFrames - 8) {
                Texture2D chain = TextureAssets.Chain27?.Value;
                if (chain != null) {
                    float poke = (t - (TellFrames - 8)) / 8f;
                    for (int k = -1; k <= 1; k++) {
                        Vector2 pos = Projectile.Center + new Vector2(k * 8f, 2f - poke * (7f + 3f * k * k));
                        sb.Draw(chain, pos - Main.screenPosition, new Rectangle(0, 0, chain.Width, 10),
                            Color.Lerp(Color.White, BloodDeep, 0.85f) * (0.8f * poke), k * 0.22f,
                            new Vector2(chain.Width * 0.5f, 10f), 0.8f, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        private bool ViewedOwner
            => KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
