using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 앱 전체에서 발생하는 모든 미처리 예외(Unhandled Exception)를 낚아채기 위해 이벤트 연결 (성능 최적화 및 안정성 확보)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // 프로그램이 뻗어버리는 대신, 사용자에게 친절한 에러 메시지를 띄워주고 작업 내용을 잃지 않도록 보호함
            MessageBox.Show(
                $"일시적인 시스템 오류가 발생했습니다.\n안전하게 작업을 계속하실 수 있습니다.\n\n[상세 내용]: {e.Exception.Message}",
                "시스템 알림",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // 핵심: "에러를 내가 처리했으니 프로그램을 강제 종료하지 마라"고 시스템에 통보
            e.Handled = true;
        }
    }
}
