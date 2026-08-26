using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    /// <summary>全屏界面的域归属：同域全屏开启时各域 HUD 自管演出（部分淡出、呼吸残留等），
    /// 异域全屏开启时一律让位</summary>
    internal enum FullScreenUIDomain
    {
        /// <summary>公共屏（任务书等），对所有 HUD 都是异域</summary>
        Common,
        /// <summary>鬼伞（湖心景）</summary>
        Kikasa,
        /// <summary>比目鱼（图鉴）</summary>
        Halibut,
        /// <summary>鬼切（改铭台/结印盘/点鬼簿/铭谱）</summary>
        Onikiri,
    }

    /// <summary>
    /// 全屏 UIHandle 的标记接口：摊开时占据整屏、要求其余 HUD 让位。
    /// 挂上即被 <see cref="FullScreenUIHub"/> 收编，开合状态读基类 IsOpen/OpenProgress
    /// </summary>
    internal interface IFullScreenUIHandle
    {
        /// <summary>本屏的域归属，默认公共屏</summary>
        FullScreenUIDomain FullScreenDomain => FullScreenUIDomain.Common;
    }

    /// <summary>
    /// 全屏界面注册与占屏判定的唯一口径。HUD 的显隐、按键入口的互斥都问这里，
    /// 别再各自散写 QuestLog.IsOpen 之类的点名判断
    /// </summary>
    internal static class FullScreenUIHub
    {
        private static List<UIHandle> members;

        /// <summary>含开合过渡：合拢淡出未走完也算占屏，HUD 复现与二次开屏都等它归零</summary>
        private static bool Occupying(UIHandle handle)
            => handle.IsOpen || handle.OpenProgress.Current > 0.01f;

        private static List<UIHandle> Members {
            get {
                //查询可能同帧来自逻辑线程(LogicUpdate)与绘制线程(Update/Draw)：
                //先建满再一次性赋引用，读者永远拿不到半成品列表
                if (members == null) {
                    List<UIHandle> built = [];
                    foreach (UIHandle handle in UIHandleLoader.UIHandles) {
                        if (handle is IFullScreenUIHandle) {
                            built.Add(handle);
                        }
                    }
                    members = built;
                }
                return members;
            }
        }

        /// <summary>任意全屏界面是否开着（含开合过渡）</summary>
        public static bool AnyOpen {
            get {
                foreach (UIHandle handle in Members) {
                    if (Occupying(handle)) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>除自身外是否还有全屏界面开着；开屏入口用它自证清白（自己的合拢尾焰不算数）</summary>
        public static bool AnyOpenExcept(UIHandle self) {
            foreach (UIHandle handle in Members) {
                if (!ReferenceEquals(handle, self) && Occupying(handle)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定域之外是否有全屏界面开着；HUD 的交互闸用它当帧断电</summary>
        public static bool AnyForeignOpen(FullScreenUIDomain domain) {
            foreach (UIHandle handle in Members) {
                if (((IFullScreenUIHandle)handle).FullScreenDomain != domain && Occupying(handle)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定域之外全屏界面的最大展开度 [0,1]；HUD 的淡出跟它同步</summary>
        public static float ForeignOcclusion01(FullScreenUIDomain domain) {
            float max = 0f;
            foreach (UIHandle handle in Members) {
                if (((IFullScreenUIHandle)handle).FullScreenDomain == domain) {
                    continue;
                }
                float progress = handle.OpenProgress.Current;
                if (handle.IsOpen && progress < 0.01f) {
                    progress = 0.01f;//刚请求打开还没走进度，也得立刻算占屏
                }
                if (progress > max) {
                    max = progress;
                }
            }
            return MathHelper.Clamp(max, 0f, 1f);
        }

        /// <summary>
        /// 开屏前的互斥闸：别的全屏界面或快捷转盘开着时拒绝抢屏并轻响一声；
        /// 通过才继续 base.Open()。全屏 UI 在 <c>Open()</c> 重写里调用，
        /// 按键/物品/图标/剧情等一切入口自然收拢到这一道闸
        /// </summary>
        public static bool TryClaimScreen(UIHandle self) {
            if (!AnyOpenExcept(self) && !RadialWheelHub.AnyOpen) {
                return true;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Volume = 0.55f,
                Pitch = -0.7f,
                MaxInstances = 2,
            });
            return false;
        }

        /// <summary>卸载清空，热重载后按新实例表重建</summary>
        internal static void Clear() => members = null;
    }

    /// <summary><see cref="FullScreenUIHub"/> 随模组卸载清理</summary>
    internal sealed class FullScreenUIHubLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => FullScreenUIHub.Clear();
    }
}
