using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using EvolutionLaws.Data;
using System.Linq;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【数据分析系统】
    /// 职责: 以固定频率采样生态数据，并导出为 CSV 文件供外部可视化分析。
    /// </summary>
    public class DataAnalyticsSystem
    {
        private StringBuilder _csvContent = new StringBuilder();
        private float _sampleInterval = 5f; // 采样频率：每 5 秒记录一次
        private float _lastSampleTime = -999f;

        public void Initialize()
        {
            // 初始化 CSV 表头
            _csvContent.AppendLine("Time,Species,Population,AvgEnergy,AvgNutrients,ForagingRate,FleeingRate,RestingRate,AvgSize,AvgSpeed,TotalPlantBiomass");
            Debug.Log("[DataAnalyticsSystem] 分析器初始化，准备记录数据...");
        }

        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float globalTime)
        {
            // 降频采样：不到时间就跳过
            if (globalTime - _lastSampleTime < _sampleInterval)
                return;

            _lastSampleTime = globalTime;

            // 1. 统计环境宏观数据
            float totalPlantBiomass = 0f;
            if (environment != null && environment.Grid != null)
            {
                foreach (var tile in environment.Grid)
                {
                    totalPlantBiomass += tile.Biomass_Plant;
                }
            }

            // 2. 按物种分组统计生物数据
            var speciesGroups = creatures.GroupBy(c => c.SpeciesID);

            foreach (var group in speciesGroups)
            {
                string species = group.Key;
                var list = group.ToList();
                int population = list.Count;

                if (population == 0) continue;

                // 累加器
                float sumEnergy = 0f, sumNutrients = 0f;
                float sumSize = 0f, sumSpeed = 0f;
                int foragingCount = 0, fleeingCount = 0, restingCount = 0;

                foreach (var c in list)
                {
                    sumEnergy += c.Energy;
                    sumNutrients += c.Nutrients;
                    sumSize += c.Size;
                    sumSpeed += c.Move_Speed;

                    if (c.CurrentBehavior == BehaviorState.Foraging) foragingCount++;
                    else if (c.CurrentBehavior == BehaviorState.Fleeing) fleeingCount++;
                    else if (c.CurrentBehavior == BehaviorState.Resting) restingCount++;
                }

                // 写入数据行
                _csvContent.AppendLine(
                    $"{globalTime:F1},{species},{population}," +
                    $"{sumEnergy / population:F2},{sumNutrients / population:F2}," +
                    $"{(float)foragingCount / population:F4},{(float)fleeingCount / population:F4},{(float)restingCount / population:F4}," +
                    $"{sumSize / population:F3},{sumSpeed / population:F3},{totalPlantBiomass:F1}"
                );
            }
        }

        public void ExportToFile()
        {
            // 导出
            string folder = @"C:\Users\联想\Desktop\Card\临时\EvaluationLaws";
            Directory.CreateDirectory(folder); // 自动创建目录（如果不存在）

            string path = Path.Combine(folder, "EvolutionSimulationData.csv");
            File.WriteAllText(path, _csvContent.ToString(), Encoding.UTF8);
            Debug.Log($"[DataAnalyticsSystem] 📊 数据已成功导出到: {path}");
        }

        // ==========================================
        // 👇一键导出所有平衡与配置参数 (蓝图+环境设定)
        // ==========================================
        public void ExportConfigurationJSON(EnvironmentManager envManager, EnvironmentDynamicsSystem envDynamics, List<SpeciesSpawnConfig> speciesConfigs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");

            // 1. 提取 环境生成参数
            sb.AppendLine("  \"EnvironmentManager_Settings\":");
            sb.AppendLine("  " + JsonUtility.ToJson(envManager, true).Replace("\n", "\n  ") + ",");

            // 2. 提取 环境动态演化参数
            sb.AppendLine("  \"EnvironmentDynamics_Settings\":");
            sb.AppendLine("  " + JsonUtility.ToJson(envDynamics, true).Replace("\n", "\n  ") + ",");

            // 3. 提取 所有启用的物种蓝图参数
            sb.AppendLine("  \"Species_Blueprints\": [");
            var activeConfigs = speciesConfigs.Where(c => c.Enabled && c.Blueprint != null).ToList();

            for (int i = 0; i < activeConfigs.Count; i++)
            {
                // 将单个蓝图对象转为 JSON
                string blueprintJson = JsonUtility.ToJson(activeConfigs[i].Blueprint, true);
                sb.Append("    " + blueprintJson.Replace("\n", "\n    "));

                if (i < activeConfigs.Count - 1)
                    sb.AppendLine(","); // 不是最后一个加逗号
                else
                    sb.AppendLine();    // 最后一个不加逗号
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            // 导出文件
            string folder = @"C:\Users\联想\Desktop\Card\临时\EvaluationLaws";
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "SimulationSettings_AI_Prompt.json");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

            Debug.Log($"[DataAnalyticsSystem] ⚙️ 配置蓝图快照已导出到: {path}\n去把这个文件发给AI让它帮忙做数值平衡吧！");
        }
    }
}