using CalamityMod;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeavenfallLongbowProj
{
    internal class VientianePunishment : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        internal PrimitiveTrail LightningDrawer;

        public Player Owner => Main.player[Projectile.owner];

        public ref float Time => ref Projectile.ai[1];

        public ref float TargetIndex => ref Projectile.ai[2];

        public static string[] VientianeTex = new string[]
        {
            "Alluvion",
            "AstralRepeater",
            "AstrealDefeat",
            "Barinade",
            "Barinautical",
            "BlossomFlux",
            "BrimstoneFury",
            "ClockworkBow",
            "Contagion",
            "ContinentalGreatbow",
            "CorrodedCaustibow",
            "CosmicBolter",
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
            "ArterialAssault"
        };

        public Color[] VientianeColors;

        public Color vientianeColor => CWRUtils.MultiLerpColor(Time % 90 / 90f, VientianeColors);

        public int Index;

        public int FemerProjIndex;

        private int TrailWig;

        private Vector2 oldMousPos;

        private Vector2 MousPos;

        private Vector2 OrigPos;

        private Vector2[] toTargetPath = new Vector2[62];

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

                if (!CWRUtils.isServer)
                    GetColorDate();
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

            if (Time >= 120)//一个攻击的阈值限定，如果大于该阈值，那么就会开始攻击
            {
                if (Time == 120) {
                    if (Index == 0) {
                        SoundEngine.PlaySound(new SoundStyle(CWRConstant.Sound + "Pedestruct"), Projectile.Center);
                        HeavenfallLongbow.Obliterate(OrigPos);
                        SpanInfiniteRune(OrigPos, 500, 1.5f, 2, HeavenfallLongbow.rainbowColors);
                    }
                    SpanInfiniteRune(Projectile.Center, 100, 0.5f, 0.5f, VientianeColors);
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
            }
            else//否则，让万象跟随玩家鼠标
            {
                OrigPos = MousPos;
                if (Main.rand.NextBool(2) && !CWRUtils.isServer) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 120;
                    Vector2 particleSpeed = pos.To(Projectile.Center).UnitVector() * 3;
                    Color color = CWRUtils.MultiLerpColor(Main.rand.NextFloat(), VientianeColors);
                    CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                        , 0.5f, color, 60, 1, 1.5f, hueShift: 0.0f, _entity: Owner, _followingRateRatio: 1);
                    CWRParticleHandler.AddParticle(energyLeak);
                }
            }
            //对于位置等基本数据的修改需要确保涉及到的数据被正确赋值后，这也就是为什么这一段会放在最后面
            Vector2 offset = (MathHelper.TwoPi / HeavenfallLongbow.MaxVientNum * Index).ToRotationVector2() * 320;
            Projectile.Center = OrigPos + Vector2.Lerp(Vector2.Zero, offset, sengs);
            Projectile.rotation = toMou.ToRotation();
            Projectile.scale = sengs;

            Time++;
        }

        public void SpanInfiniteRune(Vector2 orig, int maxNum, float prtslp, float slp, Color[] colors) {
            SoundEngine.PlaySound(CommonCalamitySounds.PlasmaBoltSound, Projectile.Center);
            float rot = 0;
            if (!CWRUtils.isServer) {
                for (int j = 0; j < maxNum; j++) {
                    rot += MathHelper.TwoPi / maxNum;
                    float scale = 2f / (3f - (float)Math.Cos(2 * rot)) * slp;
                    float outwardMultiplier = MathHelper.Lerp(4f, 220f, Utils.GetLerpValue(0f, 120f, Time, true));
                    Vector2 lemniscateOffset = scale * new Vector2((float)Math.Cos(rot), (float)Math.Sin(2f * rot) / 2f);
                    Vector2 pos = orig + lemniscateOffset * outwardMultiplier;
                    Vector2 particleSpeed = Vector2.Zero;
                    Color color = CWRUtils.MultiLerpColor(j / maxNum, colors);
                    CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                        , prtslp, color, 120, 1, 1.5f, hueShift: 0.0f, _entity: null, _followingRateRatio: 1);
                    CWRParticleHandler.AddParticle(energyLeak);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!CWRUtils.isServer) {
                Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Cay_Wap_Ranged + VientianeTex[(int)Projectile.ai[0]]);
                for (int i = 0; i < 16; i++) {
                    CWRParticle energyLeak = new LightParticle(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(value.Width), new Vector2(0, -7)
                    , Main.rand.NextFloat(0.3f, 0.7f), vientianeColor, 60, 1, 1.5f, hueShift: 0.0f, _entity: null, _followingRateRatio: 1);
                    CWRParticleHandler.AddParticle(energyLeak);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Index == 0 && Time > 120) {
                return CWRUtils.CircularHitboxCollision(OrigPos, 300, targetHitbox);
            }
            return base.Colliding(projHitbox, targetHitbox);
        }

        public void GetColorDate() {
            Texture2D tex = CWRUtils.GetT2DValue(CWRConstant.Cay_Wap_Ranged + VientianeTex[(int)Projectile.ai[0]]);
            Color[] colors = new Color[tex.Width * tex.Height];
            tex.GetData(colors);
            List<Color> nonTransparentColors = new List<Color>();
            foreach (Color color in colors) {
                if ((color.A > 0 || color.R > 0 || color.G > 0 || color.B > 0) && (color != Color.White && color != Color.Black)) {
                    nonTransparentColors.Add(color);
                }
            }
            VientianeColors = nonTransparentColors.ToArray();
        }

        public float PrimitiveWidthFunction(float completionRatio) {
            return CalamityUtils.Convert01To010(completionRatio) * Projectile.scale * TrailWig;
        }

        public Color PrimitiveColorFunction(float completionRatio) {
            return vientianeColor;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (LightningDrawer is null)
                LightningDrawer = new PrimitiveTrail(PrimitiveWidthFunction, PrimitiveColorFunction, PrimitiveTrail.RigidPointRetreivalFunction, GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"]);

            if (Time > 120)//在开始攻击之前不要进行特效的绘制
            {
                GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].UseImage1("Images/Misc/Perlin");
                GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].Apply();

                LightningDrawer.Draw(toTargetPath, Projectile.Size * 0.5f - Main.screenPosition, 50);
            }

            Texture2D value = CWRUtils.GetT2DValue(CWRConstant.Cay_Wap_Ranged + VientianeTex[(int)Projectile.ai[0]]);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, value.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
