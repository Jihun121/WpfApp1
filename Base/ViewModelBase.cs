using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp1.Base
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        // 데이터 변경을 알리는 이벤트
        public event PropertyChangedEventHandler PropertyChanged;

        // [CallerMemberName] 덕분에 속성 이름을 문자열로 직접 치지 않아도 자동으로 추적합니다.
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}