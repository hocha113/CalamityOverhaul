using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 鏡樋「镜写」：疾走终点立一枚纸镜。<br/>
    /// 你之后的第一记刀命中时，立像同步复刻同一记斩击朝它自己的正面挥出，然后碎。<br/>
    /// 复刻是"再来一刀"，落点由立像所在决定——所以疾走停在哪、面朝哪，是有讲究的。<br/>
    /// ai[0]=立像朝向(±1) ai[1]=基础武器伤害
    /// </summary>
    internal class OniMeiMirrorStand : ModProjectile, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RiseFrames = 8;
        private const int ShatterFrames = 16;
        private const float StandHalfWidth = 22f;
        private const float StandHalfHeight = 34f;

        /// <summary>镜中人相对镜心下沉的量，让人落在镜面里而不是浮在框外</summary>
        private const float MirrorCloneDrop = 4f;

        private static readonly Color GlassBody = new(206, 208, 214);
        private static readonly Color GlassRim = new(255, 240, 226);
        private static readonly Vector3 PaperV = new(0.86f, 0.80f, 0.68f);
        private static readonly Vector3 GlassV = new(0.79f, 0.81f, 0.85f);
        private static readonly Vector3 DeepV = new(0.07f, 0.05f, 0.08f);
        private static readonly Vector3 RimV = new(0.78f, 0.15f, 0.13f);

        private int timer;
        private bool shattered;
        private int shatterTimer;
        private float swayPhase;
        /// <summary>立镜那一刻的持刀者姿态：镜里照的是当时的你，不是现在的你</summary>
        private bool poseCaptured;
        private int snapshotDirection = 1;
        private float snapshotGravDir = 1f;
        private Rectangle snapshotBodyFrame;
        private Rectangle snapshotLegFrame;

        private int Facing => Projectile.ai[0] >= 0f ? 1 : -1;
        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[1]);
        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = (int)(StandHalfWidth * 2f);
            Projectile.height = (int)(StandHalfHeight * 2f);
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = OniMeiCombat.MirrorStandLifeTicks;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端立像；同时只留一枚，新的顶掉旧的</summary>
        internal static bool TryPlace(Player player, Vector2 at, int facing, int baseWeaponDamage,
            IEntitySource source = null) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiMirrorStand>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    proj.Kill();
                }
            }
            Projectile spawned = Projectile.NewProjectileDirect(
                source ?? player.GetSource_Misc("CWR_OniMeiMirrorStand"), at, Vector2.Zero,
                type, 0, 0f, player.whoAmI,
                ai0: facing >= 0 ? 1f : -1f, ai1: Math.Max(1, baseWeaponDamage));
            return spawned.active;
        }

        /// <summary>
        /// 你的刀落下了，镜子跟着落一刀。<br/>
        /// 只认在场那一枚，复刻完即碎；返回是否真的复刻了
        /// </summary>
        internal static bool TryEcho(Player player, float aim, float knockback, float sizeMul) {
            if (player == null || Main.myPlayer != player.whoAmI) {
                return false;
            }
            int type = ModContent.ProjectileType<OniMeiMirrorStand>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type
                    || proj.ModProjectile is not OniMeiMirrorStand stand || stand.shattered
                    || stand.timer < RiseFrames) {
                    continue;
                }
                stand.Echo(aim, knockback, sizeMul);
                return true;
            }
            return false;
        }

        private void Echo(float aim, float knockback, float sizeMul) {
            shattered = true;
            shatterTimer = 0;
            Projectile.netUpdate = true;

            //镜里那一刀朝立像自己的正面挥，不是照抄玩家的角度——
            //所以"把镜子摆在哪、朝哪"才是这枚铭真正在玩的东西
            float mirrored = Facing > 0 ? 0f : MathHelper.Pi;
            //玩家那一刀的上下取向仍然保留，只把左右换成立像的朝向
            float pitch = MathHelper.WrapAngle(aim);
            if (MathF.Abs(pitch) > MathHelper.PiOver2) {
                pitch = MathHelper.WrapAngle(MathHelper.Pi - pitch);
            }
            float echoAim = MathHelper.WrapAngle(mirrored + (Facing > 0 ? pitch : -pitch));

            int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.MirrorEchoDamageMul));
            CrimsonRendCleave.Fire(Owner, Projectile.Center, echoAim, damage, knockback * 0.4f,
                sizeMul * 0.85f, flip: Facing, Projectile.GetSource_FromAI(), CleaveStyle.MirrorEcho);
            PlayEchoCue();
        }

        public override void AI() {
            if (!poseCaptured) {
                CapturePose();
            }
            timer++;
            swayPhase += 0.06f;
            if (shattered && ++shatterTimer >= ShatterFrames) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.26f, 0.24f, 0.28f));
            if (!Main.dedServ && !shattered && timer % 12 == 0) {
                //镜面反光：一线冷白沿纸缘扫过，读作"这是面镜子不是纸片"
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    Projectile.Center + Main.rand.NextVector2Circular(StandHalfWidth, StandHalfHeight),
                    -Vector2.UnitY * 0.4f, GlassRim * 0.7f, Main.rand.NextFloat(0.08f, 0.14f))
                    ?.Configure(Main.rand.Next(14, 22), affectedByGravity: false);
            }
        }

        private void PlayEchoCue() {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.55f, Volume = 0.45f }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            Owner.CWR()?.GetScreenShake(1.2f);
            //碎镜：片状碎屑向外崩，带一点转动，不做成雾
            for (int i = 0; i < 12; i++) {
                float ang = MathHelper.TwoPi * i / 12f + Main.rand.NextFloat(-0.2f, 0.2f);
                PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(Projectile.Center,
                    ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 6.5f),
                    i % 3 == 0 ? GlassRim : GlassBody, Main.rand.NextFloat(0.18f, 0.34f))
                    ?.Configure(Main.rand.Next(18, 28));
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(shattered);
            writer.Write((short)shatterTimer);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            shattered = reader.ReadBoolean();
            shatterTimer = reader.ReadInt16();
        }

        /// <summary>
        /// 拓下立镜那一刻的持刀者姿态。<br/>
        /// 纯表现用，各机器上帧号差一格无所谓，所以不进网络；
        /// 真身之后怎么动都不影响镜里那一下
        /// </summary>
        private void CapturePose() {
            poseCaptured = true;
            Player owner = Owner;
            if (owner == null || !owner.active) {
                return;
            }
            snapshotDirection = owner.direction >= 0 ? 1 : -1;
            snapshotGravDir = owner.gravDir >= 0f ? 1f : -1f;
            snapshotBodyFrame = owner.bodyFrame;
            snapshotLegFrame = owner.legFrame;
        }

        /// <summary>本帧的透明度、立起进度、碎裂进度；三处绘制共用一份</summary>
        private bool TryPose(out float fade, out float rise, out float shatter) {
            fade = 0f;
            rise = 0f;
            shatter = 0f;
            if (Main.dedServ) {
                return false;
            }
            rise = MathHelper.Clamp(timer / (float)RiseFrames, 0f, 1f);
            shatter = shattered ? MathHelper.Clamp(shatterTimer / (float)ShatterFrames, 0f, 1f) : 0f;
            fade = rise * MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f) * (1f - shatter * 0.35f);
            return fade > 0.01f;
        }

        /// <summary>立起时从地里顶出来的那一小段位移</summary>
        private Vector2 DrawCenter(float rise)
            => Projectile.Center - Main.screenPosition + Vector2.UnitY * ((1f - rise) * 14f);

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (!TryPose(out float fade, out float rise, out float shatter)) {
                return;
            }
            Effect fx = EffectLoader.OniPaperMirror?.Value;
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
            fx.Parameters["uRise"]?.SetValue(rise);
            fx.Parameters["uShatter"]?.SetValue(shatter);
            fx.Parameters["uSheen"]?.SetValue(shatter);
            fx.Parameters["uOpacity"]?.SetValue(1f);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uColPaper"]?.SetValue(PaperV);
            fx.Parameters["uColGlass"]?.SetValue(GlassV);
            fx.Parameters["uColDeep"]?.SetValue(DeepV);
            fx.Parameters["uColRim"]?.SetValue(RimV);

            //碎裂期 quad 要放大，碎片崩出原框也得画得下
            float grow = 1f + shatter * 1.2f;
            Vector2 center = DrawCenter(rise);
            float sway = MathF.Sin(swayPhase) * 0.035f;
            Color tint = Color.White * fade;

            fx.CurrentTechnique = fx.Techniques["MirrorTech"];
            DrawCard(device, fx, center, sway, StandHalfWidth * 1.35f * grow,
                StandHalfHeight * 1.30f * grow, tint);

            if (shatter > 0.001f) {
                fx.CurrentTechnique = fx.Techniques["SheenTech"];
                DrawCard(device, fx, center, sway, StandHalfWidth * 1.35f,
                    StandHalfHeight * 1.30f, tint);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }

        private static void DrawCard(GraphicsDevice device, Effect fx, Vector2 center, float rotation,
            float halfX, float halfY, Color tint) {
            Vector2 right = new Vector2(halfX, 0f).RotatedBy(rotation);
            Vector2 down = new Vector2(0f, halfY).RotatedBy(rotation);
            VertexPositionColorTexture[] verts = [
                new((center - right - down).ToVector3(), tint, new Vector2(0f, 0f)),
                new((center + right - down).ToVector3(), tint, new Vector2(1f, 0f)),
                new((center - right + down).ToVector3(), tint, new Vector2(0f, 1f)),
                new((center + right + down).ToVector3(), tint, new Vector2(1f, 1f)),
            ];
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>
        /// 镜中人走遮挡层：镜面是图元层画的，会盖住 PreDraw，所以剪影必须更晚。<br/>
        /// 姿态取自立镜那一刻的持刀者快照，左右按立像朝向翻——照的是你自己
        /// </summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (!TryPose(out float fade, out float rise, out float shatter) || !poseCaptured) {
                return;
            }
            Player owner = Owner;
            if (owner == null || !owner.active) {
                return;
            }
            float alpha = fade * (1f - shatter);
            if (alpha <= 0.01f) {
                return;
            }

            //镜里的人比镜框小一号，且随碎裂一起散
            Vector2 center = DrawCenter(rise) + Main.screenPosition;
            Vector2 topLeft = center - owner.Size * 0.5f + Vector2.UnitY * MirrorCloneDrop;
            int mirrored = -snapshotDirection * Facing;

            PlayerCloneRenderer.Prepare(owner);
            Color outline = new Color(112, 12, 25, 185) * (alpha * 0.72f);
            Color ink = new Color(10, 6, 12, 225) * (alpha * 0.80f);
            const float outlineWidth = 1.6f;
            DrawClone(topLeft + new Vector2(outlineWidth, 0f), outline, mirrored);
            DrawClone(topLeft - new Vector2(outlineWidth, 0f), outline, mirrored);
            DrawClone(topLeft + new Vector2(0f, outlineWidth), outline, mirrored);
            DrawClone(topLeft - new Vector2(0f, outlineWidth), outline, mirrored);
            DrawClone(topLeft, ink, mirrored);

            //镜中那把刀：朝向跟着立像，读作"镜里的人正对着你举刀"
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 size = blade.Size();
            Vector2 origin = size * OniBladePose.HiltUV;
            SpriteEffects effects = SpriteEffects.None;
            if (Facing < 0) {
                effects = SpriteEffects.FlipVertically;
                origin.Y = size.Y - origin.Y;
            }
            spriteBatch.Draw(blade, DrawCenter(rise), null,
                new Color(44, 28, 36) * (alpha * 0.85f),
                Facing > 0 ? -0.9f : 0.9f, origin, 0.55f, effects, 0f);
        }

        private void DrawClone(Vector2 position, Color color, int direction) {
            PlayerCloneRenderer.DrawPrepared(position, color, direction,
                snapshotBodyFrame, snapshotLegFrame, 0f, Vector2.Zero, snapshotGravDir);
        }
    }
}
