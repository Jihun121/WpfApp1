using System;
using System.Windows.Input;

namespace WpfApp1.Base
{ 
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;     // 버튼을 눌렀을 때 실행할 함수
        private readonly Predicate<object> _canExecute; // 버튼을 누를 수 있는지(활성화) 검사하는 함수

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // 조건에 따라 버튼의 활성/비활성 상태를 자동으로 UI에 통보
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
    }
}