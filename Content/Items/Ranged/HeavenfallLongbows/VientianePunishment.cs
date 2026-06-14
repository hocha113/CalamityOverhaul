using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class VientianePunishment : ModProjectile, ICWRLoader
    {
        public override string Texture => CWRConstant.Placeholder;

        public Player Owner => Main.player[Projectile.owner];

        public ref float Time => ref Projectile.ai[1];

        public ref float TargetIndex => ref Projectile.ai[2];

        //PostSetupContent 的 SetupData 阶段填充，仅收录 CWRID 解析成功的灾厄弓
        private static int[] ValidBowItemIds = [];

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

        void ICWRLoader.SetupData() {
            ValidBowItemIds = [.. new int[] {
                CWRID.Item_Alluvion,
                CWRID.Item_ArterialAssault,
                CWRID.Item_AstralBow,
                CWRID.Item_AstrealDefeat,
                CWRID.Item_Barinade,
                CWRID.Item_Barinautical,
                CWRID.Item_BlossomFlux,
                CWRID.Item_BrimstoneFury,
                CWRID.Item_ClockworkBow,
                CWRID.Item_Contagion,
                CWRID.Item_CorrodedCaustibow,
                CWRID.Item_ContinentalGreatbow,
                CWRID.Item_DaemonsFlame,
                CWRID.Item_DarkechoGreatbow,
                CWRID.Item_Deathwind,
                CWRID.Item_Drataliornus,
                CWRID.Item_FlarewingBow,
                CWRID.Item_Galeforce,
                CWRID.Item_Goobow,
                CWRID.Item_HeavenlyGale,
                CWRID.Item_HoarfrostBow,
                CWRID.Item_LunarianBow,
                CWRID.Item_Malevolence,
                CWRID.Item_MarksmanBow,
                CWRID.Item_Monsoon,
                CWRID.Item_NettlevineGreatbow,
                CWRID.Item_Phangasm,
                CWRID.Item_PlanetaryAnnihilation,
                CWRID.Item_Shellshooter,
                CWRID.Item_TelluricGlare,
                CWRID.Item_TheBallista,
                CWRID.Item_TheMaelstrom,
                CWRID.Item_Ultima,
                CWRID.Item_Toxibow,
                CWRID.Item_VernalBolter,
            }.Where(CWRID.IsValid)];
        }

        void ICWRLoader.UnLoadData() {
            ValidBowItemIds = [];
        }

        public static Texture2D GetBowTexture(int index) {
            if (ValidBowItemIds.Length == 0) {
                return VaultAsset.placeholder3.Value;
            }
            int itemId = ValidBowItemIds[index % ValidBowItemIds.Length];
            Main.instance.LoadItem(itemId);
            return TextureAssets.Item[itemId].Value;
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
                    CWRAsset.ThunderTrail,
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

                lightningTrail.BasePositions = toTargetPath;
                if (Time % 3 == 0) {
                    lightningTrail.RandomThunder();
                }
            }
            else//否则，让万象跟随玩家鼠标
            {
                OrigPos = MousPos;
                if (Main.rand.NextBool(2) && !VaultUtils.isServer) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Unit() * 120;
                    Vector2 particleSpeed = pos.To(Projectile.Center).UnitVector() * 3;
                    Color color = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), VientianeColors);
                    PRTLoader.NewParticle<PRT_Light>(pos, particleSpeed
                        , color, 0.5f).Configure(60, opacity: 1, squishStrenght: 1.5f, hueShift: 0.0f, _entity: Owner, _followingRateRatio: 1);
                }
            }
            //位置等基础数据改写在末尾，等前置赋值完成
            Vector2 offset = (MathHelper.TwoPi / HeavenfallLongbow.MaxVientNum * Index).ToRotationVector2() * 320;
            Projectile.Center = OrigPos + Vector2.Lerp(Vector2.Zero, offset, sengs);
            Projectile.rotation = toMou.ToRotation();
            Projectile.scale = sengs;

            Time++;
        }

        public void SpanInfiniteRune(Vector2 orig, int maxNum, float prtslp, float slp, Color[] colors) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/PlasmaBolt".GetSound() with { Volume = 0.8f }, Projectile.Center);
            float rot = 0;
            if (!VaultUtils.isServer) {
                for (int j = 0; j < maxNum; j++) {
                    rot += MathHelper.TwoPi / maxNum;
                    float scale = 2f / (3f - (float)Math.Cos(2 * rot)) * slp;
                    float outwardMultiplier = MathHelper.Lerp(4f, 220f, Utils.GetLerpValue(0f, 120f, Time, true));
                    Vector2 lemniscateOffset = scale * new Vector2((float)Math.Cos(rot), (float)Math.Sin(2f * rot) / 2f);
                    Vector2 pos = orig + lemniscateOffset * outwardMultiplier;
                    Vector2 particleSpeed = Vector2.Zero;
                    Color color = VaultUtils.MultiStepColorLerp(j / maxNum, colors);
                    PRTLoader.NewParticle<PRT_Light>(pos, particleSpeed
                        , color, prtslp).Configure(120, opacity: 1, squishStrenght: 1.5f, hueShift: 0.0f, _followingRateRatio: 1);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                Texture2D value = GetBowTexture((int)Projectile.ai[0]);
                for (int i = 0; i < 16; i++) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(value.Width), new Vector2(0, -7)
                    , vientianeColor, Main.rand.NextFloat(0.3f, 0.7f)).Configure(60, opacity: 1, squishStrenght: 1.5f, hueShift: 0.0f, _followingRateRatio: 1);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Index == 0 && Time > 120
                ? VaultUtils.CircleIntersectsRectangle(OrigPos, 300, targetHitbox)
                : base.Colliding(projHitbox, targetHitbox);
        }

        public void GetColorDate() {
            Texture2D tex = GetBowTexture((int)Projectile.ai[0]);
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
            if (Time > 120)//在开始攻击之前不要进行特效的绘制
            {
                lightningTrail?.DrawThunder(Main.instance.GraphicsDevice);
            }

            Texture2D value = GetBowTexture((int)Projectile.ai[0]);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, value.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
