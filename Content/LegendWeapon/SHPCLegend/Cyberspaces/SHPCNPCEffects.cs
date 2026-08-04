using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>SHPC 命中附加效果，数据侵蚀/时相减速等</summary>
    internal class SHPCNPCEffects : GlobalNPC
    {
        private const int MaxChronalSlowTime = 120;

        public override bool InstancePerEntity => true;

        /// <summary>数据侵蚀剩余帧数</summary>
        public int DataErosionTime;
        /// <summary>每次 tick 伤害量</summary>
        public int DataErosionTickDmg;
        /// <summary>时相减速剩余帧数</summary>
        public int ChronalSlowTime;
        /// <summary>黑曜石裂纹剩余帧数与层数</summary>
        public int ObsidianCrackTime;
        public int ObsidianCrackStacks;
        public int ObsidianCrackOwner = Main.maxPlayers;
        public int ObsidianCrackDamage;
        /// <summary>生命芽寄生剩余帧数与 tick 伤害</summary>
        public int LifebloomTime;
        public int LifebloomTickDmg;
        public int LifebloomOwner = Main.maxPlayers;
        /// <summary>湿苔缠绕剩余帧数与层数</summary>
        public int MossTime;
        public int MossStacks;
        /// <summary>蜂巢信息素标记</summary>
        public int PheromoneTime;
        public int PheromoneOwner = Main.maxPlayers;

        private static bool _shaderActive;
        private ulong _lastChronalTick = ulong.MaxValue;
        private bool _chronalScaleApplied;

        /// <summary>施加数据侵蚀效果，新时长仅在大于当前剩余时才刷新</summary>
        public void ApplyDataErosion(int duration, int tickDmg) {
            DataErosionTime = Math.Max(DataErosionTime, duration);
            DataErosionTickDmg = Math.Max(DataErosionTickDmg, tickDmg);
        }

        /// <summary>服务端施加时相减速</summary>
        public void ApplyChronalSlow(NPC npc, int duration) {
            if (Main.netMode == NetmodeID.MultiplayerClient || npc?.active != true
                || npc.boss || duration <= 0) {
                return;
            }

            int newTime = Math.Max(ChronalSlowTime,
                Math.Clamp(duration, 1, MaxChronalSlowTime));
            if (newTime == ChronalSlowTime) {
                return;
            }

            ChronalSlowTime = newTime;
            ApplyChronalScale(npc);
            if (Main.netMode == NetmodeID.Server) {
                npc.netUpdate = true;
            }
        }

        public override void SetDefaults(NPC npc) {
            ChronalSlowTime = 0;
            _lastChronalTick = ulong.MaxValue;
            _chronalScaleApplied = false;
        }

        public override void ResetEffects(NPC npc) {
            ulong update = Main.GameUpdateCount;
            if (_lastChronalTick == update) {
                return;
            }
            _lastChronalTick = update;

            if (ChronalSlowTime <= 0 || npc.boss) {
                bool notify = ChronalSlowTime > 0 && npc.boss;
                ClearChronalSlow(npc, notify);
                return;
            }

            if (!_chronalScaleApplied) {
                ApplyChronalScale(npc);
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                ChronalSlowTime--;
                if (ChronalSlowTime == 0 && Main.netMode == NetmodeID.Server) {
                    npc.netUpdate = true;
                }
            }

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                Vector2 pos = npc.Center
                    + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f);
                Vector2 vel = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(pos, vel,
                    new Color(120, 80, 255), Main.rand.NextFloat(0.5f, 1.2f))
                    .Configure(new Color(60, 30, 180), Main.rand.Next(10, 20));
            }
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter,
            BinaryWriter binaryWriter) {
            bool active = ChronalSlowTime > 0 && !npc.boss;
            bitWriter.WriteBit(active);
            if (active) {
                binaryWriter.Write((byte)Math.Clamp((int)ChronalSlowTime, 1,
                    MaxChronalSlowTime));
            }
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader,
            BinaryReader binaryReader) {
            ChronalSlowTime = bitReader.ReadBit()
                ? Math.Clamp((int)binaryReader.ReadByte(), 1, MaxChronalSlowTime)
                : 0;
            _lastChronalTick = Main.GameUpdateCount;
            if (ChronalSlowTime > 0 && !npc.boss) {
                ApplyChronalScale(npc);
            }
            else {
                ClearChronalSlow(npc, false);
            }
        }

        public void ApplyObsidianCrack(NPC npc, int duration, int owner, int damage) {
            ObsidianCrackTime = Math.Max(ObsidianCrackTime, duration);
            ObsidianCrackOwner = owner;
            ObsidianCrackDamage = Math.Max(ObsidianCrackDamage, damage);
            ObsidianCrackStacks++;
            if (ObsidianCrackStacks >= 3) {
                BurstObsidian(npc, ObsidianCrackOwner, ObsidianCrackDamage);
                ObsidianCrackStacks = 0;
                ObsidianCrackTime = 0;
                ObsidianCrackDamage = 0;
            }
        }

        public void ApplyLifebloom(int duration, int tickDmg, int owner) {
            LifebloomTime = Math.Max(LifebloomTime, duration);
            LifebloomTickDmg = Math.Max(LifebloomTickDmg, tickDmg);
            LifebloomOwner = owner;
        }

        public void ApplyMoss(int duration, int stacks) {
            MossTime = Math.Max(MossTime, duration);
            MossStacks = Math.Min(MossStacks + stacks, 5);
        }

        public void ApplyPheromone(int duration, int owner) {
            PheromoneTime = Math.Max(PheromoneTime, duration);
            PheromoneOwner = owner;
        }

        public static void BurstObsidian(NPC npc, int owner, int damage) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (owner < 0 || owner >= Main.maxPlayers) return;
            int shardDamage = Math.Max(damage, 1);
            for (int i = 0; i < 4; i++) {
                NPC target = npc.Center.FindClosestNPC(620f, false, true, new List<NPC> { npc });
                float angle = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.25f, 0.25f);
                Vector2 dir = target != null
                    ? (target.Center - npc.Center).SafeNormalize(angle.ToRotationVector2())
                    : angle.ToRotationVector2();
                Projectile.NewProjectile(npc.GetSource_FromThis(),
                    npc.Center, dir * Main.rand.NextFloat(9f, 13f),
                    ModContent.ProjectileType<SHPCObsidianShardProj>(),
                    shardDamage, 0f, owner);
            }
            //中央冲击，Detonation 半径110 ai0=0.4
            int centerDmg = Math.Max(damage * 2, 1);
            int idx = Projectile.NewProjectile(npc.GetSource_FromThis(),
                npc.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                centerDmg, 0f, owner, ai0: 0.4f);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = 110f;
            }
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(npc.Center, vel, new Color(60, 35, 95), Main.rand.NextFloat(0.7f, 1.5f)).Configure(new Color(255, 80, 35), Main.rand.Next(16, 30));
                }
                //玻璃环双层，紫快橙慢
                PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero, new Color(150, 80, 220, 0), 0.05f).Configure(0.05f, 0.55f, 22);
                PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero, new Color(255, 110, 50, 0), 0.05f).Configure(0.05f, 0.4f, 28);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.55f, Pitch = 0.2f }, npc.Center);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = -0.3f }, npc.Center);
            }
            CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel.SHPCNaturalFx.Shake(5f);
        }

        public static List<NPC> CollectPheromoneTargets(int owner, Vector2 center, float range, int maxCount) {
            List<NPC> targets = [];
            float rangeSq = range * range;
            for (int i = 0; i < Main.maxNPCs && targets.Count < maxCount; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (Vector2.DistanceSquared(npc.Center, center) > rangeSq) continue;
                if (!npc.TryGetGlobalNPC(out SHPCNPCEffects eff)) continue;
                if (eff.PheromoneTime <= 0 || eff.PheromoneOwner != owner) continue;
                targets.Add(npc);
            }
            return targets;
        }

        public override bool PreAI(NPC npc) {
            if (DataErosionTime > 0) {
                DataErosionTime--;
                int elapsed = (int)(Main.GameUpdateCount);
                if (elapsed % 30 == 0 && DataErosionTickDmg > 0) {
                    npc.SimpleStrikeNPC(DataErosionTickDmg, 0, false, 0f, null, false, 0f, true);
                }
            }
            else {
                DataErosionTickDmg = 0;
            }

            if (ObsidianCrackTime > 0) {
                ObsidianCrackTime--;
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                    Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos, Main.rand.NextVector2Circular(1.2f, 1.2f), new Color(70, 45, 110), Main.rand.NextFloat(0.35f, 0.9f)).Configure(new Color(255, 90, 40), Main.rand.Next(8, 18));
                }
            }
            else {
                ObsidianCrackStacks = 0;
                ObsidianCrackDamage = 0;
            }

            if (LifebloomTime > 0) {
                LifebloomTime--;
                if ((int)Main.GameUpdateCount % 45 == 0 && LifebloomTickDmg > 0) {
                    npc.SimpleStrikeNPC(LifebloomTickDmg, 0, false, 0f, null, false, 0f, true);
                    TryHealLifebloomOwner(npc, Math.Max(1, LifebloomTickDmg / 4));
                }
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CyberSquare>(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f), new Vector2(0f, Main.rand.NextFloat(-1.8f, -0.4f)), new Color(90, 255, 130), Main.rand.NextFloat(0.4f, 0.9f)).Configure(new Color(30, 140, 55), Main.rand.Next(12, 24));
                }
            }
            else {
                LifebloomTickDmg = 0;
            }

            if (MossTime > 0) {
                MossTime--;
                if (!npc.boss) {
                    npc.velocity *= MossStacks >= 4 ? 0.82f : 0.94f;
                }
            }
            else {
                MossStacks = 0;
            }

            if (PheromoneTime > 0) {
                PheromoneTime--;
            }
            return true;
        }

        public override void OnKill(NPC npc) {
            ClearChronalSlow(npc, false);
            if (LifebloomTime <= 0 || LifebloomOwner < 0 || LifebloomOwner >= Main.maxPlayers) return;
            NPC target = npc.Center.FindClosestNPC(520f, false, true, new List<NPC> { npc });
            if (target == null || !target.TryGetGlobalNPC(out SHPCNPCEffects eff)) return;
            eff.ApplyLifebloom(Math.Max(LifebloomTime / 2, 90), Math.Max(LifebloomTickDmg, 1), LifebloomOwner);
        }

        private void ApplyChronalScale(NPC npc) {
            TimeFreezeSystem.SetNPCTimeScale<ChronalGripModule>(npc, 0.5f);
            _chronalScaleApplied = true;
        }

        private void ClearChronalSlow(NPC npc, bool notify) {
            ChronalSlowTime = 0;
            if (_chronalScaleApplied) {
                TimeFreezeSystem.ClearNPCTimeScale<ChronalGripModule>(npc);
                _chronalScaleApplied = false;
            }
            if (notify && Main.netMode == NetmodeID.Server) {
                npc.netUpdate = true;
            }
        }

        private void TryHealLifebloomOwner(NPC npc, int amount) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (LifebloomOwner < 0 || LifebloomOwner >= Main.maxPlayers) return;
            Player player = Main.player[LifebloomOwner];
            if (player == null || !player.active || player.dead) return;
            if (Vector2.DistanceSquared(player.Center, npc.Center) > 900f * 900f) return;
            if (player.statLife >= player.statLifeMax2) return;
            player.statLife = Math.Min(player.statLife + amount, player.statLifeMax2);
            if (Main.netMode == NetmodeID.SinglePlayer) {
                player.HealEffect(amount);
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (DataErosionTime <= 0) return true;

            Effect shader = HackEffectAssets.HackContagion;
            if (shader == null) return true;

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            //progress 0→1 随侵蚀剩余，saturate
            float totalTime = 240f;
            float progress = Math.Clamp(1f - DataErosionTime / totalTime, 0f, 1f);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(progress);
            shader.Parameters["intensity"]?.SetValue(1f);
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!_shaderActive) return;
            _shaderActive = false;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
