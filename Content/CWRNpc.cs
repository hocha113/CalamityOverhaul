using CalamityMod;
using CalamityMod.Events;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Items.Summon.Extras;
using CalamityOverhaul.Content.NPCs.OverhaulBehavior;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Security.Policy;
using Terraria;
using Terraria.GameContent.Animations;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    public class CWRNpc : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public Player CreateHitPlayer;
        public byte ContagionOnHitNum = 0;
        public byte PhosphorescentGauntletOnHitNum = 0;
        public byte TerratomereBoltOnHitNum = 0;
        public byte OrderbringerOnHitNum = 0;
        public bool TheEndSunOnHitNum;
        public byte WhipHitNum = 0;
        public byte WhipHitType = 0;
        public bool SprBoss;
        public bool ObliterateBool;
        public bool GangarusSign;
        public ushort colldHitTime = 0;
        /// <summary>
        /// 实体是否受到鬼妖升龙斩的击退
        /// </summary>
        public bool MurasamabrBeatBackBool;
        /// <summary>
        /// 击退力的具体向量
        /// </summary>
        public Vector2 MurasamabrBeatBackVr;
        /// <summary>
        /// 升龙击退的衰减力度系数，1为不衰减
        /// </summary>
        public float MurasamabrBeatBackAttenuationForce;
        /// <summary>
        /// 上一帧实体的位置
        /// </summary>
        public Vector2 oldNPCPos;

        public static void MultipleSegmentsLimitDamage(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRIDs.targetNpcTypes15.Contains(target.type) || CWRIDs.targetNpcTypes10.Contains(target.type)
                || CWRIDs.targetNpcTypes8.Contains(target.type) || CWRIDs.targetNpcTypes7.Contains(target.type)
                || CWRIDs.targetNpcTypes6.Contains(target.type) || CWRIDs.targetNpcTypes5.Contains(target.type)
                || CWRIDs.targetNpcTypes4.Contains(target.type) || CWRIDs.targetNpcTypes2.Contains(target.type)) {
                modifiers.FinalDamage *= 0.1f;
                modifiers.SetMaxDamage(50);
            }
        }

        public override bool CanBeHitByNPC(NPC npc, NPC attacker) {
            return base.CanBeHitByNPC(npc, attacker);
        }

        public override bool CheckDead(NPC npc) {
            if (ObliterateBool) {
                return true;
            }
            return base.CheckDead(npc);
        }

        public override void PostAI(NPC npc) {
            if (MurasamabrBeatBackBool) {
                npc.position += MurasamabrBeatBackVr;
                if (oldNPCPos.Y - npc.position.Y < 0) {
                    MurasamabrBeatBackVr.Y *= 0.9f;
                }
                oldNPCPos = npc.position;
                MurasamabrBeatBackVr *= MurasamabrBeatBackAttenuationForce;
                MurasamabrBeatBackVr.X *= MurasamabrBeatBackAttenuationForce;
                if (MurasamabrBeatBackVr.LengthSquared() < 2) {
                    MurasamabrBeatBackBool = false;
                }
            }
            if (!CWRUtils.isClient) {
                if (WhipHitNum > 10) {
                    WhipHitNum = 10;
                }
            }
            if (Main.bloodMoon) {//在血月的情况下让一些生物执行特殊的行为，将这段代码写在PostAI中是防止被覆盖
                if (npc.type == CWRIDs.PerforatorHive)//改动血肉宿主的行为，这会让它在血月更加的暴躁和危险
                    PerforatorBehavior.Instance.Intensive(npc);
                if (npc.type == CWRIDs.HiveMind)//改动腐巢意志的行为，这会让它在血月更加的恐怖和强大
                    HiveMindBehavior.Instance.Intensive(npc);
            }
        }

        public override bool PreKill(NPC npc) {
            if (ContagionOnHitNum > 0 && CreateHitPlayer != null) {
                if (Main.myPlayer == CreateHitPlayer.whoAmI && CreateHitPlayer.ownedProjectileCounts[ModContent.ProjectileType<NurgleSoul>()] <= 13) {
                    Projectile proj = Projectile.NewProjectileDirect(CreateHitPlayer.parent(), npc.Center, CWRUtils.randVr(13)
                        , ModContent.ProjectileType<NurgleSoul>(), npc.damage, 2, CreateHitPlayer.whoAmI);
                    proj.scale = (npc.width / proj.width) * npc.scale;
                }
            }
            return base.PreKill(npc);
        }

        public override void OnKill(NPC npc) {
            if (npc.boss) {
                if (CWRIDs.targetNpcTypes7.Contains(npc.type) || npc.type == CWRIDs.PlaguebringerGoliath) {
                    for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                        int type = Item.NewItem(npc.parent(), npc.Hitbox, CWRIDs.DubiousPlating, Main.rand.Next(7, 13));
                        if (CWRUtils.isClient) {
                            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                        }
                    }
                }
                if (npc.type == CWRIDs.PrimordialWyrmHead) {
                    int type = Item.NewItem(npc.parent(), npc.Hitbox, ModContent.ItemType<TerminusOver>());
                    if (CWRUtils.isClient) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
            }
            if (npc.type == CWRIDs.PrimordialWyrmHead) {//我不知道为什么原灾厄没有设置这个字段，为了保持进度的正常，我在这里额外设置一次
                DownedBossSystem.downedPrimordialWyrm = true;
            }
            PerforatorBehavior.Instance.BloodMoonDorp(npc);
            HiveMindBehavior.Instance.BloodMoonDorp(npc);
            base.OnKill(npc);
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit) {
            if (npc.life <= 0) {
                if (TheEndSunOnHitNum) {
                    if (!BossRushEvent.BossRushActive) {
                        for (int i = 0; i < Main.rand.Next(16, 33); i++) {
                            npc.NPCLoot();
                        }
                    }
                    else {
                        if (Main.rand.NextBool(5)) {//如果是在BossRush时期，让Boss有一定概率掉落古恒石，这是额外的掉落
                            int type = Item.NewItem(npc.parent(), npc.Hitbox, ModContent.ItemType<Rock>());
                            if (CWRUtils.isClient) {
                                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                            }
                        }
                    }
                }
            }
            base.HitEffect(npc, hit);
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot) {
            if (npc.type == CWRIDs.Polterghast) {
                npcLoot.DefineConditionalDropSet(CWRDorp.InHellDropRule).Add(ModContent.ItemType<GhostFireWhip>());
            }
            if (npc.type == CWRIDs.Yharon) {
                npcLoot.DefineConditionalDropSet(CWRDorp.GlodDragonDropRule).Add(CWRDorp.Quantity(ModContent.ItemType<AuricBar>(), 1, 36, 57, 77, 158));
            }
            if (npc.type == CWRIDs.RavagerBody) {
                npcLoot.Add(ModContent.ItemType<PetrifiedDisease>());
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            base.PostDraw(npc, spriteBatch, screenPos, drawColor);
            if (WhipHitNum > 0) {
                //DrawTameBar(spriteBatch, npc);
            }
            if (Main.bloodMoon) {
                if (npc.type == CWRIDs.PerforatorHive) {
                    PerforatorBehavior.Instance.Draw(spriteBatch, npc);
                }
                if (npc.type == CWRIDs.HiveMind) {
                    HiveMindBehavior.Instance.Draw(spriteBatch, npc);
                }
            }
        }

        public void DrawTameBar(SpriteBatch spriteBatch, NPC npc) {
            Texture2D top = CWRUtils.GetT2DValue(CWRConstant.UI + "TameBarTop");
            Texture2D bar = CWRUtils.GetT2DValue(CWRConstant.UI + "TameBar");
            Texture2D whi = WhipHitDate.Tex((WhipHitTypeEnum)WhipHitType);

            float slp = 0.75f;
            float alp = 1 - (npc.velocity.Length() / 15f);
            if (alp < 0.3f) {
                alp = 0.3f;
            }

            int sengs = (int)((1 - (WhipHitNum / 10f)) * bar.Height);
            Rectangle barRec = new(sengs, 0, bar.Width, bar.Height - sengs);
            Color color = Color.White * alp;

            Vector2 drawPos = new Vector2(npc.position.X + (npc.width / 2), npc.Bottom.Y + top.Height) - Main.screenPosition;

            spriteBatch.Draw(top, drawPos, null, color, 0, bar.Size() / 2, slp, SpriteEffects.None, 0);

            spriteBatch.Draw(bar, drawPos + (new Vector2(14, sengs + 18) * slp), barRec, color, 0, bar.Size() / 2, slp, SpriteEffects.None, 0);

            spriteBatch.Draw(whi, drawPos + (new Vector2(0, whi.Height) * slp), null, color, 0, bar.Size() / 2, slp / 2, SpriteEffects.None, 0);
        }
    }
}
