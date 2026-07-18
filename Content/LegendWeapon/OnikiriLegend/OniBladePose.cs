using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 刀权占用者契约：实现本接口的技能弹幕在 <see cref="HardOccupiesBlade"/> 期间硬占刀权——
    /// 连段(<see cref="CrimsonRendSlash"/>)让位：就地冻结在场刀光、速褪、停排且不受理重启。<br/>
    /// 占用相位由各弹幕自己的 timer 推导,技能弹幕本身经 tML 同步,
    /// 每个客户端各自推导结果一致,零网络开销
    /// </summary>
    internal interface IOniBladeOccupant
    {
        /// <summary>本帧是否硬占刀权</summary>
        bool HardOccupiesBlade { get; }
    }

    /// <summary>刀权查询：世界里只有一把鬼切,谁在持刀全部从弹幕在场状态本地推导</summary>
    internal static class OniBladeOccupancy
    {
        /// <summary>该玩家是否有硬占刀权的技能弹幕在场(except 排除查询者自身)</summary>
        public static bool AnyHardOccupant(Player player, Projectile except = null) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj == except) {
                    continue;
                }
                if (proj.ModProjectile is IOniBladeOccupant occupant && occupant.HardOccupiesBlade) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>连段是否持有刀权(排拍中/子刀光未散/实体刀未收完)——技能的软姿态让位给它,玩家输入永远优先</summary>
        public static bool ComboClaims(Player player)
            => CrimsonRendSlash.FindController(player)?.ClaimsBlade ?? false;
    }

    /// <summary>
    /// 轻量实体刀姿态渲染器：护手钉在复合手臂手心、刀尖指向 <see cref="Rotation"/> 的
    /// 单帧刀身精灵 + 短命角度残影。疾走收尾的残心/纳刀、灭世一闪的大挥、
    /// 终结乱舞的残心静立共用,补全"招式与持刀人相连"的身体动画;<br/>
    /// 连段(<see cref="CrimsonRendSlash"/>)保留自己的富姿态系统(深度/翻面/停驻回坐),不走本类。<br/>
    /// 只摆姿态不碰 itemTime,不锁操控;绘制走 <see cref="Common.IOverlayDrawable"/> 遮挡层批次
    /// </summary>
    internal sealed class OniBladePose
    {
        /// <summary>物品贴图中的护手/刀尖 UV(与 CrimsonRendSlash 的支点约定同源)</summary>
        public static readonly Vector2 HiltUV = new(0.1f, 1f);
        public static readonly Vector2 TipUV = new(0.73f, 0.01f);

        private struct Smear
        {
            public float Rotation;
            public int Facing;
            public float Strength;
            public int Life;
        }

        private const int SmearLifeFrames = 6;
        private readonly Smear[] smears = new Smear[8];
        private int smearHead;
        private Vector2 handWorld;

        /// <summary>刀尖指向(世界弧度)</summary>
        public float Rotation;
        /// <summary>朝向(由 ApplyPose 依 Rotation 解算)</summary>
        public int Facing = 1;
        /// <summary>刀身不透明度,0 时不绘制(残影独立衰减)</summary>
        public float Opacity;
        /// <summary>绘制缩放(与连段实体刀同档)</summary>
        public float Scale = 0.9f;

        /// <summary>推进一帧:衰减残影。宿主 AI 每帧调用一次</summary>
        public void Update() {
            for (int i = 0; i < smears.Length; i++) {
                if (smears[i].Life > 0) {
                    smears[i].Life--;
                }
            }
        }

        /// <summary>压入一道当前姿态的残影(挥动帧使用)</summary>
        public void PushSmear(float strength = 1f) {
            smears[smearHead] = new Smear {
                Rotation = Rotation,
                Facing = Facing,
                Strength = strength,
                Life = SmearLifeFrames,
            };
            smearHead = (smearHead + 1) % smears.Length;
        }

        /// <summary>
        /// 摆姿态:heldProj + 朝向 + 复合手臂,并解算真实手心作为刀身绘制锚点。
        /// 不触碰 itemTime/itemAnimation——姿态是非阻塞的,玩家输入随时覆盖
        /// </summary>
        public void ApplyPose(Player owner, Projectile host,
            Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.ThreeQuarters) {
            owner.heldProj = host.whoAmI;
            float cos = MathF.Cos(Rotation);
            if (MathF.Abs(cos) >= 0.05f) {
                owner.ChangeDir(cos > 0f ? 1 : -1);
            }
            Facing = owner.direction;
            float armRotation = Rotation - MathHelper.PiOver2;
            owner.SetCompositeArmFront(true, stretch, armRotation);
            owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armRotation + 0.16f * Facing);
            handWorld = owner.GetFrontHandPosition(stretch, armRotation);
        }

        /// <summary>遮挡层绘制:残影 → 阴影 → 刀体(调用方保证处于 IOverlayDrawable 的 AlphaBlend 批内)</summary>
        public void Draw(SpriteBatch sb, Player owner) {
            //残影独立于刀体存活(纳刀收完后余影仍在消散)
            for (int i = 0; i < smears.Length; i++) {
                Smear s = smears[(smearHead + i) % smears.Length];
                if (s.Life <= 0) {
                    continue;
                }
                float ageT = 1f - s.Life / (float)SmearLifeFrames;
                float alpha = s.Strength * (1f - ageT);
                if (alpha <= 0.02f) {
                    continue;
                }
                Color c = Color.Lerp(new Color(210, 42, 38, 130), new Color(126, 20, 30, 105), ageT)
                    * (alpha * 0.55f);
                DrawBladeSprite(sb, s.Rotation, s.Facing, Scale, c);
            }

            if (Opacity <= 0.01f) {
                return;
            }
            Color lightColor = Lighting.GetColor((int)(owner.Center.X / 16f), (int)(owner.Center.Y / 16f));
            Color shadow = new Color(15, 3, 8, 190) * (Opacity * 0.62f);
            DrawBladeSprite(sb, Rotation, Facing, Scale * 1.018f, shadow, new Vector2(Facing, 1f));
            Color body = Color.Lerp(lightColor, Color.White, 0.24f) * Opacity;
            DrawBladeSprite(sb, Rotation, Facing, Scale, body);
        }

        /// <summary>实体刀单帧精灵:护手钉手心、刀尖严格指向 rotation;太刀朝左垂直翻转并镜像支点(与连段同一套数学)</summary>
        private void DrawBladeSprite(SpriteBatch sb, float rotation, int facing, float scale, Color color, Vector2 posOffset = default) {
            Texture2D blade = TextureAssets.Item[ModContent.ItemType<OnikiriItem>()].Value;
            Vector2 textureSize = blade.Size();
            Vector2 origin = new(textureSize.X * HiltUV.X, textureSize.Y * HiltUV.Y);
            Vector2 textureTip = new(textureSize.X * TipUV.X, textureSize.Y * TipUV.Y);
            SpriteEffects bladeEffect = SpriteEffects.None;
            if (facing < 0) {
                bladeEffect = SpriteEffects.FlipVertically;
                origin.Y = textureSize.Y - origin.Y;
                textureTip.Y = textureSize.Y - textureTip.Y;
            }
            float textureAxis = (textureTip - origin).ToRotation();
            sb.Draw(blade, handWorld + posOffset - Main.screenPosition, null, color
                , rotation - textureAxis, origin, scale, bladeEffect, 0f);
        }

        /// <summary>角度插值(走短弧)</summary>
        public static float LerpAngle(float from, float to, float amount)
            => from + MathHelper.WrapAngle(to - from) * MathHelper.Clamp(amount, 0f, 1f);
    }
}
