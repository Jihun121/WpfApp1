using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Windows; // MessageBox 사용을 위해 추가
using System.Windows.Data;
using System.Windows.Input; // ICommand 사용을 위해 추가
using WpfApp1.Base;
using WpfApp1.Models; // VideoModel이 있는 네임스페이스

namespace WpfApp1.ViewModels
{
    // 비디오 관련 데이터와 로직만 전담하여 코드의 응집도를 높이고 유지보수를 쉽게 함
    public class VideoViewModel : ViewModelBase
    {
        public ObservableCollection<VideoModel> Videos { get; } = new ObservableCollection<VideoModel>();

        // 콤보박스에 바인딩할 대여 가능 고객 목록 컬렉션 (이름과 ID만 담는 임시 클래스 활용 권장)
        public ObservableCollection<CustomerModel> AvailableCustomers { get; } = new ObservableCollection<CustomerModel>();

        // 대시보드 통계용 프로퍼티
        private int _totalVideoCount;
        public int TotalVideoCount
        {
            get => _totalVideoCount;
            set { _totalVideoCount = value; OnPropertyChanged(); }
        }

        private int _rentedVideoCount;
        public int RentedVideoCount
        {
            get => _rentedVideoCount;
            set { _rentedVideoCount = value; OnPropertyChanged(); }
        }

        private int _overdueVideoCount;
        public int OverdueVideoCount
        {
            get => _overdueVideoCount;
            set { _overdueVideoCount = value; OnPropertyChanged(); }
        }

        // 입력 폼용 프로퍼티
        private string _inputTitle;
        public string InputTitle
        {
            get => _inputTitle;
            set { _inputTitle = value; OnPropertyChanged(); }
        }

        private string _inputGenre;
        public string InputGenre
        {
            get => _inputGenre;
            set { _inputGenre = value; OnPropertyChanged(); }
        }

        // DataGrid에서 클릭한 비디오를 담을 프로퍼티
        private VideoModel _selectedVideo;
        public VideoModel SelectedVideo
        {
            get => _selectedVideo;
            set 
            { 
                _selectedVideo = value; 
                OnPropertyChanged();

                // 목록에서 비디오를 클릭하면 수정/삭제를 쉽게 하도록 입력창에 값을 채워줌
                if (_selectedVideo != null)
                {
                    InputTitle = _selectedVideo.Title;
                    InputGenre = _selectedVideo.Genre;
                }
                else
                {
                    InputTitle = string.Empty;
                    InputGenre = string.Empty;
                }
            }
        }

        // ComboBox에서 선택한 고객을 담을 프로퍼티
        private CustomerModel _selectedCustomer;
        public CustomerModel SelectedCustomer
        {
            get => _selectedCustomer;
            set { _selectedCustomer = value; OnPropertyChanged(); }
        }

        // 대여 버튼에 바인딩할 커맨드
        public ICommand RentVideoCommand { get; }

        // 대여 현황 목록과 선택된 항목, 반납 커맨드 추가
        public ObservableCollection<RentalModel> RentedList { get; } = new ObservableCollection<RentalModel>();

        private RentalModel _selectedRentedItem;
        public RentalModel SelectedRentedItem
        {
            get => _selectedRentedItem;
            set { _selectedRentedItem = value; OnPropertyChanged(); }
        }

        public ICommand ReturnVideoCommand { get; }

        // CRUD 커맨드 선언
        public ICommand AddVideoCommand { get; }
        public ICommand UpdateVideoCommand { get; }
        public ICommand DeleteVideoCommand { get; }

        // 검색어 프로퍼티
        private string _searchKeyword;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set
            {
                _searchKeyword = value;
                OnPropertyChanged();

                // 사용자가 글자를 타이핑할 때마다 UI 스레드에서 즉시 필터링을 갱신함
                CollectionViewSource.GetDefaultView(Videos).Refresh();
            }
        }

        // 이력 조회용 프로퍼티 및 커맨드 추가
        public ObservableCollection<RentalModel> CustomerHistoryList { get; } = new ObservableCollection<RentalModel>();

