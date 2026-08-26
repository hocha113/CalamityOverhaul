using System;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core
{
    //决定论RNG:splitmix64播种+xoshiro256**主流
    //核心层与Terraria无关,种子由生成期自genRand一次性抽取(蓝图H6)
    //子系统用Fork(盐)派生独立流,互不消耗主流(镜像Dungeonworld R4纪律)
    internal sealed class HadalRng
    {
        private ulong _s0, _s1, _s2, _s3;

        internal HadalRng(ulong seed) {
            //splitmix64把任意种子扩散成四个非全零状态字
            ulong z = seed;
            _s0 = Split(ref z);
            _s1 = Split(ref z);
            _s2 = Split(ref z);
            _s3 = Split(ref z);
        }

        private static ulong Split(ref ulong z) {
            z += 0x9E3779B97F4A7C15UL;
            ulong x = z;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }

        internal ulong NextULong() {
            ulong result = Rotl(_s1 * 5, 7) * 9;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = Rotl(_s3, 45);
            return result;
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        /// <summary>[0,1)均匀浮点</summary>
        internal float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));

        /// <summary>[min,max)浮点</summary>
        internal float NextFloat(float min, float max) => min + NextFloat() * (max - min);

        /// <summary>[min,max)整数,max>min</summary>
        internal int Next(int min, int max) => min + (int)(NextULong() % (ulong)(max - min));

        /// <summary>true概率=p</summary>
        internal bool Chance(float p) => NextFloat() < p;

        /// <summary>派生独立流:同主种子+同盐⇒同流,不消耗本流状态</summary>
        internal HadalRng Fork(ulong salt) {
            ulong mix = _s0 ^ Rotl(salt, 23) ^ (salt * 0x9E3779B97F4A7C15UL);
            return new HadalRng(mix);
        }
    }
}
