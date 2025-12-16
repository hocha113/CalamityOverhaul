using System;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    /// <summary>
    /// 一个自定义的JIT过滤器属性，用于在模组未加载或版本不匹配时阻止方法被JIT编译
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property, Inherited = true)]
    public class CWRJITEnabledAttribute : MemberJitAttribute
    {
        public override bool ShouldJIT(System.Reflection.MemberInfo member) {
            return ModLoader.TryGetMod("CalamityMod", out var mod) && mod.Version >= new Version(2, 0, 7, 2);
        }
    }
}
