using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;   // SaveFileDialog 사용을 위해 추가
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel; // ICollectionView용
using System.IO;         // StreamWriter 사용을 위해 추가
using System.Linq;           // Count() 등 LINQ 확장 메서드용
using System.Text;       // Encoding 처리를 위해 추가
using System.Text.RegularExpressions; // 정규식을 위해 추가
using System.Windows;
using System.Windows.Data;   // CollectionViewSource용
using System.Windows.Input; // ICommand용
using System.Collections.Generic; // Dictionary 사용을 위해 추가
using WpfApp1.Base;
using WpfApp1.Models;

namespace WpfApp1.ViewModels
{
    // WPF 유효성 검사 표준 인터페이스(INotifyDataErrorInfo) 추가 상속
    public class CustomerViewModel : ViewModelBase, INotifyDataErrorInfo
    {
        private string _customerName;
        private string _phoneNumber;
        private string _email;
        private string _customerGrade = "BRONZE"; // 기본값을 '일반'으로 셋팅

        // 상태 관리를 위한 변수 추가
        // 현재 입력 폼의 상태가 '신규 추가'인지 '기존 데이터 수정'인지 구분하기 위한 키 값
        private int _currentCustomerId = 0; // 0이면 신규, 0보다 크면 수정 모드

        // 유효성 검사를 위한 에러 관리 컨테이너 및 인터페이스 구현 ---
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        // 화면의 DataGrid와 바인딩될 컬렉션
        // List가 아닌 ObservableCollection을 써야 데이터가 추가/삭제될 때 화면이 즉시 갱신됨
        public ObservableCollection<CustomerModel> Customers { get; set; } = new ObservableCollection<CustomerModel>();

        // 에러가 하나라도 있는지 확인하는 속성 (저장 버튼 활성화 조건에 사용)
        public bool HasErrors => _errors.Any();

        // 에러가 생기거나 지워졌을 때 화면(XAML)에 알리는 이벤트
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        // 화면 상태를 통합 관리할 프로퍼티
        // 화면 갯수가 늘어나도 상태 충돌 없이 직관적으로 제어하기 위해 문자열 상태값 도입
        private string _currentView = "Dashboard"; // 기본 시작 화면을 대시보드로 설정
        public string CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        // 사이드바 메뉴 클릭 커맨드
        public RelayCommand ShowCustomerCommand { get; }
        public RelayCommand ShowDashboardCommand { get; }

