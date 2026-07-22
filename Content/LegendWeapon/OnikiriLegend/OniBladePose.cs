using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 刀权占用.<see cref="HardOccupiesBlade"/> 硬占→连段冻结让位;
    /// <see cref="ReservesBlade"/> 软保留挡连段重启不挡位移/技能;
    /// 相位本地 timer 推导,零额外网络
    /// </summary>
    internal interface IOniBladeOccupant
    {
        /// <summary>本帧是否硬占刀权</summary>
        bool HardOccupiesBlade { get; }

        /// <summary>软保留刀权(签名拍短窗),默认否</summary>
        bool ReservesBlade => false;
    }

    /// <summary>刀权查询,本地从在场弹幕推导</summary>
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

        /// <summary>是否有软保留在场,连段重启需等窗</summary>
        public static bool BladeReserved(Player player) {
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI) {
                    continue;
                }
                if (proj.ModProjectile is IOniBladeOccupant occupant && occupant.ReservesBlade) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>连段是否持刀权</summary>
        public static bool ComboClaims(Player player)
            => CrimsonRendSlash.FindController(player)?.ClaimsBlade ?? false;
    }

    /// <summary>
    /// 刀角交接黑板,<see cref="OniBladePose.ApplyPose"/> 发布;
    /// 接刀方新鲜期内读取,每玩家一槽,不进网络
    /// </summary>
    internal static class OniBladeHandoff
    {
        /// <summary>交接新鲜期(帧)</summary>
        public const int FreshFrames = 10;

        private struct Slot
        {
            public float Rotation;
            public int Facing;
            public long Frame;
        }
        private static readonly Slot[] slots = new Slot[Main.maxPlayers + 1];

        /// <summary>发布当前刀角</summary>
        public static void Publish(Player owner, float rotation, int facing) {
            if (owner == null) {
                return;
            }
            slots[owner.whoAmI] = new Slot {
                Rotation = rotation,
                Facing = facing,
                Frame = (long)Main.GameUpdateCount,
            };
        }

        /// <summary>读新鲜交接刀角,过期 false</summary>
        public static bool TryPeek(Player owner, out float rotation, out int facing) {
            rotation = 0f;
            facing = 1;
            if (owner == null) {
                return false;
            }
            Slot slot = slots[owner.whoAmI];
            if (slot.Frame <= 0 || (long)Main.GameUpdateCount - slot.Frame > FreshFrames) {
                return false;
            }
            rotation = slot.Rotation;
            facing = slot.Facing;
            return true;
        }
    }

    /// <summary>
    /// 轻量实体刀姿态,护手钉手心、尖指 <see cref="Rotation"/>;
    /// 连段走自有富姿态,本类不碰 itemTime;
    /// 绘于 <see cref="Common.IOverlayDrawable"/>
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

        /// <summary>推进一帧,衰减残影</summary>
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
        /// 摆姿态,手心锚点,不碰 itemTime;
        /// 可见帧发布 <see cref="OniBladeHandoff"/>
        /// </summary>
        public void ApplyPose(Player owner, Projectile host,
            Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.ThreeQuarters,
            int fixedFacing = 0) {
            owner.heldProj = host.whoAmI;
            if (fixedFacing != 0) {
                owner.ChangeDir(fixedFacing);
            }
            else {
                float cos = MathF.Cos(Rotation);
                if (MathF.Abs(cos) >= 0.05f) {
                    owner.ChangeDir(cos > 0f ? 1 : -1);
                }
            }
            Facing = owner.direction;
            float armRotation = Rotation - MathHelper.PiOver2;
            owner.SetCompositeArmFront(true, stretch, armRotation);
            owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armRotation + 0.16f * Facing);
            handWorld = owner.GetFrontHandPosition(stretch, armRotation);

            if (Opacity > 0.05f) {
                OniBladeHandoff.Publish(owner, Rotation, Facing);
            }
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