        private bool _isHistoryModalOpen;
        public bool IsHistoryModalOpen
        {
            get => _isHistoryModalOpen;
            set { _isHistoryModalOpen = value; OnPropertyChanged(); }
        }

        public ICommand ShowHistoryCommand { get; }
        public ICommand CloseHistoryCommand { get; }

        public ICommand ExportCsvCommand { get; }

        public VideoViewModel()
        {
            // 대여 커맨드 초기화 및 비동기 실행 연결
            RentVideoCommand = new RelayCommand(async p => await RentVideoAsync());

            // 반납 버튼 클릭 시 실행될 비동기 커맨드 연결
            ReturnVideoCommand = new RelayCommand(async p => await ReturnVideoAsync());

            // 각 버튼 클릭 시 비동기로 DB 작업 수행
            AddVideoCommand = new RelayCommand(async p => await AddVideoAsync());
            UpdateVideoCommand = new RelayCommand(async p => await UpdateVideoAsync());
            DeleteVideoCommand = new RelayCommand(async p => await DeleteVideoAsync());

            // 뷰모델 생성 시 비디오 목록과 대여 가능한 고객 목록을 동시에 불러옴
            _ = LoadVideosAsync();
            _ = LoadCustomersForRentAsync();
            _ = LoadRentedListAsync(); // 화면이 열릴 때 대여 목록도 자동 로드

            // 비디오 컬렉션에 필터 조건 연결
            // DB에 매번 쿼리를 날리지 않고, 이미 메모리에 로드된 Videos 리스트 내에서 검색을 처리하여 성능 최적화
            ICollectionView videoView = CollectionViewSource.GetDefaultView(Videos);
            videoView.Filter = SearchVideoFilter;

            // 이력 조회 커맨드 연결
            // 이력 버튼 클릭 시 데이터 로드, 닫기 버튼 클릭 시 모달 닫기
            ShowHistoryCommand = new RelayCommand(async p => await LoadCustomerHistoryAsync());
            CloseHistoryCommand = new RelayCommand(p => IsHistoryModalOpen = false);

            ExportCsvCommand = new RelayCommand(ExportVideosToCsv);
        }

