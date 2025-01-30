using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 群星巨舰
    /// </summary>
    internal class Starship : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override void SetDefaults() {
            Item.SetItemCopySD<Infinity>();
            Item.SetCartridgeGun<StarshipHeld>(1300);
        }
    }

    internal class StarshipAmmo : ModProjectile, ICWRLoader, IPrimitiveDrawable, IWarpDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        public static Asset<Texture2D> ArcWave { get; private set; }
        public static Asset<Texture2D> ColorGradientTop { get; private set; }
        public static Asset<Texture2D> ColorGradientBttom { get; private set; }
        public static Asset<Texture2D> TransverseTwill { get; private set; }
        public int trailCount = 10;
        public int trailWidth = 8;
        public float trailAlpha = 1;
        private bool init = true;
        public Trail trail;
        public Trail warpTrail;
        public int Timer;
        void ICWRLoader.LoadAsset() {
            ArcWave = CWRUtils.GetT2DAsset(CWRConstant.Masking + "ArcWave");
            ColorGradientTop = CWRUtils.GetT2DAsset(CWRConstant.ColorBar + "DevilsDevastation_Bar");
            ColorGradientBttom = CWRUtils.GetT2DAsset(CWRConstant.ColorBar + "Greentide_Bar");
            TransverseTwill = CWRUtils.GetT2DAsset(CWRConstant.Masking + "TransverseTwill");
        }
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.extraUpdates = 3;
            Projectile.friendly = true;
            Projectile.timeLeft = 60 * Projectile.MaxUpdates * 4;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Initialize();
            Projectile.rotation = Projectile.velocity.ToRotation();
            bool normal = false;
            UpdateOldPos(normal);
            SpawnDust(normal);
            Timer++;
        }

        private void UpdateOldPos(bool normal) {
            if (Timer % 2 == 0) {
                if (!VaultUtils.isServer) {
                    Projectile.UpdateOldPosCache();

                    Vector2[] pos2 = new Vector2[trailCount + 4];

                    //延长一下拖尾数组，因为使用的贴图比较特别
                    for (int i = 0; i < Projectile.oldPos.Length; i++)
                        pos2[i] = Projectile.oldPos[i] + Projectile.velocity;

                    Vector2 dir = Projectile.rotation.ToRotationVector2();
                    int exLength = normal ? 4 : 12;

                    for (int i = 1; i < 5; i++)
                        pos2[trailCount + i - 1] = Projectile.oldPos[^1] + dir * i * exLength + Projectile.velocity;

                    trail.TrailPositions = pos2;

                    if (!normal)
                        warpTrail.TrailPositions = Projectile.oldPos;
                }
            }
        }

        private void SpawnDust(bool normal) {
            if (Main.rand.NextBool(3)) {
                int width = normal ? 8 : 24;

                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(width, width), DustID.FireworksRGB
                    , Projectile.velocity * Main.rand.NextFloat(0.4f, 0.8f), 75, Scale: Main.rand.NextFloat(1, 1.4f));
                d.noGravity = true;
                d.color = Main.DiscoColor;
            }
        }

        public void Initialize() {
            if (init) {
                init = false;
                trailWidth = 5;
                trailCount = 8;

                if (!VaultUtils.isServer) {
                    Projectile.InitOldPosCache(trailCount);
                    trail = new Trail(Main.instance.GraphicsDevice, trailCount + 4, new EmptyMeshGenerator()
                        , f => trailWidth * trailAlpha, f => new Color(255, 255, 255, 170));
                    warpTrail = new Trail(Main.instance.GraphicsDevice, trailCount, new EmptyMeshGenerator()
                            , f => (trailWidth + 30) * trailAlpha, f => {
                                float r = Projectile.rotation % 6.18f;
                                float dir = (r >= 3.14f ? r - 3.14f : r + 3.14f) / MathHelper.TwoPi;
                                float p = 1 - f.X;
                                return new Color(dir, p, 0f, p);
                            });
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            float rot = Projectile.rotation + MathHelper.Pi;
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextFromList(DustID.ApprenticeStorm, DustID.IceTorch)
                    , (rot + Main.rand.NextFloat(-0.2f, 0.2f)).ToRotationVector2() * Main.rand.NextFloat(2f, 8f), 50, Scale: Main.rand.NextFloat(1f, 2f));
                d.noGravity = true;
            }

            rot -= MathHelper.Pi;
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextFromList(DustID.ApprenticeStorm, DustID.IceTorch)
                    , (rot + Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2() * Main.rand.NextFloat(1f, 6f), 50, Scale: Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = -oldVelocity;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trail == null)
                return;

            Effect effect = Filters.Scene["CWRMod:gradientTrail"].GetShader().Shader;
            effect.Parameters["transformMatrix"].SetValue(CWRUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "LightShot"));
            effect.Parameters["uFlow"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "Airflow"));
            effect.Parameters["uGradient"].SetValue(ColorGradientBttom.Value);
            effect.Parameters["uDissolve"].SetValue(TransverseTwill.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.NonPremultiplied;
            trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            trail?.DrawTrail(effect);

            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        void IWarpDrawable.Warp() {
            if (warpTrail == null) {
                return;
            }

            Effect effect = Filters.Scene["CWRMod:trailWarp"].GetShader().Shader;

            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "LightShot"));
            effect.Parameters["uFlow"].SetValue(TransverseTwill.Value);
            effect.Parameters["uTransform"].SetValue(CWRUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);

            warpTrail?.DrawTrail(effect);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D tex = ArcWave.Value;

            Vector2 scale = new Vector2(0.8f, 0.55f * trailAlpha) * 0.65f;
            Vector2 pos = Projectile.Center - Projectile.rotation.ToRotationVector2() * 12;
            Effect effect = Filters.Scene["CWRMod:gradientTrail"].GetShader().Shader;

            effect.Parameters["transformMatrix"].SetValue(CWRUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.03f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.1f);
            effect.Parameters["udissolveS"].SetValue(2f);
            effect.Parameters["uBaseImage"].SetValue(ArcWave.Value);
            effect.Parameters["uFlow"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "Airflow"));
            effect.Parameters["uGradient"].SetValue(ColorGradientBttom.Value);
            effect.Parameters["uDissolve"].SetValue(TransverseTwill.Value);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(tex, pos, null
                , new Color(108, 133, 161), Projectile.rotation, tex.Size() / 2, scale * 1.1f, 0, 0);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(tex, pos - Main.screenPosition, null, new Color(255, 255, 255, 120)
                , Projectile.rotation, tex.Size() / 2, scale, 0, 0);
        }

        public void DrawCustom(SpriteBatch spriteBatch) { }
    }

    internal class StarshipHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Starship";
        public override int TargetID => ModContent.ItemType<Starship>();
        public override void SetRangedProperty() {
            kreloadMaxTime = 110;
            FireTime = 10;
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 5;
            HandFireDistanceX = 40;
            HandFireDistanceY = 0;
            ShootPosNorlLengValue = 0;
            ShootPosToMouLengValue = 40;
            LoadingAmmoAnimation_AlwaysSetInFireRoding = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            RepeatedCartridgeChange = true;
            MustConsumeAmmunition = false;
            HandheldDisplay = false;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0.2f;
            RangeOfStress = 25;
        }

        public override void PreInOwnerUpdate() {

        }

        public override void PostInOwnerUpdate() {

        }

        public override void HanderSpwanDust() {

        }

        public override void FiringShoot() {

        }
    }
}
