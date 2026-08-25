using CalamityOverhaul.Content.Industrials.MachineModules;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets
{
    /// <summary>
    /// 防御塔TP基类:从特斯拉电磁塔提炼的索敌/开火节奏/弹药耗电/模式位骨架。<br/>
    /// 表现(粒子/音效/光环视觉)与弹幕生成全部留在子类;基类只管逻辑。<br/>
    /// 新塔弹幕一律用普通 ModProjectile 在权威端生成(owner 取默认 Main.myPlayer,
    /// 服务器上即 255,NewProjectile 自动广播 spawn 包并由服务器结算命中);
    /// 特斯拉沿用旧的玩家端 BaseHeldProj 路径,靠 <see cref="SimulateOnAllEndpoints"/> 保持全端模拟
    /// </summary>
    internal abstract class BaseTurretTP : BaseBattery
    {
        public override bool ReceivedEnergy => true;

        /// <summary>索敌半径(像素)</summary>
        public virtual float AttackRange => 700;
        /// <summary>单发耗电</summary>
        public virtual float ShotCost => 60;
        /// <summary>开火间隔(帧)</summary>
        public virtual int FireInterval => 60;
        /// <summary>索敌时是否无视瓦片遮挡(false=FindClosestNPC 内部做 Collision.CanHit 视线检查)</summary>
        public virtual bool TargetingIgnoresTiles => false;
        /// <summary>索敌是否Boss优先</summary>
        public virtual bool BossPriorityTargeting => true;
        /// <summary>模块槽数;0=完全不启用模块架,序列化零字节(特斯拉靠它保持旧包序/旧档格式)</summary>
        public virtual int ModuleSlotCount => 2;
        /// <summary>
        /// true=逻辑在所有端同跑(特斯拉旧行为、光环塔的篝火模型);
        /// false=索敌与开火判定仅权威端(!isClient),客户端只跑 <see cref="UpdateTurretClient"/> 表现
        /// </summary>
        public virtual bool SimulateOnAllEndpoints => false;
        /// <summary>存档缺失模式键时的默认值(特斯拉旧档语义为 false=护卫,新塔默认 true=开机)</summary>
        protected virtual bool DefaultModeWhenMissing => true;

        /// <summary>通用模式位:特斯拉=攻击/护卫切换,其余塔=开/关;右键或电线翻转,SendData 传播</summary>
        public bool AttackPattern { get; set; } = true;
        /// <summary>当前目标,由 <see cref="RunAttackCycle"/> 在权威端赋值</summary>
        public NPC TargetByNPC { get; set; }
        /// <summary>开火冷却计数器</summary>
        public int FireCoolden { get; set; }

        /// <summary>塔族模块架;<see cref="ModuleSlotCount"/> 为 0 时不参与任何序列化与更新</summary>
        internal readonly MachineModuleRack ModuleRack = new(MachineModuleTarget.Turret);

        #region 模块生效值(槽数 0 时聚合乘数恒为 1,特斯拉行为逐字节不变)
        /// <summary>模块生效索敌半径;光环塔的光环半径同乘</summary>
        public float EffectiveRange => AttackRange * ModuleRack.TurretRangeMult;
        /// <summary>模块生效开火间隔(射速乘数缩短间隔,下限 1 帧)</summary>
        public int EffectiveFireInterval => System.Math.Max(1, (int)(FireInterval / ModuleRack.TurretRateMult));
        /// <summary>模块生效单发耗电</summary>
        public float EffectiveShotCost => ShotCost * ModuleRack.TurretEnergyMult;
        #endregion

        #region 序列化:MachineData → AttackPattern → 模块架(槽数>0时追加)
        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(AttackPattern);
            if (ModuleSlotCount > 0) {
                ModuleRack.Send(data, ModuleSlotCount);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            bool oldMode = AttackPattern;
            AttackPattern = reader.ReadBoolean();
            if (ModuleSlotCount > 0) {
                ModuleRack.Receive(reader, ModuleSlotCount);
            }
            //入世快照不放表现,只有真实的模式翻转才触发钩子
            if (!TileProcessorNetWork.InitializeWorld && oldMode != AttackPattern) {
                OnModeChangedByNet();
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["AttackPattern"] = AttackPattern;
            if (ModuleSlotCount > 0) {
                ModuleRack.Save(tag, ModuleSlotCount);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (tag.TryGet("AttackPattern", out bool mode)) {
                AttackPattern = mode;
            }
            else {
                AttackPattern = DefaultModeWhenMissing;
            }
            if (ModuleSlotCount > 0) {
                ModuleRack.Load(tag, ModuleSlotCount, GetType().Name);
            }
        }
        #endregion

        /// <summary>模式经网络包变更(非入世快照)时的表现钩子,在收包端执行</summary>
        protected virtual void OnModeChangedByNet() { }

        /// <summary>模式在本地翻转时的表现钩子</summary>
        protected virtual void OnModeToggleEffect() { }

        /// <summary>
        /// 右键/电线翻转模式;ModTile.RightClick 与 HitWire 只在交互端/布线端执行,
        /// SendData 即传播机制(镜像特斯拉 RightEvent)
        /// </summary>
        public virtual void RightEvent() {
            AttackPattern = !AttackPattern;
            SendData();
            OnModeToggleEffect();
        }

        public sealed override void UpdateMachine() {
            if (ModuleSlotCount > 0) {
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.Refresh();
            }
            if (!SimulateOnAllEndpoints && VaultUtils.isClient) {
                UpdateTurretClient();
                return;
            }
            UpdateTurret();
        }

        /// <summary>塔逻辑主体;<see cref="SimulateOnAllEndpoints"/>=false 时仅权威端执行</summary>
        protected abstract void UpdateTurret();

        /// <summary>权威 gate 生效时客户端的表现帧(索敌开火不在此进行)</summary>
        protected virtual void UpdateTurretClient() { }

        /// <summary>
        /// 标准攻击循环,与特斯拉旧攻击帧逐字节等价(模块乘数在槽数 0 时恒为 1):
        /// 电量门在冷却自增之前(短路,电不足时冷却不涨);找到目标才扣电;
        /// 无论是否找到目标,判定过后冷却归零
        /// </summary>
        protected void RunAttackCycle() {
            if (MachineData.UEvalue >= EffectiveShotCost && ++FireCoolden > EffectiveFireInterval) {
                TargetByNPC = AcquireTarget();
                if (TargetByNPC != null) {
                    Fire(TargetByNPC);
                    MachineData.UEvalue -= EffectiveShotCost;
                }
                FireCoolden = 0;
            }
        }

        /// <summary>索敌:默认取范围内最近敌怪,受视线/Boss优先开关控制</summary>
        protected virtual NPC AcquireTarget()
            => CenterInWorld.FindClosestNPC(EffectiveRange, TargetingIgnoresTiles, BossPriorityTargeting);

        /// <summary>开火实现:弹幕生成与开火表现全在子类</summary>
        protected virtual void Fire(NPC target) { }

        public override void MachineKill() {
            //模块随拆机倒出(权威端)
            if (!VaultUtils.isClient && ModuleSlotCount > 0) {
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.DropAll(item => DropItem(item));
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
