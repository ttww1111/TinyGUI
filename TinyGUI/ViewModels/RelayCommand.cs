using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TinyGUI.ViewModels
{
    /// <summary>
    /// 支持同步与异步执行的 ICommand 实现（替代原先未被使用的 Commands/Command.cs）。
    /// 异步场景：Execute 内启动 Task 并捕获异常，避免 async void 裸奔。
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Func<object, Task> _asyncExecute;
        private readonly Action<object> _syncExecute;
        private readonly Predicate<object> _canExecute;
        private bool _isExecuting;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _syncExecute = execute;
            _canExecute = canExecute;
        }

        public RelayCommand(Func<object, Task> asyncExecute, Predicate<object> canExecute = null)
        {
            _asyncExecute = asyncExecute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public async void Execute(object parameter)
        {
            if (_syncExecute != null)
            {
                _syncExecute(parameter);
                return;
            }

            if (_asyncExecute == null) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();
            try
            {
                await _asyncExecute(parameter);
            }
            catch (Exception ex)
            {
                // 异常统一交由全局处理器；此处避免静默吞掉
                System.Windows.MessageBox.Show(ex.Message, "ERROR");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
