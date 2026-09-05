using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 枪械族共享的手写持枪姿态帮手（方案枪的姿态件与 held 枪共用；特种枪、喷射器两族只读引用）。<br/>
    /// 手持接管一律 <see cref="BaseHeldProj"/> 手写姿态，禁止继承 BaseHeldGun
    /// （其 TargetID 扫描会把原版物品永久写进 ItemIsGun 表，违反模式关闭零足迹）
    /// </summary>
    internal static class GsGunPose
    {
        /// <summary>
        /// 手写双手持枪姿态：朝向鼠标、双臂角、枪心锚定、动画锁。
        /// 公式镜像 BaseHeldGun 的姿态数学但完全自持。返回本帧瞄准角
        /// </summary>
        /// <param name="held">手持弹幕</param>
        /// <param name="handDistX">枪心沿瞄准向的距离</param>
        /// <param name="handDistY">枪心垂直落差</param>
        /// <param name="recoilPitch">后坐上抬角（弧度）</param>
        /// <param name="recoilBack">后坐制退位移（沿瞄准向后退 px）</param>
        /// <param name="backArmLift">后手向枪口侧的托举偏角</param>
        /// <param name="lockUseAnim">强撑 itemAnimation=2 维持使用态；装填/蓄力等
        /// 需要保留「点击可触发新 use」的姿态件传 false（原版触发使用要求 itemAnimation==0）</param>
        /// <param name="sway">后坐颠动：枪心沿瞄准法向的偏移 px（见 <see cref="GsGunRecoil.Wobble"/>），0 不颠</param>
        public static float Update(BaseHeldProj held, float handDistX, float handDistY,
            float recoilPitch, float recoilBack, float backArmLift = 0.32f, bool lockUseAnim = true, float sway = 0f) {
            Player owner = held.Owner;
            Projectile proj = held.Projectile;

            owner.ChangeDir(held.ToMouse.X >= 0f ? 1 : -1);
            int safeGrav = held.SafeGravDir;
            int dirSign = owner.direction * safeGrav;

            float aimRot = held.ToMouseA - recoilPitch * dirSign;
            proj.rotation = aimRot;
            Vector2 aimUnit = aimRot.ToRotationVector2();
            proj.Center = owner.GetPlayerStabilityCenter()
                + aimUnit * (handDistX - recoilBack)
                + new Vector2(-aimUnit.Y, aimUnit.X) * sway
                + new Vector2(0f, handDistY * safeGrav);

            //手臂角公式与 BaseHeldGun 同构：正重力下等价于 aimRot - PiOver2
            float armRot = (MathHelper.PiOver2 * safeGrav - aimRot) * dirSign * safeGrav;
            owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRot * -dirSign);
            owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters,
                armRot * -dirSign + owner.direction * backArmLift);

            owner.heldProj = proj.whoAmI;
            if (lockUseAnim) {
                owner.itemTime = owner.itemAnimation = 2;
            }
            owner.itemRotation = (aimUnit * owner.direction).ToRotation();
            return aimRot;
        }

        /// <summary>取枪口世界坐标：沿枪身向前 forward，再沿法向偏 normal</summary>
        public static Vector2 MuzzlePos(Projectile proj, int dirSign, float forward, float normal) {
            Vector2 forwardUnit = proj.rotation.ToRotationVector2();
            Vector2 normalUnit = (proj.rotation + (dirSign > 0 ? MathHelper.PiOver2 : -MathHelper.PiOver2)).ToRotationVector2();
            return proj.Center + forwardUnit * forward + normalUnit * normal;
        }

        /// <summary>
        /// 用原版物品贴图画枪体，面左翻转（手持接管期间原版不绘制枪体，此为必要的一笔）
        /// </summary>
        public static void DrawGunBody(int itemId, Vector2 center, float rotation, int dirSign,
            Color lightColor, float scale = 1f) {
            Main.instance.LoadItem(itemId);
            Texture2D tex = TextureAssets.Item[itemId].Value;
            SpriteEffects fx = dirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Main.EntitySpriteDraw(tex, center - Main.screenPosition, null, lightColor, rotation,
                tex.Size() / 2f, scale, fx, 0);
        }
    }

    /// <summary>
    /// 通用持枪姿态件：useStyle-5 的枪在 itemAnimation==0 时不被绘制（TML PlayerDrawLayers 契约），
    /// 装填仪式 / 蓄力等非 use 时段由本件维持枪体在手。<br/>
    /// 生命周期契约：owner 每帧调 <see cref="Ensure"/> 续命，断供数帧即自灭；
    /// 不锁 itemAnimation（保住「装几发打几发」的可打断装填——原版触发新 use 要求 itemAnimation==0），
    /// itemAnimation&gt;0 时主动让位给 use 流的原版持枪绘制。<br/>
    /// 装填态不同步，远端不续命即近生即灭且不绘制（旁观者不可见与既有 owner-local 缺口一致，归 A00 专项）。<br/>
    /// ai[0]=接管的物品 type，ai[1]=枪口俯仰（弧度，负=下垂）
    /// </summary>
    internal class GsGunHoldPoseProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName =>
            HeldItemId > ItemID.None && HeldItemId < ItemID.Count
                ? Language.GetText("ItemName." + ItemID.Search.GetName(HeldItemId))
                : base.DisplayName;

        /// <summary>装填姿态俯仰：枪口下垂读作「手上有活」的非战斗持械</summary>
        internal const float ReloadPitch = -0.26f;

        /// <summary>本机玩家在场件槽位缓存（myPlayer 专用，查找 O(1)）</summary>
        private static int localInstance = -1;

        private int HeldItemId => (int)Projectile.ai[0];
        private float Pitch => Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            //无人续命数帧内自灭；远端不续命，近生即灭
            Projectile.timeLeft = 6;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        /// <summary>出生帧立即摆姿态：从弹幕更新循环内生成时本帧 AI 可能不跑，防 rotation=0 闪帧</summary>
        public override void OnSpawn(IEntitySource source) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                GsGunPose.Update(this, 18f, -4f, Pitch, 0f, lockUseAnim: false);
            }
        }

        /// <summary>
        /// owner 每帧调用：在场则续命，不在场则生成。pitch 只在生成帧写入 ai[1]。
        /// 只在本机玩家路径生效
        /// </summary>
        internal static void Ensure(Player player, int itemId, float pitch) {
            if (player.whoAmI != Main.myPlayer || player.HeldItem == null || player.HeldItem.IsAir) {
                return;
            }
            int type = ModContent.ProjectileType<GsGunHoldPoseProj>();
            if (localInstance >= 0 && localInstance < Main.maxProjectiles) {
                Projectile cached = Main.projectile[localInstance];
                if (cached.active && cached.type == type && cached.owner == player.whoAmI) {
                    cached.timeLeft = Math.Max(cached.timeLeft, 4);
                    return;
                }
                localInstance = -1;
            }
            localInstance = Projectile.NewProjectile(player.GetSource_ItemUse(player.HeldItem),
                player.Center, Vector2.Zero, type, 0, 0f, player.whoAmI, itemId, pitch);
        }

        public override void AI() {
            //硬性兜底：模式关闭 / 换持 / 死亡立即收
            if (!GameModeSystem.GodSmithActive || Owner.dead || !Owner.active || Owner.noItems
                || Item.type != HeldItemId) {
                Projectile.Kill();
                return;
            }
            //use 流已接管画面（可打断装填被开火打断等），让位原版持枪绘制
            if (Owner.itemAnimation > 0) {
                Projectile.Kill();
                return;
            }
            //不锁 itemAnimation：装填中点击仍能触发新 use（可打断装填契约）
            GsGunPose.Update(this, 18f, -4f, Pitch, 0f, lockUseAnim: false);
        }

        public override bool PreDraw(ref Color lightColor) {
            //装填/蓄力态是本地节拍层，远端件不绘制（近生即灭的保险）
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return false;
            }
            GsGunPose.DrawGunBody(HeldItemId, Projectile.Center, Projectile.rotation, DirSign, lightColor);
            return false;
        }
    }
}
