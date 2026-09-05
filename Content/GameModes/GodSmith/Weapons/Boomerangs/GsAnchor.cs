using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 船锚重铸（本族重坠特化）。材质：锈铁船锚与铁链。签名行为：①抛物去程，过顶点即转直坠，
    /// 砸地轰出震荡波并短暂驻地 ②玩家与锚之间全程铁链逐节可见，回收时沿链加速拽回
    /// ③砸地时震屏与低沉铁鸣
    /// </summary>
    internal class GsAnchor : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Anchor;

        internal override int BoomerProjType => ModContent.ProjectileType<GsAnchorProj>();

        internal override float DamageMul => 1.10f;   //笨重难用的怪械，补一成底伤

        internal override float ThrowSpeedMul => 0.95f;

        protected override string GsDescFallback =>
            "Flies in a heavy arc, then plunges straight down past its peak; the slam quakes the ground\nfor 60% area damage and the anchor digs in a moment before reeling home along its chain";
    }

    /// <summary>铁锚体：沉锚重坠，链体逐节用原版链条贴图画出</summary>
    internal class GsAnchorProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Anchor;

        protected override int OutTime => 34;
        protected override float OutDrag => 0.985f;      //空气阻力，重力另加
        protected override int HoverTime => 70;          //坠落相上限（触地即提前结束）
        protected override float ReturnAccel => 0.7f;
        protected override float ReturnMaxSpeed => 19f;
        protected override int HitboxSize => 34;
        protected override float SpinRateMul => 0.6f;    //铁锚沉重，转不快
        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.7f, Pitch = -0.5f };

        /// <summary>ai[2]：0=坠落 1=驻地（借悬停相位实现重坠）</summary>
        private bool Embedded {
            get => Projectile.ai[2] == 1f;
            set => Projectile.ai[2] = value ? 1f : 0f;
        }

        protected override void OnOutTick(Player owner) {
            Projectile.velocity.Y += 0.30f;   //抛物重坠
        }

        /// <summary>过顶点（开始下坠）即转坠落相</summary>
        protected override bool OutFinished(Player owner)
            => PhaseTimer >= OutTime || Projectile.velocity.Y > 2f;

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase == PhaseHover) {
                //坠落起点：竖直向下加速，保持地形碰撞等触地
                Embedded = false;
                Projectile.velocity = new Vector2(Projectile.velocity.X * 0.1f, 6f);
                Projectile.tileCollide = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
                }
            }
        }

        protected override void OnHoverTick(Player owner) {
            if (Embedded) {
                //驻地：锚咬进地里，链条绷直，22 帧后起链回收
                Projectile.velocity = Vector2.Zero;
                if (PhaseTimer >= 22) {
                    EnterPhase(PhaseReturn, owner);
                }
                return;
            }
            //坠落：持续加速，压掉家族悬停的减速与浮沉
            Projectile.velocity.X *= 0.9f;
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 1.1f, 24f);
            //锚头朝下扶正（视觉近似，锚贴图对角朝向）
            Projectile.rotation = Projectile.rotation.AngleLerp(MathHelper.PiOver4, 0.18f);
        }

        /// <summary>坠落相自旋停摆，回收恢复慢旋</summary>
        protected override float SpinTarget(int phase) {
            if (phase == PhaseHover) {
                return 0f;
            }
            return base.SpinTarget(phase);
        }

        protected override bool HandleTileCollide(Vector2 oldVelocity) {
            //坠落触地：轰震 + 驻地
            if (Phase == PhaseHover && !Embedded) {
                Embedded = true;
                PhaseTimer = 0;
                Projectile.velocity = Vector2.Zero;
                QuakeSlam(oldVelocity);
                return false;
            }
            return base.HandleTileCollide(oldVelocity);
        }

        private void QuakeSlam(Vector2 impactVel) {
            //震荡波：owner 端生成 60% 伤害的贴地判定
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.6f));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center + new Vector2(0f, 6f), Vector2.Zero,
                    ModContent.ProjectileType<GsAnchorQuakeProj>(), dmg, 8f, Owner.whoAmI);
            }
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.9f, Pitch = -0.7f }, Projectile.Center);
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                Vector2.UnitY, 5f, 7f, 12, 900f, "GsAnchorSlam"));
        }

        //==================== 链体：原版链条贴图逐节一笔 ====================

        protected override void PreDrawUnder(SpriteBatch sb, Vector2 drawPos, Color lightColor) {
            Texture2D chain = TextureAssets.Chain.Value;
            Player owner = Owner;
            Vector2 from = owner.MountedCenter;
            Vector2 to = Projectile.Center;
            Vector2 delta = to - from;
            float dist = delta.Length();
            if (dist < 8f) {
                return;
            }
            Vector2 dir = delta / dist;
            float rot = dir.ToRotation() + MathHelper.PiOver2;
            int segs = (int)(dist / chain.Height) + 1;
            //驻地时链条绷直微颤（whoAmI 种子，不掷 Main.rand）
            float strain = Embedded ? MathF.Sin((Main.GlobalTimeWrappedHourly * 40f) + Projectile.whoAmI) * 1.2f : 0f;
            Vector2 normal = new(-dir.Y, dir.X);
            for (int i = 0; i < segs; i++) {
                Vector2 pos = from + (dir * chain.Height * i);
                //链条中段轻微下垂，两端收紧
                float sag = Embedded ? 0f : MathF.Sin(i / (float)segs * MathHelper.Pi) * MathF.Min(14f, dist * 0.03f);
                pos += (Vector2.UnitY * sag) + (normal * strain);
                Color c = Lighting.GetColor((int)(pos.X / 16f), (int)(pos.Y / 16f));
                sb.Draw(chain, pos - Main.screenPosition, null, c, rot,
                    new Vector2(chain.Width / 2f, chain.Height / 2f), 1f, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>沉锚震荡波：贴地宽判定，存活 3 帧的无形判定箱，不画本体</summary>
    internal class GsAnchorQuakeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3;
            Projectile.knockBack = 8f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
