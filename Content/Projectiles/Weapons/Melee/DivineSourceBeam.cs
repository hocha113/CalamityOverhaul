using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class DivineSourceBeam : ModProjectile, IPrimitiveDrawable
    {
        public Vector2[] ControlPoints;
        private Player owner => CWRUtils.GetPlayerInstance(Projectile.owner);
        public const float EndRot = 60 * CWRUtils.atoR;
        public const float StarRot = -170 * CWRUtils.atoR;
        public const float LEndRot = -240 * CWRUtils.atoR;
        public const float LStarRot = -10 * CWRUtils.atoR;
        public bool Flipped => Projectile.ai[0] == 1f;
        public override string Texture => CWRConstant.Placeholder;
        private Trail Trail;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 144;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = CWRRef.GetTrueMeleeDamageClass();
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source)
            => Projectile.rotation = Projectile.velocity.X > 0 ? -160 : 90;

        public IEnumerable<Vector2> GenerateSlashPoints(bool dir) {
            float starRot = StarRot;
            float endRot = EndRot;
            if (dir) {
                starRot = LStarRot - 120 * CWRUtils.atoR;
                endRot = LEndRot + 30 * CWRUtils.atoR - 120 * CWRUtils.atoR;
            }

            for (int i = 0; i < 30; i++) {
                float completion = MathHelper.Lerp(endRot + Projectile.rotation.AtoR(), starRot + Projectile.rotation.AtoR(), i / 30f);
                completion *= Math.Sign(Projectile.velocity.X) * -1;
                yield return completion.ToRotationVector2() * 84f;
            }
        }

        public override void AI() {
            if (owner != null) {
                Projectile.position += owner.CWR().PlayerPositionChange;
            }
            Projectile.Opacity = Utils.GetLerpValue(Projectile.localAI[0], 26f, Projectile.timeLeft, clamped: true);
            Projectile.velocity *= 0.91f;
            Projectile.scale *= 1.03f;
            Projectile.rotation -= 5f * Math.Sign(Projectile.velocity.X);
        }

        public float GetWidthFunc(float completionRatio) {
            return Projectile.scale * 50f;
        }

        public Color GetColorFunc(Vector2 completionRatio) {
            float sengs = MathF.Sin(completionRatio.X * MathF.PI);
            if (completionRatio.X < 0.4f) {
                sengs = MathF.Pow(completionRatio.X, 3) * 13;
            }
            return Color.Lime * sengs * Projectile.Opacity;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (ControlPoints == null || ControlPoints.Length == 0) {
                return;
            }

            //准备轨迹点 - 根据方向翻转处理
            Vector2[] positions = new Vector2[ControlPoints.Length];
            bool facingLeft = Projectile.velocity.X < 0;

            for (int i = 0; i < ControlPoints.Length; i++) {
                Vector2 offset = ControlPoints[i] + ControlPoints[i].SafeNormalize(Vector2.Zero) * (Projectile.scale - 1f) * 70f;

                // 如果朝左，需要水平翻转控制点
                if (facingLeft) {
                    offset.X = -offset.X;
                }

                positions[i] = offset + Projectile.Center + new Vector2(0, -60);
            }

            //创建或更新 Trail
            Trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = positions;

            //使用 InnoVault 的绘制方法
            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "SlashFlatBlurHVMirror"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "DragonRage_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Placeholder_White.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            for (int j = 0; j < 3; j++) {
                Trail?.DrawTrail(effect);
            }
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        public override bool PreDraw(ref Color lightColor) {
            ControlPoints = GenerateSlashPoints(Projectile.velocity.X < 0).ToArray();
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool collBool = false;
            float point = 0;
            Vector2 starPos = Projectile.Center;
            for (int i = 0; i < 20; i++) {
                Vector2 endPos = Projectile.Center + MathHelper.ToRadians(-160 + 11 * i).ToRotationVector2() * Projectile.scale * 120;
                if (Projectile.velocity.X < 0)
                    endPos = Projectile.Center + MathHelper.ToRadians(20 - 11 * i).ToRotationVector2() * Projectile.scale * 120;
                collBool = Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), starPos, endPos, 32, ref point);
                if (collBool) {
                    break;
                }
            }

            return collBool;
        }
    }
}
