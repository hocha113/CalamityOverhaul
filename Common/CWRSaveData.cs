using System;
using Terraria;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 存档读写的安全工具集。<br/>
    /// tML 的契约：<see cref="TagCompound.TryGet{T}"/> 只对"键缺失"静默，键存在但类型不匹配（旧档换过类型、
    /// 外部工具改写）会直接抛 <see cref="System.IO.IOException"/>；ModPlayer / ModSystem 的读档异常会被 tML 包成
    /// CustomModDataException 让整个角色/世界拒绝载入，ModItem / GlobalItem 的读写异常则完全没有兜底，
    /// 会让整份玩家档或世界档写失败。这里的方法把"单个键坏了"收敛成"这个键回默认值 + 记日志"
    /// </summary>
    internal static class CWRSaveData
    {
        public static Item LoadItemTag(TagCompound itemTag, string context) {
            if (itemTag == null) {
                return new Item();
            }
            try {
                return ItemIO.Load(itemTag) ?? new Item();
            } catch (Exception ex) {
                CWRMod.Instance?.Logger?.Error($"[{context}] Failed to load saved item: {ex.Message}");
                return new Item();
            }
        }

        public static Item LoadItemFromTag(TagCompound tag, string key, string context) {
            if (tag != null && tag.TrySafeGet(key, out TagCompound itemTag, context)) {
                return LoadItemTag(itemTag, $"{context}:{key}");
            }
            return new Item();
        }

        /// <summary>
        /// 安全序列化单件物品：null 按空气写；他模组物品的 SaveData 抛出时记日志并落一件空气，
        /// 别让一件坏物品把整台机器 / 整个仓的存档一起报废
        /// </summary>
        public static TagCompound SaveItemTag(Item item, string context = null) {
            try {
                return ItemIO.Save(item ?? new Item());
            } catch (Exception ex) {
                CWRMod.Instance?.Logger?.Error(
                    $"[SaveItem{FormatContext(context)}] Failed to save item '{item?.Name}' (type {item?.type}), writing air instead: {ex}");
                return ItemIO.Save(new Item());
            }
        }

        /// <summary>
        /// 安全版 TryGet：键缺失、tag 为空、或存档里的类型与 <typeparamref name="T"/> 不匹配都返回 false，
        /// 后者额外记一条警告。调用方拿到 false 就走默认值，永远不会因为一个键把整段读档炸掉
        /// </summary>
        public static bool TrySafeGet<T>(this TagCompound tag, string key, out T value, string context = null) {
            value = default;
            if (tag == null || string.IsNullOrEmpty(key) || !tag.ContainsKey(key)) {
                return false;
            }
            try {
                return tag.TryGet(key, out value);
            } catch (Exception ex) {
                value = default;
                CWRMod.Instance?.Logger?.Warn(
                    $"[SaveData{FormatContext(context)}] key '{key}' could not be read as {typeof(T).Name}, " +
                    $"falling back to default: {ex.GetBaseException().Message}");
                return false;
            }
        }

        /// <summary>安全读取，缺失或类型不匹配时返回 <paramref name="fallback"/></summary>
        public static T SafeGet<T>(this TagCompound tag, string key, T fallback = default, string context = null)
            => tag.TrySafeGet(key, out T value, context) ? value : fallback;

        /// <summary>
        /// 整数宽容读取：byte / short / int / long 任一存法都能读回 int。
        /// 用于历史上换过整数宽度的键（例如槽位号从 int 改成 byte），直接 TryGet&lt;byte&gt; 读到 int 会抛
        /// </summary>
        public static bool TryGetAsInt(this TagCompound tag, string key, out int value) {
            value = 0;
            if (tag == null || string.IsNullOrEmpty(key) || !tag.ContainsKey(key)) {
                return false;
            }
            switch (tag[key]) {
                case int i:
                    value = i;
                    return true;
                case short s:
                    value = s;
                    return true;
                case byte b:
                    value = b;
                    return true;
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    value = (int)l;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 给没有框架兜底的读写主体套一层：异常只记日志不上抛。
        /// 用在 SaveMod 的 DoSave/DoLoad、ModItem/GlobalItem 的 SaveData/LoadData 这类"一抛就整档报废"的位置
        /// </summary>
        public static void Guard(string context, Action body) {
            if (body == null) {
                return;
            }
            try {
                body();
            } catch (Exception ex) {
                LogError(context, ex);
            }
        }

        /// <summary>读档主体兜住异常后统一记录；带完整堆栈，方便从 client.log 定位是哪个键坏了</summary>
        public static void LogLoadError(string context, Exception ex)
            => CWRMod.Instance?.Logger?.Error($"[{context}] LoadData failed, remaining fields keep defaults so the save can still be opened: {ex}");

        /// <summary>存档主体兜住异常后统一记录；已写入的键保留，未写入的键下次读档走默认值</summary>
        public static void LogSaveError(string context, Exception ex)
            => CWRMod.Instance?.Logger?.Error($"[{context}] SaveData failed, keys written before the error are kept: {ex}");

        private static void LogError(string context, Exception ex)
            => CWRMod.Instance?.Logger?.Error($"[{context}] save/load step failed: {ex}");

        private static string FormatContext(string context)
            => string.IsNullOrEmpty(context) ? string.Empty : ":" + context;
    }
}
