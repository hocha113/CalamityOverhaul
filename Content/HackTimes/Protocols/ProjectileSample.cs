using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 弹幕采样：采下一发敌弹的型号，二十秒内你每十二次开火复刻它一发。<br/>
    /// 采样槽是 owner 端自治状态：存在施术者本机的 ModPlayer 里、本机消费、不进网络
    /// 复刻弹由 owner 端 NewProjectile 生成再自然同步，这是弹幕生成唯一的正路
    /// </summary>
    internal class ProjectileSample : QuickHackDef
    {
        /// <summary>采样槽寿命（帧）</summary>
        internal const int SampleDuration = 60 * 20;
        /// <summary>每几次开火复刻一发</summary>
        internal const int ShotsPerEcho = 12;

        private static readonly Color Sample = new(120, 255, 190);

        //白名单：只收 aiStyle 自持的类型，0 直线弹、1 箭矢、2 重抛体、
        //8 弹跳法弹、18 镰刀。这些风格不引用父 NPC 的 ai[]，脱离原主也能正常跑；
        //Calamity 大量 aiStyle -1 的 Boss 弹依赖父体状态，收进来会 NRE 或行为失控
        private static readonly HashSet<int> SelfContainedAiStyles = [0, 1, 2, 8, 18];

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 5;
            Category = QuickHackCategory.Contagion;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => SampleDuration;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            if (!projectile.hostile || projectile.damage <= 0) return false;
            //白名单外一律拒绝（锁定框上的"不可采样"提示是面板侧的整合待办）
            return SelfContainedAiStyles.Contains(projectile.aiStyle);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            //单人时权威端就是施术者本机，直接落账；
            //联机时服务端不写任何玩家状态，靠效果广播让施术者本机在
            //OnReplicatedApply 里自己落账
            if (Main.netMode == NetmodeID.SinglePlayer) {
                caster.GetModPlayer<ProjectileSamplePlayer>()
                    .Acquire(projectile.type, SampleDuration);
            }
            if (Main.netMode != NetmodeID.Server) EmitSampled(projectile.Center);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            Player caster = HackEffectTracker.ResolveEffectCaster(this, target);
            if (caster != null && caster.whoAmI == Main.myPlayer) {
                caster.GetModPlayer<ProjectileSamplePlayer>()
                    .Acquire(projectile.type, SampleDuration - Math.Max(0, elapsed));
            }
            EmitSampled(projectile.Center);
        }

        //挂在目标弹上的效果只是采样的载具：敌弹几秒内就会自然消失并带走这个效果，
        //采样槽的寿命由 ModPlayer 自己数满二十秒，所以 OnTick/OnRemove 都只管表现
        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server && elapsed % 10 == 0
                && HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitTag(projectile);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (elapsed % 10 == 0
                && HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitTag(projectile);
            }
        }

        //取样框标记：目标弹四角各一粒括号光点
        private static void EmitTag(Projectile projectile) {
            Vector2 half = new(projectile.width * 0.6f + 8f,
                projectile.height * 0.6f + 8f);
            for (int i = 0; i < 4; i++) {
                Vector2 corner = new(i % 2 == 0 ? -half.X : half.X,
                    i < 2 ? -half.Y : half.Y);
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center + corner,
                    Vector2.Zero, Sample, 0.5f)?.Configure(false, 8);
            }
        }

        private static void EmitSampled(Vector2 center) {
            for (int i = 0; i < 12; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(24f, 24f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset, -offset * 0.12f,
                    Sample, 0.9f)?.Configure(false, 16);
            }
        }
    }

    /// <summary>
    /// 施术者本机的采样槽。非持久（退档即弃），倒计时自管：
    /// 挂在敌弹上的效果早在弹幕消失时就没了，这里才是二十秒的真时长
    /// </summary>
    internal sealed class ProjectileSamplePlayer : ModPlayer
    {
        /// <summary>已采样的弹幕类型，0 为空槽</summary>
        internal int SampledType;
        /// <summary>剩余帧数</summary>
        internal int FramesLeft;
        /// <summary>自上次复刻起的开火计数</summary>
        internal int ShotCounter;

        internal bool SampleActive
            => SampledType > ProjectileID.None && FramesLeft > 0;

        public override void Initialize() => Clear();

        public override void PlayerDisconnect() => Clear();

        private void Clear() {
            SampledType = 0;
            FramesLeft = 0;
            ShotCounter = 0;
        }

        internal void Acquire(int type, int framesLeft) {
            if (type <= ProjectileID.None
                || type >= ProjectileLoader.ProjectileCount) {
                return;
            }
            SampledType = type;
            FramesLeft = Math.Max(1, framesLeft);
            ShotCounter = 0;
            if (Player.whoAmI == Main.myPlayer && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.ResearchComplete with { Volume = 0.6f },
                    Player.Center);
            }
        }

        //死亡帧 PostUpdate 不跑，倒计时靠 UpdateDead 补齐
        public override void PostUpdate() => TickDown();

        public override void UpdateDead() => TickDown();

        private void TickDown() {
            if (FramesLeft > 0 && --FramesLeft <= 0) {
                Clear();
            }
        }
    }

    /// <summary>开火计数与复刻，只在本机 owner 端做</summary>
    internal sealed class ProjectileSampleShots : GlobalItem
    {
        public override bool Shoot(Item item, Player player,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //Shoot 本就只在 owner 端跑到生成分支，这里再压一道闸自证语义
            if (player.whoAmI == Main.myPlayer) {
                TryEchoSample(player, position, velocity, damage, knockback);
            }
            return true;
        }

        private static void TryEchoSample(Player player, Vector2 position,
            Vector2 velocity, int damage, float knockback) {
            var sampler = player.GetModPlayer<ProjectileSamplePlayer>();
            if (!sampler.SampleActive) return;
            if (++sampler.ShotCounter < ProjectileSample.ShotsPerEcho) return;
            sampler.ShotCounter = 0;

            int sampled = sampler.SampledType;
            if (sampled <= ProjectileID.None
                || sampled >= ProjectileLoader.ProjectileCount) {
                return;
            }
            //复刻弹伤害取这次射击的伤害而不是原弹的（Boss 弹数值不进玩家手里），
            //穿透压到 1；转阵营在 OnSpawn 里做完，owner 端 NewProjectile
            //内部发出的生成包里就带着转换结果，各端第一帧即一致
            var conversion = new HackConversionSource(player.whoAmI, capPenetrate: true);
            int index = Projectile.NewProjectile(conversion, position, velocity,
                sampled, Math.Max(1, damage), knockback, player.whoAmI);
            if (index >= 0 && index < Main.maxProjectiles
                && Main.netMode != NetmodeID.Server) {
                HackConvertedProjectile.EmitConvertFlash(
                    Main.projectile[index].Center);
            }
        }
    }
}
