using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 枪械后坐剖面：出膛帧的后挫与角度踢峰值，其后叠一段阻尼颠动（枪身在手里抖回原位），
    /// 再加 owner 端震屏强度。全部是姿态量（itemLocation / itemRotation / 镜头），不产生任何绘制层
    /// </summary>
    internal readonly struct GsGunRecoilProfile
    {
        /// <summary>沿瞄准向后挫峰值（px）</summary>
        public readonly float Shift;
        /// <summary>角度踢峰值（弧度，正=枪口上抬，负=下压）</summary>
        public readonly float Kick;
        /// <summary>颠动位移幅度（px，垂直瞄准轴）</summary>
        public readonly float Shake;
        /// <summary>颠动角幅（弧度）</summary>
        public readonly float ShakeRot;
        /// <summary>颠动角频率（弧度/帧，2.1 ≈ 三帧一个来回）</summary>
        public readonly float ShakeFreq;
        /// <summary>颠动逐帧衰减系数（0~1，越小停得越快）</summary>
        public readonly float Damping;
        /// <summary>震屏强度（PunchCameraModifier 的 strength，0=不震）</summary>
        public readonly float Screen;

        public GsGunRecoilProfile(float shift, float kick, float shake, float shakeRot,
            float shakeFreq, float damping, float screen) {
            Shift = shift;
            Kick = kick;
            Shake = shake;
            ShakeRot = shakeRot;
            ShakeFreq = shakeFreq;
            Damping = damping;
            Screen = screen;
        }

        /// <summary>
        /// 只给后挫与角踢两个主参数，派生一套完整剖面：颠动幅度随后挫走，
        /// 震屏只给重枪（后挫 3px 以上才有：霰弹枪 6px → 2.4，四管齐鸣 4px → 0.8，速射枪一律 0）
        /// </summary>
        public static GsGunRecoilProfile Derive(float shift, float kick) {
            float absKick = MathF.Abs(kick);
            return new GsGunRecoilProfile(
                shift, kick,
                shake: 0.35f + shift * 0.45f,
                shakeRot: 0.012f + absKick * 0.5f,
                shakeFreq: 2.1f,
                damping: 0.84f,
                screen: MathHelper.Clamp((shift - 3f) * 0.8f, 0f, 3.2f));
        }

        /// <summary>整体缩放（坐骑减半、连发档加重等）；频率与衰减不缩</summary>
        public GsGunRecoilProfile Scaled(float s)
            => new(Shift * s, Kick * s, Shake * s, ShakeRot * s, ShakeFreq, Damping, Screen * s);
    }

    /// <summary>
    /// 枪械后坐的施加数学（方案枪 GsUseStyle 与 held 枪共用）。<br/>
    /// 原版事实：useStyle-5 的 itemLocation 每帧被原版绝对重置，直接加减安全；
    /// itemRotation 只在射击瞬间被 snap 一次，动画期无人重算，直接减是逐帧累减，
    /// 故角度一律走「目标绝对剖面的逐帧差分」（<see cref="ApplyRotationTarget"/>）：
    /// Δ = want − 已施加量；owner 端射击帧由射击路径清账对齐 snap，远端靠动画重启检测清账，
    /// 记账失真随包络归零自愈，因此不设 myPlayer 守门，旁观者也看得到踢
    /// </summary>
    internal static class GsGunRecoil
    {
        /// <summary>
        /// 施加一帧后坐姿态。包络 = 动画剩余比例的平方（出膛帧最猛随后回落）；
        /// 颠动 = 阻尼正弦，相位用 whoAmI 定种，各端确定性一致；
        /// 动画重启帧（= 新一发出膛）顺带给 owner 端一记震屏
        /// </summary>
        /// <param name="rotApplied">记账字段：当前已施加在 itemRotation 上的绝对偏移（各端各持）</param>
        /// <param name="lastAnim">记账字段：上帧观察到的 itemAnimation（动画重启检测）</param>
        public static void Apply(Player player, ref float rotApplied, ref int lastAnim, in GsGunRecoilProfile p) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            bool newShot = player.itemAnimation > lastAnim;
            int elapsed = player.itemAnimationMax - player.itemAnimation;
            float env = player.itemAnimation / (float)player.itemAnimationMax;
            env *= env;
            //镜像角乘朝向还原世界瞄准向；法向取其左手垂直
            Vector2 aim = player.itemRotation.ToRotationVector2() * player.direction;
            Vector2 perp = new(-aim.Y, aim.X);
            float wobble = Wobble(elapsed, p.ShakeFreq, p.Damping, player.whoAmI);

            player.itemLocation -= aim * (p.Shift * env);
            player.itemLocation += perp * (p.Shake * wobble);
            ApplyRotationTarget(player, ref rotApplied, ref lastAnim, p.Kick * env + p.ShakeRot * wobble);
            if (newShot) {
                ScreenPunch(player, aim, p.Screen);
            }
        }

        /// <summary>
        /// 阻尼颠动波形：出膛帧为 0（先挫再抖，不抢出膛帧的干净后挫），此后正弦振荡逐帧衰减。
        /// seed 只改相位，held 枪传 identity、方案枪传 whoAmI
        /// </summary>
        public static float Wobble(int elapsed, float freq, float damping, int seed) {
            if (elapsed <= 0) {
                return 0f;
            }
            return MathF.Sin(elapsed * freq + seed * 1.7f) * MathF.Pow(damping, elapsed);
        }

        /// <summary>
        /// 以「目标绝对偏移」差分施加角度。镜像角约定（atan2(vy·dir, vx·dir)）：
        /// 面右朝上=角减、面左朝上=角增，倒挂重力再翻一次，故上踢符号 = −direction·gravDir
        /// </summary>
        public static void ApplyRotationTarget(Player player, ref float applied, ref int lastAnim, float want) {
            if (player.itemAnimation > lastAnim) {
                //动画重启 = 新一发：远端无射击回调，靠此清账（owner 端与射击路径清账互为双保险）
                applied = 0f;
            }
            lastAnim = player.itemAnimation;
            float dirSign = player.direction * player.gravDir;
            player.itemRotation -= (want - applied) * dirSign;
            applied = want;
        }

        /// <summary>
        /// 出膛震屏：只在本机玩家端、且客户端开了屏幕震动时生效。方向取瞄准反向（镜头被枪推一下）
        /// </summary>
        public static void ScreenPunch(Player player, Vector2 aimDir, float strength, string identity = "GsGunRecoil") {
            if (strength <= 0f || VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = (-aimDir).SafeNormalize(Vector2.UnitX);
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.Center, dir,
                strength, 6f, 7, 900f, identity));
        }
    }
}
