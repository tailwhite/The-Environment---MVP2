using EvolutionLaws.Data;
using EvolutionLaws.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【交互系统】
    /// 职责:
    /// 1. 处理生物与环境的交互 (进食/采集)
    /// 2. 计算资源转化效率
    /// 3. 更新环境资源状态
    /// 原则:纯逻辑类,不含MonoBehaviour
    /// </summary>
    public class InteractionSystem
    {
        // ==========================================
        // 配置参数
        // ==========================================
        [Header("Feeding Settings")]
        public float Feeding_Interval = 1.0f;           // 进食间隔(秒)

        public float Feeding_Amount_Base = 30f;         // 基础进食量
        public float Feeding_Energy_Cost = 1.0f;        // 进食动作能量消耗

        // ==========================================
        // 使用仿真累计时间
        // ==========================================
        private float _simulationTime = 0f;

        private Dictionary<string, float> _lastFeedTime = new Dictionary<string, float>();

        // ==========================================
        // 核心Tick方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行交互逻辑
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float deltaTime)
        {
            // 累加仿真时间
            _simulationTime += deltaTime;

            foreach (var creature in creatures)
            {
                //每帧重置进食状态，由InteractionSystem控制何时设置为true
                creature.IsFeeding = false;
                if (creature.IsDead || creature.IsUnconscious) continue;

                if (!ShouldFeed(creature))
                    continue;

                int tileX = Mathf.FloorToInt(creature.Position.x);
                int tileY = Mathf.FloorToInt(creature.Position.y);
                var tile = environment.GetTile(tileX, tileY);

                if (tile == null) continue;

                bool fedSuccessfully = TryFeedFromTile(creature, tile);

                if (fedSuccessfully)
                {
                    creature.IsFeeding = true;// 设置进食状态
                    //Debug.Log($"[InteractionSystem] {creature.SpeciesID} 进食成功 | 位置: ({tileX}, {tileY})");
                }
            }
        }

        // ==========================================
        // 决策逻辑:是否应该进食（仿真时间）
        // ==========================================
        private bool ShouldFeed(CreatureData creature)
        {
            if (creature.Nutrients >= creature.Nutrients_Max)
                return false;

            // 使用仿真时间
            if (_lastFeedTime.ContainsKey(creature.UID))
            {
                float timeSinceLastFeed = _simulationTime - _lastFeedTime[creature.UID];// 计算仿真时间差
                if (timeSinceLastFeed < Feeding_Interval)// 如果仿真时间差小于进食间隔,即处于进食冷却中（没吃完就过来吃了）,返回false
                    return false;
            }

            return true;
        }

        // ==========================================
        // 进食执行
        // ==========================================
        private bool TryFeedFromTile(CreatureData creature, TileData tile)
        {
            // 找出最高效的可用资源类型
            ResourceType bestResource = ResourceType.None;
            float bestEfficiency = 0f;
            // 检查植物资源
            if (tile.Biomass_Plant > 0f)
            {   // 有植物资源
                float plantEfficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Plant_Fiber);
                if (plantEfficiency > bestEfficiency)
                {
                    bestResource = ResourceType.Plant_Fiber;
                    bestEfficiency = plantEfficiency;
                }
            }
            // 检查肉类资源
            if (tile.Biomass_Meat > 0f)
            {   // 有肉类资源
                float meatEfficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Meat);
                // 比较效率
                if (meatEfficiency > bestEfficiency)
                {   // 更新最佳资源
                    bestResource = ResourceType.Meat;

                    bestEfficiency = meatEfficiency;
                }
            }
            // 检查矿物资源
            if (tile.Biomass_Mineral > 0f)
            {
                float mineralEfficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Mineral);
                if (mineralEfficiency > bestEfficiency)
                {
                    bestResource = ResourceType.Mineral;
                    bestEfficiency = mineralEfficiency;
                }
            }
            // 如果没有可用资源，返回失败
            if (bestResource == ResourceType.None || bestEfficiency <= 0f)
                return false;

            // 计算可进食量
            float availableAmount = 0f;
            // 根据最佳资源类型获取可用量
            if (bestResource == ResourceType.Plant_Fiber)
                availableAmount = tile.Biomass_Plant;
            else if (bestResource == ResourceType.Meat)
                availableAmount = tile.Biomass_Meat;
            else if (bestResource == ResourceType.Mineral)
                availableAmount = tile.Biomass_Mineral;
            // 期望进食量
            float desiredAmount = Feeding_Amount_Base * creature.Size;
            // 计算实际可进食量 (受限于资源和营养需求)
            float maxCanEat = creature.Nutrients_Max - creature.Nutrients;
            // 实际进食量
            float actualAmount = Mathf.Min(desiredAmount, availableAmount, maxCanEat / bestEfficiency);
            // 如果实际量为零，返回失败
            if (actualAmount <= 0f)
                return false;

            // 扣除环境资源
            if (bestResource == ResourceType.Plant_Fiber)
                tile.Biomass_Plant -= actualAmount;
            else if (bestResource == ResourceType.Meat)
                tile.Biomass_Meat -= actualAmount;
            else if (bestResource == ResourceType.Mineral)
                tile.Biomass_Mineral -= actualAmount;
            // 【核心调整】：肉类拥有极高的能量密度，结算时强行给予高额杠杆倍率
            float nutrientMultiplier = 1.0f;
            if (bestResource == ResourceType.Meat)
            {
                nutrientMultiplier = 4.0f; // 吃一口肉抵得上吃四口草，符合真实生态链的能量富集逻辑
            }
            else if (bestResource == ResourceType.Mineral)
            {
                nutrientMultiplier = 2.0f; // 吃一口矿顶两口草
            }

            // 增加生物营养
            float gainedNutrients = actualAmount * bestEfficiency * nutrientMultiplier;

            // 更新生物状态
            creature.Nutrients += gainedNutrients;
            // 夹紧上限
            creature.Nutrients = Mathf.Min(creature.Nutrients, creature.Nutrients_Max);// 营养不能超过上限

            creature.Energy -= Feeding_Energy_Cost;
            creature.Lifetime_EnergySpent_Action += Feeding_Energy_Cost;
            // 更新饥饿度
            float nutrientPercent = creature.Nutrients / creature.Nutrients_Max;
            creature.Need_Hunger = Mathf.Lerp(100f, 0f, nutrientPercent);

            //  记录仿真时间
            _lastFeedTime[creature.UID] = _simulationTime;

            //Debug.Log($"[InteractionSystem] {creature.SpeciesID}[{creature.UID.Substring(0, 6)}] 进食 {actualAmount:F1} {bestResource} | " +
            //$"获得营养: {gainedNutrients:F1} | 效率: {bestEfficiency:P0}");

            return true;
        }
    }
}