using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.Storage
{
    /// <summary>
    /// 存储提供者接口，定义了存储对象的统一抽象
    /// 任何可以存储物品的容器都应该实现此接口
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>
        /// 存储提供者的唯一标识符
        /// </summary>
        string Identifier { get; }

        /// <summary>
        /// 存储对象在世界中的位置(物块坐标)
        /// </summary>
        Point16 Position { get; }

        /// <summary>
        /// 存储对象在世界中的中心位置(像素坐标)
        /// </summary>
        Vector2 WorldCenter { get; }

        /// <summary>
        /// 存储对象的碰撞区域(像素坐标)
        /// </summary>
        Rectangle HitBox { get; }

        /// <summary>
        /// 检查存储对象是否仍然有效(比如箱子可能被破坏)
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 检查是否可以添加指定物品到存储中
        /// </summary>
        /// <param name="item">要检查的物品</param>
        /// <returns>如果可以添加返回true</returns>
        bool CanAcceptItem(Item item);

        /// <summary>
        /// 检查存储是否还有剩余空间
        /// </summary>
        bool HasSpace { get; }

        /// <summary>
        /// 将物品存入存储
        /// </summary>
        /// <param name="item">要存入的物品</param>
        /// <returns>存入操作是否成功</returns>
        bool DepositItem(Item item);

        /// <summary>
        /// 从存储中取出指定类型的物品
        /// </summary>
        /// <param name="itemType">物品类型ID</param>
        /// <param name="count">要取出的数量</param>
        /// <returns>取出的物品，如果无法取出返回空物品</returns>
        Item WithdrawItem(int itemType, int count);

        /// <summary>
        /// 获取存储中所有物品的枚举
        /// </summary>
        /// <returns>物品枚举</returns>
        IEnumerable<Item> GetStoredItems();

        /// <summary>
        /// 获取存储中指定类型物品的总数量
        /// </summary>
        /// <param name="itemType">物品类型ID</param>
        /// <returns>物品总数量</returns>
        long GetItemCount(int itemType);

        /// <summary>
        /// 执行存入动画效果(可选实现)
        /// </summary>
        void PlayDepositAnimation() { }
    }
}