        // 필터 로직 메서드 신규 추가
        private bool SearchVideoFilter(object item)
        {
            // 검색어가 비어있으면 모든 데이터를 그대로 보여줌
            if (string.IsNullOrWhiteSpace(SearchKeyword))
                return true;

            var video = item as VideoModel;

            // 예외 처리 및 유효성 검사: 제목이 null이 아니고, 대소문자 구분 없이 검색어가 포함되어 있는지 확인
            return video?.Title.IndexOf(SearchKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 로딩 상태 프로퍼티 추가
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public async Task LoadVideosAsync()
        {
            // DB 접근을 시작하기 전에 로딩 화면을 켬
            IsLoading = true;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    // EXISTS 구문을 활용하여 Rentals 테이블에 아직 반납되지 않은(ReturnDate IS NULL) 기록이 있는지 검사(성능 최적화)
                    string query = @"
                                SET NOCOUNT ON;
                                SELECT 
                                    v.VideoId, 
                                    v.Title, 
                                    v.Genre,
                                    CASE 
                                        WHEN EXISTS (SELECT 1 FROM Rentals r WHERE r.VideoId = v.VideoId AND r.ReturnDate IS NULL) THEN 1 
                                        ELSE 0 
                                    END AS IsRented
                                FROM Videos v
                                ORDER BY v.VideoId DESC;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        App.Current.Dispatcher.Invoke(() => Videos.Clear());

                        while (await reader.ReadAsync())
                        {
                            bool isRented = Convert.ToBoolean(reader["IsRented"]);

                            var video = new VideoModel
                            {
                                VideoId = Convert.ToInt32(reader["VideoId"]),
                                Title = reader["Title"].ToString(),
                                Genre = reader["Genre"].ToString(),

                                // 변경 이유: isRented 값에 따라 화면에 바인딩될 텍스트와 색상(HEX 코드)을 분기 처리
                                StatusText = isRented ? "대여 중" : "대여가능",
                                StatusColor = isRented ? "#E74C3C" : "#2ECC71" // 빨간색(대여 중) vs 초록색(대여가능)
                            };

                            App.Current.Dispatcher.Invoke(() => Videos.Add(video));
                        }

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            // 총 비디오 개수를 리스트의 Count를 통해 즉시 계산하여 UI 갱신
                            TotalVideoCount = Videos.Count;
                        });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // 예외 처리
                System.Windows.MessageBox.Show($"비디오 목록 로드 실패: {sqlEx.Message}");
            }
            finally
            {
                // 예외(에러)가 발생하든 성공하든 무조건 마지막에 로딩 화면을 끄도록 finally 블록 사용
                IsLoading = false;
            }
        }

        // 고객 목록 비동기 로드 메서드 신규 추가
        private async Task LoadCustomersForRentAsync()
        {
            // DB 접근을 시작하기 전에 로딩 화면을 켬
            IsLoading = true;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 콤보박스에 보여줄 필수 데이터(ID, 이름)만 조회하여 메모리 낭비 최소화
                    string query = @"
                                SET NOCOUNT ON; 
                                SELECT CustomerId, CustomerName 
                                FROM Customers 
                                ORDER BY CustomerName ASC;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        App.Current.Dispatcher.Invoke(() => AvailableCustomers.Clear());

                        while (await reader.ReadAsync())
                        {
                            var customer = new CustomerModel
                            {
                                CustomerId = Convert.ToInt32(reader["CustomerId"]),
                                CustomerName = reader["CustomerName"].ToString()
                            };
                            App.Current.Dispatcher.Invoke(() => AvailableCustomers.Add(customer));
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"고객 목록 로드 실패: {sqlEx.Message}", "DB 에러");
            }
            finally
            {
                // 예외(에러)가 발생하든 성공하든 무조건 마지막에 로딩 화면을 끄도록 finally 블록 사용
                IsLoading = false;
            }
        }

        // 대여 현황 로드 (JOIN 쿼리 활용)
        private async Task LoadRentedListAsync()
        {
            // DB 접근을 시작하기 전에 로딩 화면을 켬
            IsLoading = true;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // ReturnDate가 NULL(아직 반납 안됨)인 기록만 조회하되, PK를 활용해 고객명과 제목을 JOIN
                    // 대여일(RentalDate)과 현재(GETDATE)의 차이가 3일을 초과하면 1(True), 아니면 0(False) 반환
                    string query = @"
                                SET NOCOUNT ON;
                                SELECT 
                                    r.RentalId, 
                                    c.CustomerName, 
                                    v.Title AS VideoTitle, 
                                    CONVERT(VARCHAR(10), r.RentalDate, 120) AS RentDate,                               
                                    CASE 
                                        WHEN DATEDIFF(day, r.RentalDate, GETDATE()) > 3 THEN 1 
                                        ELSE 0 
                                    END AS IsOverdue
                                FROM Rentals r
                                INNER JOIN Customers c ON r.CustomerId = c.CustomerId
                                INNER JOIN Videos v ON r.VideoId = v.VideoId
                                WHERE r.ReturnDate IS NULL
                                ORDER BY r.RentalDate DESC;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        App.Current.Dispatcher.Invoke(() => RentedList.Clear());

                        while (await reader.ReadAsync())
                        {
                            bool isOverdue = Convert.ToBoolean(reader["IsOverdue"]);

                            var rental = new RentalModel
                            {
                                RentalId = Convert.ToInt32(reader["RentalId"]),
                                CustomerName = reader["CustomerName"].ToString(),
                                VideoTitle = reader["VideoTitle"].ToString(),
                                RentalDate = reader["RentDate"].ToString(),

                                // 데이터베이스에서 계산한 결과(1 또는 0)를 C#의 bool 타입으로 변환하여 모델에 담음
                                IsOverdue = isOverdue,

                                // 연체 여부에 따라 텍스트를 모델에 저장
                                StatusText = isOverdue ? "*연체" : "대여중"


                            };
                            App.Current.Dispatcher.Invoke(() => RentedList.Add(rental));
                        }

                        App.Current.Dispatcher.Invoke(() =>
                        {
                            // 대여 중인 총 개수와, LINQ의 Count(조건)를 활용해 연체 중(IsOverdue가 true)인 개수만 필터링하여 계산
                            RentedVideoCount = RentedList.Count;
                            OverdueVideoCount = RentedList.Count(r => r.IsOverdue);
                        });
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"대여 목록 로드 실패: {sqlEx.Message}", "DB 에러");
            }
            finally
            {
                // 예외(에러)가 발생하든 성공하든 무조건 마지막에 로딩 화면을 끄도록 finally 블록 사용
                IsLoading = false;
            }
        }

        // --- 비디오 추가 로직 ---
        private async Task AddVideoAsync()
        {
            if (string.IsNullOrWhiteSpace(InputTitle) || string.IsNullOrWhiteSpace(InputGenre))
            {
                MessageBox.Show("제목과 장르를 모두 입력해주세요.", "알림");
                return;
            }

            // DB 접근을 시작하기 전에 로딩 화면을 켬
            IsLoading = true;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 예방 및 최적화: SQL 인젝션 방지를 위해 매개변수화된 쿼리 사용
                    string query = "SET NOCOUNT ON; INSERT INTO Videos (Title, Genre) VALUES (@Title, @Genre);";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", InputTitle);
                        cmd.Parameters.AddWithValue("@Genre", InputGenre);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show("새 비디오가 등록되었습니다.");
                InputTitle = string.Empty;
                InputGenre = string.Empty;
                await LoadVideosAsync(); // DB 갱신 후 목록 새로고침
            }
            catch (SqlException ex) 
            { 
                MessageBox.Show($"추가 에러: {ex.Message}"); 
            }
            finally
            {
                // 예외(에러)가 발생하든 성공하든 무조건 마지막에 로딩 화면을 끄도록 finally 블록 사용
                IsLoading = false;
            }
        }

        // --- 비디오 수정 로직 ---
        private async Task UpdateVideoAsync()
        {
            if (SelectedVideo == null) return;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    string query = "SET NOCOUNT ON; UPDATE Videos SET Title = @Title, Genre = @Genre WHERE VideoId = @VideoId;";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Title", InputTitle);
                        cmd.Parameters.AddWithValue("@Genre", InputGenre);
                        cmd.Parameters.AddWithValue("@VideoId", SelectedVideo.VideoId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                MessageBox.Show("비디오 정보가 수정되었습니다.");
                await LoadVideosAsync();
            }
            catch (SqlException ex) { MessageBox.Show($"수정 에러: {ex.Message}"); }
        }

        // --- 비디오 삭제 로직 ---
        private async Task DeleteVideoAsync()
        {
            if (SelectedVideo == null) return;

            if (MessageBox.Show($"'{SelectedVideo.Title}' 비디오를 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 문제 해결: 누군가 대여했던 기록(Rentals 테이블)이 남아있으면 FK 제약조건 때문에 삭제 실패 에러가 발생함.
                    // 따라서 실무에서는 관련된 대여 기록을 먼저 지우거나, 삭제 대신 활성/비활성 상태(IsActive)를 업데이트하는 방식을 씁니다.
                    // 여기서는 가장 직관적인 '대여 기록 동시 삭제 후 비디오 삭제' 방식을 적용했습니다.
                    string query = @"
                                SET NOCOUNT ON; 
                                DELETE FROM Rentals WHERE VideoId = @VideoId; 
                                DELETE FROM Videos WHERE VideoId = @VideoId;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@VideoId", SelectedVideo.VideoId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                MessageBox.Show("비디오가 삭제되었습니다.");
                SelectedVideo = null;
                InputTitle = string.Empty;
                InputGenre = string.Empty;
                await LoadVideosAsync();
            }
            catch (SqlException ex) { MessageBox.Show($"삭제 에러: {ex.Message}"); }
        }

        // 반납 처리 비동기 메서드 (UPDATE 쿼리 활용)
        private async Task ReturnVideoAsync()
        {
            if (SelectedRentedItem == null)
            {
                MessageBox.Show("반납할 항목을 아래 목록에서 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // DB 접근을 시작하기 전에 로딩 화면을 켬
            IsLoading = true;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 기록을 지우지 않고 ReturnDate에 현재 시간을 기록하여 반납 처리 (데이터 보존)
                    string query = @"
                                SET NOCOUNT ON;
                                UPDATE Rentals 
                                SET ReturnDate = GETDATE() 
                                WHERE RentalId = @RentalId;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@RentalId", SelectedRentedItem.RentalId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show($"[{SelectedRentedItem.VideoTitle}] 반납이 완료되었습니다.", "반납 성공");

                // UX 개선: 반납 완료 후 즉시 목록을 갱신하여 화면에서 사라지게 만듦
                await LoadRentedListAsync();
                SelectedRentedItem = null;
                await LoadVideosAsync();
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"반납 처리 중 오류 발생: {sqlEx.Message}", "에러");
            }
            finally
            {
                // 예외(에러)가 발생하든 성공하든 무조건 마지막에 로딩 화면을 끄도록 finally 블록 사용
                IsLoading = false;
            }
        }

        // 비디오 대여 처리 비동기 메서드 신규 추가
        private async Task RentVideoAsync()
        {
            // 예외 처리: 비디오나 고객을 선택하지 않고 버튼을 누른 경우 방어
            if (SelectedVideo == null || SelectedCustomer == null)
            {
                MessageBox.Show("비디오와 대여할 고객을 모두 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // DB 접근을 시작하기 전에 로딩 화면을 켬
            IsLoading = true;

            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // --- [중복 대여 검증] ---
                    // Rentals 테이블에서 해당 VideoId가 반납되지 않은 상태(ReturnDate IS NULL)로 존재하는지 개수(COUNT)를 셈
                    string checkQuery = @"
                                    SELECT COUNT(*) 
                                    FROM Rentals 
                                    WHERE VideoId = @VideoId AND ReturnDate IS NULL;";

                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@VideoId", SelectedVideo.VideoId);

                        // ExecuteScalarAsync: 쿼리 결과의 첫 번째 행, 첫 번째 열의 값(COUNT)만 단일 값으로 빠르게 가져옴
                        int rentCount = (int)await checkCmd.ExecuteScalarAsync();

                        if (rentCount > 0)
                        {
                            // 누군가 이미 빌려간 상태이므로 로직을 중단(return)함
                            MessageBox.Show("현재 다른 고객이 대여 중인 비디오입니다.\n반납 처리 후 다시 시도해주세요.", "대여 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // 검증을 통과했다면 Rentals 테이블에 매개변수(@)를 활용하여 INSERT 수행 (SQL 인젝션 방지 및 성능 최적화)
                    string query = @"
                                SET NOCOUNT ON;
                                INSERT INTO Rentals (CustomerId, VideoId, RentalDate) 
                                VALUES (@CustomerId, @VideoId, GETDATE());";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CustomerId", SelectedCustomer.CustomerId);
                        cmd.Parameters.AddWithValue("@VideoId", SelectedVideo.VideoId);

                        // 비동기로 INSERT 실행
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show($"[{SelectedVideo.Title}] 비디오가 '{SelectedCustomer.CustomerName}' 고객님께 대여되었습니다!", "대여 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                // UX 개선: 대여 완료 후 다음 작업을 위해 선택 상태 초기화
                SelectedVideo = null;
                SelectedCustomer = null;

                // 대여에 성공하면 실시간으로 하단 '대여 현황 목록'에 갱신되도록 호출
                await LoadRentedListAsync();
                await LoadVideosAsync();
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"대여 처리 중 데이터베이스 오류가 발생했습니다.\n상세: {sqlEx.Message}", "대여 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 예외(에러)가 발생하든 성공하든 무조건 마지막에 로딩 화면을 끄도록 finally 블록 사용
                IsLoading = false;
            }
        }

        // 고객별 이력 로드 비동기 메서드 추가
        private async Task LoadCustomerHistoryAsync()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("이력을 조회할 고객을 먼저 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsLoading = true;
            string connectionString = "Server=JBook;Database=jhDB;Integrated Security=True;TrustServerCertificate=True;";
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // 선택한 고객(CustomerId)의 모든 기록을 가져오되, 반납 여부에 따라 상태 텍스트를 다르게 처리함
                    string query = @"
                                SET NOCOUNT ON;
                                SELECT 
                                    r.RentalId, 
                                    v.Title AS VideoTitle, 
                                    CONVERT(VARCHAR(10), r.RentalDate, 120) AS RentDate,
                                    CONVERT(VARCHAR(10), r.ReturnDate, 120) AS ReturnDateStr
                                FROM Rentals r
                                INNER JOIN Videos v ON r.VideoId = v.VideoId
                                WHERE r.CustomerId = @CustomerId
                                ORDER BY r.RentalDate DESC;";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // 최적화: 매개변수화된 쿼리로 특정 고객만 조회
                        cmd.Parameters.AddWithValue("@CustomerId", SelectedCustomer.CustomerId);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            App.Current.Dispatcher.Invoke(() => CustomerHistoryList.Clear());

                            while (await reader.ReadAsync())
                            {
                                string returnDate = reader["ReturnDateStr"].ToString();
                                bool isNotReturned = string.IsNullOrEmpty(returnDate);

                                var rental = new RentalModel
                                {
                                    RentalId = Convert.ToInt32(reader["RentalId"]),
                                    VideoTitle = reader["VideoTitle"].ToString(),
                                    RentalDate = reader["RentDate"].ToString(),
                                    ReturnDateDisplay = isNotReturned ? "-" : returnDate,
                                    StatusText = isNotReturned ? "대여중" : "✅ 반납완료"
                                };
                                App.Current.Dispatcher.Invoke(() => CustomerHistoryList.Add(rental));
                            }
                        }
                    }
                }
                // 조회 완료 후 모달 팝업 열기
                IsHistoryModalOpen = true;
            }
            catch (SqlException sqlEx)
            {
                MessageBox.Show($"이력 조회 실패: {sqlEx.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // CSV 내보내기
        public void ExportVideosToCsv(object parameter)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일 (*.csv)|*.csv",
                    FileName = $"보유비디오목록_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // 변경 이유: StreamWriter 사용 시 메모리 누수를 방지하기 위해 using 블록 사용 (성능 최적화)
                    // 엑셀에서 한글이 깨지지 않도록 UTF8(BOM 포함) 인코딩을 강제 적용함
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, new System.Text.UTF8Encoding(true)))
                    {
                        // 1. CSV 컬럼 헤더 작성
                        writer.WriteLine("NO,장르,비디오 제목,상태");

                        // 2. 비디오 목록 데이터 순회 및 작성
                        foreach (var video in Videos)
                        {
                            // 변경 이유: 비디오 제목에 쉼표(,)가 포함되어 있으면 CSV 열이 밀리는 현상을 방지하기 위해 따옴표로 감싸는 방어 로직 추가
                            string safeTitle = video.Title != null ? video.Title.Replace("\"", "\"\"") : "";

                            writer.WriteLine($"{video.VideoId},{video.Genre},\"{safeTitle}\",{video.StatusText}");
                        }
                    }

                    System.Windows.MessageBox.Show("비디오 목록이 성공적으로 엑셀(CSV)로 저장되었습니다.", "저장 완료",
                                                   System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (IOException ioEx)
            {
                // 변경 이유: 사용자가 해당 CSV 파일을 엑셀 등 다른 프로그램에서 이미 열어놓고 있어서 덮어쓰지 못하는 가장 흔한 에러(근본 원인)를 명확히 안내
                System.Windows.MessageBox.Show("파일을 저장할 수 없습니다. 파일을 다른 프로그램(엑셀 등)에서 열어두었는지 확인 후 종료해 주세요.\n\n" + ioEx.Message,
                                               "파일 접근 에러", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                // 변경 이유: 권한 문제나 디스크 용량 부족 등 기타 예외 상황 발생 시 프로그램 강제 종료를 막고 메시지 출력
                System.Windows.MessageBox.Show($"알 수 없는 오류가 발생했습니다.\n{ex.Message}",
                                               "시스템 에러", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
