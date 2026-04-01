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

        // 验尸房档案：记录每个物种的【累计死亡原因】
        // 结构：SpeciesID -> (DeathCause -> Count)
        private Dictionary<string, Dictionary<DeathCause, int>> _deathStatsGrouped = new Dictionary<string, Dictionary<DeathCause, int>>();

        public void Initialize()
        {
            // 初始化 CSV 表头（在后面追加了四种死因）
            _csvContent.AppendLine("Time,Species,Population,AvgEnergy,AvgNutrients,ForagingRate,FleeingRate,RestingRate,AvgSize,AvgSpeed,TotalPlant,TotalMeat,TotalMineral,AvgFertility,MetabolismCost,TempCost,MoveCost,ActionCost,Dead_Starvation,Dead_Killed,Dead_Env,Dead_OldAge");
            Debug.Log("[DataAnalyticsSystem] 分析器初始化，准备记录包含验尸和资源数据的文件...");
        }

        // ==========================================
        // 外部接口：通报有生物死亡
        // ==========================================
        public void RecordDeath(CreatureData deadCreature)
        {
            string sp = deadCreature.SpeciesID;
            if (!_deathStatsGrouped.ContainsKey(sp))
            {
                _deathStatsGrouped[sp] = new Dictionary<DeathCause, int>();
            }

            if (!_deathStatsGrouped[sp].ContainsKey(deadCreature.CauseOfDeath))
            {
                _deathStatsGrouped[sp][deadCreature.CauseOfDeath] = 0;
            }

            _deathStatsGrouped[sp][deadCreature.CauseOfDeath]++;
        }

        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float globalTime)
        {
            // 降频采样：不到时间就跳过
            if (globalTime - _lastSampleTime < _sampleInterval)
                return;

            _lastSampleTime = globalTime;

            // 1. 统计环境宏观数据
            float totalPlant = 0f;
            float totalMeat = 0f;
            float totalMineral = 0f;
            float totalFertility = 0f;
            int tileCount = 0;
            if (environment != null && environment.Grid != null)
            {
                tileCount = environment.Grid.Length;
                foreach (var tile in environment.Grid)
                {
                    totalPlant += tile.Biomass_Plant;
                    totalMeat += tile.Biomass_Meat;
                    totalMineral += tile.Biomass_Mineral;
                    totalFertility += tile.Soil_Fertility;
                }
            }
            float avgFertility = tileCount > 0 ? (totalFertility / tileCount) : 0f;

            // 获取所有存活的物种 + 已经灭绝但曾经有死亡记录的物种（防止灭绝后不在CSV里显示最终死亡柱状图）
            HashSet<string> trackedSpecies = new HashSet<string>(creatures.Select(c => c.SpeciesID));
            foreach (var sp in _deathStatsGrouped.Keys) trackedSpecies.Add(sp);

            // 2. 按物种分组统计生物数据
            foreach (var species in trackedSpecies)
            {
                var list = creatures.Where(c => c.SpeciesID == species).ToList();
                int population = list.Count;

                // 读取累计死因
                int dStarve = 0, dKilled = 0, dEnv = 0, dAge = 0;
                if (_deathStatsGrouped.ContainsKey(species))
                {
                    dStarve = _deathStatsGrouped[species].GetValueOrDefault(DeathCause.Starvation, 0);
                    dKilled = _deathStatsGrouped[species].GetValueOrDefault(DeathCause.Killed, 0);
                    dEnv = _deathStatsGrouped[species].GetValueOrDefault(DeathCause.Environment, 0);
                    dAge = _deathStatsGrouped[species].GetValueOrDefault(DeathCause.OldAge, 0);
                }

                // 如果种群灭绝了，活体平均数值补0，但依然输出死亡数量
                if (population == 0)
                {
                    _csvContent.AppendLine(
                        $"{globalTime:F1},{species},{population}," +
                        $"0,0,0,0,0,0,0,{totalPlant:F1},{totalMeat:F1},{totalMineral:F1},{avgFertility:F3},0,0,0,0," +
                        $"{dStarve},{dKilled},{dEnv},{dAge}"
                    );
                    continue;
                }

                // 累加器(活体)
                float sumEnergy = 0f, sumNutrients = 0f;
                float sumSize = 0f, sumSpeed = 0f;
                int foragingCount = 0, fleeingCount = 0, restingCount = 0;
                float sumBurnMetabolism = 0f, sumBurnTemp = 0f, sumBurnMove = 0f, sumBurnAction = 0f;

                foreach (var c in list)
                {
                    sumEnergy += c.Energy;
                    sumNutrients += c.Nutrients;
                    sumSize += c.Size;
                    sumSpeed += c.Move_Speed;

                    sumBurnMetabolism += c.Lifetime_EnergySpent_Metabolism;
                    sumBurnTemp += c.Lifetime_EnergySpent_Temp;
                    sumBurnMove += c.Lifetime_EnergySpent_Move;
                    sumBurnAction += c.Lifetime_EnergySpent_Action;

                    if (c.CurrentBehavior == BehaviorState.Foraging) foragingCount++;
                    else if (c.CurrentBehavior == BehaviorState.Fleeing) fleeingCount++;
                    else if (c.CurrentBehavior == BehaviorState.Resting) restingCount++;
                }

                // 写入数据行 (追加了新的资源环境参数)
                _csvContent.AppendLine(
                    $"{globalTime:F1},{species},{population}," +
                    $"{sumEnergy / population:F2},{sumNutrients / population:F2}," +
                    $"{(float)foragingCount / population:F4},{(float)fleeingCount / population:F4},{(float)restingCount / population:F4}," +
                    $"{sumSize / population:F3},{sumSpeed / population:F3}," +
                    $"{totalPlant:F1},{totalMeat:F1},{totalMineral:F1},{avgFertility:F3}," +
                    $"{sumBurnMetabolism / population:F2},{sumBurnTemp / population:F2}," +
                    $"{sumBurnMove / population:F2},{sumBurnAction / population:F2}," +
                    $"{dStarve},{dKilled},{dEnv},{dAge}"
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
    }
}