using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>
    /// 藤蔓格栅梁：架在两只钩爪之间的可破坏藤墙。
    /// ai[0]/ai[1]=两端钩爪whoAmI ai[2]=生命比(1→0，-1=强制凋萎)；
    /// 玩家弹幕打梁掉血，打断开缺口
    /// </summary>
    internal class PlanteraVineLattice : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int GrowTime = 36;
        internal const int ActiveTime = 330;
        internal const int WitherTime = 26;
        internal const int TotalLife = GrowTime + ActiveTime + WitherTime;

        private const float HitWidth = 30f;

        private NPC HookA => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private NPC HookB => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;
        private float Age => TotalLife - Projectile.timeLeft;
        private bool Withering => Projectile.ai[2] <= -0.5f || Projectile.timeLeft <= WitherTime;

        /// <summary>服务端伤害吸收记账：弹幕whoAmI→冷却帧</summary>
        private readonly Dictionary<int, int> hitCooldown = [];
        private int serverHP;
        private bool hardened;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>服务端生成一根梁</summary>
        internal static void Spawn(NPC boss, NPC hookA, NPC hookB, int damage) {
            if (VaultUtils.isClient) {
                return;
            }
            int id = Projectile.NewProjectile(boss.GetSource_FromAI(),
                (hookA.Center + hookB.Center) * 0.5f, Vector2.Zero,
                ModContent.ProjectileType<PlanteraVineLattice>(), damage, 0f, Main.myPlayer,
                hookA.whoAmI, hookB.whoAmI, 1f);
            if (id >= 0 && id < Main.maxProjectiles) {
                Main.projectile[id].netUpdate = true;
            }
        }

        public override void AI() {
            NPC a = HookA;
            NPC b = HookB;

            //锚点失效或主控离开格栅态→凋萎
            NPC boss = PlanteraAI.FindBoss();
            bool anchorsValid = a.Alives() && a.type == NPCID.PlanterasHook
                && b.Alives() && b.type == NPCID.PlanterasHook;
            bool bossValid = boss != null
                && (PlanteraAI.GetStateIndex(boss) == PlanteraStateIndex.VineLattice || Age < GrowTime + 30);

            if (!VaultUtils.isClient && !Withering && (!anchorsValid || !bossValid)) {
                Projectile.ai[2] = -1f;
                Projectile.netUpdate = true;
            }

            if (!anchorsValid) {
                //锚都没了直接快收
                if (Projectile.timeLeft > WitherTime) {
                    Projectile.timeLeft = WitherTime;
                }
                return;
            }

            Projectile.Center = (a.Center + b.Center) * 0.5f;

            //初始化服务端耐久
            if (serverHP == 0) {
                serverHP = PlanteraDirector.LatticeBeamHP;
            }

            //凋萎触发：立即进入尾段
            if (Withering && Projectile.timeLeft > WitherTime) {
                Projectile.timeLeft = WitherTime;
            }

            //硬化拍：成型瞬间绷直+闪光+咔声
            if (!hardened && Age >= GrowTime) {
                hardened = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.2f, Volume = 0.75f, MaxInstances = 4 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.4f, Volume = 0.9f, MaxInstances = 4 }, Projectile.Center);
                    PlanteraRenderHelper.SpawnSporePuff(Projectile.Center, 0.7f);
                }
            }

            //服务端：吸收玩家弹幕伤害
            if (!VaultUtils.isClient && hardened && !Withering) {
                AbsorbPlayerDamage(a.Center, b.Center);
            }

            //梁上零星荧光尘
            if (!VaultUtils.isServer && hardened && !Withering && Main.rand.NextBool(10)) {
                float t = Main.rand.NextFloat();
                Vector2 pos = Vector2.Lerp(a.Center, b.Center, t);
                InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(pos,
                    new Vector2(0f, -0.4f), PlanteraRenderHelper.SporeGreen * 0.7f, 0.7f)?.SetLife(30);
            }

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.SporeGreen.ToVector3() * 0.35f);
        }

        /// <summary>玩家弹幕磨梁：每发12帧记一次伤，耐久尽则断裂</summary>
        private void AbsorbPlayerDamage(Vector2 endA, Vector2 endB) {
            //冷却递减
            if (hitCooldown.Count > 0) {
                List<int> keys = [.. hitCooldown.Keys];
                foreach (int key in keys) {
                    int remain = hitCooldown[key] - 1;
                    if (remain <= 0) {
                        hitCooldown.Remove(key);
                    }
                    else {
                        hitCooldown[key] = remain;
                    }
                }
            }

            foreach (var proj in Main.ActiveProjectiles) {
                if (!proj.friendly || proj.damage <= 0 || hitCooldown.ContainsKey(proj.whoAmI)) {
                    continue;
                }
                float unused = 0f;
                bool hit = Collision.CheckAABBvLineCollision(proj.Hitbox.TopLeft(), proj.Hitbox.Size(),
                    endA, endB, HitWidth + 8f, ref unused);
                if (!hit) {
                    continue;
                }
                hitCooldown[proj.whoAmI] = 12;
                serverHP -= proj.damage;

                //同步生命比给各端(粗粒度)
                float frac = MathHelper.Clamp(serverHP / (float)PlanteraDirector.LatticeBeamHP, 0f, 1f);
                if (Math.Abs(Projectile.ai[2] - frac) > 0.08f || serverHP <= 0) {
                    Projectile.ai[2] = frac;
                    Projectile.netUpdate = true;
                }

                if (serverHP <= 0) {
                    BreakBeam();
                    return;
                }
            }
        }

        /// <summary>梁被打断：缺口打开</summary>
        private void BreakBeam() {
            Projectile.ai[2] = -1f;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 10);
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            NPC a = HookA;
            NPC b = HookB;
            Vector2 center = a.Alives() && b.Alives() ? (a.Center + b.Center) * 0.5f : Projectile.Center;
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.2f, Volume = 1f, MaxInstances = 3 }, center);
            PlanteraRenderHelper.SpawnPetalBurst(center, 8, 6f, false);
            PlanteraRenderHelper.SpawnSporePuff(center, 1f);
        }

        /// <summary>生长期与凋萎期无伤(公平阀)</summary>
        public override bool? CanDamage() {
            if (Age < GrowTime + 6 || Withering) {
                return false;
            }
            return null;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            NPC a = HookA;
            NPC b = HookB;
            if (!a.Alives() || !b.Alives()) {
                return false;
            }
            float unused = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                a.Center, b.Center, HitWidth, ref unused);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC a = HookA;
            NPC b = HookB;
            if (!a.Alives() || !b.Alives()) {
                return false;
            }

            float grow = MathHelper.Clamp(Age / (float)GrowTime, 0f, 1f);
            float wither = Projectile.timeLeft <= WitherTime
                ? Projectile.timeLeft / (float)WitherTime : 1f;
            float hpFrac = MathHelper.Clamp(Projectile.ai[2], 0f, 1f);

            NPC boss = PlanteraAI.FindBoss();
            bool phase2 = boss != null && boss.ai[3] > 0.5f;

            VineParams vine = VineParams.Default;
            vine.RestLength = Vector2.Distance(a.Center, b.Center) * 1.01f;
            vine.HalfWidth = 13f;
            vine.Taut = hardened ? 0.85f : 0.25f;
            //受创的梁行波变弱变暗
            vine.Pulse = hardened ? 0.35f * hpFrac + 0.1f : 0f;
            vine.PulseDir = 1f;
            vine.Grow = grow;
            vine.Fade = wither * MathHelper.Lerp(0.55f, 1f, hpFrac);
            vine.Phase2 = phase2;
            vine.Seed = 0.11f + Projectile.whoAmI * 0.037f % 0.8f;

            PlanteraVineRenderer.DrawVine(Main.spriteBatch, a.Center, b.Center, vine);
            return false;
        }
    }
}
