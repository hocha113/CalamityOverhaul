using InnoVault.GameSystem;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.Calamity
{
    /// <summary>
    /// 灾厄弱引用修复补丁的公共骨架：只在灾厄在场且非服务端时挂钩，
    /// 反射失败仅记录日志并放弃该补丁，绝不让 CWR 自身加载失败。<br/>
    /// 钩子记录自持：CWR 卸载先于 InnoVault，提前撤钩防止悬挂指针指向本程序集
    /// </summary>
    internal abstract class CalamityPatchBase : ICWRLoader
    {
        //按类型登记的实例表，设置界面据此读取补丁挂载状态
        private static readonly Dictionary<Type, CalamityPatchBase> registry = [];

        private readonly List<(MethodBase Method, Delegate Hook)> hookRecords = [];

        /// <summary>日志前缀，默认取类型名</summary>
        protected virtual string LogName => GetType().Name;

        /// <summary>目标方法是否全部挂钩成功，供设置界面显示补丁状态</summary>
        public bool Applied { get; private set; }

        /// <summary>灾厄程序集</summary>
        protected static Mod CalamityMod => CWRMod.Instance?.calamity;

        /// <summary>指定补丁是否已成功挂载</summary>
        public static bool IsApplied<T>() where T : CalamityPatchBase
            => registry.TryGetValue(typeof(T), out CalamityPatchBase patch) && patch.Applied;

        void ICWRLoader.LoadData() {
            registry[GetType()] = this;
            Applied = false;
            if (Main.dedServ || CalamityMod == null) {
                return;
            }
            try {
                Applied = Install(CalamityMod);
            } catch (Exception ex) {
                Applied = false;
                CWRMod.Instance.Logger.Warn($"{LogName}: install failed, patch disabled. {ex.GetType().Name}: {ex.Message}");
            }
            if (!Applied) {
                Unhook();
            }
            else {
                CWRMod.Instance.Logger.Info($"{LogName}: patch applied ({hookRecords.Count} hook(s)).");
            }
        }

        void ICWRLoader.SetupData() {
            if (!Applied) {
                return;
            }
            try {
                Setup(CalamityMod);
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Warn($"{LogName}: setup failed. {ex.GetType().Name}: {ex.Message}");
            }
        }

        void ICWRLoader.UnLoadData() {
            Unhook();
            Applied = false;
            registry.Remove(GetType());
            Cleanup();
        }

        /// <summary>解析类型/成员并挂钩，返回是否全部就位</summary>
        protected abstract bool Install(Mod calamity);

        /// <summary>PostSetupContent 期补充解析（如内容 ID）</summary>
        protected virtual void Setup(Mod calamity) { }

        /// <summary>卸载时清空静态缓存</summary>
        protected virtual void Cleanup() { }

        private void Unhook() {
            foreach ((MethodBase method, Delegate hookDelegate) in hookRecords) {
                if (!VaultHook.Hooks.TryRemove((method, hookDelegate), out Hook hook) || hook == null) {
                    continue;
                }
                if (hook.IsApplied) {
                    hook.Undo();
                }
                hook.Dispose();
            }
            hookRecords.Clear();
        }

        protected Type FindType(Mod mod, string fullName) {
            Type type = mod.Code?.GetType(fullName);
            if (type == null) {
                CWRMod.Instance.Logger.Warn($"{LogName}: type {fullName} not found, Calamity may have changed.");
            }
            return type;
        }

        protected MethodInfo FindMethod(Type type, string name, BindingFlags flags, Type[] parameterTypes = null) {
            if (type == null) {
                return null;
            }
            MethodInfo method = parameterTypes == null
                ? type.GetMethod(name, flags)
                : type.GetMethod(name, flags, null, parameterTypes, null);
            if (method == null) {
                CWRMod.Instance.Logger.Warn($"{LogName}: method {type.FullName}.{name} not found, Calamity may have changed.");
            }
            return method;
        }

        protected FieldInfo FindField(Type type, string name, BindingFlags flags) {
            if (type == null) {
                return null;
            }
            FieldInfo field = type.GetField(name, flags);
            if (field == null) {
                CWRMod.Instance.Logger.Warn($"{LogName}: field {type.FullName}.{name} not found, Calamity may have changed.");
            }
            return field;
        }

        /// <summary>挂钩并登记，方法为空时返回 false</summary>
        protected bool Hook(MethodBase method, Delegate hookDelegate) {
            if (method == null || hookDelegate == null) {
                return false;
            }
            VaultHook.Add(method, hookDelegate);
            hookRecords.Add((method, hookDelegate));
            return true;
        }
    }
}
