using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Industrials.Generator.WindGriven
{
    public abstract class BaseWindGrivenTP : BaseGeneratorTP
    {
        public float rotition;
        public float rotSpeed;
        public int soundCount;
        /// <summary>
        /// 基础转速基数
        /// </summary>
        public float baseRotSpeed = 0.012f;
        /// <summary>
        /// 能量转化系数
        /// </summary>
        public float energyConversion = 2f;
        /// <summary>
        /// 音量变化基数
        /// </summary>
        public float baseSoundPith = 0.4f;
        /// <summary>
        /// 基础音量
        /// </summary>
        public float baseVolume = 0.35f;
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
            // 计算风速影响（风速越大，旋转越快，最低0.8倍，最高2.5倍）
            float windFactor = MathHelper.Clamp(0.8f + Main.windSpeedCurrent * 1.7f, 0.8f, 2.5f);

            // 旋转速度随风速变化
            rotSpeed = baseRotSpeed * windFactor;
            rotition += rotSpeed;

            // 充能速度提升，开启风力物理时额外增加充能效率
            float ueGain = rotSpeed * energyConversion;
            if (Main.windPhysics) {
                ueGain *= 1.5f;  // 开启风力物理时加速充能
            }

            if (MachineData.UEvalue < MaxUEValue) {
                MachineData.UEvalue += ueGain;
            }

            // 风速影响音效播放频率和音量
            int soundInterval = (int)(160 / windFactor);  // 风大时声音播放更频繁
            float volumeFactor = MathHelper.Clamp(baseSoundPith + Main.windSpeedCurrent * 0.8f, baseSoundPith, 1.0f); // 音量随风速变化

            if (++soundCount > soundInterval && Main.LocalPlayer.DistanceSQ(CenterInWorld) < 640000) {
                SoundEngine.PlaySound(CWRSound.Windmill with { Volume = baseVolume * volumeFactor, MaxInstances = 12 }, CenterInWorld);
                soundCount = 0;
            }
        }
    }
}
