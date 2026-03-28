using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【战斗系统】
    /// 职责:
    /// 1. 检测攻击范围
    /// 2. 计算伤害
    /// 3. 应用伤害到目标
    /// 原则: 纯逻辑类, 不含 MonoBehaviour
    /// </summary>
    public class CombatSystem
    {
        // ==========================================
        // 配置参数
        // ==========================================
        public float Global_Damage_Multiplier = 1.0f; // 全局伤害倍率

        // ==========================================
        // 运行时状态
        // ==========================================
        private float _simulationTime = 0f;

        // ==========================================
        // 核心 Tick 方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行战斗逻辑
        /// </summary>
        public void Tick(List<CreatureData> creatures, float deltaTime)
        {
            _simulationTime += deltaTime;

            foreach (var attacker in creatures)
            {
                if (attacker.IsDead || attacker.IsUnconscious) continue;

                // ──────────────────────────────────
                // 检查是否有攻击目标
                // ──────────────────────────────────
                if (string.IsNullOrEmpty(attacker.TargetCreatureUID))
                    continue;

                // ──────────────────────────────────
                // 查找目标生物
                // ──────────────────────────────────
                var target = creatures.Find(c => c.UID == attacker.TargetCreatureUID);
                if (target == null || target.IsDead)
                {
                    // 目标不存在或已死亡,清空目标
                    attacker.TargetCreatureUID = null;
                    attacker.TargetPosition = null;
                    continue;
                }

                // ──────────────────────────────────
                // 检查攻击范围
                // ──────────────────────────────────
                float distance = Vector2.Distance(attacker.Position, target.Position);
                if (distance > attacker.Attack_Range)
                    continue; // 距离太远,跳过

                // ──────────────────────────────────
                // 检查攻击冷却
                // ──────────────────────────────────
                float timeSinceLastAttack = _simulationTime - attacker.Last_Attack_Time;
                if (timeSinceLastAttack < attacker.Attack_Cooldown)
                    continue; // 还在冷却中

                // ──────────────────────────────────
                // 执行攻击
                // ──────────────────────────────────
                ExecuteAttack(attacker, target);

                // 记录攻击时间
                attacker.Last_Attack_Time = _simulationTime;
            }
        }

        // ==========================================
        // 执行攻击
        // ==========================================
        /// <summary>
        /// 计算并应用伤害
        /// </summary>
        private void ExecuteAttack(CreatureData attacker, CreatureData target)
        {
            // ──────────────────────────────────
            // 1. 基础伤害
            // ──────────────────────────────────
            float baseDamage = attacker.Attack_Damage;

            // ──────────────────────────────────
            // 2. 体型修正 (大型生物对小型生物有优势)
            // ──────────────────────────────────
            float sizeFactor = attacker.Size / Mathf.Max(target.Size, 0.1f);
            sizeFactor = Mathf.Clamp(sizeFactor, 0.5f, 2.0f); // 限制在 0.5-2.0 倍

            // ──────────────────────────────────
            // 3. 最终伤害
            // ──────────────────────────────────
            float finalDamage = baseDamage * sizeFactor * Global_Damage_Multiplier;

            // ──────────────────────────────────
            // 4. 应用伤害到结构值
            // ──────────────────────────────────
            target.Structure_Current -= finalDamage;
            target.Structure_Current = Mathf.Max(target.Structure_Current, 0f);

            // ──────────────────────────────────
            // 5. 增加目标压力值 (被攻击会恐慌)
            // ──────────────────────────────────
            target.Stress_Current += 20f;
            target.Stress_Current = Mathf.Min(target.Stress_Current, 100f);

            // ──────────────────────────────────
            // 6. 日志输出
            // ──────────────────────────────────
            Debug.Log($"[CombatSystem] {attacker.SpeciesID}[{attacker.UID.Substring(0, 6)}] 攻击 {target.SpeciesID}[{target.UID.Substring(0, 6)}] | " +
                      $"伤害: {finalDamage:F1} | 剩余结构: {target.Structure_Current:F1}/{target.Structure_Max}");

            // ──────────────────────────────────
            // 7. 检查目标是否死亡
            // ──────────────────────────────────
            if (target.Structure_Current <= 0f && !target.IsDead)
            {
                target.IsDead = true;
                Debug.Log($"[CombatSystem] {target.SpeciesID}[{target.UID.Substring(0, 6)}] 被 {attacker.SpeciesID}[{attacker.UID.Substring(0, 6)}] 击杀!");

                // 攻击者清空目标 (目标已死亡)
                attacker.TargetCreatureUID = null;
                attacker.TargetPosition = null;
                attacker.CurrentBehavior = BehaviorState.Idle;
            }
        }
    }
}