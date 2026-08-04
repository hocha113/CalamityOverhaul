using CalamityOverhaul.Content.HackTimes.Protocols;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇入效果 NPC AI 钩子</summary>
    internal class HackEffectNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private readonly List<ActiveHackEffect> effectsCache = [];
        //赛博精神病接触伤冷却
        private int _cyberDamageCooldown;

        public bool? PreAIByOverNPC(NPC npc) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return null;
            //时停不干预
            if (TimeFreezes.WorldFreezeSystem.IsActive) return null;
            HackEffectTracker.GetEffects(npc.whoAmI, effectsCache);
            if (effectsCache.Count == 0) return null;

            bool? allowAI = true;
            for (int i = 0; i < effectsCache.Count; i++) {
                var eff = effectsCache[i];
                switch (eff.Hack) {
                    case Cyberpsychosis://重定向
                        RedirectAI(npc, eff, ref _cyberDamageCooldown);
                        allowAI = false;
                        break;
                    case OpticOverload://游荡
                        BlindWander(npc);
                        allowAI = false;
                        break;
                    case MemoryWipe://停追击
                        WipeAggro(npc);
                        allowAI = false;
                        break;
                }
            }

            if (allowAI.HasValue) {
                return allowAI.Value;
            }

            return null;
        }

        public override bool PreAI(NPC npc) {
            //优先级不够，走 PreAIByOverNPC
            return true;
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            //赛博精神病不伤玩家
            if (HackEffectTracker.HasEffect<Cyberpsychosis>(npc.whoAmI)) {
                modifiers.FinalDamage *= 0f;
            }
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            //记忆清除受击恢复
            if (HackEffectTracker.HasEffect<MemoryWipe>(npc.whoAmI)) {
                var eff = HackEffectTracker.GetEffect<MemoryWipe>(npc.whoAmI);
                if (eff != null) {
                    HackEffectTracker.RemoveAuthorityEffect(eff.ActivationId);
                }
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (HackEffectTracker.HasEffect<MemoryWipe>(npc.whoAmI)) {
                var eff = HackEffectTracker.GetEffect<MemoryWipe>(npc.whoAmI);
                if (eff != null) {
                    HackEffectTracker.RemoveAuthorityEffect(eff.ActivationId);
                }
            }
        }

        //赛博精神病重定向
        private static void RedirectAI(NPC npc, ActiveHackEffect eff, ref int damageCooldown) {
            if (damageCooldown > 0) damageCooldown--;
            float closestDist = float.MaxValue;
            NPC closestNPC = null;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active || other.whoAmI == npc.whoAmI || other.friendly || other.dontTakeDamage)
                    continue;
                //跳过已感染
                if (HackEffectTracker.HasEffect<Cyberpsychosis>(other.whoAmI))
                    continue;
                float dist = Vector2.DistanceSquared(npc.Center, other.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closestNPC = other;
                }
            }

            if (closestNPC != null) {
                Vector2 dir = closestNPC.Center - npc.Center;
                float dist = dir.Length();
                if (dist > 0) dir /= dist;
                float speed = Math.Max(npc.velocity.Length(), 3f);
                npc.velocity = Vector2.Lerp(npc.velocity, dir * speed, 0.08f);
                npc.direction = closestNPC.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                //接触伤，60 帧冷却
                if (dist < (npc.width + closestNPC.width) * 0.6f && damageCooldown <= 0) {
                    int dmg = Math.Max(npc.damage / 2, 10);
                    NPC.HitInfo hitInfo = new() {
                        Damage = dmg,
                        Knockback = 2f,
                        HitDirection = npc.direction,
                    };
                    closestNPC.StrikeNPC(hitInfo);
                    damageCooldown = 60;
                }
            }
            else {
                npc.velocity *= 0.96f;
            }
        }

        //视觉过载游荡
        private static void BlindWander(NPC npc) {
            if (Main.GameUpdateCount % 30 == 0) {
                npc.velocity = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }
            npc.velocity *= 0.98f;
        }

        //记忆清除减速，受击 OnHit 结束
        private static void WipeAggro(NPC npc) {
            npc.velocity *= 0.9f;
            if (npc.velocity.Length() < 0.1f)
                npc.velocity = Vector2.Zero;
        }
    }

    /// <summary>骇入效果 NPC 着色器绘制</summary>
    internal class HackEffectNPCDraw : GlobalNPC
    {
        private static bool _shaderActive;
        private static QuickHackDef _activeShaderHack;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            var effects = HackEffectTracker.AllActiveEffects;
            QuickHackDef bestHack = null;
            float bestProgress = 0f;

            for (int i = 0; i < effects.Count; i++) {
                var eff = effects[i];
                if (!eff.Active || eff.TargetIndex != npc.whoAmI) continue;
                //即时效果不持续画
                if (eff.Hack.GetDuration() == 0) continue;

                float progress = 0f;
                int dur = (int)(eff.Hack.GetDuration() * eff.EffectMult);
                if (dur > 0) progress = (float)eff.Elapsed / dur;

                //取最新施加
                if (bestHack == null || eff.Elapsed < bestProgress * dur) {
                    bestHack = eff.Hack;
                    bestProgress = progress;
                }
            }

            if (bestHack == null) return true;

            Effect shader = bestHack switch {
                SynapseBurn => HackEffectAssets.HackSynapseBurn,
                Cyberpsychosis => HackEffectAssets.HackCyberpsychosis,
                SystemReset => HackEffectAssets.HackSystemReset,
                OpticOverload => HackEffectAssets.HackOpticOverload,
                MemoryWipe => HackEffectAssets.HackMemoryWipe,
                Contagion => HackEffectAssets.HackContagion,
                _ => null
            };
            if (shader == null) return true;

            Texture2D tex = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["progress"]?.SetValue(bestProgress);
            shader.Parameters["intensity"]?.SetValue(1f);
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();

            _shaderActive = true;
            _activeShaderHack = bestHack;
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!_shaderActive) return;
            _shaderActive = false;
            _activeShaderHack = null;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
