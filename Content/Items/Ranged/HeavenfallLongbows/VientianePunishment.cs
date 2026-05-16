using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class VientianePunishment : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public Player Owner => Main.player[Projectile.owner];

        public ref float Time => ref Projectile.ai[1];

        public ref float TargetIndex => ref Projectile.ai[2];

        public static string[] VientianeTex = [
            "Alluvion",
            "ArterialAssault",
            "AstralBow",
            "AstrealDefeat",
            "Barinade",
            "Barinautical",
            "BlossomFlux",
            "BrimstoneFury",
            "ClockworkBow",
            "Contagion",
            "CorrodedCaustibow",
            "ContinentalGreatbow",
            "DaemonsFlame",
            "DarkechoGreatbow",
            "Deathwind",
            "Drataliornus",
            "FlarewingBow",
            "Galeforce",
            "Goobow",
            "HeavenlyGale",
            "HoarfrostBow",
            "LunarianBow",
            "Malevolence",
            "MarksmanBow",
            "Monsoon",
            "NettlevineGreatbow",
            "Phangasm",
            "PlanetaryAnnihilation",
            "Shellshooter",
            "TelluricGlare",
            "TheBallista",
            "TheMaelstrom",
            "Ultima",
            "Toxibow",
            "VernalBolter"
        ];

        public Color[] VientianeColors;

        public Color vientianeColor => VaultUtils.MultiStepColorLerp(Time % 90 / 90f, VientianeColors);

        public int Index;

        public int FemerProjIndex;

        private int TrailWig;

        private Vector2 oldMousPos;

        private Vector2 MousPos;

        private Vector2 OrigPos;

        private Vector2[] toTargetPath = new Vector2[62];

        private ThunderTrail lightningTrail;

        private float auraSpin;     //背景棱镜光环旋转角

        private static Dictionary<int, Asset<Texture2D>> BowTextures = new();

        public static Asset<Texture2D> GetBowTexture(int index) {
            if (BowTextures.TryGetValue(index, out var asset)) return asset;

            if (index >= 0 && index < VientianeTex.Length) {
                string path = CWRConstant.Cay_Wap_Ranged + VientianeTex[index];
                if (ModContent.HasAsset(path)) {
                    asset = CWRUtils.GetT2DAsset(path);
                    BowTextures[index] = asset;
                    return asset;
                }
            }
            return VaultAsset.placeholder3;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.WriteVector2(MousPos);
            writer.Write(Index);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            MousPos = reader.ReadVector2();
            Index = reader.ReadInt32();
        }

        public override void SetDefaults() {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 320;
        }

        public override void AI() {
            if (Time == 0) {

                if (!VaultUtils.isServer)
                    GetColorDate();
            }

            if (lightningTrail == null) {
                lightningTrail = new ThunderTrail(
                    CWRUtils.GetT2DAsset(CWRConstant.Masking + "ThunderTrail"),
                    GetTrailWidth,
                    GetTrailColor,
                    (f) => 1f
                );
                lightningTrail.SetExpandWidth(4);
                lightningTrail.SetRange((0, 5));
                lightningTrail.CanDraw = true;
                lightningTrail.UseNonOrAdd = true;
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                oldMousPos = MousPos;
                MousPos = Main.MouseWorld;
                if (oldMousPos != MousPos)
                    Projectile.netUpdate = true;
            }
            float sengs = Time / 60f;
            if (sengs > 1)
                sengs = 1;

            Vector2 toMou = Projectile.Center.To(OrigPos);

            //棱镜光环慢旋
            auraSpin += 0.012f;

            if (Time >= 120)//一个攻击的阈值限定, 如果大于该阈值, 那么就会开始攻击
            {
                if (Time == 120) {
                    if (Index == 0) {
                        SoundEngine.PlaySound(new SoundStyle(CWRConstant.Sound + "Pedestruct"), Projectile.Center);
                        HeavenfallLongbow.Obliterate(OrigPos);
                        SpanPrismRune(OrigPos, 120, 1.4f, HeavenfallLongbow.rainbowColors, auroraCount: 12, auroraLengthScale: 1.6f);
                    }
                    SpanPrismRune(Projectile.Center, 60, 0.55f
                        , VientianeColors != null && VientianeColors.Length > 1 ? VientianeColors : HeavenfallLongbow.rainbowColors
                        , auroraCount: 4, auroraLengthScale: 0.75f);
                }

                if (Time < 300) {
                    TrailWig += 2;
                    if (TrailWig > 32)
                        TrailWig = 32;
                }
                else {
                    TrailWig -= 2;
                    if (TrailWig < 0)
                        TrailWig = 0;
                }

                float stepSize = toMou.Length() / 62f;
                Vector2 rotToVr = Projectile.rotation.ToRotationVector2() * stepSize;
                for (int i = 0; i < toTargetPath.Length; i++) {
                    toTargetPath[i] = Projectile.Center + rotToVr * i;
                }

                lightningTrail.BasePositions = toTargetPath;
                if (Time % 3 == 0) {
                    lightningTrail.RandomThunder();
                }
            }
            else//否则, 让万象跟随玩家鼠标
            {
                OrigPos = MousPos;
                //集结期: 用棱镜碎片替代旧版稀疏的 PRT_Light, 视觉更精致
                if (Main.rand.NextBool(2) && !VaultUtils.isServer) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 120;
                    Vector2 particleSpeed = pos.To(Projectile.Center).UnitVector() * 3;
                    Color baseCol = VientianeColors != null && VientianeColors.Length > 0
                        ? VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), VientianeColors)
                        : VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), HeavenfallLongbow.rainbowColors);
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        pos, particleSpeed, baseCol,
                        Main.rand.NextFloat(0.4f, 0.7f), 60,
                        Main.rand.NextFloat(3f, 5f), shortStretch: true));
                }
            }
            //对于位置等基本数据的修改需要确保涉及到的数据被正确赋值后, 这也就是为什么这一段会放在最后面
            Vector2 offset = (MathHelper.TwoPi / HeavenfallLongbow.MaxVientNum * Index).ToRotationVector2() * 320;
            Projectile.Center = OrigPos + Vector2.Lerp(Vector2.Zero, offset, sengs);
            Projectile.rotation = toMou.ToRotation();
            Projectile.scale = sengs;

            Time++;
        }

        /// <summary>
        /// 万象惩戒发难的核心法术展示: 棱镜碎片 + 极光丝带螺旋. 替代旧版 500x PRT_Light 八字形撒粒子.
        /// </summary>
        public void SpanPrismRune(Vector2 orig, int prismCount, float prismScale, Color[] colors, int auroraCount, float auroraLengthScale) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaBolt".GetSound() with { Volume = 0.8f }, Projectile.Center);
            if (VaultUtils.isServer) {
                return;
            }
            if (colors == null || colors.Length == 0) {
                colors = HeavenfallLongbow.rainbowColors;
            }

            //棱镜碎片: 沿伯努利双纽线 (lemniscate of Bernoulli) 散布, 视觉与旧版兼容但密度大幅降低
            float rot = 0;
            float outward = MathHelper.Lerp(4f, 220f, Utils.GetLerpValue(0f, 120f, Time, true));
            for (int j = 0; j < prismCount; j++) {
                rot += MathHelper.TwoPi / prismCount;
                float scale = 2f / (3f - (float)Math.Cos(2 * rot)) * prismScale;
                Vector2 lemniscateOffset = scale * new Vector2((float)Math.Cos(rot), (float)Math.Sin(2f * rot) / 2f);
                Vector2 pos = orig + lemniscateOffset * outward;
                Color col = VaultUtils.MultiStepColorLerp(j / (float)prismCount, colors);
                PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                    pos, Vector2.Zero, col,
                    Main.rand.NextFloat(0.8f, 1.4f) * prismScale, Main.rand.Next(70, 110),
                    Main.rand.NextFloat(3f, 6f), shortStretch: true));
            }

            //极光丝带: 环形外放, 营造大爆发的史诗感
            for (int i = 0; i < auroraCount; i++) {
                float ang = MathHelper.TwoPi * i / auroraCount + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3.5f, 6.5f);
                PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                    orig, vel,
                    Main.rand.NextFloat(120f, 180f) * auroraLengthScale, Main.rand.NextFloat(22f, 32f),
                    Main.rand.Next(34, 50),
                    huePhase: i / (float)auroraCount, hueSpeed: 0.022f, driftScale: 1.2f));
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                Texture2D value = GetBowTexture((int)Projectile.ai[0]).Value;
                //旧 16x PRT_Light → 10x PRT_HeavenfallPrism (碎片散落) + 3x 极光
                for (int i = 0; i < 10; i++) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(value.Width);
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(2f, 5f));
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        pos, vel, vientianeColor,
                        Main.rand.NextFloat(0.55f, 1.0f), Main.rand.Next(40, 60),
                        Main.rand.NextFloat(3f, 5f), shortStretch: true));
                }
                for (int i = 0; i < 3; i++) {
                    float ang = MathHelper.TwoPi * i / 3f + Main.rand.NextFloat(0.2f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.2f, 3.5f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        Projectile.Center, vel,
                        Main.rand.NextFloat(80f, 130f), Main.rand.NextFloat(16f, 26f),
                        Main.rand.Next(28, 40),
                        huePhase: i / 3f, hueSpeed: 0.025f, driftScale: 1f));
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Index == 0 && Time > 120
                ? VaultUtils.CircleIntersectsRectangle(OrigPos, 300, targetHitbox)
                : base.Colliding(projHitbox, targetHitbox);
        }

        public void GetColorDate() {
            Texture2D tex = GetBowTexture((int)Projectile.ai[0]).Value;
            if (tex == null) return;
            Color[] colors = new Color[tex.Width * tex.Height];
            tex.GetData(colors);
            List<Color> nonTransparentColors = [];
            foreach (Color color in colors) {
                if ((color.A > 0 || color.R > 0 || color.G > 0 || color.B > 0) && color != Color.White && color != Color.Black) {
                    nonTransparentColors.Add(color);
                }
            }
            VientianeColors = [.. nonTransparentColors];
        }

        public float GetTrailWidth(float completionRatio) {
            return MathF.Sin(MathHelper.Pi * MathHelper.Clamp(completionRatio, 0f, 1f)) * Projectile.scale * TrailWig;
        }

        public Color GetTrailColor(float completionRatio) {
            return vientianeColor;
        }

        public override bool PreDraw(ref Color lightColor) {
            //背景棱镜光环 (用 Aura 着色器在弓体后面绘制旋转光晕)
            DrawAura();

            //闪电拖尾 (攻击阶段后绘制)
            if (Time > 120) {
                lightningTrail?.DrawThunder(Main.instance.GraphicsDevice);
            }

            //弓体: RGB 三相位色散叠加, 模拟棱镜折射
            Texture2D value = GetBowTexture((int)Projectile.ai[0]).Value;
            if (value == null) {
                return false;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = value.Size() * 0.5f;
            //色散偏移方向: 垂直于箭矢前进方向
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * 2.2f * Projectile.scale;

            Color cR = new(255, 80, 80, 120);
            Color cG = Color.White;
            Color cB = new(80, 110, 255, 120);

            Main.EntitySpriteDraw(value, drawPos - perp, null, cR * 0.55f, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos + perp, null, cB * 0.55f, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, drawPos, null, cG, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        private void DrawAura() {
            if (Projectile.scale < 0.05f) {
                return;
            }

            Effect shader = EffectLoader.HeavenfallPrismTrail?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || glow == null || noise == null) {
                return;
            }

            //攻击阶段更亮, 集结阶段较弱
            float intensity = Time > 120 ? 1.0f : MathHelper.Clamp(Time / 120f, 0f, 1f) * 0.6f;
            float fade = Projectile.scale * intensity;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f + auraSpin);
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["coreIntensity"]?.SetValue(0.5f + intensity * 0.4f);
            shader.Parameters["dispersion"]?.SetValue(0.06f);
            shader.Parameters["flowSpeed"]?.SetValue(0.4f);
            shader.Parameters["hueOffset"]?.SetValue(Index * 0.077f);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.CurrentTechnique = shader.Techniques["Aura"];

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

            float pulse = 1f + 0.07f * MathF.Sin((float)Main.timeForVisualEffects * 0.2f + Index);
            float baseSize = 220f * Projectile.scale * pulse;
            float scale = baseSize / glow.Width;
            sb.Draw(glow, Projectile.Center - Main.screenPosition, null, Color.White, auraSpin,
                glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
