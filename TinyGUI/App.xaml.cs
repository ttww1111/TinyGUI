using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TinyGUI.Properties;
using TinyGUI.Services;
using TinyGUI.ViewModels;
using TinyGUI.Views;

namespace TinyGUI
{
    public partial class App
    {
        /// <summary>应用版本号（显示用）。
        /// 直接从程序集版本读取，单一来源 = csproj 的 &lt;Version&gt;，永不与编译产物脱节。</summary>
        public static string Version
        {
            get
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }
        private static readonly string[] AvailableLocales = { "zh", "zh-hant", "en", "de-de" };

        protected override void OnStartup(StartupEventArgs e)
        {
            AppSettings.Load();
            InitLanguage();

            // 全局异常处理（DEBUG 也保留 Dispatcher 异常，便于排查）
            RegisterEvents();

            base.OnStartup(e);
        }

        /// <summary>
        /// 语言切换即时生效：只换文化并通知绑定刷新。
        /// 不再重建窗口——界面原地更新，压缩中的任务也不会断。
        /// </summary>
        public static void ApplyLanguage(int index)
        {
            SetCulture(index);
            Loc.Instance.Refresh();
        }

        private static void SetCulture(int index)
        {
            string locale = (index >= 0 && index < AvailableLocales.Length)
                ? AvailableLocales[index]
                : AvailableLocales[0];
            try
            {
                var ci = CultureInfo.GetCultureInfo(locale);
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
                // 显式设置资源文化，否则 Resources 会一直沿用首次解析时的文化。
                // 注意必须写完全限定名：App 继承了 Application.Resources，会遮蔽 Properties.Resources
                TinyGUI.Properties.Resources.Culture = ci;
            }
            catch
            {
                var fallback = CultureInfo.GetCultureInfo(AvailableLocales[0]);
                Thread.CurrentThread.CurrentUICulture = fallback;
                TinyGUI.Properties.Resources.Culture = fallback;
            }
        }

        private void InitLanguage()
        {
            int index = AppSettings.Current.LanguageIndex;
            if (index < 0)
            {
                // 自动：依系统区域判断
                string lang = CultureInfo.CurrentCulture.Name;
                index = (!string.IsNullOrEmpty(lang) && lang.ToLowerInvariant().Contains("zh")) ? 0 : 2;
                AppSettings.Current.LanguageIndex = index;
            }
            SetCulture(index);
        }

        private void RegisterEvents()
        {
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try { if (e.Exception is Exception ex) HandleException(ex); } catch (Exception ex) { HandleException(ex); }
            finally { e.SetObserved(); }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try { if (e.ExceptionObject is Exception ex) HandleException(ex); } catch (Exception ex) { HandleException(ex); }
        }

        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try { HandleException(e.Exception); } catch (Exception ex) { HandleException(ex); }
            finally { e.Handled = true; }
        }

        private static void HandleException(Exception e)
        {
            try
            {
                var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tinygui_crash.log");
                System.IO.File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss}] {e.GetType().Name}: {e.Message}\n{e.InnerException?.ToString() ?? e.StackTrace}\n\n");
            }
            catch { }
            try { MessageBox.Show(e.InnerException?.Message ?? e.Message, "ERROR"); } catch { }
        }
    }
}
