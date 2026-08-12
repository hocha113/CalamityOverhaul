using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    public abstract class BaseWindGrivenTP : BaseGeneratorTP, IGeneratorReadout
    {
        public float rotition;
        public float rotSpeed;
        public int soundCount;
        /// <summary>基础转速</summary>
        public float baseRotSpeed = 0.012f;
        /// <summary>能量转化系数</summary>
        public float energyConversion = 2f;
        /// <summary>音量下限基数</summary>
        public float baseSoundPith = 0.4f;
        /// <summary>基础音量</summary>
        public float baseVolume = 0.35f;

        public override MachineModules.MachineModuleTarget ModuleHostKind
            => MachineModules.MachineModuleTarget.WindGenerator;
        /// <summary>玩家风机默认两槽;荒野敌对结构覆写为 0</summary>
        public override int ModuleSlotCount => 2;

        #region 读数板
        /// <summary>与 GeneratorUpdate 同一条风速倍率公式;低风齿轮箱模块抬升下限</summary>
        internal float WindFactor
            => MathF.Max(MathHelper.Clamp(0.8f + Main.windSpeedCurrent * 1.7f, 0.8f, 2.5f),
                ModuleRack.GenConditionFloor);
        public GeneratorReadoutKind ReadoutKind => GeneratorReadoutKind.Wind;
        public float ConditionRatio => WindFactor / 2.5f;
        public bool ConditionOk => WindFactor > 0.95f;
        public float OutputPerSecond {
            get {
                float gain = baseRotSpeed * WindFactor * energyConversion * ModuleRack.GenOutputMult;
                if (Main.windPhysics) {
                    gain *= 1.5f;
                }
                return gain * 60f;
            }
        }
        #endregion
        public sealed override void SetGenerator() {
            baseRotSpeed = 0.012f;
            energyConversion = 2f;
            baseSoundPith = 0.4f;
            baseVolume = 0.35f;
            SetWindGriven();
        }

        public virtual void SetWindGriven() {

        }

        public sealed override void GeneratorUpdate() {
            //风速倍率 0.8~2.5,低风齿轮箱抬下限(与读数板同一条公式)
            float windFactor = WindFactor;

            rotSpeed = baseRotSpeed * windFactor;
            rotition += rotSpeed;

            float ueGain = rotSpeed * energyConversion * ModuleRack.GenOutputMult;
            if (Main.windPhysics) {
                ueGain *= 1.5f;//windPhysics 额外加速
            }

            if (MachineData.UEvalue < MaxUEValue) {
                MachineData.UEvalue += ueGain;
            }

            int soundInterval = (int)(160 / windFactor);//风大更频
            float volumeFactor = MathHelper.Clamp(baseSoundPith + Main.windSpeedCurrent * 0.8f, baseSoundPith, 1.0f);

            if (++soundCount > soundInterval && Main.LocalPlayer.DistanceSQ(CenterInWorld) < 640000) {
                //并行阶段延后到主线程
                Defer(() => SoundEngine.PlaySound(CWRSound.Windmill with { Volume = baseVolume * volumeFactor, MaxInstances = 12 }, CenterInWorld));
                soundCount = 0;
            }
        }
    }
}
