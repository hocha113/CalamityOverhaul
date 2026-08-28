using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·星旋套】「破隐撕裂」：星旋裂口（被撕开的青绿等离子空间创面）。
    /// ①进入星旋隐身即蓄好破隐一击，青绿游丝向身侧汇聚；②隐身中或现身一瞬半秒内的
    /// 远程命中撕开裂口，裂口拖曳周围敌人并反复放电；③裂口塌缩内爆，虚空尘驻留。<br/>
    /// 与原版套装技联动而非覆盖：原版隐身（增伤/仇恨/减速）照常运作，神赋只在
    /// 隐身状态机上挂一个「破隐」蓄势层；蓄势与消耗都在攻击方端本地，裂口 owner 侧生成
    /// </summary>
    internal class GsVortexArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.VortexHelmet];

        public override int BodyID => ItemID.VortexBreastplate;

        public override int LegsID => ItemID.VortexLeggings;

        protected override string EndowLineFallback =>
            "Breach Rend: entering vortex stealth primes a breach shot; a ranged hit from stealth, or right after leaving it, tears open a rift that drags enemies in";

        //星旋色板
        internal static readonly Color RiftBright = new(198, 255, 240);
        internal static readonly Color RiftMain = new(0, 216, 178);
        internal static readonly Color RiftDeep = new(8, 44, 42);

        /// <summary>现身后仍可破隐的宽限帧数</summary>
        private const int BreachGraceFrames = 90;

        /// <summary>持续隐身多久后自动重新蓄势</summary>
        private const int RePrimeFrames = 480;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            bool stealth = player.vortexStealthActive;
            bool prevStealth = state.EndowCharge == 1;

            //入隐沿：蓄好破隐一击
            if (stealth && !prevStealth) {
                Prime(player, state);
            }
            //出隐沿：记下现身时刻，宽限期内仍可破隐
            if (!stealth && prevStealth) {
                state.EndowTimer = Main.GameUpdateCount;
            }
            //久蛰复蓄：隐身中被消耗后，静候片刻自动重新蓄势
            if (stealth && !state.EndowFlag && Main.GameUpdateCount - state.EndowTimer > RePrimeFrames) {
                Prime(player, state);
            }
            state.EndowCharge = stealth ? 1 : 0;

            //蓄势驻场：隐身身影边缘的青绿游丝
            if (VaultUtils.isServer || !state.EndowFlag || !stealth) {
                return;
            }
            if (Main.rand.NextBool(8)) {
                Vector2 edge = player.Center + Main.rand.NextVector2CircularEdge(20f, 28f);
                PRTLoader.NewParticle<PRT_Spark>(edge,
                    (player.Center - edge).SafeNormalize(Vector2.Zero) * 0.8f,
                    RiftMain, Main.rand.NextFloat(0.22f, 0.34f))?.Configure(false, 14);
            }
        }

        private static void Prime(Player player, GodSmithArmorPlayer state) {
            state.EndowFlag = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.4f, Pitch = 0.5f }, player.Center);
                for (int i = 0; i < 6; i++) {
                    float ang = MathHelper.TwoPi * i / 6f;
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + ang.ToRotationVector2() * 26f,
                        -ang.ToRotationVector2() * 1.6f, RiftBright, 0.32f)?.Configure(false, 13);
                }
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //裂口自身放电不再触发，防自循环；假人不算数
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsVortexRiftProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy || !state.EndowFlag) {
                return;
            }
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged)) {
                return;
            }
            //破隐窗口：隐身中，或现身一瞬的宽限期内
            bool inWindow = player.vortexStealthActive
                || Main.GameUpdateCount - state.EndowTimer <= BreachGraceFrames;
            if (!inWindow) {
                return;
            }

            state.EndowFlag = false;
            state.EndowTimer = Main.GameUpdateCount;//复蓄计时从消耗时刻起算
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.75f, Pitch = -0.3f }, target.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                //裂口逐跳伤害按触发伤害折算并封顶；一次隐身循环一击，收益在神赋包络内
                int riftDamage = Math.Clamp((int)(damageDone * 0.30f), 12, 350);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithVortexEndow"),
                    target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsVortexRiftProj>(), riftDamage, 1f, player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 星旋裂口：一道被撕开的空间创面，不是光球。缓缓漂向最近的敌群，
    /// 拖曳周围敌人（免击退者不受拽）并反复放电；生命末段塌缩内爆，
    /// 虚空尘向心收拢后驻留。裂缝亮芯双缝旋转，全程转速驱动的旋涂抹层
    /// </summary>
    internal class GsVortexRiftProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>拖曳半径</summary>
        private const float PullRadius = 190f;

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9127f % 3.19f;

        /// <summary>开口-稳定-塌缩的口径生命周期</summary>
        private float Aperture {
            get {
                float open = MathHelper.Clamp(Life / 10f, 0f, 1f);
                float collapse = 1f - MathHelper.Clamp((Life - 72f) / 18f, 0f, 1f);
                return open * collapse;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
        }

        public override void AI() {
            Life++;
            Projectile.rotation += 0.11f + Aperture * 0.05f;

            //漂移：缓缓挪向最近的可追目标（非匀速，越近越慢）
            NPC drift = FindNearest(420f);
            if (drift != null) {
                Vector2 want = (drift.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 2.2f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.04f);
            }
            else {
                Projectile.velocity *= 0.95f;
            }

            //拖曳：周围敌人被拽向裂口，免击退者不受拽（各端同跑，服务端权威）
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f) {
                    continue;
                }
                float dist = npc.Center.Distance(Projectile.Center);
                if (dist > PullRadius || dist < 12f) {
                    continue;
                }
                Vector2 pullDir = (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero);
                npc.velocity = Vector2.Lerp(npc.velocity, pullDir * 5.5f, 0.07f * npc.knockBackResist * Aperture);
            }

            //驻场相：裂缘游丝内旋 + 偶发放电噼啪
            if (!Main.dedServ) {
                Lighting.AddLight(Projectile.Center, GsVortexArmor.RiftMain.ToVector3() * (0.4f * Aperture));
                if (Life % 3 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 rim = Projectile.Center + ang.ToRotationVector2() * (34f * Aperture);
                    PRTLoader.NewParticle<PRT_Spark>(rim,
                        (Projectile.Center - rim).SafeNormalize(Vector2.Zero).RotatedBy(0.9f) * 2.2f,
                        Main.rand.NextBool(3) ? GsVortexArmor.RiftBright : GsVortexArmor.RiftMain,
                        Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(10, 16));
                }
                if (Life % 17 == 0) {
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.28f, Pitch = 0.35f, MaxInstances = 2 }, Projectile.Center);
                }
            }
        }

        private NPC FindNearest(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //塌缩内爆：虚空尘向心收拢，随后一记闷响的白青闪，余尘驻留
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsVortexArmor.RiftBright, 0.16f)?.Configure(10, 0.8f);
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f;
                Vector2 from = Projectile.Center + ang.ToRotationVector2() * 40f;
                PRTLoader.NewParticle<PRT_Spark>(from, -ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f),
                    Main.rand.NextBool() ? GsVortexArmor.RiftMain : GsVortexArmor.RiftDeep,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(false, Main.rand.Next(14, 26));
            }
        }

        //==================== 绘制：暗渊压边 + 等离子旋层 + 双缝亮芯，旋速驱动涂抹 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float ap = Aperture;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            //旋转涂抹：旋速换算成层间错相
            float smear = 0.22f;
            float wob = MathF.Sin(Life * 0.5f + Seed * 5f) * 0.07f;

            //暗渊压边（真 alpha 贴图压出裂口外檐的暗）
            Main.EntitySpriteDraw(tex, pos, null, GsVortexArmor.RiftDeep * (0.9f * ap), Projectile.rotation, origin,
                new Vector2(0.56f + wob, 0.50f - wob) * ap, SpriteEffects.None, 0);
            //等离子旋层：双层反向错相，转出旋涡感
            Main.EntitySpriteDraw(tex, pos, null, GsVortexArmor.RiftMain * (0.8f * ap), Projectile.rotation + smear, origin,
                new Vector2(0.44f, 0.34f + wob) * ap, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, GsVortexArmor.RiftMain * (0.5f * ap), -Projectile.rotation * 0.8f - smear, origin,
                new Vector2(0.38f + wob, 0.28f) * ap, SpriteEffects.None, 0);
            //白青亮芯：加色双缝，随裂口同旋
            Color core = GsVortexArmor.RiftBright with { A = 0 };
            Main.EntitySpriteDraw(tex, pos, null, core * (0.7f * ap), Projectile.rotation * 1.4f, origin,
                new Vector2(0.40f, 0.06f) * ap, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, core * (0.55f * ap), Projectile.rotation * 1.4f + MathHelper.PiOver2, origin,
                new Vector2(0.30f, 0.05f) * ap, SpriteEffects.None, 0);
            return false;
        }
    }
}
