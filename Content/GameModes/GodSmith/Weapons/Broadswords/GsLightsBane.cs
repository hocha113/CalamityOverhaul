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
    /// 【暗影蚀刃】材质：淬暗影的恶魔铁。签名：①终结拍在空中留下驻留灼噬的暗影蚀痕
    /// ②第三拍长举蓄势后爆发前压斩出
    /// </summary>
    internal class GsLightsBane : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.LightsBane;

        protected override int HeldProjID => ModContent.ProjectileType<GsLightsBaneHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash tears a shadow scar into the air; the third strike sinks the blade into shadow, then erupts forward, and its scar lingers to corrode";
        internal static readonly Color VoidBright = new(196, 156, 255); //苍紫刃缘
        internal static readonly Color VoidMain = new(108, 76, 190);    //恶魔铁紫
        internal static readonly Color VoidHot = new(168, 64, 255);     //蚀影亮紫

        //底伤 +6%：终结拍 1.3x + 每三拍一道灼噬蚀痕（满驻留 3 跳约 0.47x，实战常 2 跳），
        //按 max(useTime, 弹幕总帧) 摊算综合 DPS 约为原版 110%~119%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;
    }

    /// <summary>
    /// 暗影蚀刃手持：三拍连击。0/1 交替快斩，2 影噬终结（长举蓄势、爆发+前压）。
    /// 终结拍收势时沿挥弧生成灼噬蚀痕。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsLightsBaneHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.LightsBane;
        protected override Color EdgeBright => GsLightsBane.VoidBright;
        protected override Color BodyMain => GsLightsBane.VoidMain;
        protected override Color HotAccent => GsLightsBane.VoidHot;

        private bool scarSpawned;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //影噬终结：长举 + 滞帧蓄势 + 快爆发
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

        protected override void HandlePhaseEvents(int phase) {
            //影噬起手：一记低哑的暗影嘶声
            if (IsFinisher && timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.4f }, Owner.Center);
            }
            base.HandlePhaseEvents(phase);

            //终结拍收势首帧沿挥弧留下灼噬蚀痕
            if (!scarSpawned && phase == PhaseRecover && IsFinisher) {
                scarSpawned = true;
                float startAng = ArcStart - (swingDir * 0.08f);
                int scarDamage = Math.Max(1, (int)(Projectile.damage * 0.12f));
                SpawnOwnedProj(ModContent.ProjectileType<GsLightsBaneScarProj>(), Hand, Vector2.Zero,
                    scarDamage, 0f, startAng, ArcEnd, FullReach);
            }
        }
    }

    /// <summary>
    /// 暗影蚀痕：终结斩在空间留下的驻留灼噬弧痕（40 帧，约 16 帧一跳）。
    /// ai[0]=弧起角 ai[1]=弧止角 ai[2]=触及半径；用原版暗影焰贴图在弧中点画一笔作范围提示
    /// </summary>
    internal class GsLightsBaneScarProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ShadowFlame;

        private const int LifeDamaging = 40;
        private const int Segments = 9;

        private float ArcStart => Projectile.ai[0];
        private float ArcEnd => Projectile.ai[1];
        private float Reach => Projectile.ai[2];

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

        /// <summary>范围提示：原版暗影焰贴图在弧中点画一笔，沿切线摆放并随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float mid = MathHelper.Lerp(ArcStart, ArcEnd, 0.5f);
            Vector2 at = Projectile.Center + mid.ToRotationVector2() * (Reach * 0.78f) - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeDamaging, 0f, 1f);
            float scale = Reach * 0.4f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, at, null, lightColor * fade, mid + MathHelper.PiOver2,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
