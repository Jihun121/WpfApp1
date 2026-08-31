using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp1.ViewModels;

namespace WpfApp1.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new CustomerViewModel();
            this.DataContext = viewModel;

            // 창이 처음 켜질 때 DB 데이터 로드 (비동기로 실행)
            this.Loaded += async (s, e) => { await viewModel.LoadCustomersAsync(); };
        }
    }
}