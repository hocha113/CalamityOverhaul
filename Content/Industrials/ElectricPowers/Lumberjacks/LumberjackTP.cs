using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    internal class LumberjackTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<LumberjackTile>();
        public override int TargetItem => ModContent.ItemType<Lumberjack>();
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 500;
        internal const int maxSearchDistance = 1200;
        public Vector2 ArmPos => CenterInWorld + new Vector2(0, -8);
        private int textIdleTime;
        internal bool BatteryPrompt;
        internal int dontSpawnArmTime;
        internal int consumeUE = 10;
        internal List<int> ArmActorIndices = new List<int>();

        public override void SetBattery() {
            DrawExtendMode = 1600;
        }

        public override void SendData(ModPacket data) {
            base.SendData(data);
            data.Write(BatteryPrompt);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            BatteryPrompt = reader.ReadBoolean();
        }

        internal static bool IsArmActorValid(int actorIndex) {
            if (actorIndex < 0 || actorIndex >= ActorLoader.MaxActorCount) return false;
            Actor actor = ActorLoader.Actors[actorIndex];
            return actor != null && actor.Active && actor is LumberjackSaw;
        }

        /// <summary>
        /// 检查并生成机械锯臂(仅服务器端)
        /// </summary>
        private void SpawnArmsIfNeeded() {
            if (VaultUtils.isClient) return;
            if (dontSpawnArmTime > 0) return;

            //清理失效的索引
            ArmActorIndices.RemoveAll(index => !IsArmActorValid(index));

            if (ArmActorIndices.Count < 1) {
                int actorIndex = ActorLoader.NewActor<LumberjackSaw>(ArmPos, Vector2.Zero);
                ArmActorIndices.Add(actorIndex);
                ActorLoader.Actors[actorIndex].OnSpawn(Position);
            }
        }

        public override void UpdateMachine() {
            consumeUE = 10;

            if (textIdleTime > 0) {
                textIdleTime--;
            }
            if (dontSpawnArmTime > 0) {
                dontSpawnArmTime--;
            }

            //检查机械臂总数限制
            int totalArms = ActorLoader.GetActiveActors<LumberjackSaw>().Count;
            if (totalArms > 100) {
                if (textIdleTime <= 0) {
                    CombatText.NewText(HitBox, Color.YellowGreen, Lumberjack.Text1.Value);
                    textIdleTime = 300;
                }
                return;
            }

            SpawnArmsIfNeeded();

            //检查能量状态
            BatteryPrompt = MachineData.UEvalue < consumeUE;
            if (BatteryPrompt && textIdleTime <= 0) {
                CombatText.NewText(HitBox, Color.YellowGreen, Lumberjack.Text2.Value);
                textIdleTime = 300;
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
