using System.Threading;
using TinifyAPI;

namespace TinyGUI.Services
{
    /// <summary>
    /// Tinify.Key 是全局静态的，而且每次给它赋值都会销毁并重建内部的 HttpClient。
    /// 并发压缩时如果边请求边改 Key，正在飞的请求就会撞上
    /// "Cannot access a disposed object. Object name: 'System.Net.Http.HttpClient'"。
    ///
    /// 这里做一层闸门：
    ///   · 用同一个 Key 的请求完全并行，不做任何串行化（保住并发数带来的速度）；
    ///   · 需要换 Key 时，先等所有在飞请求结束，再切换，切换后继续并行；
    ///   · Key 没变化时一次赋值都不做，避免无谓地重建 HttpClient。
    /// </summary>
    internal static class TinifyKeyGate
    {
        private static readonly object Sync = new object();
        private static string _currentKey;
        private static int _inFlight;

        /// <summary>进入一次请求：必要时切换 Key，并登记一个在飞请求</summary>
        public static void Enter(string key)
        {
            lock (Sync)
            {
                // 要换 Key 就必须先让在飞的请求全部落地，否则会拆掉它们脚下的 HttpClient
                while (_inFlight > 0 && _currentKey != key)
                    Monitor.Wait(Sync);

                if (_currentKey != key)
                {
                    Tinify.Key = key;
                    _currentKey = key;
                }
                _inFlight++;
            }
        }

        /// <summary>请求结束（成功、失败、取消都必须调用）</summary>
        public static void Exit()
        {
            lock (Sync)
            {
                if (_inFlight > 0) _inFlight--;
                if (_inFlight == 0) Monitor.PulseAll(Sync);
            }
        }

        /// <summary>忘记当前 Key（下次请求会强制重新赋值）</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                while (_inFlight > 0)
                    Monitor.Wait(Sync);
                _currentKey = null;
            }
        }
    }
}
