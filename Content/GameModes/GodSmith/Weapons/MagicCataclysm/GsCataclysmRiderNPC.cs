using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 灾变族左键 rider 的逐 NPC 状态载体（P13 返工新增）。
    /// 所有字段都是 owner-local 量：命中类钩子只在攻击方端执行，每端只记本机玩家叠的层，
    /// 不跨端同步；触发的跨端表现全部走真弹幕
    /// </summary>
    internal class GsCataclysmRiderNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>剃刀松「松脂」层数（5 层引发针环收缩）</summary>
        internal int ResinStacks;

        /// <summary>松脂剩余帧，归零清层</summary>
        internal int ResinTimer;

        public override void PostAI(NPC npc) {
            if (ResinTimer > 0 && --ResinTimer == 0) {
                ResinStacks = 0;
            }
        }

        /// <summary>该 NPC 可否承载 rider 层数（排除友军、假人、无血皮演出体）</summary>
        internal static bool CanCarry(NPC npc)
            => npc.active && !npc.friendly && npc.lifeMax > 5 && npc.type != NPCID.TargetDummy;
    }

    /// <summary>灾变族左键 rider 共用的小工具（纯静态，无状态）</summary>
    internal static class GsCataclysmRiderLib
    {
        /// <summary>速度重影拖尾：沿速度反向三层衰减残像，A=0 加色，identity 定相零随机</summary>
        internal static void DrawSpeedGhost(Projectile proj, Color color, float strength) {
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + proj.identity * 0.61f);
            Color glow = color with { A = 0 };
            for (int i = 1; i <= 3; i++) {
                Vector2 at = proj.Center - proj.velocity * (0.55f * i);
                Main.EntitySpriteDraw(tex, at - Main.screenPosition, null, glow * (strength * pulse / i),
                    proj.rotation, tex.Size() * 0.5f, 1f, SpriteEffects.None, 0);
            }
        }

        /// <summary>找 exclude 之外距 from 最近的可追击敌怪（owner 端索敌用）</summary>
        internal static NPC FindAnotherEnemy(NPC exclude, Vector2 from, float maxDist) {
            NPC best = null;
            float bestDistSq = maxDist * maxDist;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || (exclude != null && npc.whoAmI == exclude.whoAmI)) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, from);
                if (d < bestDistSq) {
                    bestDistSq = d;
                    best = npc;
                }
            }
            return best;
        }
    }
}
