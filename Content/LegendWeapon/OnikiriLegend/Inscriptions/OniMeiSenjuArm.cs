using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 千手：终结定格期自玩家背后浮出的一只持刀鬼手。<br/>
    /// 六只错拍挥出，各自朝不同角度落一记断斩——终结那一停里同框多刀。<br/>
    /// 手本身不结算伤害，斩由 <see cref="CrimsonRendCleave"/> 承担，
    /// 所以它不会偷偷多打，看到几刀就是几刀。<br/>
    /// ai[0]=绕身角度 ai[1]=基础武器伤害 ai[2]=起手延迟(帧)
    /// </summary>
    internal class OniMeiSenjuArm : ModProjectile, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>浮现帧数</summary>
        private const int RiseFrames = 8;
        /// <summary>抬手蓄势帧数</summary>
        private const int WindupFrames = 9;
        /// <summary>挥出到收回的帧数</summary>
        private const int SwingFrames = 12;
        private const int FadeFrames = 8;
        /// <summary>手浮在离身多远</summary>
        private const float ArmRadius = 66f;
        /// <summary>挥砍的角幅</summary>
        private const float SwingSpan = 2.0f;
        /// <summary>臂条带的分段数，够让贝塞尔的弯读得出来</summary>
        private const int LimbSegments = 14;
        /// <summary>臂条带的物理半宽(px)：着色器在带内做锥度，这里给最粗处</summary>
        private const float LimbHalfWidth = 15f;
        /// <summary>手的 quad 半边(px)</summary>
        private const float HandHalfSize = 19f;

        private static readonly Vector3 ArmInkV = new(0.10f, 0.05f, 0.07f);
        private static readonly Vector3 ArmRimV = new(0.77f, 0.16f, 0.14f);
        private static readonly Vector3 ArmHotV = new(1.00f, 0.95f, 0.88f);
        private static readonly Color ArmInk = new(26, 12, 18);

        private int timer;
        private bool swung;

        private float Angle => Projectile.ai[0];
        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[1]);
        private int Delay => (int)Projectile.ai[2];
        private Player Owner => Main.player[Projectile.owner];
        private int Local => timer - Delay;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端一次浮出全部六手，错拍挥出</summary>
        internal static void FireVolley(Player player, int baseWeaponDamage, IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return;
            }
            int count = OniMeiCombat.SenjuArmCount;
            for (int i = 0; i < count; i++) {
                //上半周散开，读作"自背后长出来"而不是围成一圈
                float angle = MathHelper.Lerp(-MathHelper.Pi * 0.92f, -MathHelper.Pi * 0.08f,
                    count <= 1 ? 0.5f : i / (count - 1f));
                Projectile.NewProjectile(
                    source ?? player.GetSource_Misc("CWR_OniMeiSenjuArm"),
                    player.Center, Vector2.Zero, ModContent.ProjectileType<OniMeiSenjuArm>(),
                    0, 0f, player.whoAmI,
                    ai0: angle, ai1: Math.Max(1, baseWeaponDamage), ai2: i * 5);
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            timer++;
            Projectile.Center = Owner.Center + Angle.ToRotationVector2() * ArmRadius;

            int local = Local;
            if (local < 0) {
                return;
            }
            if (local == 0 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.20f, Volume = 0.24f },
                    Projectile.Center);
            }
            if (local == RiseFrames + WindupFrames && !swung) {
                swung = true;
                Swing();
            }
            if (local >= RiseFrames + WindupFrames + SwingFrames + FadeFrames) {
                Projectile.Kill();
                return;
            }
            if (!Main.dedServ && local < RiseFrames) {
                //浮现：墨自身后涌起聚成手，不是贴图淡入
                PRTLoader.NewParticle<PRT_OniInkDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 16f),
                    (Projectile.Center - Owner.Center).SafeNormalize(Vector2.UnitX) * 0.8f,
                    ArmInk, Main.rand.NextFloat(0.14f, 0.24f))
                    ?.Configure(Main.rand.Next(12, 20));
            }
        }

        private void Swing() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.SenjuArmDamageMul));
                //每只手朝自己所在的方向落刀：六个角度覆盖，不是同一刀画六遍
                float aim = Angle;
                CrimsonRendCleave.Fire(Owner, Projectile.Center + aim.ToRotationVector2() * 40f,
                    aim, damage, 3f, scale: 0.78f,
                    flip: Projectile.identity % 2 == 0 ? 1 : -1,
                    Projectile.GetSource_FromAI(), CleaveStyle.Plain);
            }
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(CWRSound.KatanaSwing with {
                Pitch = 0.35f + Projectile.identity % 5 * 0.06f,
                Volume = 0.34f,
                MaxInstances = 4,
            }, Projectile.Center);
        }

        /// <summary>
        /// 这一帧的姿态：透明度、探出度、攥紧度、刀线角。<br/>
        /// 三处绘制（条带臂 / 指链手 / 刀）都从这一份读，免得各自算出不同的手
        /// </summary>
        private bool TryPose(out float alpha, out float reach, out float grip, out float bladeAngle) {
            alpha = 0f;
            reach = 0f;
            grip = 0f;
            bladeAngle = 0f;
            int local = Local;
            if (Main.dedServ || local < 0) {
                return false;
            }
            reach = MathHelper.Clamp(local / (float)RiseFrames, 0f, 1f);
            int swingLocal = local - RiseFrames - WindupFrames;
            float fade = 1f;
            if (swingLocal > SwingFrames) {
                fade = MathHelper.Clamp(1f - (swingLocal - SwingFrames) / (float)FadeFrames, 0f, 1f);
            }
            alpha = reach * fade;
            if (alpha <= 0.01f) {
                return false;
            }
            //攥紧：探出期张着，蓄势期一路合拢，挥出后一直攥着
            grip = MathHelper.Clamp((local - RiseFrames) / (float)WindupFrames, 0f, 1f);

            //挥砍角：蓄势时向后拉，挥出时扫过 SwingSpan
            float swingT = swingLocal <= 0
                ? MathHelper.Clamp((local - RiseFrames) / (float)WindupFrames, 0f, 1f) * -0.35f
                : MathHelper.Clamp(swingLocal / (float)SwingFrames, 0f, 1f);
            bladeAngle = Angle + MathHelper.Lerp(-SwingSpan * 0.5f, SwingSpan * 0.5f,
                MathHelper.Clamp(swingT, 0f, 1f)) + (swingT < 0f ? swingT : 0f);
            return true;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>
        /// 刀走遮挡层：图元层（臂与手）跑在 EndEntityDraw，会盖住 PreDraw 的精灵，
        /// 所以刀得画在图元之后，否则手会糊在刀身上
        /// </summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (!TryPose(out float alpha, out _, out _, out float bladeAngle)) {
                return;
            }
            //刀：与本体同一套支点数学，尺寸压小，读作"分身的刀"
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 size = blade.Size();
            Vector2 origin = size * OniBladePose.HiltUV;
            Vector2 tip = size * OniBladePose.TipUV;
            float spriteAngle = (tip - origin).ToRotation();
            spriteBatch.Draw(blade, Projectile.Center - Main.screenPosition, null,
                Color.White * (alpha * 0.88f), bladeAngle - spriteAngle, origin, 0.72f,
                SpriteEffects.None, 0f);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!TryPose(out float alpha, out float reach, out float grip, out float bladeAngle)) {
                return;
            }
            Effect fx = EffectLoader.OniSenjuArm?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.6180339887f % 1f);
            fx.Parameters["uReach"]?.SetValue(reach);
            fx.Parameters["uGrip"]?.SetValue(grip);
            fx.Parameters["uOpacity"]?.SetValue(1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColInk"]?.SetValue(ArmInkV);
            fx.Parameters["uColRim"]?.SetValue(ArmRimV);
            fx.Parameters["uColHot"]?.SetValue(ArmHotV);

            Color tint = Color.White * alpha;
            fx.CurrentTechnique = fx.Techniques["ArmTech"];
            DrawLimb(device, fx, tint);

            fx.CurrentTechnique = fx.Techniques["HandTech"];
            DrawHand(device, fx, tint, bladeAngle);

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        /// <summary>
        /// 臂是一条弯的：肩到腕走二次贝塞尔，肘偏向按 identity 分左右，
        /// 六只手才不会长成同一根平行棍
        /// </summary>
        private void DrawLimb(GraphicsDevice device, Effect fx, Color tint) {
            Vector2 shoulder = Owner.Center - Main.screenPosition;
            Vector2 wrist = Projectile.Center - Main.screenPosition;
            Vector2 span = wrist - shoulder;
            float length = span.Length();
            if (length < 6f) {
                return;
            }
            Vector2 perp = span.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float bend = length * 0.26f * (Projectile.identity % 2 == 0 ? 1f : -1f);
            Vector2 elbow = shoulder + span * 0.5f + perp * bend;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[(LimbSegments + 1) * 2];
            for (int i = 0; i <= LimbSegments; i++) {
                float t = i / (float)LimbSegments;
                Vector2 point = Bezier(shoulder, elbow, wrist, t);
                //切向取相邻采样差分，转折处的法线才不会翻
                Vector2 next = Bezier(shoulder, elbow, wrist, MathHelper.Min(t + 0.02f, 1f));
                Vector2 prev = Bezier(shoulder, elbow, wrist, MathHelper.Max(t - 0.02f, 0f));
                Vector2 normal = (next - prev).SafeNormalize(Vector2.UnitX)
                    .RotatedBy(MathHelper.PiOver2);
                verts[i * 2] = new((point - normal * LimbHalfWidth).ToVector3(), tint,
                    new Vector2(t, 0f));
                verts[i * 2 + 1] = new((point + normal * LimbHalfWidth).ToVector3(), tint,
                    new Vector2(t, 1f));
            }
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, LimbSegments * 2);
            }
        }

        /// <summary>手：quad 按刀线转向，着色器的 +x 即握把方向</summary>
        private void DrawHand(GraphicsDevice device, Effect fx, Color tint, float bladeAngle) {
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 axis = bladeAngle.ToRotationVector2() * HandHalfSize;
            Vector2 side = axis.RotatedBy(MathHelper.PiOver2);
            VertexPositionColorTexture[] verts = [
                new((center - axis - side).ToVector3(), tint, new Vector2(0f, 0f)),
                new((center + axis - side).ToVector3(), tint, new Vector2(1f, 0f)),
                new((center - axis + side).ToVector3(), tint, new Vector2(0f, 1f)),
                new((center + axis + side).ToVector3(), tint, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t) {
            float inv = 1f - t;
            return inv * inv * a + 2f * inv * t * c + t * t * b;
        }
    }
}
