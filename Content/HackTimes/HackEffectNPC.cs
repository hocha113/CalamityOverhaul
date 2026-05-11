using CalamityOverhaul.Content.HackTimes.Protocols;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>
    /// 骇入协议效果的NPC全局钩子
    /// <br/>处理效果对NPC行为的干预（AI修改、绘制覆盖等）
    /// </summary>
    internal class HackEffectNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //缓存列表避免每帧GC
        private readonly List<ActiveHackEffect> effectsCache = [];
        //赛博精神病接触伤害冷却计时器
        private int _cyberDamageCooldown;

        public bool? PreAIByOverNPC(NPC npc) {
            //时停期间不执行任何骇入效果的AI干预
            if (TimeFreezes.WorldFreezeSystem.IsActive) return null;
            HackEffectTracker.GetEffects(npc.whoAmI, effectsCache);
            if (effectsCache.Count == 0) return null;

            bool? allowAI = true;
            for (int i = 0; i < effectsCache.Count; i++) {
                var eff = effectsCache[i];
                switch (eff.Hack) {
                    //系统重置：完全阻止AI
                    case SystemReset:
                        npc.velocity = Vector2.Zero;
                        allowAI = false;
                        break;
                    //赛博精神病：重定向AI攻击最近NPC
                    case Cyberpsychosis:
                        RedirectAI(npc, eff, ref _cyberDamageCooldown);
                        allowAI = false;
                        break;
                    //视觉过载：NPC失去跟踪能力，随机游荡
                    case OpticOverload:
                        BlindWander(npc);
                        allowAI = false;
                        break;
                    //记忆清除：清除仇恨，停止追击
                    case MemoryWipe:
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
            //写在这里提醒自己，这个钩子的优先级不够高，导致可能无法很好的处理停滞效果等ai修改拦截，目前已经在PreAIByOverNPC函数中处理，由TimeFreezeEntities调用
            return true;
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            //赛博精神病状态下NPC不对玩家造成伤害
            if (HackEffectTracker.HasEffect<Cyberpsychosis>(npc.whoAmI)) {
                modifiers.FinalDamage *= 0f;
            }
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            //记忆清除：被玩家攻击立刻恢复
            if (HackEffectTracker.HasEffect<MemoryWipe>(npc.whoAmI)) {
                var eff = HackEffectTracker.GetEffect<MemoryWipe>(npc.whoAmI);
                if (eff != null) {
                    eff.Hack.OnRemove(eff.Target);
                    eff.Active = false;
                }
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (HackEffectTracker.HasEffect<MemoryWipe>(npc.whoAmI)) {
                var eff = HackEffectTracker.GetEffect<MemoryWipe>(npc.whoAmI);
                if (eff != null) {
                    eff.Hack.OnRemove(eff.Target);
                    eff.Active = false;
                }
            }
        }

        //赛博精神病：重定向NPC攻击最近的其他NPC
        private static void RedirectAI(NPC npc, ActiveHackEffect eff, ref int damageCooldown) {
            if (damageCooldown > 0) damageCooldown--;
            //寻找最近的其他活跃NPC
            float closestDist = float.MaxValue;
            NPC closestNPC = null;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC other = Main.npc[i];
                if (!other.active || other.whoAmI == npc.whoAmI || other.friendly || other.dontTakeDamage)
                    continue;
                //不攻击已经被赛博精神病影响的NPC
                if (HackEffectTracker.HasEffect<Cyberpsychosis>(other.whoAmI))
                    continue;
                float dist = Vector2.DistanceSquared(npc.Center, other.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    closestNPC = other;
                }
            }

            if (closestNPC != null) {
                //朝最近NPC移动
                Vector2 dir = closestNPC.Center - npc.Center;
                float dist = dir.Length();
                if (dist > 0) dir /= dist;
                float speed = Math.Max(npc.velocity.Length(), 3f);
                npc.velocity = Vector2.Lerp(npc.velocity, dir * speed, 0.08f);
                npc.direction = closestNPC.Center.X > npc.Center.X ? 1 : -1;
                npc.spriteDirection = npc.direction;

                //接触伤害，60帧冷却防止帧伤
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
                //没有目标时减速游荡
                npc.velocity *= 0.96f;
            }
        }

        //视觉过载：随机游荡
        private static void BlindWander(NPC npc) {
            //缓慢随机改变方向
            if (Main.GameUpdateCount % 30 == 0) {
                npc.velocity = Main.rand.NextVector2CircularEdge(1.5f, 1.5f);
                npc.direction = npc.velocity.X > 0 ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }
            npc.velocity *= 0.98f;
        }

        //记忆清除：类似时停，减速冻结NPC，阻止AI运行
        //被玩家攻击时在OnHitByItem/OnHitByProjectile中提前结束效果
        private static void WipeAggro(NPC npc) {
            npc.velocity *= 0.9f;
            if (npc.velocity.Length() < 0.1f)
                npc.velocity = Vector2.Zero;
        }
    }

    /// <summary>
    /// 骇入协议效果的NPC绘制钩子
    /// <br/>在受到协议影响的NPC身上叠加对应的着色器特效
    /// <br/>与HackTimeNPCDraw分开处理——HackTimeNPCDraw处理选中/悬停高亮
    /// <br/>此类处理已施加的效果视觉
    /// </summary>
    internal class HackEffectNPCDraw : GlobalNPC
    {
        //当前帧激活的着色器标记
        private static bool _shaderActive;
        //按优先级选择的效果类型（用于选择shader）
        private static QuickHackDef _activeShaderHack;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            var effects = HackEffectTracker.AllActiveEffects;
            QuickHackDef bestHack = null;
            float bestProgress = 0f;

            //找到此NPC上优先级最高的可视效果
            for (int i = 0; i < effects.Count; i++) {
                var eff = effects[i];
                if (!eff.Active || eff.TargetIndex != npc.whoAmI) continue;
                //即时效果（ShortCircuit）不持续绘制
                if (eff.Hack.GetDuration() == 0) continue;

                float progress = 0f;
                int dur = (int)(eff.Hack.GetDuration() * eff.EffectMult);
                if (dur > 0) progress = (float)eff.Elapsed / dur;

                //取最新施加的效果做着色器渲染
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
