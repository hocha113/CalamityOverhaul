using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 回旋镖族方案基类。主题锚：三相轨迹——去程持续减速、悬停原地蓄势、回程持续加速追手，
    /// 禁匀速直飞；飞行中按右键可命令镖冲向光标处再回手。<br/>
    /// 接管方式：GsShoot 压掉原版镖弹改发 <see cref="GsBoomerProjBase"/> 子类；
    /// 同场上限逻辑与原版 ItemCheck 对齐（单发表 / 光盘 6 / 香蕉 10 / 三重 3）。<br/>
    /// 掷姿：族层 GsUseItemFrame 提供后引-甩出-跟随三段臂弧；
    /// A 档武器改走 <see cref="GsBoomerThrowHeldBase"/> 手持蓄力掷（方案自行覆写 GsCanUseItem）
    /// </summary>
    internal abstract class GsBoomerScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Boomerangs";

        /// <summary>自定义镖弹类型（子类返回 ModContent.ProjectileType）</summary>
        internal abstract int BoomerProjType { get; }

        /// <summary>同场上限，与原版对齐；无上限武器给 int.MaxValue</summary>
        internal virtual int MaxAirborne => 1;

        /// <summary>底伤倍率（机制收益另计，总包络原版 100%~120%）</summary>
        internal virtual float DamageMul => 1.05f;

        /// <summary>出手速度倍率（乘在原版 shootSpeed 合成的 velocity 上）</summary>
        internal virtual float ThrowSpeedMul => 1.15f;

        public override bool? GsCanUseItem(Item item, Player player) {
            //镜像原版 ItemCheck 的同场上限；原版该判定也只跑本地玩家
            if (player.whoAmI == Main.myPlayer
                && player.ownedProjectileCounts[BoomerProjType] >= MaxAirborne) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //压掉原版镖弹改发三相镖；GsShoot 只在 owner 端执行，生成包自动过线
            Projectile.NewProjectile(source, position, velocity * ThrowSpeedMul,
                BoomerProjType, damage, knockback, player.whoAmI);
            return false;
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= DamageMul;

        //==================== 掷姿臂弧（各端确定性：只消费 direction 与动画进度） ====================

        public override void GsUseItemFrame(Item item, Player player) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float t = 1f - (player.itemAnimation / (float)player.itemAnimationMax);
            float a;    //以 direction=1 计算的世界臂角，负值朝上
            Player.CompositeArmStretchAmount stretch;
            if (t < 0.28f) {
                //后引：臂举过肩
                a = MathHelper.Lerp(-0.5f, -2.15f, EaseOutQuad(t / 0.28f));
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            else if (t < 0.52f) {
                //甩出：过顶前甩
                a = MathHelper.Lerp(-2.15f, 0.3f, SmoothStep01((t - 0.28f) / 0.24f));
                stretch = Player.CompositeArmStretchAmount.Full;
            }
            else {
                //跟随回收
                a = MathHelper.Lerp(0.3f, 0.08f, (t - 0.52f) / 0.48f);
                stretch = Player.CompositeArmStretchAmount.Full;
            }
            if (player.direction < 0) {
                a = MathHelper.Pi - a;
            }
            player.SetCompositeArmFront(true, stretch, a - MathHelper.PiOver2);
        }

        internal static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));

        internal static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
