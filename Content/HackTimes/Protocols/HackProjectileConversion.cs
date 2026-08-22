using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 骇入复制/复刻弹的生成来源（镜像水面、弹幕采样共用）。<br/>
    /// friendly/hostile 不在任何原版同步包里（弹道接管的既有教训），
    /// 而 NewProjectile 的生成包在它内部就发走了，生成后再补赋值必然漏包。
    /// 唯一赶在包发出之前的钩子是 OnSpawn，所以用自定义源把
    /// "这发要转成友方"带进 <see cref="HackConvertedProjectile.OnSpawn"/>
    /// </summary>
    internal sealed class HackConversionSource : IEntitySource
    {
        public string Context => "CWRHackConversion";

        /// <summary>施术者索引，复制弹的归属</summary>
        internal int CasterIndex { get; }

        /// <summary>复刻弹压到 1 穿透（弹幕采样），镜像复制弹保留原型穿透</summary>
        internal bool CapPenetrate { get; }

        internal HackConversionSource(int casterIndex, bool capPenetrate) {
            CasterIndex = casterIndex;
            CapPenetrate = capPenetrate;
        }
    }

    /// <summary>
    /// 敌弹转友方的持久标记。<br/>
    /// 标记走 GlobalProjectile 的 ExtraAI 通道随 27 号包到达每一端，
    /// 各端收到后自己翻阵营，和弹道接管"各端各自翻"是同一套分工，
    /// 只是这里有生成包可搭，不需要各端各自扫描
    /// </summary>
    internal class HackConvertedProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private static readonly Color Signal = new(140, 235, 255);

        private bool converted;
        //远端首次收到标记时闪一下；后续重同步不再闪
        private bool flashPlayed;

        internal static bool IsConverted(Projectile projectile)
            => projectile != null
                && projectile.TryGetGlobalProjectile(out HackConvertedProjectile marks)
                && marks.converted;

        //槽位复用必须清干净，否则新弹幕继承旧弹的转阵营标记
        public override void SetDefaults(Projectile projectile) {
            converted = false;
            flashPlayed = false;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (source is not HackConversionSource conversion) {
                return;
            }
            //OnSpawn 在生成包发出之前跑，这里翻好阵营，包里带的就是转换后的标记
            Apply(projectile, conversion.CapPenetrate);
        }

        internal static void Apply(Projectile projectile, bool capPenetrate) {
            if (projectile?.active != true
                || !projectile.TryGetGlobalProjectile(out HackConvertedProjectile marks)) {
                return;
            }
            marks.converted = true;
            FlipFaction(projectile);
            //穿透 -1 是无限穿透，压它是削弱不是保底，所以无限与多段都压到 1
            if (capPenetrate && projectile.penetrate != 1) {
                projectile.penetrate = 1;
            }
        }

        private static void FlipFaction(Projectile projectile) {
            projectile.friendly = true;
            projectile.hostile = false;
        }

        //个别类型的 AI 会自己改阵营标志，转过的弹每帧压回去
        public override void PostAI(Projectile projectile) {
            if (converted) {
                FlipFaction(projectile);
            }
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter,
            BinaryWriter binaryWriter) {
            bitWriter.WriteBit(converted);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader,
            BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) {
                return;
            }
            converted = true;
            FlipFaction(projectile);
            if (!flashPlayed) {
                flashPlayed = true;
                EmitConvertFlash(projectile.Center);
            }
        }

        /// <summary>转换落位的确认闪光，读作"这发已经归你"</summary>
        internal static void EmitConvertFlash(Vector2 center) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Signal, 0.8f)
                    ?.Configure(false, 14);
            }
            PRTLoader.NewParticle<PRT_Spark>(center, Vector2.Zero, Color.White, 1.3f)
                ?.Configure(false, 8);
        }
    }
}
