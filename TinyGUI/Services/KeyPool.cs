using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TinyGUI.Services
{
    /// <summary>
    /// 多 API Key 状态机。
    /// Tinify.Key 是全局静态的，所有并发请求共用同一个“当前生效 Key”，
    /// 因此 ActiveKey 对同一时刻的所有任务返回同一个 Key（除非该 Key 被标记用尽后轮换）。
    /// 真正的并发安全由 <see cref="TinifyKeyGate"/> 负责：换 Key 前会等在飞请求落地。
    /// </summary>
    public class KeyPool
    {
        private readonly List<string> _keys;
        private readonly bool[] _exhausted;
        private int _activeIndex;
        private readonly object _lock = new object();

        public KeyPool(IEnumerable<string> keys)
        {
            _keys = (keys ?? Enumerable.Empty<string>())
                .Select(k => (k ?? string.Empty).Trim())
                .Where(k => k.Length > 0)
                .Distinct()
                .ToList();
            _exhausted = new bool[_keys.Count];
            _activeIndex = 0;
        }

        public int Count => _keys.Count;

        public bool AnyAvailable
        {
            get
            {
                lock (_lock)
                {
                    return _keys.Count > 0 && Array.IndexOf(_exhausted, false) >= 0;
                }
            }
        }

        /// <summary>当前所有并发请求共用的生效 Key（无可用 Key 时返回 null）</summary>
        public string ActiveKey
        {
            get
            {
                lock (_lock)
                {
                    if (_keys.Count == 0) return null;
                    if (_activeIndex >= _keys.Count) _activeIndex = 0;
                    return _exhausted[_activeIndex] ? null : _keys[_activeIndex];
                }
            }
        }

        /// <summary>标记某 Key 额度用尽</summary>
        public void MarkExhausted(string key)
        {
            lock (_lock)
            {
                int idx = _keys.IndexOf(key);
                if (idx >= 0) _exhausted[idx] = true;
            }
        }

        /// <summary>轮换到下一个未耗尽的 Key</summary>
        public void Rotate()
        {
            lock (_lock)
            {
                if (_keys.Count == 0) return;
                for (int step = 1; step <= _keys.Count; step++)
                {
                    int idx = (_activeIndex + step) % _keys.Count;
                    if (!_exhausted[idx])
                    {
                        _activeIndex = idx;
                        return;
                    }
                }
                // 全部耗尽，保持原位，ActiveKey 将返回 null
            }
        }
    }
}