        // --- [실시간 검색 및 통계용 변수 추가] ---
        private ICollectionView _customerView; // 필터링을 담당할 화면용 뷰 객체
        private string _searchText;
        private int _totalCustomerCount;
        private int _vipCustomerCount;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                // 텍스트박스에 글자를 칠 때마다 즉시 필터링 로직(Refresh)을 호출하여 화면을 갱신함
                _customerView?.Refresh();
            }
        }

        // 통계 바인딩용 프로퍼티
        public int TotalCustomerCount
        {
            get => _totalCustomerCount;
            set { _totalCustomerCount = value; OnPropertyChanged(); }
        }

        public int VipCustomerCount
        {
            get => _vipCustomerCount;
            set { _vipCustomerCount = value; OnPropertyChanged(); }
        }

        // DB에서 고객 목록을 불러오는 비동기 메서드
        public async Task LoadCustomersAsync()
        {
            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SET NOCOUNT ON;
                        SELECT 
                            c.CustomerId,
                            c.CustomerName,
                            c.PhoneNumber,
                            c.Email,
                            CASE 
                                WHEN COUNT(r.RentalId) >= 15 THEN 'VIP'
                                WHEN COUNT(r.RentalId) >= 7 THEN 'GOLD'
                                WHEN COUNT(r.RentalId) >= 3 THEN 'SILVER'
                                ELSE 'BRONZE'
                            END AS CustomerGrade,
                            c.CreatedDate,
                            COUNT(r.RentalId) AS TotalRentals
                        FROM Customers c
                        LEFT JOIN Rentals r ON c.CustomerId = r.CustomerId
                        GROUP BY c.CustomerId, c.CustomerName, c.PhoneNumber, c.Email, c.CreatedDate
                        ORDER BY TotalRentals DESC;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        Customers.Clear(); // 갱신을 위해 기존 목록 싹 비우기

                        while (await reader.ReadAsync())
                        {
                            // DB Null 체크 시에도 인덱스 번호 대신 컬럼명("Email")을 사용하여 순서 변경에 대한 안전성 확보
                            string emailVal = reader["Email"] != DBNull.Value ? reader["Email"].ToString() : "";

                            Customers.Add(new CustomerModel
                            {
                                CustomerId = Convert.ToInt32(reader["CustomerId"]),
                                CustomerName = reader["CustomerName"].ToString(),
                                PhoneNumber = reader["PhoneNumber"].ToString(),
                                Email = emailVal,
                                CustomerGrade = reader["CustomerGrade"].ToString(),
                                CreatedDate = Convert.ToDateTime(reader["CreatedDate"]).ToString("yyyy-MM-dd"),

                                // 그룹핑으로 집계된 총 대여 횟수
                                TotalRentals = Convert.ToInt32(reader["TotalRentals"])
                            });
                        }
                        // ObservableCollection을 ICollectionView로 래핑하여 검색/필터링을 메모리 단에서 고속으로 처리
                        _customerView = CollectionViewSource.GetDefaultView(Customers);
                        _customerView.Filter = CustomerFilter; // 필터링 규칙 연결

                        // 전체 리스트가 갱신될 때마다 대시보드 통계 수치도 즉시 재계산
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            TotalCustomerCount = Customers.Count;

                            // 동적으로 계산되어 모델에 들어온 CustomerGrade가 'VIP'인 사람만 카운트함
                            VipCustomerCount = Customers.Count(c => c.CustomerGrade == "VIP");

                            // (참고: 만약 대여 횟수로 직접 세고 싶다면 아래처럼)
                            // VipCustomerCount = Customers.Count(c => c.TotalRentals >= 15);
                        });

                        // 오늘 날짜와 일치하는 CreatedDate를 가진 고객의 수를 카운트하여 요약 카드에 바인딩
                        string todayString = DateTime.Now.ToString("yyyy-MM-dd");
                        TodayNewCustomerCount = Customers.Count(c => c.CreatedDate == todayString);

                        // 처음에 앱을 켤 때 대시보드가 먼저 뜨므로, 데이터를 다 불러온 직후 차트도 미리 갱신해 둠
                        UpdateChartData();

                        // 고객 정보를 다 가져온 직후, 비디오 대여 트렌드 데이터도 비동기로 즉시 불러오도록 연결
                        await LoadRentalTrendAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터를 불러오는데 실패했습니다: {ex.Message}");
            }
        }

        // --- 필터링 규칙 메서드 ---
        // DataGrid의 각 행(item)마다 이 메서드가 실행되며, true를 반환하면 화면에 남고 false면 숨겨짐
        private bool CustomerFilter(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true; // 검색어가 없으면 전부 표시

            var customer = item as CustomerModel;
            if (customer == null) return false; // 예외 처리: 데이터가 비정상이면 숨김

            // 예외 처리 및 편의성: 영문 대소문자를 무시(OrdinalIgnoreCase)하고 이름이나 전화번호에 검색어가 포함되어 있는지 검사
            return (customer.CustomerName != null && customer.CustomerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                   (customer.PhoneNumber != null && customer.PhoneNumber.Contains(SearchText));
        }

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || !_errors.ContainsKey(propertyName))
                return null;
            return _errors[propertyName];
        }

        // 에러 추가 메서드
        private void AddError(string propertyName, string error)
        {
            if (!_errors.ContainsKey(propertyName))
                _errors[propertyName] = new List<string>();

            if (!_errors[propertyName].Contains(error))
            {
                _errors[propertyName].Add(error);
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                CommandManager.InvalidateRequerySuggested(); // 변경 이유: 에러 발생 시 버튼 상태 즉시 갱신
            }
        }

        // 에러 초기화 메서드
        private void ClearErrors(string propertyName)
        {
            if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
                CommandManager.InvalidateRequerySuggested(); // 변경 이유: 에러 해결 시 버튼 상태 즉시 갱신
            }
        }

        // 데이터 바인딩 프로퍼티 (XAML의 TextBox와 연결)
        public string CustomerName
        {
            get => _customerName;
            set
            {
                _customerName = value;
                OnPropertyChanged();

                // 새로운 값이 들어올 때마다 이름 관련 기존 에러 초기화
                ClearErrors(nameof(CustomerName));

                // 값이 비어있거나 공백(스페이스바)만 있으면 에러 추가
                if (string.IsNullOrWhiteSpace(value))
                {
                    AddError(nameof(CustomerName), "이름을 입력하세요.");
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        // 프로퍼티 및 버튼 조건 수정 ---
        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                _phoneNumber = value;
                OnPropertyChanged();

                // 입력값이 들어올 때마다 기존 에러를 지우고 새로 검사
                ClearErrors(nameof(PhoneNumber));

                // 정규식(Regex)을 사용하여 숫자가 아닌 문자가 포함되어 있는지 검사
                if (!string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, @"^[0-9]+$"))
                {
                    AddError(nameof(PhoneNumber), "숫자만 입력할 수 있습니다.");
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        // 이메일 프로퍼티 추가 (이메일 양식 '@' 포함 여부 검사)
        public string Email
        {
            get => _email;
            set
            {
                _email = value;
                OnPropertyChanged();
                ClearErrors(nameof(Email));

                // 이메일 칸이 채워져 있는데 '@'나 '.'이 없으면 양식 에러 처리
                if (!string.IsNullOrWhiteSpace(value) && (!value.Contains("@") || !value.Contains(".")))
                {
                    AddError(nameof(Email), "올바른 이메일 형식이 아닙니다.");
                }
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // 고객 등급 프로퍼티 추가 (콤보박스에서 선택된 값이 여기로 들어옴)
        public string CustomerGrade
        {
            get => _customerGrade;
            set
            {
                _customerGrade = value;
                OnPropertyChanged();
            }
        }

        // 버튼 클릭 명령 처리
        public RelayCommand SaveCommand { get; }

        // 폼 초기화(신규 입력 모드 전환) 커맨드 추가
        public RelayCommand ClearCommand { get; }

        // 삭제 커맨드 선언
        public RelayCommand DeleteCommand { get; }

        // CSV 내보내기 커맨드 선언
        public RelayCommand ExportCsvCommand { get; }

        // 오늘의 신규 고객 수를 담을 프로퍼티 추가
        private int _todayNewCustomerCount;
        public int TodayNewCustomerCount
        {
            get => _todayNewCustomerCount;
            set { _todayNewCustomerCount = value; OnPropertyChanged(); }
        }

        // 막대 차트용 프로퍼티
        public SeriesCollection GradeSeries { get; set; }
        public List<string> GradeLabels { get; set; } // X축에 표시될 이름 (일반, VIP, VVIP)

        // 선 차트용 프로퍼티
        public SeriesCollection RentalTrendSeries { get; set; }
        public List<string> TrendLabels { get; set; }

        // 비디오 화면 전환 커맨드
        public RelayCommand ShowVideoCommand { get; }

        public CustomerViewModel()
        {
            // RelayCommand 세팅: (실행할 함수, 활성화 검사 함수)
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            ClearCommand = new RelayCommand(ExecuteClear); // 커맨드 연결

            // 삭제 명령 객체 생성 및 활성화 조건 연결
            DeleteCommand = new RelayCommand(ExecuteDelete, CanExecuteDelete);

            // CSV 내보내기 커맨드 생성 및 활성화 조건 연결
            ExportCsvCommand = new RelayCommand(ExecuteExportCsv, CanExecuteExportCsv);

            GradeSeries = new SeriesCollection();
            GradeLabels = new List<string>();

            RentalTrendSeries = new SeriesCollection();
            TrendLabels = new List<string>();

            // 각 버튼을 누를 때마다 CurrentView의 문자열 값을 변경하여 XAML에 상태 전달
            ShowCustomerCommand = new RelayCommand(p => CurrentView = "Customer");

            ShowDashboardCommand = new RelayCommand(async p =>
            {
                CurrentView = "Dashboard";
                UpdateChartData();
                await LoadRentalTrendAsync();
            });

            ShowVideoCommand = new RelayCommand(p => CurrentView = "Video");
        }

        // --- 막대 차트 데이터 생성 로직 ---
        // 현재 DB(Customers 리스트)에 있는 데이터를 바탕으로 등급별(일반, VIP, VVIP) 인원수를 계산해 차트에 주입
        private void UpdateChartData()
        {
            GradeSeries.Clear();
            GradeLabels.Clear();

            var gradeGroups = Customers.GroupBy(c => c.CustomerGrade);

            // 막대 차트 객체 생성
            var columnSeries = new ColumnSeries
            {
                Title = "인원 수",
                Values = new ChartValues<int>(),
                DataLabels = true // 막대 위에 숫자 표시
            };

            foreach (var group in gradeGroups)
            {
                GradeLabels.Add(group.Key);             // X축 라벨(등급명) 추가
                columnSeries.Values.Add(group.Count()); // Y축 수치(인원수) 추가
            }

            GradeSeries.Add(columnSeries);
        }

        // 선 차트 비동기 DB 연동 메서드
        // UI 스레드 멈춤 없이 백그라운드에서 DB 트렌드 데이터를 가져오기 위함
        private async Task LoadRentalTrendAsync()
        {
            TrendLabels.Clear();

            // 문제 해결 및 예방: 특정 날짜에 대여 기록이 0건이면 SQL 결과에 그 날짜 자체가 누락됩니다.
            // 이를 방지하기 위해 오늘부터 과거 7일 치 날짜를 미리 Dictionary에 '0'으로 깔아둡니다.
            var tempRentals = new Dictionary<string, int>();
            for (int i = 6; i >= 0; i--)
            {
                string dateStr = DateTime.Now.AddDays(-i).ToString("MM-dd");
                TrendLabels.Add(dateStr);
                tempRentals[dateStr] = 0;
            }

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 성능 최적화: SET NOCOUNT ON으로 불필요한 메시지 반환을 막고,
                    // 날짜별 COUNT 연산을 DB 서버에 위임하여 네트워크 트래픽을 최소화합니다.
                    string query = @"
                                SET NOCOUNT ON; 
                                SELECT 
                                    CONVERT(VARCHAR(5), RentalDate, 110) AS RentDate, 
                                    COUNT(RentalId) AS RentCount
                                FROM Rentals
                                WHERE RentalDate >= CAST(GETDATE() - 7 AS DATE)
                                GROUP BY CONVERT(VARCHAR(5), RentalDate, 110);";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string dbDate = reader["RentDate"].ToString();
                            int count = Convert.ToInt32(reader["RentCount"]);

                            // DB에서 가져온 날짜가 딕셔너리에 있으면 0을 실제 대여 건수로 덮어씀
                            if (tempRentals.ContainsKey(dbDate))
                            {
                                tempRentals[dbDate] = count;
                            }
                        }
                    }
                }

                // 3. UI 차트에 데이터 바인딩
                var lineSeries = new LineSeries
                {
                    Title = "대여 건수",
                    Values = new ChartValues<int>(),
                    PointGeometrySize = 10,
                    LineSmoothness = 0.5
                };

                // 미리 정렬해둔 7일 치 라벨 순서대로 딕셔너리 값을 차트에 추가
                foreach (string label in TrendLabels)
                {
                    lineSeries.Values.Add(tempRentals[label]);
                }

                RentalTrendSeries.Clear();
                RentalTrendSeries.Add(lineSeries);
            }
            catch (SqlException sqlEx)
            {
                // 예외 처리: DB 통신 실패 시 사용자에게 원인 분석 힌트 제공
                MessageBox.Show($"비디오 대여 트렌드를 불러오는 데 실패했습니다.\n(Rentals 테이블 생성 여부 및 네트워크 상태를 확인하세요)\n상세 에러: {sqlEx.Message}", "DB 에러");
            }
        }

        // 선택을 취소하고 빈 화면에서 새 고객을 등록할 수 있게 폼을 비워주는 로직
        private void ExecuteClear(object parameter)
        {
            _currentCustomerId = 0; // 신규 모드로 초기화
            SelectedCustomer = null;
            CustomerName = string.Empty;
            PhoneNumber = string.Empty;
            Email = string.Empty;
            CustomerGrade = "BRONZE";
            ClearErrors(nameof(CustomerName));
            ClearErrors(nameof(PhoneNumber));
            ClearErrors(nameof(Email));
        }

        // [저장 버튼 활성화 조건]
        private bool CanExecuteSave(object parameter)
        {
            // 기존 빈 칸 검사에 추가로 '!HasErrors(에러가 없을 것)' 조건을 달아 에러 시 저장 방지
            return !string.IsNullOrWhiteSpace(CustomerName) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   !HasErrors;
        }

        // [저장 로직] 버튼이 클릭되었을 때 실행 (아직 DB 연결 전이므로 메시지박스 테스트)
        // 기존 ExecuteSave 메서드 수정 (Insert / Update 분기 처리)
        private async void ExecuteSave(object parameter)
        {
            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string query = "";

                    // _currentCustomerId 값에 따라 신규 등록(Insert)인지 정보 수정(Update)인지 판단
                    if (_currentCustomerId == 0)
                    {
                        query = @"
                            SET NOCOUNT ON; 
                            INSERT INTO Customers (CustomerName, PhoneNumber, Email) 
                            VALUES (@Name, @Phone, @Email);";
                    }
                    else
                    {
                        // 성능 최적화: PK(CustomerId)를 기준으로 인덱스를 타서 빠르게 업데이트
                        query = @"
                            SET NOCOUNT ON; 
                            UPDATE Customers 
                            SET CustomerName = @Name, PhoneNumber = @Phone, Email = @Email 
                            WHERE CustomerId = @Id;";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", CustomerName);
                        cmd.Parameters.AddWithValue("@Phone", PhoneNumber);
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(Email) ? DBNull.Value : Email);

                        if (_currentCustomerId != 0)
                            cmd.Parameters.AddWithValue("@Id", _currentCustomerId);

                        await cmd.ExecuteNonQueryAsync();

                        string msg = _currentCustomerId == 0 ? "저장되었습니다." : "수정되었습니다.";
                        MessageBox.Show(msg, "완료");

                        ExecuteClear(null); // 완료 후 폼 초기화 (신규 모드로)
                        await LoadCustomersAsync(); // DataGrid 화면 갱신
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"DB 통신 에러: {sqlEx.Message}");
            }
        }

        // [삭제 버튼 활성화 조건]
        private bool CanExecuteDelete(object parameter)
        {
            // 신규 모드(_currentCustomerId == 0)일 때는 지울 데이터가 없으므로 버튼 비활성화,
            // DataGrid에서 기존 데이터를 클릭하여 _currentCustomerId에 값이 들어왔을 때만 활성화됨.
            return _currentCustomerId != 0;
        }

        // [삭제 로직]
        private async void ExecuteDelete(object parameter)
        {
            // 예기치 않은 데이터 손실을 방지하기 위해 DB 접근 전 사용자에게 한 번 더 확인(예방 조치)
            var result = MessageBox.Show("정말로 이 고객 정보를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                                         "삭제 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return; // '아니오'를 누르면 로직 즉시 중단

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // PK(CustomerId)를 조건으로 인덱스를 타서 정확히 1건의 행만 빠르고 안전하게 삭제
                    string query = "SET NOCOUNT ON; DELETE FROM Customers WHERE CustomerId = @Id;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", _currentCustomerId);
                        await cmd.ExecuteNonQueryAsync();

                        MessageBox.Show("고객 정보가 성공적으로 삭제되었습니다.", "삭제 완료");

                        ExecuteClear(null); // 삭제 완료 후 왼쪽 폼을 빈 칸(신규 모드)으로 초기화
                        await LoadCustomersAsync(); // 우측 DataGrid 화면 즉시 갱신
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // 문제 해결 및 디버깅 포인트: 향후 다른 테이블(예: 주문 내역)과 이 고객이 Foreign Key로 연결되어 있다면 
                // 무결성 제약 조건(FK 충돌) 에러가 발생할 수 있습니다. 이를 명확히 캐치하기 위해 SqlException 처리.
                MessageBox.Show($"DB 삭제 중 오류가 발생했습니다.\n(다른 데이터와 연결되어 있는지 확인하세요)\n에러 내용: {sqlEx.Message}");
            }
        }

        // DataGrid 선택 항목 바인딩용 프로퍼티 추가
        private CustomerModel _selectedCustomer;
        public CustomerModel SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                _selectedCustomer = value;
                OnPropertyChanged();

                // DataGrid에서 특정 행을 클릭하면 이 setter가 실행됨. 
                // 선택된 객체의 데이터를 왼쪽 입력 폼(ViewModel 프로퍼티)에 자동으로 채워줌.
                if (_selectedCustomer != null)
                {
                    _currentCustomerId = _selectedCustomer.CustomerId; // 수정 모드로 전환
                    CustomerName = _selectedCustomer.CustomerName;
                    PhoneNumber = _selectedCustomer.PhoneNumber;       // 하이픈 없는 원본 숫자
                    Email = _selectedCustomer.Email;
                    CustomerGrade = _selectedCustomer.CustomerGrade;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        // [내보내기 버튼 활성화 조건]
        private bool CanExecuteExportCsv(object parameter)
        {
            // 목록에 내보낼 데이터가 1건이라도 있을 때만 버튼 활성화
            return Customers != null && Customers.Count > 0;
        }

        // [내보내기 로직]
        private void ExecuteExportCsv(object parameter)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "고객 목록 CSV 내보내기",
                    Filter = "CSV 파일 (*.csv)|*.csv",
                    // 사용자의 바탕화면 경로를 자동으로 찾아 기본 저장 위치로 지정
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    FileName = $"고객목록_{DateTime.Now:yyyyMMdd_HHmm}.csv" // 파일명에 현재 시간 포함
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // 엑셀에서 열었을 때 한글이 깨지지 않도록 UTF-8(BOM 포함) 인코딩 강제 적용
                    using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                    {
                        // 최상단 컬럼 헤더 작성
                        sw.WriteLine("NO,이름,연락처,이메일,등급,등록일");

                        // ★ 핵심: 전체 데이터(Customers)가 아닌, 화면에 필터링된 결과(_customerView)만 순회합니다.
                        // 이렇게 하면 '김'씨만 검색한 상태에서 내보내기를 누르면 '김'씨 목록만 저장됩니다.
                        foreach (CustomerModel customer in _customerView)
                        {
                            // 혹시라도 이메일이나 이름 데이터 중간에 쉼표(,)가 있으면 CSV 열이 밀려버리는 버그 방지(쌍따옴표로 이스케이프)
                            string name = $"\"{customer.CustomerName}\"";
                            string phone = $"\"{customer.DisplayPhoneNumber}\"";
                            string email = $"\"{customer.Email}\"";
                            string grade = $"\"{customer.CustomerGrade}\"";
                            string date = $"\"{customer.CreatedDate}\"";

                            sw.WriteLine($"{customer.CustomerId},{name},{phone},{email},{grade},{date}");
                        }
                    }
                    MessageBox.Show("바탕화면에 CSV 파일이 성공적으로 저장되었습니다.", "내보내기 성공");
                }
            }
            catch (IOException ioEx)
            {
                // 방금 만든 CSV 파일을 엑셀로 열어둔 상태에서 또 덮어쓰기 저장을 시도하면 발생하는 파일 점유(Lock) 에러 처리
                MessageBox.Show($"파일을 저장할 수 없습니다.\n엑셀 등 열려있는 파일을 닫고 다시 시도해 주세요.\n(상세: {ioEx.Message})", "저장 실패");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 중 알 수 없는 오류가 발생했습니다.\n{ex.Message}", "에러");
            }
        }
    }
}