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
        /// <summary>基础转速</summary>
        public float baseRotSpeed = 0.012f;
        /// <summary>能量转化系数</summary>
        public float energyConversion = 2f;
        /// <summary>音量下限基数</summary>
        public float baseSoundPith = 0.4f;
        /// <summary>基础音量</summary>
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
            //风速倍率 0.8~2.5
            float windFactor = MathHelper.Clamp(0.8f + Main.windSpeedCurrent * 1.7f, 0.8f, 2.5f);

            rotSpeed = baseRotSpeed * windFactor;
            rotition += rotSpeed;

            float ueGain = rotSpeed * energyConversion;
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
