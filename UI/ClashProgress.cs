using System;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using WpfButton = System.Windows.Controls.Button;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace RevitFamilyImporter.UI
{
    // Esta janela tem sua própria fila de mensagens. Nunca acessa a API do Revit.
    internal sealed class ClashProgress : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);
        private readonly Thread _thread;
        private Dispatcher _dispatcher;
        private Window _window;
        private WpfTextBlock _message;
        private WpfProgressBar _bar;
        private WpfButton _cancelButton;
        private Exception _startupError;
        private volatile bool _disposed;
        private bool _allowClose;
        private bool _finishing;
        private readonly object _reportLock = new object();
        private long _lastReportTicks;
        private string _lastStage = string.Empty;

        public ClashProgress(string title)
        {
            _thread = new Thread(() => RunWindow(title)) { IsBackground = true, Name = "Office clash progress" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!_ready.Wait(TimeSpan.FromSeconds(10)))
            {
                _disposed = true;
                throw new InvalidOperationException("Não foi possível abrir a janela de progresso da análise.");
            }
            if (_startupError != null)
                throw new InvalidOperationException("Não foi possível abrir a janela de progresso da análise.", _startupError);
        }

        public CancellationToken Token => _cancellation.Token;

        public void Report(string stage, int current = 0, int total = 0)
        {
            Dispatcher dispatcher = _dispatcher;
            if (_disposed || dispatcher == null || dispatcher.HasShutdownStarted) return;
            lock (_reportLock)
            {
                long now = Stopwatch.GetTimestamp();
                string phase = (stage ?? string.Empty).Split(':')[0].TrimEnd('.', ' ');
                if (string.Equals(_lastStage, phase, StringComparison.Ordinal) &&
                    _lastReportTicks != 0 && now - _lastReportTicks < Stopwatch.Frequency / 10) return;
                _lastStage = phase;
                _lastReportTicks = now;
            }
            try
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_disposed || _window == null || _finishing) return;
                    _message.Text = total > 0 ? $"{stage} ({current:N0} / {total:N0})" : stage;
                    _bar.IsIndeterminate = total <= 0;
                    if (total > 0) { _bar.Maximum = total; _bar.Value = Math.Max(0, Math.Min(current, total)); }
                }));
            }
            catch (InvalidOperationException) { }
        }

        public void BeginFinishing(string message = null)
        {
            Token.ThrowIfCancellationRequested();
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                throw new InvalidOperationException("A janela de progresso foi encerrada inesperadamente.");
            dispatcher.Invoke(new Action(() =>
            {
                _finishing = true;
                _message.Text = message ?? "Preparando a tabela de resultados. Aguarde a conclusão desta etapa.";
                _bar.IsIndeterminate = true;
                _cancelButton.IsEnabled = false;
            }), DispatcherPriority.Send);
            Token.ThrowIfCancellationRequested();
        }

        private void RunWindow(string title)
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                _window = new Window
                {
                    Title = title, Width = 480, SizeToContent = SizeToContent.Height,
                    ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = true, Topmost = true
                };
                var panel = new WpfStackPanel { Margin = new Thickness(20) };
                _window.Content = panel;
                panel.Children.Add(new WpfTextBlock { Text = "Analisando conflitos", FontSize = 18, FontWeight = FontWeights.SemiBold });
                _message = new WpfTextBlock { Text = "Preparando análise...", Margin = new Thickness(0, 12, 0, 10), TextWrapping = TextWrapping.Wrap };
                panel.Children.Add(_message);
                _bar = new WpfProgressBar { Height = 18, IsIndeterminate = true };
                panel.Children.Add(_bar);
                panel.Children.Add(new WpfTextBlock { Text = "O Revit pode ficar ocupado durante operações individuais. O cancelamento ocorre entre etapas.", Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap });
                _cancelButton = new WpfButton { Content = "Cancelar análise", HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(12, 6, 12, 6) };
                _cancelButton.Click += (sender, args) => RequestCancel();
                panel.Children.Add(_cancelButton);
                _window.Closing += (sender, args) => { if (!_allowClose) { args.Cancel = true; RequestCancel(); } };
                if (!_disposed) _window.Show();
                _ready.Set();
                if (!_disposed) Dispatcher.Run();
            }
            catch (Exception ex) { _startupError = ex; _ready.Set(); }
        }

        private void RequestCancel()
        {
            if (_finishing) return;
            if (!_cancellation.IsCancellationRequested) _cancellation.Cancel();
            if (_message != null) _message.Text = "Cancelando... aguarde a conclusão da operação atual do Revit.";
            if (_cancelButton != null) _cancelButton.IsEnabled = false;
        }

        public void Dispose()
        {
            _disposed = true;
            Dispatcher dispatcher = _dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                try
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        _allowClose = true;
                        _window?.Close();
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    }));
                }
                catch (InvalidOperationException) { }
            }
            if (_thread.Join(TimeSpan.FromSeconds(2)))
            {
                _ready.Dispose();
                _cancellation.Dispose();
            }
        }
    }
}
