using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【蚁狮下颚刀】材质：蚁狮几丁质颚壳。签名：①每拍是一开一合的双弧钳咬，
    /// 主刀之外有一道镜像颚影同步咬合且真实带判定 ②终结拍双弧闭合夹击 +25% 伤害
    /// ③命中迸溅沙尘与甲壳碎屑
    /// </summary>
    internal class GsMandibleBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.AntlionClaw;

        protected override int HeldProjID => ModContent.ProjectileType<GsMandibleBladeHeld>();

        protected override string GsDescFallback =>
            "Reforged: every swing bites with twin mandible arcs that snap shut like pincers; " +
            "the third bite clamps down for extra damage";

        //几丁质色板
        internal static readonly Color ChitinBright = new(232, 206, 150); //甲壳亮黄
        internal static readonly Color ChitinMain = new(178, 140, 88);    //几丁土棕
        internal static readonly Color ChitinHot = new(255, 178, 96);     //颚芯琥珀
        internal static readonly Color ChitinDeep = new(54, 40, 26);      //穴底暗棕

        //底伤持平摊账：镜像颚影扩大覆盖但不叠伤（同一目标同帧只判一次），
        //终结钳咬 +25% 均摊到三拍约 +8%，综合 DPS 约为原版 105%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.0f;
    }

    /// <summary>
    /// 蚁狮下颚刀手持：三拍钳咬。主刀走常规弧，镜像颚影以瞄准线为轴对称咬合；
    /// 判定对两条弧各做一次逐段采样；终结拍闭口更深且 +25% 伤害。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsMandibleBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.AntlionClaw;
        protected override Color EdgeBright => GsMandibleBlade.ChitinBright;
        protected override Color BodyMain => GsMandibleBlade.ChitinMain;
        protected override Color HotAccent => GsMandibleBlade.ChitinHot;
        protected override Color DeepShadow => GsMandibleBlade.ChitinDeep;

        /// <summary>镜像颚影角：以瞄准线为轴与主刀对称</summary>
        private float MirrorAngle => 2f * baseAngle - mainAngle;
        private float MirrorLastAngle => 2f * baseAngle - lastAngle;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //终结钳咬：双弧从更开的口合到更深处
                return new GsBroadBeat {
                    Raise = 7, Hold = 3, Slash = 4, Recover = 11,
                    RaiseBack = 2.0f, Follow = 1.35f, ReachScale = 1.1f, LeanAmp = 0.075f,
                    DamageMult = 1f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.15f,
                };
            }
            //普通钳咬：开口小、咬合快，音高偏尖（甲壳摩擦感）
            GsBroadBeat b = GsBroadBeat.Standard;
            b.Raise = 5;
            b.Slash = 3;
            b.Recover = 8;
            b.RaiseBack = 1.5f;
            b.Follow = 1.15f;
            b.SwingPitch = stage == 0 ? 0.18f : 0.10f;
            return b;
        }

        /// <summary>终结拍双弧闭合夹击 +25%</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (IsFinisher) {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        /// <summary>判定：主弧照基类，再对镜像颚影做一次同样的逐段采样</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            bool? baseResult = base.Colliding(projHitbox, targetHitbox);
            if (baseResult == true || !sweepDamageActive) {
                return baseResult;
            }

            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(8, 8);
            Vector2 hand = Hand;
            float delta = MirrorAngle - MirrorLastAngle;
            float reach = mainReach * 1.04f + 10f;
            int steps = Math.Clamp((int)MathF.Ceiling(MathF.Abs(delta) * reach / 30f), 1, 18);
            float collisionPoint = 0f;
            for (int i = 0; i <= steps; i++) {
                float ang = MathHelper.Lerp(MirrorLastAngle, MirrorAngle, i / (float)steps);
                Vector2 tip = hand + ang.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , hand, tip, CollisionWidth, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //沙尘扬起 + 甲壳碎屑弹跳
            int grains = IsFinisher ? 10 : 6;
            for (int i = 0; i < grains; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Sand,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f), 90, default,
                    Main.rand.NextFloat(0.8f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>镜像颚影：原版贴图加色画一道对称弧，咬合期最实，收势随扇淡出</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (fanFade <= 0.05f) {
                return;
            }
            Main.instance.LoadItem(SwordItemID);
            Texture2D tex = TextureAssets.Item[SwordItemID].Value;
            Vector2 origin = tex.Size() / 2f;
            //镜像弧扫向相反，翻转贴图让刃口朝前
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
            SpriteEffects mirrorEffect = effect == SpriteEffects.FlipVertically
                ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float mirrorRotOffset = -rotOffset;
            float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);

            //斩切期颚影最实，其余相位保持半透提示「这是双颚武器」
            float strength = CurrentPhase == PhaseSlash ? 0.55f : 0.30f;
            strength *= fanFade;

            //主影 + 一道拖尾影，双层显出咬合速度
            for (int g = 1; g >= 0; g--) {
                float ang = MirrorAngle + g * (MirrorAngle - MirrorLastAngle) * -1.6f;
                Color c = (g == 0 ? GsMandibleBlade.ChitinBright : GsMandibleBlade.ChitinMain)
                    * (strength * (g == 0 ? 1f : 0.45f));
                c.A = 0;
                Vector2 pos = Hand + (ang.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                sb.Draw(tex, pos, null, c, ang + mirrorRotOffset, origin, scale, mirrorEffect, 0);
            }
        }
    }
}
