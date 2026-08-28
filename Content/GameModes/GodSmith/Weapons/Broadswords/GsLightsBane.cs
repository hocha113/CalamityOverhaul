using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【暗影蚀刃】材质：淬暗影的恶魔铁。签名：①每一斩都在空中留下暗影蚀痕，
    /// 终结拍的蚀痕驻留灼噬 ②第三拍刀身先隐入暗影、再自影中爆发前压斩出
    /// ③命中迸溅恶魔紫焰与暗影火花
    /// </summary>
    internal class GsLightsBane : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.LightsBane;

        protected override int HeldProjID => ModContent.ProjectileType<GsLightsBaneHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash tears a shadow scar into the air; " +
            "the third strike sinks the blade into shadow, then erupts forward, and its scar lingers to corrode";

        //蚀影色板
        internal static readonly Color VoidBright = new(196, 156, 255); //苍紫刃缘
        internal static readonly Color VoidMain = new(108, 76, 190);    //恶魔铁紫
        internal static readonly Color VoidHot = new(168, 64, 255);     //蚀影亮紫
        internal static readonly Color VoidDeep = new(22, 10, 38);      //近黑暗紫

        //底伤 +6%：终结拍 1.3x + 每三拍一道灼噬蚀痕（满驻留 3 跳约 0.47x，实战常 2 跳），
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 110%~119%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 暗影蚀刃手持：三拍连击。0/1 交替快斩，2 影噬终结（举相刀身渐隐入影、
    /// 滞帧全隐蓄势、斩切自影中显形爆发+前压）。每拍收势时沿挥弧生成蚀痕
    /// （普通拍纯演出、终结拍灼噬）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsLightsBaneHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.LightsBane;
        protected override Color EdgeBright => GsLightsBane.VoidBright;
        protected override Color BodyMain => GsLightsBane.VoidMain;
        protected override Color HotAccent => GsLightsBane.VoidHot;
        protected override Color DeepShadow => GsLightsBane.VoidDeep;

        private bool scarSpawned;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //影噬终结：长举（渐隐入影）+ 全隐滞帧 + 快爆发（影中显形）
                return new GsBroadBeat {
                    Raise = 9, Hold = 3, Slash = 4, Recover = 11,
                    RaiseBack = 2.1f, Follow = 1.3f, ReachScale = 1.15f, LeanAmp = 0.085f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.4f, SwingPitch = -0.3f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = 5;
            b.Recover = 8;
            b.SwingPitch = stage == 0 ? -0.05f : -0.14f;
            return b;
        }

        //==================== 影噬演出 ====================

        /// <summary>终结拍的刀身可见度：举相渐隐入影，滞帧全隐，斩切首两帧自影显形</summary>
        protected override float BladeAlpha {
            get {
                if (!IsFinisher) {
                    return 1f;
                }
                int phase = CurrentPhase;
                if (phase == PhaseRaise) {
                    return MathHelper.Lerp(1f, 0.12f, timer / (float)raiseDur);
                }
                if (phase == PhaseHold) {
                    return 0.1f;
                }
                if (phase == PhaseSlash) {
                    int into = timer - raiseDur - holdDur;
                    return into <= 1 ? 0.55f : 1f;
                }
                return 1f;
            }
        }

        //暗质刀身吸光；刃缘常年渗紫
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsLightsBane.VoidDeep, 0.30f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsLightsBane.VoidHot : GsLightsBane.VoidBright;

        protected override void HandlePhaseEvents(int phase) {
            //影噬起手：一记低哑的暗影嘶声
            if (IsFinisher && timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.4f }, Owner.Center);
            }
            base.HandlePhaseEvents(phase);

            //收势首帧沿挥弧留下蚀痕：终结拍灼噬（damage>0），普通拍纯演出
            if (!scarSpawned && phase == PhaseRecover) {
                scarSpawned = true;
                float startAng = ArcStart - (swingDir * 0.08f);
                int scarDamage = IsFinisher ? Math.Max(1, (int)(Projectile.damage * 0.12f)) : 0;
                SpawnOwnedProj(ModContent.ProjectileType<GsLightsBaneScarProj>(), Hand, Vector2.Zero,
                    scarDamage, 0f, startAng, ArcEnd, FullReach);
            }
        }

        protected override void OnSlashBegin() {
            //自影中显形的爆发瞬间：刃身紫闪 + 一圈影尘外抛
            if (!IsFinisher) {
                return;
            }
            SetFlash(6);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = (mainAngle + Main.rand.NextFloat(-0.9f, 0.9f)).ToRotationVector2()
                    * Main.rand.NextFloat(2.5f, 6f);
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.3f, 0.9f)),
                    DustID.Shadowflame, vel, 80, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (IsFinisher && phase is PhaseRaise or PhaseHold) {
                //蓄影：暗影雾自四周向手心聚拢
                Vector2 hand = Hand;
                Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(38f, 70f);
                PRTLoader.NewParticle<PRT_Light>(at, (hand - at) * 0.13f, GsLightsBane.VoidHot,
                    Main.rand.NextFloat(0.06f, 0.11f))?.Configure(9, 0.55f);
            }
            else if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                //斩切期刃面渗出暗影火
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                    DustID.Shadowflame, Vector2.Zero, 100, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 1.6f;
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //恶魔紫焰迸溅
            int flames = IsFinisher ? 7 : 4;
            for (int i = 0; i < flames; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f), 60, default,
                    Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
            if (IsFinisher) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsLightsBane.VoidHot, 0.3f)
                    ?.Configure(12, 0.85f);
            }
        }

        /// <summary>影噬蓄势时画刀身的紫边轮廓（本体已隐没，只剩影的形状）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            float hidden = 1f - BladeAlpha;
            if (hidden < 0.2f) {
                return;
            }
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 drawPos = Hand + (mainAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
            Color outline = GsLightsBane.VoidHot * (hidden * 0.55f);
            outline.A = 0;
            //轮廓微呼吸：确定性抖动，不掷 Main.rand
            float breath = 1.03f + 0.02f * MathF.Sin(timer * 0.6f + DrawRand01(3) * 6.28f);
            sb.Draw(tex, drawPos, null, outline, mainAngle + rotOffset, tex.Size() / 2f, scale * breath, effect, 0);
        }
    }

    /// <summary>
    /// 暗影蚀痕：挥砍在空间留下的驻留弧痕。ai[0]=弧起角 ai[1]=弧止角 ai[2]=触及半径；
    /// damage&gt;0 为灼噬痕（40 帧，约 16 帧一跳），否则纯演出（24 帧）。
    /// 暗体用真 alpha 贴图压暗背景，紫边走加色；绘制抖动全部 identity 播种
    /// </summary>
    internal class GsLightsBaneScarProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeDamaging = 40;
        private const int LifeVisual = 24;
        private const int Segments = 9;

        private float ArcStart => Projectile.ai[0];
        private float ArcEnd => Projectile.ai[1];
        private float Reach => Projectile.ai[2];
        private int TotalLife => Projectile.damage > 0 ? LifeDamaging : LifeVisual;
        private float Life01 => 1f - (Projectile.timeLeft / (float)TotalLife);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 16;
            Projectile.timeLeft = LifeDamaging;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Projectile.damage > 0 && Projectile.timeLeft > 8 ? null : false;

        public override void AI() {
            //演出痕在首帧对齐自己的短寿命（damage 随生成包过线，各端一致）
            if (Projectile.damage <= 0 && Projectile.timeLeft > LifeVisual) {
                Projectile.timeLeft = LifeVisual;
            }

            float mid = MathHelper.Lerp(ArcStart, ArcEnd, 0.5f);
            Lighting.AddLight(Projectile.Center + mid.ToRotationVector2() * (Reach * 0.75f),
                GsLightsBane.VoidMain.ToVector3() * (0.4f * (1f - Life01)));

            if (!VaultUtils.isServer && Projectile.damage > 0 && Main.rand.NextBool(3)) {
                //蚀痕上升起暗影余尘
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, Main.rand.NextFloat());
                Vector2 at = Projectile.Center + ang.ToRotationVector2() * (Reach * Main.rand.NextFloat(0.6f, 1f));
                Dust d = Dust.NewDustPerfect(at, DustID.Shadowflame,
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)), 120, default, Main.rand.NextFloat(0.6f, 1f));
                d.noGravity = true;
            }
        }

        /// <summary>判定：沿弧逐段采样，从半径 45% 到刃尖的线段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 center = Projectile.Center;
            float collisionPoint = 0f;
            for (int i = 0; i <= Segments; i++) {
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, i / (float)Segments);
                Vector2 dir = ang.ToRotationVector2();
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    center + dir * (Reach * 0.45f), center + dir * (Reach * 1.02f), 26f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Shadowflame,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 90, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return false;
            }
            float life = Life01;
            Vector2 center = Projectile.Center - Main.screenPosition;
            bool damaging = Projectile.damage > 0;

            for (int i = 0; i <= Segments; i++) {
                float t = i / (float)Segments;
                //蚀散次序确定性乱序：每段有自己的死亡时刻
                float dieAt = 0.5f + 0.5f * SegRand(i);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.3f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, t);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 at = center + dir * (Reach * 0.78f);
                float segScale = 0.75f + 0.5f * SegRand(i + 40);

                //暗体：真 alpha 压暗一块空间（加色物理上做不出暗痕）
                Color dark = GsLightsBane.VoidDeep * (segFade * (damaging ? 0.62f : 0.4f));
                Main.EntitySpriteDraw(blot, at, null, dark, ang + MathHelper.PiOver2,
                    blot.Size() * 0.5f, new Vector2(0.30f, 0.16f) * segScale, SpriteEffects.None, 0);

                //紫边余焰：加色小光斑挂在痕外缘，明灭相位各段错开
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + SegRand(i + 80) * 6.28f);
                Color edge = (damaging ? GsLightsBane.VoidHot : GsLightsBane.VoidBright) * (segFade * 0.5f * pulse);
                edge.A = 0;
                Main.EntitySpriteDraw(glow, center + dir * (Reach * 0.95f), null, edge, 0f,
                    glow.Size() * 0.5f, 0.5f * segScale, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
