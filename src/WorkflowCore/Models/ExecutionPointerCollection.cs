using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WorkflowCore.Models
{
    /// <summary>
    /// 执行指针集合，提供按 Id/作用域/状态的查询能力及运行时压缩能力。
    /// </summary>
    public class ExecutionPointerCollection : ICollection<ExecutionPointer>
    {
        private readonly Dictionary<string, ExecutionPointer> _dictionary = new Dictionary<string, ExecutionPointer>();

        /// <summary>
        /// 初始化空的执行指针集合。
        /// </summary>
        public ExecutionPointerCollection()
        {
        }

        /// <summary>
        /// 按指定容量初始化执行指针集合。
        /// </summary>
        /// <param name="capacity">初始容量。</param>
        public ExecutionPointerCollection(int capacity)
        {
            _dictionary = new Dictionary<string, ExecutionPointer>(capacity);
        }

        /// <summary>
        /// 使用已有指针集合初始化当前集合。
        /// </summary>
        /// <param name="pointers">待复制的执行指针集合。</param>
        public ExecutionPointerCollection(ICollection<ExecutionPointer> pointers)
        {
            foreach (var ptr in pointers)
            {
                Add(ptr);
            }
        }

        /// <summary>
        /// 返回用于遍历集合的泛型枚举器。
        /// </summary>
        /// <returns>执行指针枚举器。</returns>
        public IEnumerator<ExecutionPointer> GetEnumerator()
        {
            return _dictionary.Values.GetEnumerator();
        }

        /// <summary>
        /// 返回用于遍历集合的非泛型枚举器。
        /// </summary>
        /// <returns>非泛型执行指针枚举器。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 根据指针 Id 查找执行指针。
        /// </summary>
        /// <param name="id">指针 Id。</param>
        /// <returns>找到时返回对应指针；未找到时返回 <c>null</c>。</returns>
        public ExecutionPointer FindById(string id)
        {
            if (!_dictionary.ContainsKey(id))
                return null;

            return _dictionary[id];
        }

        /// <summary>
        /// 查找作用域中包含指定栈帧 Id 的所有指针。
        /// </summary>
        /// <param name="stackFrame">作用域栈帧 Id。</param>
        /// <returns>匹配的执行指针集合。</returns>
        public ICollection<ExecutionPointer> FindByScope(string stackFrame)
        {
            return _dictionary.Values.Where(x => x.Scope.Contains(stackFrame)).ToList();
        }

        /// <summary>
        /// 添加一个执行指针。
        /// </summary>
        /// <param name="item">待添加的指针。</param>
        public void Add(ExecutionPointer item)
        {
            _dictionary.Add(item.Id, item);
        }

        /// <summary>
        /// 清空集合中的所有指针。
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
        }

        /// <summary>
        /// 判断集合中是否包含指定指针实例。
        /// </summary>
        /// <param name="item">待判断的指针实例。</param>
        /// <returns>包含则为 <c>true</c>，否则为 <c>false</c>。</returns>
        public bool Contains(ExecutionPointer item)
        {
            return _dictionary.ContainsValue(item);
        }

        /// <summary>
        /// 将当前集合复制到目标数组。
        /// </summary>
        /// <param name="array">目标数组。</param>
        /// <param name="arrayIndex">复制起始索引。</param>
        public void CopyTo(ExecutionPointer[] array, int arrayIndex)
        {
            _dictionary.Values.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 从集合中移除指定指针。
        /// </summary>
        /// <param name="item">待移除指针。</param>
        /// <returns>移除成功返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public bool Remove(ExecutionPointer item)
        {
            return _dictionary.Remove(item.Id);
        }

        /// <summary>
        /// 获取当前所有未结束指针形成的阻塞 Id 集合。
        /// 该集合用于阻止父容器指针在子分支未结束时提前运行。
        /// </summary>
        /// <returns>被阻塞的指针 Id 集合。</returns>
        public HashSet<string> GetBlockedPointerIds()
        {
            var result = new HashSet<string>();

            foreach (var pointer in _dictionary.Values.Where(x => x.EndTime == null))
            {
                foreach (var scopeId in pointer.Scope)
                {
                    result.Add(scopeId);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取当前可运行的指针集合。
        /// 条件：指针处于激活状态、未到睡眠时间或无需睡眠、且不被子分支阻塞。
        /// </summary>
        /// <param name="asAt">运行判定时间点。</param>
        /// <returns>可运行指针集合。</returns>
        public IReadOnlyCollection<ExecutionPointer> GetRunnablePointers(DateTime asAt)
        {
            var blockedIds = GetBlockedPointerIds();

            return _dictionary.Values
                .Where(x => x.Active && (!x.SleepUntil.HasValue || x.SleepUntil < asAt) && !blockedIds.Contains(x.Id))
                .ToList();
        }

        /// <summary>
        /// 判断指定指针是否存在未结束的后代指针。
        /// </summary>
        /// <param name="pointerId">父指针 Id。</param>
        /// <returns>存在未结束后代则返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        public bool HasDescendants(string pointerId)
        {
            return _dictionary.Values.Any(x => x.EndTime == null && x.Scope.Contains(pointerId));
        }

        /// <summary>
        /// 压缩并移除所有已结束指针，同时维护父指针的 <c>Children</c> 引用。
        /// </summary>
        public void CompactEndedPointers()
        {
            var endedPointers = _dictionary.Values
                .Where(x => x.EndTime.HasValue)
                .ToList();

            foreach (var pointer in endedPointers)
            {
                if (!string.IsNullOrEmpty(pointer.ParentId) && _dictionary.TryGetValue(pointer.ParentId, out var parent))
                {
                    parent.Children.Remove(pointer.Id);
                }

                _dictionary.Remove(pointer.Id);
            }
        }

        /// <summary>
        /// 根据谓词查找第一条匹配的执行指针。
        /// </summary>
        /// <param name="match">匹配条件。</param>
        /// <returns>第一条匹配项；若无匹配则返回 <c>null</c>。</returns>
        public ExecutionPointer Find(Predicate<ExecutionPointer> match)
        {
            return _dictionary.Values.FirstOrDefault(x => match(x));
        }

        /// <summary>
        /// 按指针状态查找执行指针。
        /// </summary>
        /// <param name="status">目标状态。</param>
        /// <returns>匹配状态的指针集合。</returns>
        public ICollection<ExecutionPointer> FindByStatus(PointerStatus status)
        {
            //TODO: track states in hash table
            return _dictionary.Values.Where(x => x.Status == status).ToList();
        }

        /// <summary>
        /// 获取集合中的指针数量。
        /// </summary>
        public int Count => _dictionary.Count;

        /// <summary>
        /// 获取集合是否为只读集合。
        /// </summary>
        public bool IsReadOnly => false;
    }
}
