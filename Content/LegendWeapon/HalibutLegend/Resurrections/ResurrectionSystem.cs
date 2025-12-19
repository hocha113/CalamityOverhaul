using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    public class ResurrectionSystem
    {
        #region 核心数据
        internal Player Player;

        /// <summary>
        /// 当前复苏值
        /// </summary>
        private float currentValue = 0f;

        /// <summary>
        /// 最大复苏值
        /// </summary>
        private float maxValue = 100f;

        /// <summary>
        /// 复苏速度（每帧增加的值，默认0表示不自动增长）
        /// </summary>
        private float resurrectionRate = 0f;

        /// <summary>
        /// 是否启用复苏系统
        /// </summary>
        private bool isEnabled = true;
        #endregion

        #region 事件系统
        /// <summary>
        /// 复苏值变化事件
        /// </summary>
        public event Action<float, float> OnValueChanged;

        /// <summary>
        /// 复苏值达到最大值事件（危险！会导致玩家死亡）
        /// </summary>
        public event Action OnMaxValueReached;

        /// <summary>
        /// 复苏值归零事件
        /// </summary>
        public event Action OnValueZero;

        /// <summary>
        /// 复苏速度变化事件
        /// </summary>
        public event Action<float> OnRateChanged;

        /// <summary>
        /// 进入危险区域事件（70%以上）
        /// </summary>
        public event Action OnEnterDangerZone;

        /// <summary>
        /// 进入极危区域事件（90%以上）
        /// </summary>
        public event Action OnEnterCriticalZone;

        /// <summary>
        /// 离开危险区域事件
        /// </summary>
        public event Action OnLeaveDangerZone;

        private bool wasInDangerZone = false;
        private bool wasInCriticalZone = false;
        #endregion

        #region 阈值回调
        /// <summary>
        /// 阈值触发器字典 (阈值比例 -> 回调)
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<float, Action> thresholdCallbacks = new();

        /// <summary>
        /// 已触发的阈值集合
        /// </summary>
        private readonly System.Collections.Generic.HashSet<float> triggeredThresholds = new();
        #endregion

        #region 属性访问器
        /// <summary>
        /// 获取当前复苏值（只读）
        /// </summary>
        public float CurrentValue => currentValue;

        /// <summary>
        /// 获取或设置最大复苏值
        /// </summary>
        public float MaxValue {
            get => maxValue;
            set {
                if (value <= 0) {
                    throw new ArgumentException("The maximum value must be greater than 0");
                }
                maxValue = value;
                //如果当前值超过新的最大值，则裁剪
                if (currentValue > maxValue) {
                    SetValue(maxValue);
                }
            }
        }

        /// <summary>
        /// 获取或设置复苏速度
        /// </summary>
        public float ResurrectionRate {
            get => resurrectionRate;
            set {
                //如果数值变化极小，则忽略
                if (Math.Abs(resurrectionRate - value) < 0.0001f) {
                    return;
                }

                //本地设置值（不触发同步逻辑）
                SetResurrectionRateNoSync(value);

                //网络同步逻辑
                if (Player == null || !Player.active) {
                    return;
                }

                //如果是客户端，且是本机玩家，发送给服务器
                if (VaultUtils.isClient && Player.whoAmI == Main.myPlayer) {
                    SendResurrectionRate(value, Player.whoAmI);
                }
                //如果是服务器，发送给所有客户端，作为权威数据
                else if (VaultUtils.isServer) {
                    SendResurrectionRate(value, Player.whoAmI);
                }
            }
        }

        /// <summary>
        /// 获取复苏进度比例（0-1）
        /// </summary>
        public float Ratio => maxValue > 0 ? Math.Clamp(currentValue / maxValue, 0f, 1f) : 0f;

        /// <summary>
        /// 获取或设置系统启用状态
        /// </summary>
        public bool IsEnabled {
            get => isEnabled;
            set => isEnabled = value;
        }

        /// <summary>
        /// 检查是否已满
        /// </summary>
        public bool IsFull => currentValue >= maxValue;

        /// <summary>
        /// 检查是否为空
        /// </summary>
        public bool IsEmpty => currentValue <= 0f;
        #endregion

        #region 网络同步
        /// <summary>
        /// 设置复苏速度但不触发网络同步，用于接收网络包或内部更新
        /// </summary>
        public void SetResurrectionRateNoSync(float value) {
            float oldRate = resurrectionRate;
            resurrectionRate = value;
            if (Math.Abs(oldRate - value) > 0.0001f) {
                OnRateChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// 发送复苏速度到网络
        /// </summary>
        /// <param name="value"></param>
        /// <param name="whoAmI"></param>
        internal static void SendResurrectionRate(float value, int whoAmI) {
            if (VaultUtils.isSinglePlayer) {
                return;
            }
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ResurrectionRate);
            modPacket.Write((byte)whoAmI);
            modPacket.Write(value);
            modPacket.Send();
        }

        /// <summary>
        /// 处理复苏速度网络包
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="senderWhoAmI"></param>
        internal static void HandleResurrectionRate(BinaryReader reader, int senderWhoAmI) {
            int playerIndex = reader.ReadByte();
            float value = reader.ReadSingle();
            if (!playerIndex.TryGetPlayer(out var player)) {
                return;
            }
            if (!player.TryGetHalibutPlayer(out var halibutPlayer)) {
                return;
            }

            //使用 NoSync 方法避免死循环，避免再次触发 Setter 的同步逻辑
            halibutPlayer.ResurrectionSystem.SetResurrectionRateNoSync(value);

            if (!VaultUtils.isServer) {
                return;
            }
            //服务器转发给其他客户端（排除发送者）
            ModPacket modPacket = CWRMod.Instance.GetPacket();
            modPacket.Write((byte)CWRMessageType.ResurrectionRate);
            modPacket.Write((byte)playerIndex);
            modPacket.Write(value);
            modPacket.Send(-1, senderWhoAmI);
        }
        #endregion

        #region 核心方法
        /// <summary>
        /// 设置复苏值（直接设置）
        /// </summary>
        /// <param name="value">目标值</param>
        /// <param name="triggerEvents">是否触发事件（默认true）</param>
        public void SetValue(float value, bool triggerEvents = true) {
            if (!isEnabled) {
                return;
            }

            float oldValue = currentValue;
            currentValue = Math.Clamp(value, 0f, maxValue);

            if (triggerEvents && Math.Abs(oldValue - currentValue) > 0.001f) {
                OnValueChanged?.Invoke(oldValue, currentValue);

                //检查特殊状态
                if (currentValue >= maxValue && oldValue < maxValue) {
                    OnMaxValueReached?.Invoke();
                }
                else if (currentValue <= 0f && oldValue > 0f) {
                    OnValueZero?.Invoke();
                }

                //检查阈值触发
                CheckThresholds(oldValue, currentValue);
            }
        }

        /// <summary>
        /// 增加复苏值
        /// </summary>
        /// <param name="amount">增加量</param>
        /// <param name="triggerEvents">是否触发事件（默认true）</param>
        public void AddValue(float amount, bool triggerEvents = true) {
            SetValue(currentValue + amount, triggerEvents);
        }

        /// <summary>
        /// 减少复苏值
        /// </summary>
        /// <param name="amount">减少量（正数）</param>
        /// <param name="triggerEvents">是否触发事件（默认true）</param>
        public void SubtractValue(float amount, bool triggerEvents = true) {
            SetValue(currentValue - amount, triggerEvents);
        }

        /// <summary>
        /// 重置复苏值为0
        /// </summary>
        /// <param name="triggerEvents">是否触发事件（默认true）</param>
        public void Reset(bool triggerEvents = true) {
            SetValue(0f, triggerEvents);
            //重置阈值触发记录
            triggeredThresholds.Clear();
        }

        /// <summary>
        /// 填满复苏值
        /// </summary>
        /// <param name="triggerEvents">是否触发事件（默认true）</param>
        public void Fill(bool triggerEvents = true) {
            SetValue(maxValue, triggerEvents);
        }

        /// <summary>
        /// 更新系统（每帧调用）
        /// 处理自动增长等逻辑
        /// </summary>
        public void Update() {
            if (!isEnabled) {
                return;
            }

            //应用复苏速度
            if (Math.Abs(resurrectionRate) > 0.001f) {
                AddValue(resurrectionRate, true);
            }

            //检查危险区域状态
            CheckDangerZone();
        }

        /// <summary>
        /// 检查并触发危险区域事件
        /// </summary>
        private void CheckDangerZone() {
            float ratio = Ratio;
            bool isInDangerZone = ratio >= 0.7f;
            bool isInCriticalZone = ratio >= 0.9f;

            //进入极危区域
            if (isInCriticalZone && !wasInCriticalZone) {
                wasInCriticalZone = true;
                OnEnterCriticalZone?.Invoke();
            }
            //离开极危区域
            else if (!isInCriticalZone && wasInCriticalZone) {
                wasInCriticalZone = false;
            }

            //进入危险区域
            if (isInDangerZone && !wasInDangerZone) {
                wasInDangerZone = true;
                OnEnterDangerZone?.Invoke();
            }
            //离开危险区域
            else if (!isInDangerZone && wasInDangerZone) {
                wasInDangerZone = false;
                OnLeaveDangerZone?.Invoke();
            }
        }
        #endregion

        #region 阈值系统
        /// <summary>
        /// 注册阈值回调
        /// </summary>
        /// <param name="threshold">阈值比例（0-1）</param>
        /// <param name="callback">回调函数</param>
        public void RegisterThreshold(float threshold, Action callback) {
            threshold = Math.Clamp(threshold, 0f, 1f);
            thresholdCallbacks[threshold] = callback;
        }

        /// <summary>
        /// 移除阈值回调
        /// </summary>
        /// <param name="threshold">阈值比例</param>
        public void UnregisterThreshold(float threshold) {
            thresholdCallbacks.Remove(threshold);
        }

        /// <summary>
        /// 清除所有阈值回调
        /// </summary>
        public void ClearThresholds() {
            thresholdCallbacks.Clear();
            triggeredThresholds.Clear();
        }

        /// <summary>
        /// 检查阈值触发
        /// </summary>
        private void CheckThresholds(float oldValue, float newValue) {
            float oldRatio = maxValue > 0 ? oldValue / maxValue : 0f;
            float newRatio = Ratio;

            foreach (var kvp in thresholdCallbacks) {
                float threshold = kvp.Key;

                //向上穿越阈值
                if (oldRatio < threshold && newRatio >= threshold) {
                    if (!triggeredThresholds.Contains(threshold)) {
                        triggeredThresholds.Add(threshold);
                        kvp.Value?.Invoke();
                    }
                }
                //向下穿越阈值（重置触发状态）
                else if (oldRatio >= threshold && newRatio < threshold) {
                    triggeredThresholds.Remove(threshold);
                }
            }
        }
        #endregion

        #region 数据持久化
        /// <summary>
        /// 保存数据到TagCompound
        /// </summary>
        public TagCompound SaveData() {
            TagCompound tag = new TagCompound {
                ["CurrentValue"] = currentValue,
                ["MaxValue"] = maxValue,
                ["ResurrectionRate"] = resurrectionRate,
                ["IsEnabled"] = isEnabled
            };
            return tag;
        }

        /// <summary>
        /// 从TagCompound加载数据
        /// </summary>
        public void LoadData(TagCompound tag) {
            if (tag.ContainsKey("CurrentValue")) {
                currentValue = tag.GetFloat("CurrentValue");
            }
            if (tag.ContainsKey("MaxValue")) {
                maxValue = tag.GetFloat("MaxValue");
            }
            if (tag.ContainsKey("ResurrectionRate")) {
                resurrectionRate = tag.GetFloat("ResurrectionRate");
            }
            if (tag.ContainsKey("IsEnabled")) {
                isEnabled = tag.GetBool("IsEnabled");
            }

            //清空阈值触发记录
            triggeredThresholds.Clear();
        }
        #endregion

        #region Utils
        /// <summary>
        /// 获取剩余值
        /// </summary>
        public float GetRemainingValue() => maxValue - currentValue;

        /// <summary>
        /// 检查是否达到指定阈值
        /// </summary>
        /// <param name="threshold">阈值比例（0-1）</param>
        public bool HasReachedThreshold(float threshold) {
            return Ratio >= Math.Clamp(threshold, 0f, 1f);
        }

        /// <summary>
        /// 按百分比设置复苏值
        /// </summary>
        /// <param name="percentage">百分比（0-100）</param>
        public void SetValueByPercentage(float percentage) {
            percentage = Math.Clamp(percentage, 0f, 100f);
            SetValue(maxValue * (percentage / 100f));
        }

        /// <summary>
        /// 获取百分比形式的复苏值
        /// </summary>
        public float GetPercentage() => Ratio * 100f;

        /// <summary>
        /// 复制当前系统状态到另一个系统
        /// </summary>
        public void CopyTo(ResurrectionSystem other) {
            if (other == null) {
                return;
            }

            other.currentValue = currentValue;
            other.maxValue = maxValue;
            other.resurrectionRate = resurrectionRate;
            other.isEnabled = isEnabled;
        }

        /// <summary>
        /// 创建当前系统的副本
        /// </summary>
        public ResurrectionSystem Clone() {
            ResurrectionSystem clone = new ResurrectionSystem();
            CopyTo(clone);
            return clone;
        }
        #endregion
    }
}
