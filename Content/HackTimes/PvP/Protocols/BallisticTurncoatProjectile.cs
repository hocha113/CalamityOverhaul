using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 弹道倒戈的转阵营标记（独立钩子文件，不动既有分派点）。<br/>
    /// <b>形状照抄 HackConvertedProjectile</b>：唯一赶在生成包发出之前的钩子是 OnSpawn
    /// ：防守方本机（弹幕 owner 端）在这里打标，标记走 ExtraAI 随首包到达各端，
    /// 各端各自把 friendly/hostile 压平成双 false（原版碰撞全部旁路，
    /// 路人与 NPC 一个都不误伤）并跑同一套确定性回转（目标恒为 owner 本人，
    /// 各端都算得出，不掷骰不读 Main.rand，netcode §9.2）。<br/>
    /// 命中裁决只在防守方本机：手动碰撞盒相交 → 自伤 <c>Hurt(pvp:true, quiet:false)</c>
    /// （msg 16 自报 + 117 广播，PvP 生命归属方写），死因记攻击方
    /// </summary>
    internal class BallisticTurncoatProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        /// <summary>出膛后直飞的帧数，之后开始回转</summary>
        internal const int TurnDelayFrames = 20;
        private const float SteerRadPerFrame = 0.12f;
        private const float MinReturnSpeed = 8f;
        private const float MaxReturnSpeed = 22f;

        private static readonly Color TurnRed = new(230, 56, 68);

        private bool turned;
        private int age;
        //命中侧数据只在防守方本机有值：裁决端即打标端，不上线
        private int returnDamage;
        private int casterIndex = -1;
        private string casterName = string.Empty;
        //远端首次收到标记时闪一下；后续重同步不再闪（防重放）
        private bool flashPlayed;

        //槽位复用必须清干净，否则新弹幕继承旧弹的倒戈标记
        public override void SetDefaults(Projectile projectile) {
            turned = false;
            age = 0;
            returnDamage = 0;
            casterIndex = -1;
            casterName = string.Empty;
            flashPlayed = false;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            //只劫持"主动使用武器"射出的攻击弹：召唤物/哨兵/陷阱/钩爪/持握弹全豁免
            if (source is not EntitySource_ItemUse
                || !projectile.friendly || projectile.hostile
                || projectile.damage <= 0
                || projectile.minion || projectile.sentry
                || projectile.minionSlots > 0f || projectile.npcProj || projectile.trap
                || projectile.ModProjectile is BaseHeldProj) {
                return;
            }
            if (!BallisticTurncoat.TryMark(projectile, out int caster,
                out string name, out int damage)) {
                return;
            }
            //OnSpawn 在生成包发出之前跑：压平后的阵营旗与 ExtraAI 标记都随首包走
            turned = true;
            casterIndex = caster;
            casterName = name;
            returnDamage = damage;
            flashPlayed = true;
            Neutralize(projectile);
            EmitTurnFlash(projectile.Center);
        }

        //个别类型的 AI 会自己改阵营标志，转过的弹每帧压回去
        private static void Neutralize(Projectile projectile) {
            projectile.friendly = false;
            projectile.hostile = false;
        }

        public override void PostAI(Projectile projectile) {
            if (!turned) return;
            Neutralize(projectile);
            age++;

            if (age == TurnDelayFrames) {
                //调头瞬间续命：短寿弹（近程散弹）也要飞得回主人身边。
                //各端在同一 age 做同一件事，确定性写入不需要 netUpdate
                projectile.timeLeft = Math.Max(projectile.timeLeft, 240);
            }
            if (age >= TurnDelayFrames) {
                Steer(projectile);
            }
            if (!Main.dedServ && age % 3 == 0) {
                PRTLoader.NewParticle<PRT_TBUGGlitch>(
                    projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -projectile.velocity * 0.08f, default,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(14);
            }

            TryResolveHit(projectile);
        }

        /// <summary>确定性回转：恒指 owner 本人，同帧同输入各端同解</summary>
        private void Steer(Projectile projectile) {
            Player owner = Main.player[projectile.owner];
            if (owner?.active != true || owner.dead) return;
            float cur = projectile.velocity.ToRotation();
            float target = (owner.Center - projectile.Center).ToRotation();
            float diff = MathHelper.WrapAngle(target - cur);
            //起转柔和、追尾收紧，读作"被夺走控制权"而不是瞬间掉头
            float rate = SteerRadPerFrame
                + Math.Min((age - TurnDelayFrames) * 0.004f, 0.10f);
            float step = MathHelper.Clamp(diff, -rate, rate);
            float speed = Math.Max(projectile.velocity.Length(), MinReturnSpeed);
            projectile.velocity = (cur + step).ToRotationVector2()
                * Math.Min(speed * 1.012f, MaxReturnSpeed);
        }

        /// <summary>命中裁决，只在防守方本机（owner 端）跑</summary>
        private void TryResolveHit(Projectile projectile) {
            if (projectile.owner != Main.myPlayer || age <= TurnDelayFrames) return;
            Player owner = Main.player[projectile.owner];
            if (owner?.active != true || owner.dead) return;
            if (!projectile.Hitbox.Intersects(owner.Hitbox)) return;

            //残余无敌帧内命中：弹幕耗掉，伤害免了，与弹幕直觉一致
            if (returnDamage > 0 && !owner.immune) {
                BallisticTurncoat hack = QuickHackDef.Get<BallisticTurncoat>();
                PlayerDeathReason reason = hack != null
                    ? PlayerDeathReason.ByCustomReason(NetworkText.FromKey(
                        hack.DeathReason.Key, owner.name, ResolveCasterName()))
                    : PlayerDeathReason.ByCustomReason(
                        NetworkText.FromLiteral(owner.name));
                int direction = projectile.velocity.X >= 0f ? 1 : -1;
                owner.Hurt(reason, returnDamage, direction, pvp: true, quiet: false);
            }
            projectile.Kill();
        }

        private string ResolveCasterName() {
            if (!string.IsNullOrEmpty(casterName)) return casterName;
            return casterIndex >= 0 && casterIndex < Main.maxPlayers
                ? Main.player[casterIndex]?.name ?? "?" : "?";
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter,
            BinaryWriter binaryWriter) {
            //必须无条件写：extraAI 没有逐 Global 的分段头，少写一位全线错位
            bitWriter.WriteBit(turned);
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader,
            BinaryReader binaryReader) {
            if (!bitReader.ReadBit()) return;
            turned = true;
            Neutralize(projectile);
            if (!flashPlayed) {
                flashPlayed = true;
                EmitTurnFlash(projectile.Center);
            }
        }

        public override void OnKill(Projectile projectile, int timeLeft) {
            if (!turned || Main.dedServ) return;
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center,
                    Main.rand.NextVector2CircularEdge(3f, 3f), TurnRed, 0.8f)
                    ?.Configure(false, 14);
            }
        }

        /// <summary>倒戈落位的确认闪光：这发已经不归你了</summary>
        private static void EmitTurnFlash(Vector2 center) {
            if (Main.dedServ) return;
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(center,
                    Main.rand.NextVector2CircularEdge(2.4f, 2.4f), TurnRed, 0.75f)
                    ?.Configure(false, 12);
            }
            PRTLoader.NewParticle<PRT_Spark>(center, Vector2.Zero, Color.White, 1.1f)
                ?.Configure(false, 8);
        }
    }
}
